using Owin;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Owin.Builder;
using System.Threading;

namespace PeerCastStation.Core.Http
{
  public static class HttpReasonPhrase
  {
    public const string Continue                     = "Continue";
    public const string SwitchingProtocols          = "Switching Protocols";
    public const string OK                          = "OK";
    public const string Created                     = "Created";
    public const string Accepted                    = "Accepted";
    public const string NonAuthoritativeInformation = "Non-Authoritative Information";
    public const string NoContent                   = "No Content";
    public const string ResetContent                = "Reset Content";
    public const string PartialContent              = "Partial Content";
    public const string MultipleChoices             = "Multiple Choices";
    public const string MovedPermanently            = "Moved Permanently";
    public const string Found                       = "Found";
    public const string SeeOther                    = "See Other";
    public const string NotModified                 = "Not Modified";
    public const string UseProxy                    = "Use Proxy";
    public const string TemporaryRedirect           = "Temporary Redirect";
    public const string BadRequest                  = "Bad Request";
    public const string Unauthorized                = "Unauthorized";
    public const string PaymentRequired             = "Payment Required";
    public const string Forbidden                   = "Forbidden";
    public const string NotFound                    = "Not Found";
    public const string MethodNotAllowed            = "Method Not Allowed";
    public const string NotAcceptable               = "Not Acceptable";
    public const string ProxyAuthenticationRequired = "Proxy Authentication Required";
    public const string RequestTimeout              = "Request Timeout";
    public const string Conflict                    = "Conflict";
    public const string Gone                        = "Gone";
    public const string LengthRequired              = "Length Required";
    public const string PreconditionFailed          = "Precondition Failed";
    public const string PayloadTooLarge             = "Payload Too Large";
    public const string URITooLong                  = "URI Too Long";
    public const string UnsupportedMediaType        = "Unsupported Media Type";
    public const string RangeNotSatisfiable         = "Range Not Satisfiable";
    public const string ExpectationFailed           = "Expectation Failed";
    public const string UpgradeRequired             = "Upgrade Required";
    public const string InternalServerError         = "Internal Server Error";
    public const string NotImplemented              = "Not Implemented";
    public const string BadGateway                  = "Bad Gateway";
    public const string ServiceUnavailable          = "Service Unavailable";
    public const string GatewayTimeout              = "Gateway Timeout";
    public const string HTTPVersionNotSupported     = "HTTP Version Not Supported";

    public static string GetReasonPhrase(int statusCode)
    {
      switch (statusCode) {
      case 100: return Continue;
      case 101: return SwitchingProtocols;
      case 200: return OK;
      case 201: return Created;
      case 202: return Accepted;
      case 203: return NonAuthoritativeInformation;
      case 204: return NoContent;
      case 205: return ResetContent;
      case 206: return PartialContent;
      case 300: return MultipleChoices;
      case 301: return MovedPermanently;
      case 302: return Found;
      case 303: return SeeOther;
      case 304: return NotModified;
      case 305: return UseProxy;
      case 307: return TemporaryRedirect;
      case 400: return BadRequest;
      case 401: return Unauthorized;
      case 402: return PaymentRequired;
      case 403: return Forbidden;
      case 404: return NotFound;
      case 405: return MethodNotAllowed;
      case 406: return NotAcceptable;
      case 407: return ProxyAuthenticationRequired;
      case 408: return RequestTimeout;
      case 409: return Conflict;
      case 410: return Gone;
      case 411: return LengthRequired;
      case 412: return PreconditionFailed;
      case 413: return PayloadTooLarge;
      case 414: return URITooLong;
      case 415: return UnsupportedMediaType;
      case 416: return RangeNotSatisfiable;
      case 417: return ExpectationFailed;
      case 426: return UpgradeRequired;
      case 500: return InternalServerError;
      case 501: return NotImplemented;
      case 502: return BadGateway;
      case 503: return ServiceUnavailable;
      case 504: return GatewayTimeout;
      case 505: return HTTPVersionNotSupported;
      default: throw new ArgumentOutOfRangeException(nameof(statusCode));
      }
    }

    public static string GetReasonPhrase(HttpStatusCode statusCode)
    {
      return GetReasonPhrase((int)statusCode);
    }
  }

  public class OwinEnvironment
  {
    public static class Owin {
      public const string RequestBody = "owin.RequestBody"; // A Stream with the request body, if any. Stream.Null MAY be used as a placeholder if there is no request body. See Request Body.
      public const string RequestHeaders = "owin.RequestHeaders"; // An IDictionary<string, string[]> of request headers. See Headers.
      public const string RequestMethod = "owin.RequestMethod"; // A string containing the HTTP request method of the request (e.g., "GET", "POST").
      public const string RequestPath = "owin.RequestPath"; // A string containing the request path. The path MUST be relative to the "root" of the application delegate. See Paths.
      public const string RequestPathBase = "owin.RequestPathBase"; // A string containing the portion of the request path corresponding to the "root" of the application delegate; see Paths.
      public const string RequestProtocol = "owin.RequestProtocol"; // A string containing the protocol name and version (e.g. "HTTP/1.0" or "HTTP/1.1").
      public const string RequestQueryString = "owin.RequestQueryString"; // A string containing the query string component of the HTTP request URI, without the leading "?" (e.g., "foo=bar&amp;baz=quux"). The value may be an empty string.
      public const string RequestScheme = "owin.RequestScheme"; // A string containing the URI scheme used for the request (e.g., "http", "https"); see URI Scheme.
      public const string RequestId = "owin.RequestId"; // An optional string that uniquely identifies a request. The value is opaque and SHOULD have some level of uniqueness. A Host MAY specify this value. If it is not specified, middleware MAY set it. Once set, it SHOULD NOT be modified.
      public const string RequestUser = "owin.RequestUser"; // An optional identity that represents the user associated with a request. The identity MUST be a ClaimsPrincipal. Middleware MAY specify this value. If it is not specified, middleware MAY set it. Once set, it MAY BE modified.
      public const string ResponseBody = "owin.ResponseBody"; // A Stream used to write out the response body, if any. See Response Body.
      public const string ResponseHeaders = "owin.ResponseHeaders"; // An IDictionary<string, string[]> of response headers. See Headers.
      public const string ResponseStatusCode = "owin.ResponseStatusCode"; // An optional int containing the HTTP response status code as defined in RFC 2616 section 6.1.1. The default is 200.
      public const string ResponseReasonPhrase = "owin.ResponseReasonPhrase"; // An optional string containing the reason phrase associated the given status code. If none is provided then the server SHOULD provide a default as described in RFC 2616 section 6.1.1
      public const string ResponseProtocol = "owin.ResponseProtocol"; // An optional string containing the protocol name and version (e.g. "HTTP/1.0" or "HTTP/1.1"). If none is provided then the "owin.RequestProtocol" key's value is the default.
      public const string CallCancelled = "owin.CallCancelled"; // A CancellationToken indicating if the request has been canceled/aborted. See [Request Lifetime][sec-req-lifetime].
      public const string Version = "owin.Version"; // A string indicating the OWIN version. See Versioning.
    }
    public static class SSL {
      public const string ClientCertificate = "ssl.ClientCertificate"; // The client certificate provided during HTTPS SSL negotiation.
    }
    public static class Server {
      public const string RemoteIpAddress = "server.RemoteIpAddress"; // The IP Address of the remote client. E.g. 192.168.1.1 or ::1
      public const string RemotePort = "server.RemotePort"; // The port of the remote client. E.g. 1234
      public const string LocalIpAddress = "server.LocalIpAddress"; // The local IP Address the request was received on. E.g. 127.0.0.1 or ::1
      public const string LocalPort = "server.LocalPort"; // The port the request was received on. E.g. 80
      public const string IsLocal = "server.IsLocal"; // Was the request sent from the same machine? E.g. true or false.
      public const string Capabilities = "server.Capabilities"; // Global capabilities that do not change on a per-request basis. See Section 5 above.
      public const string OnSendingHeaders = "server.OnSendingHeaders"; // Allows the caller to register an Action callback that fires as a last chance to modify response headers, status code, reason phrase, or protocol. The object parameter is an optional state object that will passed to the callback.
      public const string OnInit = "server.OnInit"; // An Action<Func<Task>> that allows middleware to register a callback that the server will call once when initializing.
      public const string OnDispose = "server.OnDispose"; // A CancellationToken that represents when the server is disposing.
    }
    public static class Host {
      public const string TraceOutput = "host.TraceOutput"; // A tracing output that may be provided by the host.
      public const string Addresses = "host.Addresses"; // A list of per-address server configuration. The following keys are defined with string values: scheme, host, port, path.
    }
    public static class PeerCastStation {
      public const string PeerCastApplication = "peercaststation.PeerCastApplication";
      public const string PeerCast = "peercaststation.PeerCast";
    }

    [Flags]
    public enum TransferEncoding {
      Identity = 0x0,
      Chunked  = 0x1,
      Compress = 0x2,
      Deflate  = 0x4,
      GZip     = 0x8,
      Brotli   = 0x10,
      Exi      = 0x20,
      Unsupported = 0x8000,
    };


    public IDictionary<string,object> Environment { get; private set; }
    public OwinEnvironment()
      : this(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase))
    {
    }

    public OwinEnvironment(IDictionary<string,object> env)
    {
      Environment = env;
    }

    public bool TryGetValue<T>(string name, out T value)
    {
      if (Environment.TryGetValue(name, out var val) && val is T) {
        value = (T)val;
        return true;
      }
      else {
        value = default(T);
        return false;
      }
    }

    public bool ContainsKey(string name)
    {
      return Environment.ContainsKey(name);
    }

    private static readonly Regex SimpleFieldValuePattern = new Regex(@"^[A-Za-z0-9!#$%&'*+\-.^_|~]*$", RegexOptions.Compiled);
    private static readonly Regex CommaSeparatedFieldValuePattern = new Regex(@"^([A-Za-z0-9!#$%&'*+\-.^_|~]*,\s*)+[A-Za-z0-9!#$%&'*+\-.^_|~]*$", RegexOptions.Compiled);
    private static readonly Regex SpaceSeparatedFieldValuePattern = new Regex(@"^([A-Za-z0-9!#$%&'*+\-.^_|~]+\s+)+[A-Za-z0-9!#$%&'*+\-.^_|~]+$", RegexOptions.Compiled);
    public IList<string> ParseHeaderFieldValueList(string str)
    {
      str = str.Trim();
      if (SimpleFieldValuePattern.IsMatch(str)) {
        return new string[] { str.Trim() };
      }
      else if (CommaSeparatedFieldValuePattern.IsMatch(str)) {
        return str.Split(',').Select(v => v.Trim()).ToArray();
      }
      else if (SpaceSeparatedFieldValuePattern.IsMatch(str)) {
        return str.Split(' ', '\t').Where(v => v.Length>0).ToArray();
      }
      else {
        var lst = new List<string>();
        var buffer = new System.Text.StringBuilder();
        bool escaped = false;
        bool quated = false;
        int commented = 0;
        foreach (var c in str) {
          if (escaped) {
            buffer.Append(c);
          }
          else if (c=='\\') {
            escaped = true;
          }
          else if (c=='"') {
            if (commented>0) {
              //ignore
            }
            else {
              quated = !quated;
            }
          }
          else if (c=='(') {
            if (commented>0 || !quated) {
              commented += 1;
            }
            else {
              buffer.Append(c);
            }
          }
          else if (c==')') {
            if (commented>0) {
              commented -= 1;
            }
            else {
              buffer.Append(c);
            }
          }
          else if (c==' ' || c=='\t') {
            if (commented>0) {
              //ignore
            }
            else if (quated) {
              buffer.Append(c);
            }
            else if (buffer.Length>0) {
              lst.Add(buffer.ToString());
              buffer.Clear();
            }
          }
          else if (c==',') {
            if (commented>0) {
              //ignore
            }
            else if (quated) {
              buffer.Append(c);
            }
            else {
              lst.Add(buffer.ToString());
              buffer.Clear();
            }
          }
          else if (commented==0) {
            buffer.Append(c);
          }
        }
        if (buffer.Length>0) {
          lst.Add(buffer.ToString());
        }
        return lst;
      }
    }

    public string GetHttpHeader(string envKey, string key, string defval)
    {
      if (TryGetValue<IDictionary<string,string[]>>(envKey, out var headers) &&
          headers.TryGetValue(key, out var values) &&
          values!=null && values.Length>0) {
        return values[0];
      }
      else {
        return defval;
      }
    }

    public string GetRequestHeader(string key, string defval)
    {
      return GetHttpHeader(Owin.RequestHeaders, key, defval);
    }

    public IEnumerable<string> GetHttpHeader(string envKey, string key, string[] defval)
    {
      if (TryGetValue<IDictionary<string,string[]>>(envKey, out var headers) &&
          headers.TryGetValue(key, out var values) &&
          values!=null && values.Length>0) {
        return values.SelectMany(v => ParseHeaderFieldValueList(v));
      }
      else {
        return defval;
      }
    }

    public IEnumerable<string> GetRequestHeader(string key, string[] defval)
    {
      return GetHttpHeader(Owin.RequestHeaders, key, defval);
    }

    public bool RequestHeaderContainsKey(string key)
    {
      return TryGetValue<IDictionary<string,string[]>>(Owin.RequestHeaders, out var headers) &&
             headers.ContainsKey(key);
    }

    public bool ResponseHeaderContainsKey(string key)
    {
      return TryGetValue<IDictionary<string,string[]>>(Owin.ResponseHeaders, out var headers) &&
             headers.ContainsKey(key);
    }

    public void SetResponseHeader(string key, string value)
    {
      if (TryGetValue<IDictionary<string,string[]>>(Owin.ResponseHeaders, out var headers)) {
        headers[key] = new string[] { value };
      }
    }

    public string Get(string name, string defval)
    {
      if (Environment.TryGetValue(name, out var val) && val!=null) {
        try {
          return val.ToString();
        }
        catch (Exception) {
          return defval;
        }
      }
      else {
        return defval;
      }
    }
    public int Get(string name, int defval)
    {
      if (Environment.TryGetValue(name, out var val) && val!=null) {
        try {
          return Convert.ToInt32(val);
        }
        catch (Exception) {
          return defval;
        }
      }
      else {
        return defval;
      }
    }

    private TransferEncoding GetTransferEncoding(string envKey)
    {
      var result = TransferEncoding.Identity;
      var encodings = GetHttpHeader(envKey, "Transfer-Encoding", new string[0]);
      foreach (var enc in encodings) {
        if (StringComparer.OrdinalIgnoreCase.Equals("chunked", enc)) {
          result |= TransferEncoding.Chunked;
        }
        else if (StringComparer.OrdinalIgnoreCase.Equals("deflate", enc)) {
          result |= TransferEncoding.Deflate;
        }
        else if (StringComparer.OrdinalIgnoreCase.Equals("gzip", enc) || StringComparer.OrdinalIgnoreCase.Equals("x-gzip", enc)) {
          result |= TransferEncoding.GZip;
        }
        else if (StringComparer.OrdinalIgnoreCase.Equals("compress", enc) || StringComparer.OrdinalIgnoreCase.Equals("x-compress", enc)) {
          result |= TransferEncoding.Compress;
        }
        else if (StringComparer.OrdinalIgnoreCase.Equals("br", enc)) {
          result |= TransferEncoding.Brotli;
        }
        else if (StringComparer.OrdinalIgnoreCase.Equals("exi", enc)) {
          result |= TransferEncoding.Exi;
        }
        else if (StringComparer.OrdinalIgnoreCase.Equals("identity", enc)) {
          result |= TransferEncoding.Identity;
        }
        else {
          result |= TransferEncoding.Unsupported;
        }
      }
      return result;
    }

    public TransferEncoding GetRequestTransferEncoding()
    {
      return GetTransferEncoding(Owin.RequestHeaders);
    }

    public TransferEncoding GetResponseTransferEncoding()
    {
      return GetTransferEncoding(Owin.ResponseHeaders);
    }

    public bool IsKeepAlive()
    {
      if (Get(Owin.RequestProtocol, "HTTP/1.1")=="HTTP/1.0") {
        return RequestHeaderContainsKey("Keep-Alive");
      }
      if (Get(Owin.ResponseProtocol, "HTTP/1.1")=="HTTP/1.0") {
        return ResponseHeaderContainsKey("Keep-Alive");
      }
      if (StringComparer.OrdinalIgnoreCase.Equals(GetRequestHeader("Connection", ""), "close")) {
        return false;
      }
      return true;
    }

  }

  public class PeerCastOwinAppBuilder
    : AppBuilder
  {
    public PeerCastApplication Application { get; private set; }
    public PeerCast PeerCast { get; private set; }

    private List<Func<Task>> onInitActions = new List<Func<Task>>();
    public IReadOnlyList<Func<Task>> OnInitActions { get { return onInitActions; } }
    public IDictionary<string,object> Capabilities { get; private set; } = new Dictionary<string,object>(StringComparer.Ordinal);
    public TextWriter TraceOutput {
      get { return Properties[OwinEnvironment.Host.TraceOutput] as TextWriter; }
      set { Properties[OwinEnvironment.Host.TraceOutput] = value; }
    }
    public IList<IDictionary<string,object>> Addresses { get; private set; } = new List<IDictionary<string,object>>();

    public void AddAddress(string scheme, string host, string port, string path)
    {
      Addresses.Add(new Dictionary<string,object>(StringComparer.Ordinal) {
        { "scheme", scheme },
        { "host", host },
        { "port", port },
        { "path", path },
      });
    }

    public PeerCastOwinAppBuilder(PeerCastApplication app, PeerCast peerCast, CancellationToken cancellationToken)
    {
      Application = app;
      PeerCast = peerCast;
      Properties[OwinEnvironment.PeerCastStation.PeerCastApplication] = app;
      Properties[OwinEnvironment.PeerCastStation.PeerCast] = peerCast;
      Properties[OwinEnvironment.Server.OnInit] = new Action<Func<Task>>(func => onInitActions.Add(func));
      Properties[OwinEnvironment.Server.OnDispose] = cancellationToken;
      Properties[OwinEnvironment.Server.Capabilities] = Capabilities;
      Properties[OwinEnvironment.Host.TraceOutput] = TextWriter.Null;
      Properties[OwinEnvironment.Host.Addresses] = new List<Dictionary<string,object>>();
    }
  }

  internal class LoggerWriter
    : TextWriter
  {
    public override System.Text.Encoding Encoding {
      get { return System.Text.Encoding.UTF8; }
    }

    public Logger Logger { get; private set; }
    public LogLevel Level { get; private set; }
    private System.Text.StringBuilder buffer = new System.Text.StringBuilder(256);

    public LoggerWriter(Logger logger, LogLevel level)
    {
      Logger = logger;
      Level = level;
    }

    public override void Write(char value)
    {
      buffer.Append(value);
      if (value=='\n') Flush();
    }

    public override void Write(char[] buffer, int index, int count)
    {
      if (buffer==null) throw new ArgumentNullException("buffer");
      if (index<0) throw new ArgumentOutOfRangeException("index");
      if (count<0) throw new ArgumentOutOfRangeException("count");
      if (buffer.Length-index<count) throw new ArgumentException();
      this.buffer.Append(buffer, index, count);
      if (count==0 || buffer[index+count-1]=='\n') Flush();
    }

    public override void Write(string value)
    {
      if (value==null) return;
      buffer.Append(value);
      if (value.EndsWith("\n")) Flush();
    }

    public override void Flush()
    {
      if (buffer.Length==0) return;
      Logger.Write(Level, buffer.ToString());
      buffer.Clear();
    }
  }

  [Plugin(PluginType.Protocol, PluginPriority.Higher)]
  public class OwinHost
    : IDisposable
  {
    private class Factory
      : IDisposable
    {
      public OwinHost Host { get; private set; }
      public int Key { get; private set; }
      public Action<IAppBuilder> ConfigAction { get; private set; }

      public Factory(OwinHost host, int key, Action<IAppBuilder> action)
      {
        Host = host;
        Key = key;
        ConfigAction = action;
      }

      public void Dispose()
      {
        if (Host==null || Key<0) return;
        Host.Unregister(Key);
      }
    }

    private Logger logger = new Logger(typeof(OwinHost));
    private SortedList<int,Factory> applicationFactories = new SortedList<int, Factory>();
    private CancellationTokenSource stopCancellationTokenSource = new CancellationTokenSource();
    private int nextKey = 0;

    private Func<IDictionary<string,object>, Task> owinApp = null;
    public Func<IDictionary<string,object>, Task> OwinApp {
      get {
        if (owinApp==null) {
          owinApp = Build();
        }
        return owinApp;
      }
    }

    public PeerCastApplication Application { get; private set; }
    public PeerCast PeerCast { get; private set; }

    public OwinHost(PeerCastApplication application, PeerCast peerCast)
    {
      Application = application;
      PeerCast = peerCast;
    }

    public void Dispose()
    {
      owinApp = null;
      stopCancellationTokenSource.Cancel();
    }

    public IDisposable Register(Action<IAppBuilder> configAction)
    {
      var key = nextKey++;
      var factory = new Factory(this, key, configAction);
      applicationFactories.Add(key, factory);
      return factory;
    }

    internal void Unregister(int key)
    {
      applicationFactories.Remove(key);
      owinApp = null;
    }

    private Func<IDictionary<string,object>, Task> Build()
    {
      var builder = new PeerCastOwinAppBuilder(Application, PeerCast, stopCancellationTokenSource.Token);
      foreach (var factory in applicationFactories) {
        factory.Value.ConfigAction(builder);
      }
      builder.TraceOutput = new LoggerWriter(logger, LogLevel.Debug);
      var appfunc = builder.Build<Func<IDictionary<string,object>, Task>>();
      return (env) => {
        env[OwinEnvironment.Server.Capabilities] = builder.Capabilities;
        env[OwinEnvironment.Host.TraceOutput] = builder.TraceOutput;
        return appfunc(env);
      };
    }

    private async Task Invoke(
      Func<IDictionary<string,object>, Task> func,
      HttpRequest req,
      ConnectionStream stream,
      IPEndPoint localEndPoint,
      IPEndPoint remoteEndPoint,
      IDictionary<string,object> env,
      CancellationToken cancellationToken)
    {
      var response = new OwinResponseBodyStream(env, stream);
      env[OwinEnvironment.Owin.CallCancelled] = cancellationToken;
      env[OwinEnvironment.Owin.Version] = "1.0.1";
      env[OwinEnvironment.Owin.RequestBody] = new OwinRequestBodyStream(env, stream);
      env[OwinEnvironment.Owin.RequestHeaders] = req.Headers.ToDictionary();
      env[OwinEnvironment.Owin.RequestPath] = req.Path;
      env[OwinEnvironment.Owin.RequestPathBase] = "/";
      env[OwinEnvironment.Owin.RequestProtocol] = req.Protocol;
      env[OwinEnvironment.Owin.RequestQueryString] = req.QueryString;
      env[OwinEnvironment.Owin.RequestScheme] = "http";
      env[OwinEnvironment.Owin.ResponseBody] = response;
      env[OwinEnvironment.Owin.ResponseHeaders] = new Dictionary<string,string[]>(StringComparer.OrdinalIgnoreCase);
      env[OwinEnvironment.Server.RemoteIpAddress] = remoteEndPoint.Address.ToString();
      env[OwinEnvironment.Server.RemotePort] = remoteEndPoint.Port.ToString();
      env[OwinEnvironment.Server.IsLocal] = remoteEndPoint.Address.GetAddressLocality()==0;
      env[OwinEnvironment.Server.LocalIpAddress] = localEndPoint.Address.ToString();
      env[OwinEnvironment.Server.LocalPort] = localEndPoint.Port.ToString();
      await func.Invoke(env).ConfigureAwait(false);
      await response.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task Invoke(
      HttpRequest req,
      ConnectionStream stream,
      IPEndPoint localEndPoint,
      IPEndPoint remoteEndPoint,
      IDictionary<string,object> env,
      CancellationToken cancellationToken)
    {
      return Invoke(OwinApp, req, stream, localEndPoint, remoteEndPoint, env, cancellationToken);
    }
  }

  class OwinHostOutputStream
    : OutputStreamBase
  {
    private OwinHost owinHost;
    public OwinHostOutputStream(PeerCast peercast, OwinHost host, Stream input_stream, Stream output_stream, EndPoint remote_endpoint, AccessControlInfo access_control, Channel channel, byte[] header)
      : base(peercast, input_stream, output_stream, remote_endpoint, access_control, channel, header)
    {
      owinHost = host;
    }

    public override OutputStreamType OutputStreamType {
      get { return OutputStreamType.Interface; }
    }

    public override ConnectionInfo GetConnectionInfo()
    {
      return new ConnectionInfo("HTTP", ConnectionType.Interface, ConnectionStatus.Connected, RemoteEndPoint.ToString(), RemoteEndPoint as IPEndPoint, RemoteHostStatus.None, null, null, null, null, null, null, "Owin");
    }

    protected override async Task<StopReason> DoProcess(CancellationToken cancel_token)
    {
      var keep_count = 10;
      while (!cancel_token.IsCancellationRequested && keep_count-->0) {
        HttpRequest req;
        using (var reader=new HttpRequestReader(Connection, true)) {
          req = await reader.ReadAsync(cancel_token).ConfigureAwait(false);
        }
        var env = new OwinEnvironment();
        try {
          await owinHost.Invoke(req, Connection, RemoteEndPoint as IPEndPoint, RemoteEndPoint as IPEndPoint, env.Environment, cancel_token).ConfigureAwait(false);
        }
        catch (Exception ex) {
          Logger.Error(ex);
          return StopReason.NotIdentifiedError;
        }
        if (!env.IsKeepAlive()) {
          break;
        }
      }
      return StopReason.OffAir;
    }
  }

  public class OwinHostOutputStreamFactory
    : OutputStreamFactoryBase
  {
    public override string Name { get { return nameof(OwinHostOutputStreamFactory); } }

    public override int Priority { get { return -1; } }

    public override OutputStreamType OutputStreamType { get { return OutputStreamType.Relay | OutputStreamType.Play | OutputStreamType.Interface | OutputStreamType.Metadata; } }

    private OwinHost owinHost;

    public OwinHostOutputStreamFactory(PeerCast peerCast, OwinHost host)
      : base(peerCast)
    {
      owinHost = host;
    }

    public override IOutputStream Create(Stream input_stream, Stream output_stream, EndPoint remote_endpoint, AccessControlInfo access_control, Guid channel_id, byte[] header)
    {
      var channel = channel_id!=Guid.Empty ? PeerCast.Channels.FirstOrDefault(c => c.ChannelID==channel_id) : null;
      return new OwinHostOutputStream(PeerCast, owinHost, input_stream, output_stream, remote_endpoint, access_control, channel, header);
    }

    public override Guid? ParseChannelID(byte[] header)
    {
      var idx = Array.IndexOf(header, (byte)'\r');
      if (idx<0 ||
          idx==header.Length-1 ||
          header[idx+1]!='\n') {
        return null;
      }
      try {
        var reqline = HttpRequest.ParseRequestLine(System.Text.Encoding.ASCII.GetString(header, 0, idx));
        if (reqline!=null) {
          return Guid.Empty;
        }
        else {
          return null;
        }
      }
      catch (ArgumentException) {
        return null;
      }
    }

  }

  [Plugin(PluginType.Protocol, PluginPriority.Higher)]
  public class OwinHostPlugin
    : PluginBase
  {
    private Logger logger = new Logger(typeof(OwinHostPlugin));
    public OwinHost OwinHost { get; private set; }
    private OwinHostOutputStreamFactory factory;

    public override string Name {
      get { return nameof(OwinHostPlugin); }
    }

    protected override void OnAttach()
    {
      OwinHost = new OwinHost(Application, Application.PeerCast);
      if (factory==null) {
        factory = new OwinHostOutputStreamFactory(Application.PeerCast, OwinHost);
      }
      Application.PeerCast.OutputStreamFactories.Add(factory);
    }

    protected override void OnDetach()
    {
      OwinHost = null;
      Application.PeerCast.OutputStreamFactories.Remove(factory);
    }

    protected override void OnStart()
    {
    }

    protected override void OnStop()
    {
      OwinHost.Dispose();
    }
  }

}

