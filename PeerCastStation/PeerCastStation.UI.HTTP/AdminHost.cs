using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using PeerCastStation.Core;
using PeerCastStation.Core.Http;

namespace PeerCastStation.UI.HTTP
{
  public static class AdminHostOwinApp
  {
    private static async Task AdminHandler(OwinEnvironment ctx)
    {
      var cancel_token = ctx.Request.CallCancelled;
      switch (ctx.Request.Query.Get("cmd")) {
      case "viewxml": //リレー情報XML出力
        await OnViewXML(ctx, cancel_token).ConfigureAwait(false);
        break;
      case "stop": //チャンネル停止
        await OnStop(ctx, cancel_token).ConfigureAwait(false);
        break;
      case "bump": //チャンネル再接続
        await OnBump(ctx, cancel_token).ConfigureAwait(false);
        break;
      default:
        ctx.Response.StatusCode = HttpStatusCode.BadRequest;
        break;
      }
    }

    private static XElement BuildChannelXml(Channel c)
    {
      XElement hits;
      if (c.Nodes.Count>0) {
        hits = new XElement("hits",
          new XAttribute("hosts",      c.Nodes.Count),
          new XAttribute("listeners",  c.Nodes.Sum(n => n.DirectCount)),
          new XAttribute("relays",     c.Nodes.Sum(n => n.RelayCount)),
          new XAttribute("firewalled", c.Nodes.Count(n => n.IsFirewalled)),
          new XAttribute("closest",    c.Nodes.Min(n => n.Hops==0 ? int.MaxValue : n.Hops)),
          new XAttribute("furthest",   c.Nodes.Max(n => n.Hops)),
          new XAttribute("newest",     Environment.TickCount-c.Nodes.Max(n => n.LastUpdated)));
        foreach (var n in c.Nodes) {
          var host = new XElement("host");
          if (n.GlobalEndPoint!=null || n.LocalEndPoint!=null) {
            host.Add(new XAttribute("ip", (n.GlobalEndPoint ?? n.LocalEndPoint)!.ToString()));
          }
          host.Add(new XAttribute("hops",      n.Hops));
          host.Add(new XAttribute("listeners", n.DirectCount));
          host.Add(new XAttribute("relays",    n.RelayCount));
          host.Add(new XAttribute("uptime",    (int)n.Uptime.TotalSeconds));
          host.Add(new XAttribute("push",      n.IsFirewalled  ? 1 : 0));
          host.Add(new XAttribute("relay",     n.IsRelayFull   ? 0 : 1));
          host.Add(new XAttribute("direct",    n.IsDirectFull  ? 0 : 1));
          host.Add(new XAttribute("cin",       n.IsControlFull ? 0 : 1));
          host.Add(new XAttribute("stable",    0));
          host.Add(new XAttribute("version",   n.Version));
          host.Add(new XAttribute("update",    (Environment.TickCount-n.LastUpdated)/1000));
          host.Add(new XAttribute("tracker",   n.IsTracker ? 1 : 0));
          hits.Add(host);
        }
      }
      else {
        hits = new XElement("hits",
          new XAttribute("hosts",      0),
          new XAttribute("listeners",  0),
          new XAttribute("relays",     0),
          new XAttribute("firewalled", 0),
          new XAttribute("closest",    0),
          new XAttribute("furthest",   0),
          new XAttribute("newest",     0));
      }
      var status = "";
      switch (c.Status) {
      case SourceStreamStatus.Connecting: status = "CONNECT"; break;
      case SourceStreamStatus.Error:      status = "ERROR";   break;
      case SourceStreamStatus.Idle:       status = "IDLE";    break;
      case SourceStreamStatus.Searching:  status = "SEARCH";  break;
      case SourceStreamStatus.Receiving:
        if (c.IsBroadcasting) status = "BROADCAST";
        else                  status = "RECEIVE";
        break;
      }
      var contents = c.Contents.ToReadOnlyCollection();
      var buffersBytes = contents.Sum(cc => cc.Data.Length);
      var buffersDuration = ((contents.LastOrDefault()?.Timestamp ?? TimeSpan.Zero) - (contents.FirstOrDefault()?.Timestamp ?? TimeSpan.Zero)).TotalSeconds;
      return new XElement("channel",
        new XAttribute("id",      c.ChannelID.ToString("N").ToUpper()),
        new XAttribute("name",    c.ChannelInfo.Name ?? ""),
        new XAttribute("bitrate", c.ChannelInfo.Bitrate),
        new XAttribute("comment", c.ChannelInfo.Comment ?? ""),
        new XAttribute("desc",    c.ChannelInfo.Desc ?? ""),
        new XAttribute("genre",   c.ChannelInfo.Genre ?? ""),
        new XAttribute("type",    c.ChannelInfo.ContentType ?? ""),
        new XAttribute("url",     c.ChannelInfo.URL ?? ""),
        new XAttribute("uptime",  (int)c.Uptime.TotalSeconds),
        new XAttribute("age",     (int)c.Uptime.TotalSeconds),
        new XAttribute("skip",    0),
        new XAttribute("bcflags", 0),
        hits,
        new XElement("relay",
          new XAttribute("listeners", c.LocalDirects),
          new XAttribute("relays",    c.LocalRelays),
          new XAttribute("hosts",     c.Nodes.Count),
          new XAttribute("status",    status),
          new XElement("buffer",
            new XAttribute("bytes",    buffersBytes),
            new XAttribute("duration", buffersDuration)
          )
        ),
        new XElement("track", 
          new XAttribute("title",   c.ChannelTrack.Name ?? ""),
          new XAttribute("album",   c.ChannelTrack.Album ?? ""),
          new XAttribute("genre",   c.ChannelTrack.Genre ?? ""),
          new XAttribute("artist",  c.ChannelTrack.Creator ?? ""),
          new XAttribute("contact", c.ChannelTrack.URL ?? "")));
    }

    private static byte[] BuildViewXml(PeerCast peercast)
    {
      var root = new XElement("peercast");
      root.Add(new XAttribute("session", peercast.SessionID.ToString("N").ToUpperInvariant()));
      var servent = new XElement("servent", new XAttribute("uptime", (int)peercast.Uptime.TotalSeconds));
      var bandwidth = new XElement("bandwidth",
        new XAttribute("in",  peercast.Channels.Sum(c => c.ChannelInfo.Bitrate)),
        new XAttribute("out", peercast.Channels.Sum(c => c.GetUpstreamRate())));
      var connections = new XElement("connections",
        new XAttribute("total",  peercast.Channels.Sum(c => c.LocalDirects + c.LocalRelays)),
        new XAttribute("relays", peercast.Channels.Sum(c => c.LocalRelays)),
        new XAttribute("direct", peercast.Channels.Sum(c => c.LocalDirects)));
      var channels_relayed = new XElement("channels_relayed", 
        new XAttribute("total", peercast.Channels.Count));
      foreach (var c in peercast.Channels) {
        channels_relayed.Add(BuildChannelXml(c));
      }
      var channels_found = new XElement("channels_found", 
        new XAttribute("total", peercast.Channels.Count));
      foreach (var c in peercast.Channels) {
        channels_found.Add(BuildChannelXml(c));
      }
      root.Add(
        servent,
        bandwidth,
        connections,
        channels_relayed,
        channels_found);
      var res = new MemoryStream();
      using (var writer = new StreamWriter(res)) {
        root.Save(writer);
      }
      return res.ToArray();
    }

    private static async Task OnViewXML(OwinEnvironment ctx, CancellationToken cancel_token)
    {
      var peercast = ctx.GetPeerCast();
      if (peercast==null) {
        ctx.Response.StatusCode = HttpStatusCode.InternalServerError;
      }
      else {
        var data = BuildViewXml(peercast);
        ctx.Response.StatusCode = HttpStatusCode.OK;
        ctx.Response.ContentType = "text/xml;charset=utf-8";
        ctx.Response.ContentLength = data.LongLength;
        await ctx.Response.WriteAsync(data, cancel_token).ConfigureAwait(false);
      }
    }

    private static Channel? FindChannelFromQuery(OwinEnvironment ctx)
    {
      var peercast = ctx.GetPeerCast();
      var idstr = ctx.Request.Query.Get("id");
      if (peercast!=null && idstr!=null) {
        var md = System.Text.RegularExpressions.Regex.Match(idstr, @"([A-Fa-f0-9]{32})(\.\S+)?");
        var channel_id = Guid.Empty;
        if (md.Success) {
          try {
            channel_id = new Guid(md.Groups[1].Value);
          }
          catch (Exception) {
          }
        }
        return peercast.Channels.FirstOrDefault(c => c.ChannelID==channel_id);
      }
      else {
        return null;
      }
    }

    private static async Task OnBump(OwinEnvironment ctx, CancellationToken cancel_token)
    {
      var channel = FindChannelFromQuery(ctx);
      if (channel!=null) {
        channel.Reconnect();
        ctx.Response.StatusCode = HttpStatusCode.OK;
        await ctx.Response.WriteAsync("OK", cancel_token).ConfigureAwait(false);
      }
      else {
        ctx.Response.StatusCode = HttpStatusCode.NotFound;
        await ctx.Response.WriteAsync("Channel NotFound", cancel_token).ConfigureAwait(false);
      }
    }

    private static async Task OnStop(OwinEnvironment ctx, CancellationToken cancel_token)
    {
      var peercast = ctx.GetPeerCast();
      var channel = FindChannelFromQuery(ctx);
      if (peercast!=null && channel!=null) {
        peercast.CloseChannel(channel);
        ctx.Response.StatusCode = HttpStatusCode.OK;
        await ctx.Response.WriteAsync("OK", cancel_token).ConfigureAwait(false);
      }
      else {
        ctx.Response.StatusCode = HttpStatusCode.NotFound;
        await ctx.Response.WriteAsync("Channel NotFound", cancel_token).ConfigureAwait(false);
      }
    }

    public static void BuildApp(IAppBuilder builder)
    {
      builder.MapGET("/admin", sub => {
        sub.UseAuth(OutputStreamType.Interface);
        sub.Run(AdminHandler);
      });
    }

  }

  [Plugin]
  public class AdminHost
    : PluginBase
  {
    override public string Name { get { return "HTTP Admin Host UI"; } }

    private IDisposable? appRegistration = null;

    protected override void OnStart(PeerCastApplication app)
    {
      var owin = app.Plugins.OfType<OwinHostPlugin>().FirstOrDefault();
      appRegistration = owin?.OwinHost?.Register(AdminHostOwinApp.BuildApp);
    }

    protected override void OnStop()
    {
      appRegistration?.Dispose();
      appRegistration = null;
    }

  }

}
