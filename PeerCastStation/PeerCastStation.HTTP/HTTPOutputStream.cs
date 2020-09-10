// PeerCastStation, a P2P streaming servent.
// Copyright (C) 2011 Ryuichi Sakamoto (kumaryu@kumaryu.net)
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
using System;
using System.Linq;
using System.Net;
using System.Collections.Generic;
using System.Threading;
using PeerCastStation.Core;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using PeerCastStation.Core.Http;

namespace PeerCastStation.HTTP
{
  public static class HTTPDirectOwinApp
  {
    private static IPlayList CreatePlaylist(Channel channel, string fmt, string scheme)
    {
      IPlayList CreateDefaultPlaylist()
      {
        bool asf =
          channel.ChannelInfo.ContentType=="WMV" ||
          channel.ChannelInfo.ContentType=="WMA" ||
          channel.ChannelInfo.ContentType=="ASX";
        if (asf) {
          return new ASXPlayList(scheme, channel);
        }
        else {
          return new M3UPlayList(scheme, channel);
        }
      }

      if (String.IsNullOrEmpty(fmt)) {
        return CreateDefaultPlaylist();
      }
      else {
        switch (fmt.ToLowerInvariant()) {
        case "asx":  return new ASXPlayList(scheme, channel);
        case "m3u":  return new M3UPlayList(scheme, channel);
        default:     return CreateDefaultPlaylist();
        }
      }
    }

    private struct ParsedRequest
    {
      public static readonly Regex ChannelIdPattern = new Regex(@"\A([0-9a-fA-F]{32})(?:\.(\w+))?\z", RegexOptions.Compiled);
      public HttpStatusCode Status;
      public Guid ChannelId;
      public string Extension;
      public bool IsValid {
        get { return Status==HttpStatusCode.OK; }
      }
      public static ParsedRequest Parse(OwinEnvironment ctx)
      {
        var req = new ParsedRequest();
        var components = (String.IsNullOrEmpty(ctx.Request.Path) ? "/" : ctx.Request.Path).Split('/');
        if (components.Length>2) {
          req.Status = HttpStatusCode.NotFound;
          return req;
        }
        if (String.IsNullOrEmpty(components[1])) {
          req.Status = HttpStatusCode.Forbidden;
          return req;
        }
        var md = ChannelIdPattern.Match(components[1]);
        if (!md.Success) {
          req.Status = HttpStatusCode.NotFound;
          return req;
        }
        var channelId = Guid.Parse(md.Groups[1].Value);
        var ext = md.Groups[2].Success ? md.Groups[2].Value : null;
        req.Status = HttpStatusCode.OK;
        req.ChannelId = channelId;
        req.Extension = ext;
        return req;
      }
    }

    private static async Task<Channel> GetChannelAsync(OwinEnvironment ctx, ParsedRequest req, CancellationToken ct)
    {
      var tip = ctx.Request.Query.Get("tip");
      var channel = ctx.GetPeerCast().RequestChannel(req.ChannelId, OutputStreamBase.CreateTrackerUri(req.ChannelId, tip), true);
      if (channel==null) {
        return null;
      }
      using (var cts=CancellationTokenSource.CreateLinkedTokenSource(ct)) {
        cts.CancelAfter(10000);
        await channel.WaitForReadyContentTypeAsync(cts.Token).ConfigureAwait(false);
      }
      return channel;
    }

    private static async Task PlayListHandler(OwinEnvironment ctx)
    {
      var ct = ctx.Request.CallCancelled;
      var req = ParsedRequest.Parse(ctx);
      if (!req.IsValid) {
        ctx.Response.StatusCode = req.Status;
        return;
      }
      Channel channel;
      try {
        channel = await GetChannelAsync(ctx, req, ct).ConfigureAwait(false);
      }
      catch (TaskCanceledException) {
        ctx.Response.StatusCode = HttpStatusCode.GatewayTimeout;
        return;
      }
      if (channel==null) {
        ctx.Response.StatusCode = HttpStatusCode.NotFound;
        return;
      }

      var fmt = ctx.Request.Query.Get("pls") ?? req.Extension;
      //m3u8のプレイリストを要求された時はHLS用のパスに転送する
      if (fmt?.ToLowerInvariant()=="m3u8") {
        var location = new UriBuilder(ctx.Request.Uri);
        location.Path = $"/hls/{req.ChannelId:N}";
        if (location.Query.Contains("pls=m3u8")) {
          var queries = location.Query.Substring(1).Split('&').Where(seg => seg!="pls=m3u8").ToArray();
          if (queries.Length>0) {
            location.Query = String.Join("&", queries);
          }
          else {
            location.Query = null;
          }
        }
        ctx.Response.Redirect(HttpStatusCode.MovedPermanently, location.Uri.ToString());
        return;
      }

      var scheme = ctx.Request.Query.Get("scheme");
      var pls = CreatePlaylist(channel, fmt, scheme);
      ctx.Response.StatusCode = HttpStatusCode.OK;
      ctx.Response.Headers.Add("Cache-Control", "private");
      ctx.Response.Headers.Add("Cache-Disposition", "inline");
      ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
      ctx.Response.ContentType = pls.MIMEType;
      byte[] body;
      try {
        var baseuri = new Uri(
          new Uri(ctx.Request.Uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.UserInfo, UriFormat.UriEscaped)),
          "stream/");
        var acinfo = ctx.GetAccessControlInfo();
        using (var cts=CancellationTokenSource.CreateLinkedTokenSource(ct)) {
          cts.CancelAfter(10000);
          if (acinfo.AuthorizationRequired) {
            var parameters = new Dictionary<string, string>() {
              { "auth", acinfo.AuthenticationKey.GetToken() },
            };
            body = await pls.CreatePlayListAsync(baseuri, parameters, cts.Token).ConfigureAwait(false);
          }
          else {
            body = await pls.CreatePlayListAsync(baseuri, Enumerable.Empty<KeyValuePair<string,string>>(), cts.Token).ConfigureAwait(false);
          }
        }
      }
      catch (OperationCanceledException) {
        ctx.Response.StatusCode = HttpStatusCode.GatewayTimeout;
        return;
      }
      ctx.Response.StatusCode = HttpStatusCode.OK;
      await ctx.Response.WriteAsync(body, ct).ConfigureAwait(false);
    }

    class ChannelSink
      : IChannelSink,
        IContentSink
    {
      public class ChannelMessage
      {
        public enum MessageType {
          Broadcast,
          ChannelInfo,
          ChannelTrack,
          ContentHeader,
          ContentBody,
          ChannelStopped,
        }

        public MessageType Type;
        public Content Content;
        public byte[] Data;
      }
      private WaitableQueue<ChannelMessage> queue = new WaitableQueue<ChannelMessage>();
      private ConnectionInfoBuilder connectionInfo = new ConnectionInfoBuilder();
      private Func<float> getRecvRate = null;
      private Func<float> getSendRate = null;

      public ChannelSink(OwinEnvironment ctx)
      {
        connectionInfo.AgentName = ctx.Request.Headers.Get("User-Agent");
        connectionInfo.LocalDirects = null;
        connectionInfo.LocalRelays = null;
        connectionInfo.ProtocolName = "HTTP Direct";
        connectionInfo.RecvRate = null;
        connectionInfo.SendRate = null;
        connectionInfo.ContentPosition = 0;
        var remoteEndPoint = new IPEndPoint(IPAddress.Parse(ctx.Request.RemoteIpAddress), ctx.Request.RemotePort ?? 0);
        connectionInfo.RemoteEndPoint = remoteEndPoint;
        connectionInfo.RemoteName = remoteEndPoint.ToString();
        connectionInfo.RemoteSessionID = null;
        connectionInfo.RemoteHostStatus = RemoteHostStatus.Receiving;
        if (remoteEndPoint.Address.GetAddressLocality()<2) {
          connectionInfo.RemoteHostStatus |= RemoteHostStatus.Local;
        }
        connectionInfo.Status = ConnectionStatus.Connected;
        connectionInfo.Type = ConnectionType.Direct;
        getRecvRate = ctx.Get<Func<float>>(OwinEnvironment.PeerCastStation.GetRecvRate);
        getSendRate = ctx.Get<Func<float>>(OwinEnvironment.PeerCastStation.GetSendRate);
      }

      public Task<ChannelMessage> DequeueAsync(CancellationToken ct)
      {
        return queue.DequeueAsync(ct);
      }

      public ConnectionInfo GetConnectionInfo()
      {
        connectionInfo.RecvRate = getRecvRate?.Invoke();
        connectionInfo.SendRate = getSendRate?.Invoke();
        return connectionInfo.Build();
      }

      public void OnBroadcast(Host from, Atom packet)
      {
      }

      public void OnStopped(StopReason reason)
      {
        queue.Enqueue(new ChannelMessage { Type=ChannelMessage.MessageType.ChannelStopped, Content=null, Data=null });
      }

      public void OnChannelInfo(ChannelInfo channel_info)
      {
      }

      public void OnChannelTrack(ChannelTrack channel_track)
      {
      }

      public void OnContent(Content content)
      {
        queue.Enqueue(new ChannelMessage { Type=ChannelMessage.MessageType.ContentBody, Content=content });
        connectionInfo.ContentPosition = content.Position;
      }

      public void OnContentHeader(Content content_header)
      {
        connectionInfo.ContentPosition = content_header.Position;
        queue.Enqueue(new ChannelMessage { Type=ChannelMessage.MessageType.ContentHeader, Content=content_header });
      }

      public void OnStop(StopReason reason)
      {
        queue.Enqueue(new ChannelMessage { Type=ChannelMessage.MessageType.ChannelStopped, Content=null, Data=null });
      }
    }

    private static async Task StreamHandler(OwinEnvironment ctx)
    {
      var logger = new Logger(typeof(HTTPDirectOwinApp), $"{ctx.Request.RemoteIpAddress}:{ctx.Request.RemotePort}");
      var ct = ctx.Request.CallCancelled;
      var req = ParsedRequest.Parse(ctx);
      if (!req.IsValid) {
        ctx.Response.StatusCode = req.Status;
        return;
      }
      Channel channel;
      try {
        channel = await GetChannelAsync(ctx, req, ct).ConfigureAwait(false);
      }
      catch (TaskCanceledException) {
        ctx.Response.StatusCode = HttpStatusCode.GatewayTimeout;
        return;
      }
      if (channel==null) {
        ctx.Response.StatusCode = HttpStatusCode.NotFound;
        return;
      }
      var sink = new ChannelSink(ctx);
      using (channel.AddOutputStream(sink))
      using (channel.AddContentSink(sink)) {
        ctx.Response.StatusCode = HttpStatusCode.OK;
        bool asf =
          channel.ChannelInfo.ContentType=="WMV" ||
          channel.ChannelInfo.ContentType=="WMA" ||
          channel.ChannelInfo.ContentType=="ASX";

        if (asf && (!ctx.Request.Headers.TryGetValue("Pragma", out var values) || !values.Contains("xplaystrm=1", StringComparer.InvariantCultureIgnoreCase))) {
          ctx.Response.Headers.Add("Cache-Control", "no-cache");
          ctx.Response.Headers.Add("Server", "Rex/9.0.2980");
          ctx.Response.Headers.Add("Pragma", "no-cache", @"features=""broadcast,playlist""");
          ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
          ctx.Response.ContentType = "application/vnd.ms.wms-hdr.asfv1";

          try {
            while (!ct.IsCancellationRequested) {
              var packet = await sink.DequeueAsync(ct).ConfigureAwait(false);
              ctx.Response.ContentLength = packet.Content.Data.Length;
              if (packet.Type==ChannelSink.ChannelMessage.MessageType.ContentHeader) {
                await ctx.Response.WriteAsync(packet.Content.Data, ct).ConfigureAwait(false);
                logger.Debug("Sent ContentHeader pos {0}", packet.Content.Position);
                break;
              }
              else if (packet.Type==ChannelSink.ChannelMessage.MessageType.ChannelStopped) {
                break;
              }
            }
          }
          catch (OperationCanceledException) {
          }
        }
        else {
          if (asf) {
            ctx.Response.Headers.Add("Cache-Control", new string [] { "no-cache" });
            ctx.Response.Headers.Add("Server", new string [] { "Rex/9.0.2980" });
            ctx.Response.Headers.Add("Pragma", new string [] { "no-cache", @"features=""broadcast,playlist""" });
            ctx.Response.Headers.Add("Access-Control-Allow-Origin", new string[] { "*" });
            ctx.Response.ContentType = "application/x-mms-framed";
            ctx.Response.Headers.Add("Connection", new string[] { "close" });
          }
          else {
            ctx.Response.ContentType = channel.ChannelInfo.MIMEType;
            ctx.Response.Headers.Add("Access-Control-Allow-Origin", new string[] { "*" });
            ctx.Response.Headers.Add("Transfer-Encoding", new string [] { "chunked" });
          }

          Content sent_header = null;
          Content sent_packet = null;
          try {
            while (!ct.IsCancellationRequested) {
              var packet = await sink.DequeueAsync(ct).ConfigureAwait(false);
              if (packet.Type==ChannelSink.ChannelMessage.MessageType.ContentHeader) {
                if (sent_header!=packet.Content && packet.Content!=null) {
                  await ctx.Response.WriteAsync(packet.Content.Data, ct).ConfigureAwait(false);
                  logger.Debug("Sent ContentHeader pos {0}", packet.Content.Position);
                  sent_header = packet.Content;
                  sent_packet = packet.Content;
                }
              }
              else if (packet.Type==ChannelSink.ChannelMessage.MessageType.ContentBody) {
                if (sent_header==null) continue;
                var c = packet.Content;
                if (c.Timestamp>sent_packet.Timestamp ||
                    (c.Timestamp==sent_packet.Timestamp && c.Position>sent_packet.Position)) {
                  await ctx.Response.WriteAsync(c.Data, ct).ConfigureAwait(false);
                  sent_packet = c;
                }
              }
              else if (packet.Type==ChannelSink.ChannelMessage.MessageType.ChannelStopped) {
                break;
              }
            }
          }
          catch (OperationCanceledException) {
          }
        }
      }
    }

    public static void BuildApp(IAppBuilder builder)
    {
      builder.MapGET("/pls", sub => {
        sub.UseAuth(OutputStreamType.Play);
        sub.Run(PlayListHandler);
      });
      builder.MapGET("/stream", sub => {
        sub.UseAuth(OutputStreamType.Play);
        sub.Run(StreamHandler);
      });
    }

  }

  [Plugin]
  public class HTTPOutputStreamPlugin
    : PluginBase
  {
    override public string Name { get { return "HTTP Output"; } }

    private IDisposable appRegistration = null;
    override protected void OnAttach()
    {
      var owin = Application.Plugins.OfType<OwinHostPlugin>().FirstOrDefault();
      appRegistration = owin?.OwinHost?.Register(HTTPDirectOwinApp.BuildApp);
    }

    override protected void OnDetach()
    {
      appRegistration?.Dispose();
      appRegistration = null;
    }
  }

}

