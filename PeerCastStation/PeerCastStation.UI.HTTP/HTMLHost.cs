using System;
using System.IO;
using System.Net;
using System.Linq;
using PeerCastStation.Core;
using PeerCastStation.HTTP;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.UI.HTTP
{
  [Plugin]
  public class HTMLHost
    : PluginBase
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

    override public string Name { get { return "HTTP File Host UI"; } }
    public SortedList<string, string> VirtualPhysicalPathMap { get { return virtualPhysicalPathMap; } }
    private SortedList<string, string> virtualPhysicalPathMap = new SortedList<string,string>();

    public HTMLHost()
    {
      var basepath = Path.GetFullPath(Path.GetDirectoryName(typeof(HTMLHost).Assembly.Location));
      virtualPhysicalPathMap.Add("/html/", Path.Combine(basepath, "html"));
      virtualPhysicalPathMap.Add("/help/", Path.Combine(basepath, "help"));
      virtualPhysicalPathMap.Add("/Content/", Path.Combine(basepath, "Content"));
      virtualPhysicalPathMap.Add("/Scripts/", Path.Combine(basepath, "Scripts"));
    }

    HTMLHostOutputStreamFactory factory;
    override protected void OnAttach()
    {
      factory = new HTMLHostOutputStreamFactory(this, Application.PeerCast);
      Application.PeerCast.OutputStreamFactories.Add(factory);
    }

    override protected void OnDetach()
    {
      if (factory!=null) {
        Application.PeerCast.OutputStreamFactories.Remove(factory);
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
        AccessControlInfo access_control,
        HTTPRequest request)
        : base(peercast, input_stream, output_stream, remote_endpoint, access_control, null, null)
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
          "HTML Host",
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

      private async Task SendResponseMoveToIndex(CancellationToken cancel_token)
      {
        var content = "Moving...";
        var parameters = new Dictionary<string, string> {
          {"Content-Type",   "text/plain" },
          {"Content-Length", content.Length.ToString() },
          {"Location",       "/html/index.html" },
        };
        await Connection.WriteUTF8Async(HTTPUtils.CreateResponseHeader(HttpStatusCode.Moved, parameters), cancel_token);
        if (this.request.Method=="GET") {
          await Connection.WriteUTF8Async(content, cancel_token);
        }
      }

      private async Task SendResponseFileContent(CancellationToken cancel_token)
      {
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
          await Connection.WriteUTF8Async(HTTPUtils.CreateResponseHeader(HttpStatusCode.OK, parameters), cancel_token);
          if (this.request.Method=="GET") {
            await Connection.WriteAsync(contents, cancel_token);
          }
        }
        else {
          throw new HTTPError(HttpStatusCode.NotFound);
        }
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
          if (this.request.Uri.AbsolutePath=="/") {
            await SendResponseMoveToIndex(cancel_token);
          }
          else {
            await SendResponseFileContent(cancel_token);
          }
        }
        catch (HTTPError err) {
          await Connection.WriteUTF8Async(HTTPUtils.CreateResponseHeader(err.StatusCode), cancel_token);
        }
        catch (UnauthorizedAccessException) {
          await Connection.WriteUTF8Async(HTTPUtils.CreateResponseHeader(HttpStatusCode.Forbidden), cancel_token);
        }
        return StopReason.OffAir;
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
        AccessControlInfo access_control,
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
          catch (InvalidDataException) {
          }
        }
        return new HTMLHostOutputStream(owner, PeerCast, input_stream, output_stream, remote_endpoint, access_control, request);
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
          catch (InvalidDataException) {
          }
        }
        if (res!=null &&
            (this.owner.VirtualPhysicalPathMap.Any(kv => res.Uri.AbsolutePath.StartsWith(kv.Key)) ||
             res.Uri.AbsolutePath=="/")) {
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
}
