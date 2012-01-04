using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;
using PeerCastStation.Core;
using PeerCastStation.HTTP;

namespace PeerCastStation.UI.HTTP
{
  public class AdminHost
    : MarshalByRefObject,
      IUserInterface
  {
    public string Name { get { return "HTTP Admin Host UI"; } }

    AdminHostOutputStreamFactory factory;
    PeerCastApplication application;
    public void Start(PeerCastApplication app)
    {
      application = app;
      factory = new AdminHostOutputStreamFactory(this, app.PeerCast);
      if (application.PeerCast.OutputStreamFactories.Count>0) {
        application.PeerCast.OutputStreamFactories.Insert(application.PeerCast.OutputStreamFactories.Count-1, factory);
      }
      else {
        application.PeerCast.OutputStreamFactories.Add(factory);
      }
    }

    public void Stop()
    {
      application.PeerCast.OutputStreamFactories.Remove(factory);
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
        HTTPRequest request)
        : base(peercast, input_stream, output_stream, remote_endpoint, null, null)
      {
        this.owner   = owner;
        this.request = request;
        Logger.Debug("Initialized: Remote {0}", remote_endpoint);
      }

      protected override void OnStarted()
      {
        base.OnStarted();
        Logger.Debug("Started");
        try {
          if (this.request.Method!="HEAD" && this.request.Method!="GET") {
            throw new HTTPError(HttpStatusCode.MethodNotAllowed);
          }
          var query = HTTPUtils.ParseQuery(this.request.Uri.Query);
          string value;
          if (query.TryGetValue("cmd", out value)) {
            switch (value) {
            case "viewxml": //リレー情報XML出力
              OnViewXML(query);
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
          Send(HTTPUtils.CreateResponseHeader(err.StatusCode, new Dictionary<string, string> { }));
        }
        Stop();
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
            new XAttribute("newest",     TimeSpan.FromMilliseconds(Environment.TickCount)-c.Nodes.Max(n => n.LastUpdated)));
          foreach (var n in c.Nodes) {
            hits.Add(new XElement("host",
              new XAttribute("ip",        n.GlobalEndPoint.Address.ToString()),
              new XAttribute("hops",      n.Hops),
              new XAttribute("listeners", n.DirectCount),
              new XAttribute("relays",    n.RelayCount),
              new XAttribute("uptime",    n.Uptime),
              new XAttribute("push",      n.IsFirewalled  ? 1 : 0),
              new XAttribute("relay",     n.IsRelayFull   ? 0 : 1),
              new XAttribute("direct",    n.IsDirectFull  ? 0 : 1),
              new XAttribute("cin",       n.IsControlFull ? 0 : 1),
              new XAttribute("stable",    0),
              new XAttribute("version",   n.Version),
              new XAttribute("update",    TimeSpan.FromMilliseconds(Environment.TickCount)-n.LastUpdated),
              new XAttribute("tracker",   n.IsTracker ? 1 : 0)));
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
        return new XElement("channel",
          new XAttribute("id",      c.ChannelID.ToString("N").ToUpper()),
          new XAttribute("name",    c.ChannelInfo.Name ?? ""),
          new XAttribute("bitrate", c.ChannelInfo.Bitrate),
          new XAttribute("comment", c.ChannelInfo.Comment ?? ""),
          new XAttribute("desc",    c.ChannelInfo.Desc ?? ""),
          new XAttribute("genre",   c.ChannelInfo.Genre ?? ""),
          new XAttribute("type",    c.ChannelInfo.ContentType ?? ""),
          new XAttribute("url",     c.ChannelInfo.URL ?? ""),
          new XAttribute("uptime",  c.Uptime),
          new XAttribute("age",     c.Uptime),
          new XAttribute("skip",    0),
          new XAttribute("bcflags", 0),
          hits,
          new XElement("relay",
            new XAttribute("listeners", c.LocalDirects),
            new XAttribute("relays",    c.LocalRelays),
            new XAttribute("hosts",     c.Nodes.Count),
            new XAttribute("status",    c.Status.ToString())),
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

      private void OnViewXML(Dictionary<string, string> query)
      {
        var data = BuildViewXml();
        var parameters = new Dictionary<string, string> {
          {"Content-Type",   "text/xml" },
          {"Content-Length", data.Length.ToString() },
        };
        Send(HTTPUtils.CreateResponseHeader(HttpStatusCode.OK, parameters));
        if (this.request.Method!="HEAD") {
          Send(data);
        }
      }

      private void Send(string str)
      {
        Send(System.Text.Encoding.UTF8.GetBytes(str));
      }

      protected override void OnStopped()
      {
        Logger.Debug("Finished");
       	base.OnStopped();
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

      public override IOutputStream Create(
        Stream input_stream,
        Stream output_stream,
        EndPoint remote_endpoint,
        Guid channel_id,
        byte[] header)
      {
        HTTPRequest request = null;
        using (var stream = new MemoryStream(header)) {
          try {
            request = HTTPRequestReader.Read(stream);
          }
          catch (EndOfStreamException) {
          }
        }
        return new AdminHostOutputStream(owner, PeerCast, input_stream, output_stream, remote_endpoint, request);
      }

      public override Guid? ParseChannelID(byte[] header)
      {
        HTTPRequest res = null;
        using (var stream = new MemoryStream(header)) {
          try {
            res = HTTPRequestReader.Read(stream);
          }
          catch (EndOfStreamException) {
          }
        }
        if (res!=null && res.Uri.AbsolutePath=="/admin") {
          return Guid.Empty;
        }
        else {
          return null;
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

  [Plugin(PluginType.UserInterface)]
  public class AdminHostFactory
    : IUserInterfaceFactory
  {
    public string Name { get { return "HTTP Admin Host UI"; } }

    public IUserInterface CreateUserInterface()
    {
      return new AdminHost();
    }
  }
}
