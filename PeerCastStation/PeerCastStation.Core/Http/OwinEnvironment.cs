using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

namespace PeerCastStation.Core.Http
{
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
      public const string AccessControlInfo = "peercaststation.AccessControlInfo";
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

    private IReadOnlyDictionary<string,string> queryCache = null;
    public IReadOnlyDictionary<string,string> GetQueryParameters()
    {
      if (queryCache!=null) return queryCache;
      if (TryGetValue(Owin.RequestQueryString, out string query)) {
        queryCache =
          query
          .Split('&')
          .Where(pair => !String.IsNullOrWhiteSpace(pair))
          .Select(pair => {
            var idx = pair.IndexOf('=');
            if (idx>=0) {
              return new string [] { Uri.UnescapeDataString(pair.Substring(0,idx)), Uri.UnescapeDataString(pair.Substring(idx+1)) };
            }
            else {
              return new string [] { Uri.UnescapeDataString(pair), "" };
            }
          })
          .ToDictionary(kv => kv[0], kv => kv[1], StringComparer.OrdinalIgnoreCase);
      }
      else {
        queryCache = new Dictionary<string,string>();
      }
      return queryCache;
    }

    private IReadOnlyDictionary<string,string> cookieCache = null;
    public IReadOnlyDictionary<string,string> GetRequestCookies()
    {
      if (cookieCache!=null) return cookieCache;
      var cookies = GetRequestHeader("Cookie", new string[0]);
      cookieCache =
        cookies
        .SelectMany(ent => ent.Split(';'))
        .Where(pair => !String.IsNullOrWhiteSpace(pair))
        .Select(pair => {
          var idx = pair.IndexOf('=');
          if (idx>=0) {
            return new string [] { Uri.UnescapeDataString(pair.Substring(0,idx).Trim()), Uri.UnescapeDataString(pair.Substring(idx+1).Trim()) };
          }
          else {
            return new string [] { Uri.UnescapeDataString(pair.Trim()), "" };
          }
        })
        .ToDictionary(kv => kv[0], kv => kv[1], StringComparer.OrdinalIgnoreCase);
      return cookieCache;
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

    public void SetResponseHeaderOptional(string key, Func<string> generator)
    {
      if (TryGetValue<IDictionary<string,string[]>>(Owin.ResponseHeaders, out var headers) &&
          !headers.ContainsKey(key)) {
        headers[key] = new string[] { generator() };
      }
    }

    public void SetResponseHeaderOptional(string key, Func<string[]> generator)
    {
      if (TryGetValue<IDictionary<string,string[]>>(Owin.ResponseHeaders, out var headers) &&
          !headers.ContainsKey(key)) {
        headers[key] = generator();
      }
    }

    public void SetResponseHeaderOptional(string key, string value)
    {
      if (TryGetValue<IDictionary<string,string[]>>(Owin.ResponseHeaders, out var headers) &&
          !headers.ContainsKey(key)) {
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

}
