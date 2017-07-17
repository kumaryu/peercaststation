using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Collections.Generic;
using PeerCastStation.Core;
using PeerCastStation.HTTP;

namespace PeerCastStation.UI.HTTP
{
  [Plugin]
  public class AdminHost
    : PluginBase
  {
    override public string Name { get { return "HTTP Admin Host UI"; } }

    private OWINApplication application;

    protected override void OnStart()
    {
      base.OnStart();
      var owinhost =
        Application.PeerCast.OutputStreamFactories.FirstOrDefault(factory => factory is OWINHostOutputStreamFactory) as OWINHostOutputStreamFactory;
      if (owinhost!=null) {
        if (application!=null) {
          owinhost.RemoveApplication(application);
        }
        application = owinhost.AddApplication("/admin", PathParameters.None, OnProcess);
      }
    }

    protected override void OnStop()
    {
      var owinhost =
        Application.PeerCast.OutputStreamFactories.FirstOrDefault(factory => factory is OWINHostOutputStreamFactory) as OWINHostOutputStreamFactory;
      if (owinhost!=null && application!=null) {
        owinhost.RemoveApplication(application);
      }
      base.OnStop();
    }

    private async Task OnProcess(IDictionary<string, object> owinenv)
    {
      var env = new OWINEnv(owinenv);
      var cancel_token = env.CallCanlelled;
      try {
        if (!HTTPUtils.CheckAuthorization(env.GetAuthorizationToken(), env.AccessControlInfo)) {
          throw new HTTPError(HttpStatusCode.Unauthorized);
        }
        if (env.RequestMethod!="HEAD" && env.RequestMethod!="GET") {
          throw new HTTPError(HttpStatusCode.MethodNotAllowed);
        }
        var query = env.RequestParameters;
        string value;
        if (query.TryGetValue("cmd", out value)) {
          switch (value) {
          case "viewxml": //リレー情報XML出力
            await OnViewXML(env, query, cancel_token);
            break;
          case "stop": //チャンネル停止
            await OnStop(env, query, cancel_token);
            break;
          case "bump": //チャンネル再接続
            await OnBump(env, query, cancel_token);
            break;
          default:
            throw new HTTPError(HttpStatusCode.BadRequest);
          }
        }
        else {
          throw new HTTPError(HttpStatusCode.BadRequest);
        }
      }
      catch (HTTPError err) {
        env.ResponseStatusCode = (int)err.StatusCode;
      }
      catch (UnauthorizedAccessException) {
        env.ResponseStatusCode = (int)HttpStatusCode.Forbidden;
      }
    }

    private XElement BuildChannelXml(Channel c)
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
            host.Add(new XAttribute("ip", (n.GlobalEndPoint ?? n.LocalEndPoint).ToString()));
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
          new XAttribute("status",    status)),
        new XElement("track", 
          new XAttribute("title",   c.ChannelTrack.Name ?? ""),
          new XAttribute("album",   c.ChannelTrack.Album ?? ""),
          new XAttribute("genre",   c.ChannelTrack.Genre ?? ""),
          new XAttribute("artist",  c.ChannelTrack.Creator ?? ""),
          new XAttribute("contact", c.ChannelTrack.URL ?? "")));
    }

    private byte[] BuildViewXml()
    {
      var peercast = Application.PeerCast;
      var root = new XElement("peercast");
      root.Add(new XAttribute("session", peercast.SessionID.ToString("N").ToUpperInvariant()));
      var servent = new XElement("servent", new XAttribute("uptime", (int)peercast.Uptime.TotalSeconds));
      var bandwidth = new XElement("bandwidth",
        new XAttribute("in",  peercast.Channels.Sum(c => c.ChannelInfo.Bitrate)),
        new XAttribute("out", peercast.Channels.Sum(c => c.OutputStreams.Sum(os => os.UpstreamRate))));
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

    private async Task OnViewXML(OWINEnv env, Dictionary<string, string> query, CancellationToken cancel_token)
    {
      var data = BuildViewXml();
      env.SetResponseStatusCode(HttpStatusCode.OK);
      env.SetResponseHeader("Content-Type", "text/xml");
      env.SetResponseHeader("Content-Length", data.Length.ToString());
      if (env.RequestMethod!="HEAD") {
        await env.ResponseBody.WriteAsync(data, 0, data.Length, cancel_token);
      }
    }

    private Channel FindChannelFromQuery(Dictionary<string, string> query)
    {
      string idstr;
      if (query.TryGetValue("id", out idstr)) {
        var md = System.Text.RegularExpressions.Regex.Match(idstr, @"([A-Fa-f0-9]{32})(\.\S+)?");
        var channel_id = Guid.Empty;
        if (md.Success) {
          try {
            channel_id = new Guid(md.Groups[1].Value);
          }
          catch (Exception) {
          }
        }
        return Application.PeerCast.Channels.FirstOrDefault(c => c.ChannelID==channel_id);
      }
      else {
        return null;
      }
    }

    private async Task OnBump(OWINEnv env, Dictionary<string, string> query, CancellationToken cancel_token)
    {
      var channel = FindChannelFromQuery(query);
      if (channel!=null) {
        channel.Reconnect();
        env.SetResponseStatusCode(HttpStatusCode.OK);
        await env.SetResponseBodyAsync("OK", cancel_token);
      }
      else {
        env.SetResponseStatusCode(HttpStatusCode.NotFound);
        await env.SetResponseBodyAsync("Channel NotFound", cancel_token);
      }
    }

    private async Task OnStop(OWINEnv env, Dictionary<string, string> query, CancellationToken cancel_token)
    {
      var channel = FindChannelFromQuery(query);
      if (channel!=null) {
        Application.PeerCast.CloseChannel(channel);
        env.SetResponseStatusCode(HttpStatusCode.OK);
        await env.SetResponseBodyAsync("OK", cancel_token);
      }
      else {
        env.SetResponseStatusCode(HttpStatusCode.NotFound);
        await env.SetResponseBodyAsync("Channel NotFound", cancel_token);
      }
    }

  }

}
