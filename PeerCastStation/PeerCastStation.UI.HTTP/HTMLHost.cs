using System;
using System.IO;
using System.Net;
using System.Linq;
using PeerCastStation.Core;
using PeerCastStation.HTTP;
using System.Collections.Generic;

namespace PeerCastStation.UI.HTTP
{
  public class HTMLHost
    : MarshalByRefObject,
      IUserInterface
  {
    class FileDesc
    {
      public string MimeType   { get; set; }
    }
    private static readonly Dictionary<string, FileDesc> FileDescriptions = new Dictionary<string, FileDesc> {
      { ".html", new FileDesc { MimeType="text/html" } },
      { ".htm",  new FileDesc { MimeType="text/html" } },
      { ".txt",  new FileDesc { MimeType="text/plain" } },
      { ".xml",  new FileDesc { MimeType="text/xml" } },
      { ".json", new FileDesc { MimeType="application/json" } },
      { ".css",  new FileDesc { MimeType="text/css" } },
      { ".js",   new FileDesc { MimeType="application/javascript" } },
      { ".bmp",  new FileDesc { MimeType="image/bmp" } },
      { ".png",  new FileDesc { MimeType="image/png" } },
      { ".jpg",  new FileDesc { MimeType="image/jpeg" } },
      { ".gif",  new FileDesc { MimeType="image/gif" } },
      { ".svg",  new FileDesc { MimeType="image/svg+xml" } },
      { ".swf",  new FileDesc { MimeType="application/x-shockwave-flash" } },
      { ".xap",  new FileDesc { MimeType="application/x-silverlight-app" } },
      { "",      new FileDesc { MimeType="application/octet-stream" } },
    };

    public string Name { get { return "HTTP HTML Host UI"; } }
    public SortedList<string, string> VirtualPhysicalPathMap { get { return virtualPhysicalPathMap; } }
    private SortedList<string, string> virtualPhysicalPathMap = new SortedList<string,string>();

    public HTMLHost()
    {
      var basepath = Path.GetFullPath(Path.GetDirectoryName(typeof(HTMLHost).Assembly.Location));
      virtualPhysicalPathMap.Add("/html/", Path.Combine(basepath, "html"));
      virtualPhysicalPathMap.Add("/Content/", Path.Combine(basepath, "Content"));
      virtualPhysicalPathMap.Add("/Scripts/", Path.Combine(basepath, "Scripts"));
    }

    HTMLHostOutputStreamFactory factory;
    PeerCastApplication application;
    public void Start(PeerCastApplication app)
    {
      application = app;
      factory = new HTMLHostOutputStreamFactory(this, application.PeerCast);
      application.PeerCast.OutputStreamFactories.Add(factory);
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
        var map = this.owner.VirtualPhysicalPathMap.Reverse().FirstOrDefault(kv => virtualpath.StartsWith(kv.Key));
        if (map.Key!=null && map.Value!=null) {
          return Path.GetFullPath(Path.Combine(map.Value, virtualpath.Substring(map.Key.Length)));
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
              Send(contents);
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

      public override int Priority
      {
        get { return 10; }
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
        if (res!=null &&
            this.owner.VirtualPhysicalPathMap.Any(kv =>
              res.Uri.AbsolutePath.StartsWith(kv.Key))) {
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

  [Plugin]
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
