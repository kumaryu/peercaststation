using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Linq;

namespace PeerCastStation.UI.HTTP
{
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
  }

  class HTTPUtils
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

    public static string CreateAuthorizationToken(PeerCastStation.Core.AuthenticationKey key)
    {
      return Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(key.Id + ":" + key.Password));
    }

    public static bool CheckAuthorization(PeerCastStation.HTTP.HTTPRequest request, PeerCastStation.Core.AuthenticationKey key)
    {
      if (key==null) return true;
      var token = GetAuthorizationToken(request);
      if (token==null) return false;
      var authorized = false;
      try {
        var authorization = System.Text.Encoding.ASCII.GetString(Convert.FromBase64String(token)).Split(':');
        if (authorization.Length>=2) {
          var user = authorization[0];
          var pass = String.Join(":", authorization.Skip(1).ToArray());
          if (key.Id==user && key.Password==pass) {
            authorized = true;
          }
        }
      }
      catch (FormatException) {
      }
      catch (ArgumentException) {
      }
      return authorized;
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

  }
}
