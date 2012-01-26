using System;
using System.IO;
using System.Net;
using System.Linq;
using PeerCastStation.Core;
using PeerCastStation.HTTP;
using System.Collections.Generic;

namespace PeerCastStation.UI.HTTP
{
  public static class UriEscapeFilter
  {
    public static string UriEscape(string input)
    {
      return Uri.IsWellFormedUriString(input, UriKind.RelativeOrAbsolute) ? input : Uri.EscapeUriString(input);
    }

    public static string U(string input)
    {
      return UriEscape(input);
    }
  }

  public class ChannelTrackController
    : DotLiquid.ILiquidizable
  {
    public ChannelTrack Track { get; private set; }
    public ChannelTrackController(ChannelTrack track)
    {
      this.Track = track;
    }

    public object ToLiquid()
    {
      return new {
        name    = Track.Name,
        album   = Track.Album,
        creator = Track.Creator,
        url     = Track.URL,
        genre   = "",
      };
    }
  }

  public class ChannelInfoController
    : DotLiquid.ILiquidizable
  {
    public ChannelInfo Info { get; private set; }
    public ChannelInfoController(ChannelInfo info)
    {
      this.Info = info;
    }

    public object ToLiquid()
    {
      return new {
        name         = Info.Name,
        bitrate      = Info.Bitrate,
        comment      = Info.Comment,
        content_type = Info.ContentType,
        mime_type    = Info.MIMEType,
        description  = Info.Desc,
        genre        = Info.Genre,
        url          = Info.URL,
      };
    }
  }

  public class OutputStreamController
    : DotLiquid.ILiquidizable
  {
    public IOutputStream Stream { get; set; }
    public OutputStreamController(IOutputStream stream)
    {
      this.Stream = stream;
    }

    public object ToLiquid()
    {
      return new {
        is_local      = Stream.IsLocal,
        type          = Stream.OutputStreamType,
        upstream_rate = Stream.UpstreamRate,
        description   = Stream.ToString(),
      };
    }
  }

  public class ChannelController
    : DotLiquid.ILiquidizable
  {
    public Channel Channel { get; private set; }
    public ChannelController(Channel channel)
    {
      this.Channel = channel;
    }

    public object ToLiquid()
    {
      return new {
        id    = Channel.ChannelID.ToString("N").ToUpper(),
        info  = new ChannelInfoController(Channel.ChannelInfo),
        track = new ChannelTrackController(Channel.ChannelTrack),
        playlist_url     = "/pls/" + Channel.ChannelID.ToString("N").ToUpper(),
        stream_url       = "/stream/" + Channel.ChannelID.ToString("N").ToUpper(),
        content_position = Channel.ContentPosition,
        header_position  = Channel.ContentHeader!=null ? Channel.ContentHeader.Position : -1,
        header_length    = Channel.ContentHeader!=null ? Channel.ContentHeader.Data.Length : 0,
        local_directs    = Channel.LocalDirects,
        local_relays     = Channel.LocalRelays,
        total_directs    = Channel.TotalDirects,
        total_relays     = Channel.TotalRelays,
        uptime           = Channel.Uptime.TotalSeconds,
        source_uri       = Channel.SourceUri.ToString(),
        status           = Channel.Status.ToString(),
        is_relay_full    = Channel.IsRelayFull,
        is_direct_full   = Channel.IsDirectFull,
        output_streams   = Channel.OutputStreams.Select(s => new OutputStreamController(s)),
      };
    }
  }

  public class PeerCastController
    : DotLiquid.ILiquidizable
  {
    public PeerCast PeerCast { get; private set; }
    public PeerCastController(PeerCast peercast)
    {
      this.PeerCast = peercast;
    }

    public object ToLiquid()
    {
      return new {
        channels        = PeerCast.Channels.Select(c => new ChannelController(c)),
        agent_name      = PeerCast.AgentName,
        bcid            = PeerCast.BroadcastID.ToString("N").ToUpper(),
        gloal_endpoint  = PeerCast.GlobalEndPoint,
        local_endpoint  = PeerCast.LocalEndPoint,
        gloal_endpoint6 = PeerCast.GlobalEndPoint6,
        local_endpoint6 = PeerCast.LocalEndPoint6,
        is_firewalled   = PeerCast.IsFirewalled,
        local_directs   = PeerCast.Channels.Sum(c => c.LocalDirects),
        local_relays    = PeerCast.Channels.Sum(c => c.LocalRelays),
        upstream_rate   = PeerCast.Channels.Sum(c => c.OutputStreams.Sum(s => s.UpstreamRate)),
        downstream_rate = PeerCast.Channels.Sum(c => c.Status==SourceStreamStatus.Receiving ? c.ChannelInfo.Bitrate : 0),
        session_id      = PeerCast.SessionID.ToString("N").ToUpper(),
      };
    }
  }

  public class HTMLHost
    : MarshalByRefObject,
      IUserInterface
  {
    class FileDesc
    {
      public string MimeType   { get; set; }
      public bool   IsTemplate { get; set; }
    }
    private static readonly Dictionary<string, FileDesc> FileDescriptions = new Dictionary<string, FileDesc> {
      { ".html", new FileDesc { MimeType="text/html", IsTemplate=true } },
      { ".htm",  new FileDesc { MimeType="text/html", IsTemplate=true } },
      { ".txt",  new FileDesc { MimeType="text/plain", IsTemplate=true } },
      { ".xml",  new FileDesc { MimeType="text/xml", IsTemplate=true } },
      { ".json", new FileDesc { MimeType="application/json", IsTemplate=true } },
      { ".css",  new FileDesc { MimeType="text/css", IsTemplate=false } },
      { ".js",   new FileDesc { MimeType="application/javascript", IsTemplate=false } },
      { ".bmp",  new FileDesc { MimeType="image/bmp", IsTemplate=false } },
      { ".png",  new FileDesc { MimeType="image/png", IsTemplate=false } },
      { ".jpg",  new FileDesc { MimeType="image/jpeg", IsTemplate=false } },
      { ".gif",  new FileDesc { MimeType="image/gif", IsTemplate=false } },
      { ".svg",  new FileDesc { MimeType="image/svg+xml", IsTemplate=false } },
      { ".swf",  new FileDesc { MimeType="application/x-shockwave-flash", IsTemplate=false } },
      { ".xap",  new FileDesc { MimeType="application/x-silverlight-app", IsTemplate=false } },
      { "",      new FileDesc { MimeType="application/octet-stream", IsTemplate=false } },
    };

    public string Name { get { return "HTTP HTML Host UI"; } }
    public string PhysicalPath { get; set; }
    public string VirtualPath  { get; set; }

    public HTMLHost()
    {
      this.PhysicalPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(HTMLHost).Assembly.Location), "html"));
      this.VirtualPath  = "/html/";
    }

    HTMLHostOutputStreamFactory factory;
    PeerCastApplication application;
    public void Start(PeerCastApplication app)
    {
      application = app;
      factory = new HTMLHostOutputStreamFactory(this, application.PeerCast);
      if (app.PeerCast.OutputStreamFactories.Count>0) {
        application.PeerCast.OutputStreamFactories.Insert(app.PeerCast.OutputStreamFactories.Count-1, factory);
      }
      else {
        application.PeerCast.OutputStreamFactories.Add(factory);
      }
    }

    public void Stop()
    {
      if (factory!=null) {
        application.PeerCast.OutputStreamFactories.Remove(factory);
        factory = null;
      }
    }

    public class HTMLHostOutputStream
      : OutputStreamBase
    {
      HTMLHost owner;
      HTTPRequest request;
      public HTMLHostOutputStream(
        HTMLHost owner,
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

      class HTTPError : ApplicationException
      {
        public HttpStatusCode StatusCode { get; private set; }
        public HTTPError(HttpStatusCode code)
          : base(StatusMessage(code))
        {
          StatusCode = code;
        }

        public HTTPError(HttpStatusCode code, string message)
          : base(message)
        {
          StatusCode = code;
        }

        private static string StatusMessage(HttpStatusCode code)
        {
          return code.ToString();
        }
      };

      private void Send(string str)
      {
        Send(System.Text.Encoding.UTF8.GetBytes(str));
      }

      private string GetPhysicalPath(Uri uri)
      {
        var virtualpath = uri.AbsolutePath;
        if (virtualpath.StartsWith(this.owner.VirtualPath)) {
          return Path.GetFullPath(Path.Combine(this.owner.PhysicalPath, virtualpath.Substring(this.owner.VirtualPath.Length)));
        }
        else {
          return null;
        }
      }

      private FileDesc GetFileDesc(string ext)
      {
        FileDesc res;
        if (FileDescriptions.TryGetValue(ext, out res)) {
          return res;
        }
        else {
          return FileDescriptions[""];
        }
      }

      private Dictionary<string, string> ParseQuery(string query)
      {
        var res = new Dictionary<string, string>();
        if (query!=null && query.StartsWith("?")) {
          foreach (var q in request.Uri.Query.Substring(1).Split('&')) {
            var entry = q.Split('=');
            var key = Uri.UnescapeDataString(entry[0]).Replace('+', ' ');
            if (entry.Length>1) {
              var value = Uri.UnescapeDataString(entry[1]).Replace('+', ' ');
              res[key] = value;
            }
            else {
              res[key] = null;
            }
          }
        }
        return res;
      }

      protected override void OnStarted()
      {
        base.OnStarted();
        Logger.Debug("Started");
        try {
          if (this.request.Method!="HEAD" && this.request.Method!="GET") {
            throw new HTTPError(HttpStatusCode.MethodNotAllowed);
          }
          var localpath = GetPhysicalPath(this.request.Uri);
          if (localpath==null) throw new HTTPError(HttpStatusCode.Forbidden);
          if (Directory.Exists(localpath)) {
            localpath = Path.Combine(localpath, "index.html");
            if (!File.Exists(localpath)) throw new HTTPError(HttpStatusCode.Forbidden);
          }
          if (File.Exists(localpath)) {
            var contents = File.ReadAllBytes(localpath);
            var content_desc = GetFileDesc(Path.GetExtension(localpath));
            var parameters = new Dictionary<string, string> {
              {"Content-Type",   content_desc.MimeType },
              {"Content-Length", contents.Length.ToString() },
            };
            Send(HTTPUtils.CreateResponseHeader(HttpStatusCode.OK, parameters));
            if (this.request.Method=="GET") {
              if (content_desc.IsTemplate) {
                DotLiquid.Template.RegisterFilter(typeof(UriEscapeFilter));
                var template = DotLiquid.Template.Parse(System.Text.Encoding.UTF8.GetString(contents));
                var tmpparams = new DotLiquid.Hash();
                var query = ParseQuery(this.request.Uri.Query);
                if (query.ContainsKey("id")) {
                  var id = new Guid(query["id"]);
                  var channel = PeerCast.Channels.FirstOrDefault(c => c.ChannelID==id);
                  if (channel!=null) {
                    tmpparams.Add("channel", new ChannelController(channel));
                  }
                }
                tmpparams.Add("peercast", new PeerCastController(PeerCast));
                var result = template.Render(tmpparams);
                Send(System.Text.Encoding.UTF8.GetBytes(result));
              }
              else {
                Send(contents);
              }
            }
          }
          else {
            throw new HTTPError(HttpStatusCode.NotFound);
          }
        }
        catch (HTTPError err) {
          Send(HTTPUtils.CreateResponseHeader(err.StatusCode, new Dictionary<string, string> { }));
        }
        catch (UnauthorizedAccessException) {
          Send(HTTPUtils.CreateResponseHeader(HttpStatusCode.Forbidden, new Dictionary<string, string> { }));
        }
        Stop();
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

    public class HTMLHostOutputStreamFactory
      : OutputStreamFactoryBase
    {
      public override string Name
      {
        get { return "HTTP HTML Host UI"; }
      }

      public override OutputStreamType OutputStreamType
      {
        get { return OutputStreamType.Interface; }
      }

      HTMLHost owner;
      public HTMLHost Owner
      {
        get { return owner; }
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
        return new HTMLHostOutputStream(owner, PeerCast, input_stream, output_stream, remote_endpoint, request);
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
        if (res!=null && res.Uri.AbsolutePath.StartsWith(this.owner.VirtualPath)) {
          return Guid.Empty;
        }
        else {
          return null;
        }
      }

      public HTMLHostOutputStreamFactory(HTMLHost owner, PeerCast peercast)
        : base(peercast)
      {
        this.owner = owner;
      }
    }
  }

  [Plugin(PluginType.UserInterface)]
  public class HTMLHostFactory
    : IUserInterfaceFactory
  {
    public string Name { get { return "HTTP HTML Host UI"; } }

    public IUserInterface CreateUserInterface()
    {
      return new HTMLHost();
    }
  }
}
