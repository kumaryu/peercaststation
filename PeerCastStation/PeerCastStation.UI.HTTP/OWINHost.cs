using System;
using System.Linq;
using System.IO;
using System.Net;
using System.Collections.Generic;
using PeerCastStation.Core;
using PeerCastStation.HTTP;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.UI.HTTP
{
  using AppFunc = Func<
      IDictionary<string, object>, // Environment
      Task>; // Done

  public enum PathParameters {
    Any,
    None,
  }

  public class OWINApplication
  {
    public string Path { get; private set; }
    public PathParameters Parameters { get; private set; }
    public AppFunc AppFunc { get; private set; }
    public OWINApplication(string path, PathParameters parameters, AppFunc appfunc)
    {
      this.Path = path;
      this.Parameters = parameters;
      this.AppFunc = appfunc;
    }
  }

  public class OWINHostOutputStream
    : OutputStreamBase
  {
    private HTTPRequest request;
    private OWINApplication application;

    public OWINHostOutputStream(
      PeerCast peercast,
      Stream input_stream,
      Stream output_stream,
      EndPoint local_endpoint,
      EndPoint remote_endpoint,
      AccessControlInfo access_control,
      HTTPRequest request,
      byte[] header,
      OWINApplication application)
      : base(peercast, input_stream, output_stream, local_endpoint, remote_endpoint, access_control, null, header)
    {
      this.request = request;
      this.application = application;
      Logger.Debug("Initialized: Remote {0}", remote_endpoint);
    }

    public override ConnectionInfo GetConnectionInfo()
    {
      ConnectionStatus status = ConnectionStatus.Connected;
      if (IsStopped) {
        status = HasError ? ConnectionStatus.Error : ConnectionStatus.Idle;
      }
      return new ConnectionInfoBuilder {
        ProtocolName     = "OWIN Host",
        Type             = ConnectionType.Interface,
        Status           = status,
        RemoteName       = RemoteEndPoint.ToString(),
        RemoteEndPoint   = (IPEndPoint)RemoteEndPoint,
        RemoteHostStatus = IsLocal ? RemoteHostStatus.Local : RemoteHostStatus.None,
        RecvRate         = Connection.ReadRate,
        SendRate         = Connection.WriteRate,
        AgentName        = request.Headers["USER-AGENT"],
      }.Build();
    }

    private async Task<IDictionary<string, object>> CreateOWINEnvironment(CancellationToken cancel_token)
    {
      var env = new Dictionary<string, object>(StringComparer.Ordinal);
      env["owin.RequestMethod"]      = this.request.Method;
      env["owin.RequestScheme"]      = this.request.Uri.Query;
      env["owin.RequestPathBase"]    = application.Path;
      env["owin.RequestPath"]        = this.request.Uri.LocalPath.Substring(application.Path.Length);
      env["owin.RequestQueryString"] = this.request.Uri.Query;
      env["owin.RequestProtocol"]    = this.request.Protocol;
      var request_headers = new Dictionary<string, string[]>();
      foreach (var kv in this.request.Headers) {
        request_headers.Add(kv.Key, new string[] { kv.Value });
      }
      env["owin.RequestHeaders"]     = request_headers;
      string value;
      int length;
      if (request.Headers.TryGetValue("Content-Length", out value) && 
          Int32.TryParse(value, out length)) {
        var bytes = await this.Connection.ReadBytesAsync(length, cancel_token).ConfigureAwait(false);
        env["owin.RequestBody"] = new MemoryStream(bytes);
      }
      else if (request.ChunkedEncoding) {
        env["owin.RequestBody"] = new HTTPChunkedContentStream(this.Connection, true);
      }
      else {
        env["owin.RequestBody"] = this.Connection;
      }
      env["owin.CallCancelled"]      = cancel_token;
      env["owin.Version"]            = "OWIN 1.0.0";
      env["peercast.AccessControlInfo"] = this.AccessControlInfo;

      var response_headers = new Dictionary<string, string[]>();
      env["owin.ResponseHeaders"]    = response_headers;
      env["owin.ResponseBody"]       = new MemoryStream();
      env["owin.ResponseStatusCode"] = 200;
      env["owin.ResponseProtocol"]   = this.request.Protocol;
      SetHeader(response_headers, "Server", PeerCast.AgentName);
      SetHeader(response_headers, "Connection", "close");
      return env;
    }

    T GetEnvValue<T>(IDictionary<string, object> env, string key, T default_value)
    {
      object value;
      if (env.TryGetValue(key, out value) && value is T) {
        return (T)value;
      }
      else {
        return default_value;
      }
    }

    private void AddHeader(Dictionary<string, string[]> headers, string key, string value)
    {
      string[] v;
      if (headers.TryGetValue(key, out v)) {
        if (v.Any(line => line.Split(',').Select(token => token.Trim()).Any(token => token==value))) {
          return;
        }
        if (v.Length==1) {
          headers[key] = new string[] {  String.Join(",", v[0], value) };
        }
        else {
          headers[key] = v.Concat(new string[] { value }).ToArray();
        }
      }
      else {
        headers.Add(key, new string[] { value });
      }
    }

    private void SetHeader(Dictionary<string, string[]> headers, string key, string value)
    {
      string[] v;
      if (!headers.TryGetValue(key, out v)) {
        headers.Add(key, new string[] { value });
      }
    }

    private bool ResponseHasMessageBody(string request_method, int status_code)
    {
      if (request_method == "HEAD") {
        return false;
      }
      if (status_code >= 100 && status_code <= 199 || // 1xx Informational
          status_code == 204 || // 204 No Content
          status_code == 304) { // 304 Not Modified
        return false;
      }
      if (request_method == "CONNECT" &&
          status_code >= 200 && status_code <= 299) {
        return false;
      }

      return true;
    }

    private async Task ProcessResponse(IDictionary<string, object> env, CancellationToken cancel_token)
    {
      var headers       = (Dictionary<string, string[]>)env["owin.ResponseHeaders"];
      var body          = (MemoryStream)env["owin.ResponseBody"];
      var protocol      = GetEnvValue(env, "owin.ResponseProtocol", this.request.Protocol);
      var status_code   = GetEnvValue(env, "owin.ResponseStatusCode", 200);
      var reason_phrase = GetEnvValue(env, "owin.ResponseReasonPhrase", HTTPUtils.GetReasonPhrase(status_code));
      var method        = GetEnvValue(env, "owin.RequestMethod", "GET");
      body.Close();
      var body_ary = body.ToArray();
      if (ResponseHasMessageBody(method, status_code)) {
        SetHeader(headers, "Content-Length", body_ary.Length.ToString());
      }
      if (status_code==(int)HttpStatusCode.Unauthorized) {
        SetHeader(headers, "WWW-Authenticate", "Basic realm=\"PeerCastStation\"");
      }

      var header = new System.Text.StringBuilder($"{protocol} {status_code} {reason_phrase}\r\n");
      foreach (var kv in headers) {
        foreach (var v in kv.Value) {
          header.Append($"{kv.Key}: {v}\r\n");
        }
      }
      header.Append("\r\n");
      await Connection.WriteUTF8Async(header.ToString(), cancel_token).ConfigureAwait(false);
      if (body_ary.Length>0) {
        await Connection.WriteAsync(body_ary, cancel_token).ConfigureAwait(false);
      }
    }

    protected override async Task<StopReason> DoProcess(CancellationToken cancel_token)
    {
      try {
        var env = await CreateOWINEnvironment(cancel_token).ConfigureAwait(false);
        await application.AppFunc.Invoke(env).ConfigureAwait(false);
        await ProcessResponse(env, cancel_token).ConfigureAwait(false);
      }
      catch (IOException) {
        return StopReason.ConnectionError;
      }
      catch (Exception) {
        await Connection.WriteUTF8Async(
          HTTPUtils.CreateResponseHeader(HttpStatusCode.InternalServerError),
          cancel_token).ConfigureAwait(false);
      }
      return StopReason.OffAir;
    }

    public override OutputStreamType OutputStreamType {
      get { return OutputStreamType.Interface; }
    }
  }

  public class OWINHostOutputStreamFactory
    : OutputStreamFactoryBase
  {
    public override string Name {
      get { return "OWIN Host"; }
    }

    public override OutputStreamType OutputStreamType {
      get { return OutputStreamType.Interface; }
    }

    public override int Priority {
      get { return 10; }
    }

    private SynchronizedList<OWINApplication> applications = new SynchronizedList<OWINApplication>();

    public OWINApplication AddApplication(string path, PathParameters parameters, AppFunc application)
    {
      var app = new OWINApplication(path, parameters, application);
      applications.Add(app);
      return app;
    }

    public void RemoveApplication(OWINApplication app)
    {
      applications.Remove(app);
    }

    private static readonly char[] PathSeparator = new char[] { '/' };
    private OWINApplication FindApplication(Uri uri)
    {
      var path = uri.AbsolutePath.Split(PathSeparator, StringSplitOptions.RemoveEmptyEntries);
      return applications.FirstOrDefault(app => {
        var apppath = app.Path.Split(PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        switch (app.Parameters) {
        case PathParameters.Any:
          return apppath.Length<=path.Length &&
                 apppath.SequenceEqual(path.Take(apppath.Length));
        case PathParameters.None:
          return apppath.SequenceEqual(path);
        default:
          return false;
        }
      });
    }

    public override IOutputStream Create(
      Stream input_stream,
      Stream output_stream,
      EndPoint local_endpoint,
      EndPoint remote_endpoint,
      AccessControlInfo access_control,
      Guid channel_id,
      byte[] header)
    {
      using (var stream=new MemoryStream(header)) {
        var req = HTTPRequestReader.Read(stream);
        var bytes = stream.Position;
        var application = FindApplication(req.Uri);
        return new OWINHostOutputStream(
          PeerCast,
          input_stream,
          output_stream,
          local_endpoint,
          remote_endpoint,
          access_control,
          req,
          header.Skip((int)bytes).ToArray(),
          application);
      }
    }

    public override Guid? ParseChannelID(byte[] header)
    {
      using (var stream=new MemoryStream(header)) {
        var req = HTTPRequestReader.Read(stream);
        if (req==null) return null;
        if (FindApplication(req.Uri)!=null) {
          return Guid.Empty;
        }
        else {
          return null;
        }
      }
    }

    public OWINHostOutputStreamFactory(PeerCast peercast)
      : base(peercast)
    {
    }
  }

  [Plugin(PluginPriority.Higher)]
  public class OWINHost
    : PluginBase
  {
    override public string Name { get { return "HTTP OWIN Host"; } }
    private OWINHostOutputStreamFactory factory;
    override protected void OnAttach()
    {
      factory = new OWINHostOutputStreamFactory(Application.PeerCast);
      Application.PeerCast.OutputStreamFactories.Add(factory);
    }

    protected override void OnStart()
    {
    }

    protected override void OnStop()
    {
    }

    protected override void OnDetach()
    {
      Application.PeerCast.OutputStreamFactories.Remove(factory);
    }

  }
}
