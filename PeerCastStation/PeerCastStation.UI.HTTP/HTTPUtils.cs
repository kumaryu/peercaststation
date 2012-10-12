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
    public static string CreateResponseHeader(HttpStatusCode code, Dictionary<string, string> parameters)
    {
      var header = new System.Text.StringBuilder(String.Format("HTTP/1.0 {0} {1}\r\n", (int)code, code.ToString()));
      foreach (var param in parameters) {
        header.AppendFormat("{0}: {1}\r\n", param.Key, param.Value);
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
