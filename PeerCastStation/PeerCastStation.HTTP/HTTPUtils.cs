using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Linq;

namespace PeerCastStation.HTTP
{
  public class HTTPError : ApplicationException
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
  }

  public class HTTPUtils
  {
    private static string GetAuthorizationToken(PeerCastStation.HTTP.HTTPRequest request)
    {
      String result = null;
      if (request.Headers.ContainsKey("AUTHORIZATION")) {
        var md = System.Text.RegularExpressions.Regex.Match(
          request.Headers["AUTHORIZATION"],
          @"\s*BASIC (\S+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (md.Success) {
          result = md.Groups[1].Value;
        }
      }
      if (result==null) {
        request.Parameters.TryGetValue("auth", out result);
      }
      if (result==null) {
        request.Cookies.TryGetValue("auth", out result);
      }
      return result;
    }

    public static bool CheckAuthorization(string authorization_token, PeerCastStation.Core.AccessControlInfo acinfo)
    {
      if (!acinfo.AuthorizationRequired || acinfo.AuthenticationKey==null) return true;
      if (authorization_token==null) return false;
      var authorized = false;
      try {
        var authorization = System.Text.Encoding.ASCII.GetString(Convert.FromBase64String(authorization_token)).Split(':');
        if (authorization.Length>=2) {
          var user = authorization[0];
          var pass = String.Join(":", authorization.Skip(1).ToArray());
          authorized = acinfo.CheckAuthorization(user, pass);
        }
      }
      catch (FormatException) {
      }
      catch (ArgumentException) {
      }
      return authorized;
    }

    public static bool CheckAuthorization(PeerCastStation.HTTP.HTTPRequest request, PeerCastStation.Core.AccessControlInfo acinfo)
    {
      return CheckAuthorization(GetAuthorizationToken(request), acinfo);
    }

    public static string CreateResponseHeader(HttpStatusCode code)
    {
      var header = new System.Text.StringBuilder(String.Format("HTTP/1.0 {0} {1}\r\n", (int)code, code.ToString()));
      if (code==HttpStatusCode.Unauthorized) {
        header.AppendFormat("{0}: {1}\r\n", "WWW-Authenticate", "Basic realm=\"PeerCastStation\"");
      }
      header.Append("\r\n");
      return header.ToString();
    }

    public static string CreateResponseHeader(HttpStatusCode code, Dictionary<string, string> parameters)
    {
      var header = new System.Text.StringBuilder(String.Format("HTTP/1.0 {0} {1}\r\n", (int)code, code.ToString()));
      foreach (var param in parameters) {
        header.AppendFormat("{0}: {1}\r\n", param.Key, param.Value);
      }
      if (code==HttpStatusCode.Unauthorized && !parameters.ContainsKey("WWW-Authenticate")) {
        header.AppendFormat("{0}: {1}\r\n", "WWW-Authenticate", "Basic realm=\"PeerCastStation\"");
      }
      header.Append("\r\n");
      return header.ToString();
    }

    public static byte[] CreateResponse(HttpStatusCode code, Dictionary<string, string> parameters, string data)
    {
      var mem = new MemoryStream();
      using (var writer = new StreamWriter(mem)) {
        writer.Write(data);
      }
      var bytes = mem.ToArray();
      var header = new System.Text.StringBuilder(String.Format("HTTP/1.0 {0} {1}\r\n", (int)code, code.ToString()));
      if (!parameters.ContainsKey("Content-Type")) {
        header.AppendFormat("{0}: {1}\r\n", "Content-Type", "text/plain");
      }
      if (!parameters.ContainsKey("Content-Length")) {
        header.AppendFormat("{0}: {1}\r\n", "Content-Length", bytes.Length);
      }
      foreach (var param in parameters) {
        header.AppendFormat("{0}: {1}\r\n", param.Key, param.Value);
      }
      header.Append("\r\n");
      mem = new MemoryStream();
      using (var writer = new StreamWriter(mem)) {
        writer.Write(header.ToString());
      }
      return mem.ToArray().Concat(bytes).ToArray();
    }

    public static Dictionary<string, string> ParseQuery(string query)
    {
      var res = new Dictionary<string, string>();
      if (query!=null && query.StartsWith("?")) {
        foreach (var q in query.Substring(1).Split('&')) {
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

    public static string GetReasonPhrase(int status_code)
    {
      switch (status_code) {
      case 100: return "Continue";
      case 101: return "Switching Protocols";
      case 200: return "OK";
      case 201: return "Created";
      case 202: return "Accepted";
      case 203: return "Non-Authoritative Information";
      case 204: return "No Content";
      case 205: return "Reset Content";
      case 206: return "Partial Content";
      case 300: return "Multiple Choices";
      case 301: return "Moved Permanently";
      case 302: return "Found";
      case 303: return "See Other";
      case 304: return "Not Modified";
      case 305: return "Use Proxy";
      case 307: return "Temporary Redirect";
      case 400: return "Bad Request";
      case 401: return "Unauthorized";
      case 402: return "Payment Required";
      case 403: return "Forbidden";
      case 404: return "Not Found";
      case 405: return "Method Not Allowed";
      case 406: return "Not Acceptable";
      case 407: return "Proxy Authentication Required";
      case 408: return "Request Time-out";
      case 409: return "Conflict";
      case 410: return "Gone";
      case 411: return "Length Required";
      case 412: return "Precondition Failed";
      case 413: return "Request Entity Too Large";
      case 414: return "Request-URI Too Large";
      case 415: return "Unsupported Media Type";
      case 416: return "Requested range not satisfiable";
      case 417: return "Expectation Failed";
      case 500: return "Internal Server Error";
      case 501: return "Not Implemented";
      case 502: return "Bad Gateway";
      case 503: return "Service Unavailable";
      case 504: return "Gateway Time-out";
      case 505: return "HTTP Version not supported";
      default: return "";
      }
    }

  }
}
