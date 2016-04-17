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

    AdminHostOutputStreamFactory factory;
    override protected void OnAttach()
    {
      factory = new AdminHostOutputStreamFactory(this, Application.PeerCast);
      Application.PeerCast.OutputStreamFactories.Add(factory);
    }

    override protected void OnDetach()
    {
      Application.PeerCast.OutputStreamFactories.Remove(factory);
    }

    public class AdminHostOutputStream
      : OutputStreamBase
    {
      AdminHost owner;
      HTTPRequest request;
      public AdminHostOutputStream(
        AdminHost owner,
        PeerCast peercast,
        Stream input_stream,
        Stream output_stream,
        EndPoint remote_endpoint,
        AccessControlInfo acinfo,
        HTTPRequest request)
        : base(peercast, input_stream, output_stream, remote_endpoint, acinfo, null, null)
      {
        this.owner   = owner;
        this.request = request;
        Logger.Debug("Initialized: Remote {0}", remote_endpoint);
      }

      public override ConnectionInfo GetConnectionInfo()
      {
        ConnectionStatus status = ConnectionStatus.Connected;
        if (IsStopped) {
          status = HasError ? ConnectionStatus.Error : ConnectionStatus.Idle;
        }
        return new ConnectionInfo(
          "Admin Host",
          ConnectionType.Interface,
          status,
          RemoteEndPoint.ToString(),
          (IPEndPoint)RemoteEndPoint,
          IsLocal ? RemoteHostStatus.Local : RemoteHostStatus.None,
          null,
          Connection.ReadRate,
          Connection.WriteRate,
          null,
          null,
          request.Headers["USER-AGENT"]);
      }

      protected override async Task<StopReason> DoProcess(CancellationToken cancel_token)
      {
        try {
          if (!HTTPUtils.CheckAuthorization(this.request, this.AccessControlInfo)) {
            throw new HTTPError(HttpStatusCode.Unauthorized);
          }
          if (this.request.Method!="HEAD" && this.request.Method!="GET") {
            throw new HTTPError(HttpStatusCode.MethodNotAllowed);
          }
          var query = HTTPUtils.ParseQuery(this.request.Uri.Query);
          string value;
          if (query.TryGetValue("cmd", out value)) {
            switch (value) {
            case "viewxml": //リレー情報XML出力
              await OnViewXML(query, cancel_token);
              break;
            case "stop": //チャンネル停止
              await OnStop(query, cancel_token);
              break;
            case "bump": //チャンネル再接続
              await OnBump(query, cancel_token);
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
          await Connection.WriteUTF8Async(HTTPUtils.CreateResponseHeader(err.StatusCode, new Dictionary<string, string> { }), cancel_token);
        }
        return StopReason.OffAir;
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
        var root = new XElement("peercast");
        var servent = new XElement("servent", new XAttribute("uptime", (int)PeerCast.Uptime.TotalSeconds));
        var bandwidth = new XElement("bandwidth",
          new XAttribute("in",  PeerCast.Channels.Sum(c => c.ChannelInfo.Bitrate)),
          new XAttribute("out", PeerCast.Channels.Sum(c => c.OutputStreams.Sum(os => os.UpstreamRate))));
        var connections = new XElement("connections",
          new XAttribute("total",  PeerCast.Channels.Sum(c => c.LocalDirects + c.LocalRelays)),
          new XAttribute("relays", PeerCast.Channels.Sum(c => c.LocalRelays)),
          new XAttribute("direct", PeerCast.Channels.Sum(c => c.LocalDirects)));
        var channels_relayed = new XElement("channels_relayed", 
          new XAttribute("total", PeerCast.Channels.Count));
        foreach (var c in PeerCast.Channels) {
          channels_relayed.Add(BuildChannelXml(c));
        }
        var channels_found = new XElement("channels_found", 
          new XAttribute("total", PeerCast.Channels.Count));
        foreach (var c in PeerCast.Channels) {
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

      private async Task OnViewXML(Dictionary<string, string> query, CancellationToken cancel_token)
      {
        var data = BuildViewXml();
        var parameters = new Dictionary<string, string> {
          {"Content-Type",   "text/xml" },
          {"Content-Length", data.Length.ToString() },
        };
        await Connection.WriteUTF8Async(HTTPUtils.CreateResponseHeader(HttpStatusCode.OK, parameters), cancel_token);
        if (this.request.Method!="HEAD") {
          await Connection.WriteAsync(data, cancel_token);
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
          return PeerCast.Channels.FirstOrDefault(c => c.ChannelID==channel_id);
        }
        else {
          return null;
        }
      }

      private Task OnBump(Dictionary<string, string> query, CancellationToken cancel_token)
      {
        HttpStatusCode status;
        var parameters = new Dictionary<string, string> {};
        string res;
        var channel = FindChannelFromQuery(query);
        if (channel!=null) {
          channel.Reconnect();
          status = HttpStatusCode.OK;
          res = "OK";
        }
        else {
          status = HttpStatusCode.NotFound;
          res = "Channel NotFound";
        }
        return Connection.WriteAsync(HTTPUtils.CreateResponse(status, parameters, res), cancel_token);
      }

      private Task OnStop(Dictionary<string, string> query, CancellationToken cancel_token)
      {
        HttpStatusCode status;
        var parameters = new Dictionary<string, string> {};
        string res;
        var channel = FindChannelFromQuery(query);
        if (channel!=null) {
          PeerCast.CloseChannel(channel);
          status = HttpStatusCode.OK;
          res = "OK";
        }
        else {
          status = HttpStatusCode.NotFound;
          res = "Channel NotFound";
        }
        return Connection.WriteAsync(HTTPUtils.CreateResponse(status, parameters, res), cancel_token);
      }

      public override OutputStreamType OutputStreamType
      {
        get { return OutputStreamType.Interface; }
      }
    }

    public class AdminHostOutputStreamFactory
      : OutputStreamFactoryBase
    {
      public override string Name
      {
        get { return "Admin Host UI"; }
      }

      public override OutputStreamType OutputStreamType
      {
        get { return OutputStreamType.Interface; }
      }

      public override int Priority
      {
        get { return 10; }
      }

      public override IOutputStream Create(
        Stream input_stream,
        Stream output_stream,
        EndPoint remote_endpoint,
        AccessControlInfo access_control,
        Guid channel_id,
        byte[] header)
      {
        using (var stream=new MemoryStream(header)) {
          var request = HTTPRequestReader.Read(stream);
          return new AdminHostOutputStream(owner, PeerCast, input_stream, output_stream, remote_endpoint, access_control, request);
        }
      }

      public override Guid? ParseChannelID(byte[] header)
      {
        using (var stream = new MemoryStream(header)) {
          var res = HTTPRequestReader.Read(stream);
          if (res!=null && res.Uri.AbsolutePath=="/admin") {
            return Guid.Empty;
          }
          else {
            return null;
          }
        }
      }

      AdminHost owner;
      public AdminHostOutputStreamFactory(AdminHost owner, PeerCast peercast)
        : base(peercast)
      {
        this.owner = owner;
      }
    }
  }
}
