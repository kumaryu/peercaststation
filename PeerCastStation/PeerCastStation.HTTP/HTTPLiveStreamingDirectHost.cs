using Microsoft.Owin;
using Owin;
using PeerCastStation.Core;
using PeerCastStation.Core.Http;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.HTTP
{
  public class HTTPLiveStreamingDirectOwinApp
  {
    class M3U8PlayList
    {
      private string scheme;
      public string MIMEType { get { return "application/vnd.apple.mpegurl"; } }
      public Channel Channel { get; private set; }

      public M3U8PlayList(string scheme, Channel channel)
      {
        this.scheme = String.IsNullOrEmpty(scheme) ? "http" : scheme.ToLowerInvariant();
        Channel = channel;
      }

      public async Task<byte[]> CreatePlayListAsync(Uri baseuri, IEnumerable<KeyValuePair<string,string>> parameters, HTTPLiveStreamingSegmenter segmenter, CancellationToken cancellationToken)
      {
        var res = new System.Text.StringBuilder();
        var segments = await segmenter.GetSegmentsAsync(cancellationToken).ConfigureAwait(false);
        res.AppendLine("#EXTM3U");
        res.AppendLine("#EXT-X-VERSION:3");
        res.AppendLine("#EXT-X-ALLOW-CACHE:NO");
        res.AppendLine("#EXT-X-TARGETDURATION:2");
        res.AppendLine("#EXT-X-MEDIA-SEQUENCE:" + segments.FirstOrDefault().Index);
        var queries = String.Join("&", parameters.Select(kv => Uri.EscapeDataString(kv.Key) + "=" + Uri.EscapeDataString(kv.Value)));
        foreach (var seg in segments) {
          if (seg.Data==null) {
            res.AppendLine("#EXT-X-ENDLIST");
          }
          else {
            var url = new UriBuilder(new Uri(baseuri, Channel.ChannelID.ToString("N").ToUpperInvariant() + String.Format("/{0:00000}.ts", seg.Index)));
            url.Scheme = scheme;
            if (queries!="") {
              url.Query = queries;
            }
            res.AppendLine("#EXTINF:" + seg.Duration.ToString("F2") + ",");
            res.AppendLine(url.ToString());
          }
        }
        return System.Text.Encoding.UTF8.GetBytes(res.ToString());
      }
    }

    class HLSContentSink
      : IDisposable
    {
      public HTTPLiveStreamingSegmenter Segmenter { get; private set; } = new HTTPLiveStreamingSegmenter();
      private HTTPLiveStreamingDirectOwinApp owner;
      private Channel channel;
      private IDisposable subscription;
      private int referenceCount = 0;
      private HLSContentSink(HTTPLiveStreamingDirectOwinApp owner, Channel channel)
      {
        this.owner = owner;
        this.channel = channel;
      }

      private HLSContentSink AddRef()
      {
        if (Interlocked.Increment(ref referenceCount)==1 && subscription==null) {
          var sink =
              "flvtots".Split(',')
              .Select(name => channel.PeerCast.ContentFilters.FirstOrDefault(filter => filter.Name.ToLowerInvariant() == name.ToLowerInvariant()))
              .Where(filter => filter != null)
              .Aggregate((IContentSink)Segmenter, (r, filter) => filter.Activate(r));
          Interlocked.Exchange(ref subscription, channel.AddContentSink(sink))?.Dispose();
        }
        return this;
      }

      public void Dispose()
      {
        Task.Run(async () => {
          await Task.Delay(5000).ConfigureAwait(false);
          if (Interlocked.Decrement(ref referenceCount)==0) {
            owner.contentSinks.TryRemove(channel, out var s);
            Interlocked.Exchange(ref subscription, null)?.Dispose();
          }
        });
      }

      public static HLSContentSink GetSubscription(HTTPLiveStreamingDirectOwinApp owner, Channel channel)
      {
        return owner.contentSinks.GetOrAdd(channel, k => new HLSContentSink(owner, channel)).AddRef();
      }
    }
    private ConcurrentDictionary<Channel,HLSContentSink> contentSinks = new ConcurrentDictionary<Channel,HLSContentSink>();

    class HLSChannelSink
      : IChannelSink,
        IDisposable
    {
      private HTTPLiveStreamingDirectOwinApp owner;
      private ConnectionInfoBuilder connectionInfo = new ConnectionInfoBuilder();
      private Func<float> getRecvRate = null;
      private Func<float> getSendRate = null;
      private Tuple<Channel,string> session;
      private CancellationTokenSource stoppedCancellationTokenSource = new CancellationTokenSource();

      public string SessionId {
        get { return session.Item2; }
      }
      public Channel Channel {
        get { return session.Item1; }
      }
      public CancellationToken Stopped {
        get { return stoppedCancellationTokenSource.Token; }
      }


      public HLSContentSink GetContentSink()
      {
        return HLSContentSink.GetSubscription(owner, Channel);
      }

      public static HLSChannelSink GetSubscription(HTTPLiveStreamingDirectOwinApp owner, Channel channel, IOwinContext ctx, string session)
      {
        if (String.IsNullOrWhiteSpace(session)) {
          var source = new IPEndPoint(IPAddress.Parse(ctx.Request.RemoteIpAddress), ctx.Request.RemotePort ?? 0).ToString();
          using (var md5=System.Security.Cryptography.MD5.Create()) {
            session =
              md5
              .ComputeHash(System.Text.Encoding.ASCII.GetBytes(source))
              .Aggregate(new System.Text.StringBuilder(), (builder,v) => builder.Append(v.ToString("X2")))
              .ToString();
          }
        }
        return owner.channelSinks.GetOrAdd(new Tuple<Channel,string>(channel, session), k => new HLSChannelSink(owner, ctx, k)).AddRef(ctx);
      }

      private IDisposable subscription;
      private int referenceCount = 0;

      private HLSChannelSink(HTTPLiveStreamingDirectOwinApp owner, IOwinContext ctx, Tuple<Channel,string> session)
      {
        this.owner = owner;
        this.session = session;
        connectionInfo.AgentName = ctx.Request.Headers.Get("User-Agent");
        connectionInfo.LocalDirects = null;
        connectionInfo.LocalRelays = null;
        connectionInfo.ProtocolName = "HLS Direct";
        connectionInfo.RecvRate = null;
        connectionInfo.SendRate = null;
        connectionInfo.ContentPosition = 0;
        var remoteEndPoint = new IPEndPoint(IPAddress.Parse(ctx.Request.RemoteIpAddress), ctx.Request.RemotePort ?? 0);
        connectionInfo.RemoteEndPoint = remoteEndPoint;
        connectionInfo.RemoteName = remoteEndPoint.ToString();
        connectionInfo.RemoteSessionID = null;
        connectionInfo.RemoteHostStatus = RemoteHostStatus.Receiving;
        if (ctx.Get<bool>(OwinEnvironment.Server.IsLocal)) {
          connectionInfo.RemoteHostStatus |= RemoteHostStatus.Local;
        }
        connectionInfo.Status = ConnectionStatus.Connected;
        connectionInfo.Type = ConnectionType.Direct;
        getRecvRate = ctx.Get<Func<float>>(OwinEnvironment.PeerCastStation.GetRecvRate);
        getSendRate = ctx.Get<Func<float>>(OwinEnvironment.PeerCastStation.GetSendRate);
      }

      private HLSChannelSink AddRef(IOwinContext ctx)
      {
        connectionInfo.AgentName = ctx.Request.Headers.Get("User-Agent");
        var remoteEndPoint = new IPEndPoint(IPAddress.Parse(ctx.Request.RemoteIpAddress), ctx.Request.RemotePort ?? 0);
        connectionInfo.RemoteEndPoint = remoteEndPoint;
        connectionInfo.RemoteName = remoteEndPoint.ToString();
        if (ctx.Get<bool>(OwinEnvironment.Server.IsLocal)) {
          connectionInfo.RemoteHostStatus |= RemoteHostStatus.Local;
        }
        else {
          connectionInfo.RemoteHostStatus &= ~RemoteHostStatus.Local;
        }
        getRecvRate = ctx.Get<Func<float>>(OwinEnvironment.PeerCastStation.GetRecvRate);
        getSendRate = ctx.Get<Func<float>>(OwinEnvironment.PeerCastStation.GetSendRate);
        if (Interlocked.Increment(ref referenceCount)==1 && subscription==null) {
          Interlocked.Exchange(ref subscription, Channel.AddOutputStream(this))?.Dispose();
        }
        return this;
      }

      public void Dispose()
      {
        Task.Run(async () => {
          await Task.Delay(5000).ConfigureAwait(false);
          if (Interlocked.Decrement(ref referenceCount)==0) {
            owner.channelSinks.TryRemove(session, out var s);
            Interlocked.Exchange(ref subscription, null)?.Dispose();
            stoppedCancellationTokenSource.Dispose();
          }
        });
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
        stoppedCancellationTokenSource.Cancel();
        Interlocked.Exchange(ref subscription, null)?.Dispose();
      }
    }
    private ConcurrentDictionary<Tuple<Channel,string>,HLSChannelSink> channelSinks = new ConcurrentDictionary<Tuple<Channel,string>, HLSChannelSink>();

    private struct ParsedRequest
    {
      public static readonly Regex ChannelIdPattern = new Regex(@"\A/([0-9a-fA-F]{32})(?:/(\d{1,32}))?(?:\.(\w+))?/?\z", RegexOptions.Compiled);
      public HttpStatusCode Status;
      public Guid ChannelId;
      public int? FragmentNumber;
      public string Extension;
      public string Session;
      public bool IsValid {
        get { return Status==HttpStatusCode.OK; }
      }
      public static ParsedRequest Parse(IOwinContext ctx)
      {
        var req = new ParsedRequest();
        var path = ctx.Request.Path.HasValue ? ctx.Request.Path.Value : "/";
        if (path=="/") {
          req.Status = HttpStatusCode.Forbidden;
          return req;
        }
        var md = ChannelIdPattern.Match(path);
        if (!md.Success) {
          req.Status = HttpStatusCode.NotFound;
          return req;
        }
        req.Status = HttpStatusCode.OK;
        req.ChannelId = Guid.Parse(md.Groups[1].Value);
        req.FragmentNumber = md.Groups[2].Success ? (int?)Int32.Parse(md.Groups[2].Value) : null;
        req.Extension = md.Groups[3].Success ? md.Groups[3].Value : null;
        req.Session = ctx.Request.Query.Get("session");
        return req;
      }
    }

    private static async Task<Channel> GetChannelAsync(IOwinContext ctx, ParsedRequest req, CancellationToken ct)
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

    private async Task PlayListHandler(IOwinContext ctx, ParsedRequest req, Channel channel)
    {
      var ct = ctx.Request.CallCancelled;
      var session = req.Session;
      if (String.IsNullOrWhiteSpace(session)) {
        var source = new IPEndPoint(IPAddress.Parse(ctx.Request.RemoteIpAddress), ctx.Request.RemotePort ?? 0).ToString();
        using (var md5=System.Security.Cryptography.MD5.Create()) {
          session =
            md5
            .ComputeHash(System.Text.Encoding.ASCII.GetBytes(source))
            .Aggregate(new System.Text.StringBuilder(), (builder,v) => builder.Append(v.ToString("X2")))
            .ToString();
        }
        var location = new UriBuilder(ctx.Request.Uri);
        if (String.IsNullOrEmpty(location.Query)) {
          location.Query = $"session={session}";
        }
        else {
          location.Query = location.Query.Substring(1) + $"&session={session}"; 
        }
        ctx.Response.Redirect(location.Uri.ToString());
        return;
      }
      var pls = new M3U8PlayList(ctx.Request.Query.Get("scheme"), channel);
      ctx.Response.StatusCode = (int)HttpStatusCode.OK;
      ctx.Response.Headers.Add("Cache-Control", new string [] { "private" });
      ctx.Response.Headers.Add("Cache-Disposition", new string [] { "inline" });
      ctx.Response.Headers.Add("Content-Type", new string [] { pls.MIMEType });
      using (var subscription=HLSChannelSink.GetSubscription(this, channel, ctx, session)) {
        subscription.Stopped.ThrowIfCancellationRequested();
        byte[] body;
        try {
          var baseuri = new Uri(
            new Uri(ctx.Request.Uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.UserInfo, UriFormat.UriEscaped)),
            "hls/");
          var acinfo = ctx.GetAccessControlInfo();
          using (var contents=subscription.GetContentSink())
          using (var cts=CancellationTokenSource.CreateLinkedTokenSource(ct, subscription.Stopped)) {
            cts.CancelAfter(10000);
            if (acinfo.AuthorizationRequired) {
              var parameters = new Dictionary<string, string>() {
                { "auth", acinfo.AuthenticationKey.GetToken() },
                { "session", subscription.SessionId },
              };
              body = await pls.CreatePlayListAsync(baseuri, parameters, contents.Segmenter, cts.Token).ConfigureAwait(false);
            }
            else {
              var parameters = new Dictionary<string, string>() {
                { "session", subscription.SessionId },
              };
              body = await pls.CreatePlayListAsync(baseuri, parameters, contents.Segmenter, cts.Token).ConfigureAwait(false);
            }
          }
        }
        catch (OperationCanceledException) {
          ctx.Response.StatusCode = (int)HttpStatusCode.GatewayTimeout;
          return;
        }
        ctx.Response.StatusCode = (int)HttpStatusCode.OK;
        await ctx.Response.WriteAsync(body, ct).ConfigureAwait(false);
      }
    }

    private async Task FragmentHandler(IOwinContext ctx, ParsedRequest req, Channel channel)
    {
      var ct = ctx.Request.CallCancelled;
      using (var subscription=HLSChannelSink.GetSubscription(this, channel, ctx, req.Session)) {
        subscription.Stopped.ThrowIfCancellationRequested();
        using (var contents=subscription.GetContentSink())
        using (var cts=CancellationTokenSource.CreateLinkedTokenSource(ct, subscription.Stopped)) {
          cts.CancelAfter(10000);
          var segments = await contents.Segmenter.GetSegmentsAsync(cts.Token).ConfigureAwait(false);
          var segment = segments.FirstOrDefault(s => s.Index==req.FragmentNumber);
          if (segment.Index==0) {
            ctx.Response.StatusCode = (int)HttpStatusCode.NotFound;
          }
          else {
            ctx.Response.StatusCode = (int)HttpStatusCode.OK;
            ctx.Response.ContentType = "video/MP2T";
            ctx.Response.ContentLength = segment.Data.LongLength;
            await ctx.Response.WriteAsync(segment.Data, cts.Token).ConfigureAwait(false);
          }
        }
      }
    }

    private async Task HLSHandler(IOwinContext ctx)
    {
      var ct = ctx.Request.CallCancelled;
      var req = ParsedRequest.Parse(ctx);
      if (!req.IsValid) {
        ctx.Response.StatusCode = (int)req.Status;
        return;
      }
      Channel channel;
      try {
        channel = await GetChannelAsync(ctx, req, ct).ConfigureAwait(false);
      }
      catch (TaskCanceledException) {
        ctx.Response.StatusCode = (int)HttpStatusCode.GatewayTimeout;
        return;
      }
      if (channel==null) {
        ctx.Response.StatusCode = (int)HttpStatusCode.NotFound;
        return;
      }

      if (req.FragmentNumber.HasValue) {
        await FragmentHandler(ctx, req, channel).ConfigureAwait(false);
      }
      else {
        await PlayListHandler(ctx, req, channel).ConfigureAwait(false);
      }
    }

    public static void BuildApp(IAppBuilder builder)
    {
      var app = new HTTPLiveStreamingDirectOwinApp();
      builder.Map("/hls", sub => {
        sub.MapMethod("GET", withmethod => {
          withmethod.UseAuth(OutputStreamType.Play);
          withmethod.Run(app.HLSHandler);
        });
      });
    }

  }

  [Plugin]
  public class HTTPLiveStreamingDirectHostPlugin
    : PluginBase
  {
    override public string Name { get { return "HTTP Live Streaming Host"; } }

    private IDisposable appRegistration = null;
    override protected void OnAttach()
    {
      var owin = Application.Plugins.OfType<OwinHostPlugin>().FirstOrDefault();
      appRegistration = owin?.OwinHost?.Register(HTTPLiveStreamingDirectOwinApp.BuildApp);
    }

    override protected void OnDetach()
    {
      appRegistration?.Dispose();
      appRegistration = null;
    }
  }

}

