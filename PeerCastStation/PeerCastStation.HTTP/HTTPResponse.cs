using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace PeerCastStation.HTTP
{
  public class HTTPResponse
  {
    /// <summary>
    /// HTTPバージョンを取得および設定します
    /// </summary>
    public string Protocol { get; private set; }
    /// <summary>
    /// HTTPステータスを取得および設定します
    /// </summary>
    public int Status { get; private set; }
    public string ReasonPhrase { get; private set; }
    /// <summary>
    /// レスポンスヘッダの値のコレクション取得します
    /// </summary>
    public IDictionary<string, string> Headers { get; private set; }
    public byte[] Body { get; private set; }

    public HTTPResponse(
      string protocol,
      int    status,
      string reason_phrase,
      IDictionary<string, string> headers,
      byte[] body)
    {
      this.Protocol     = protocol;
      this.Status       = status;
      this.ReasonPhrase = reason_phrase ?? HTTPUtils.GetReasonPhrase(status);
      this.Headers      = new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);
      this.Body         = body;
    }

    public HTTPResponse(
      string protocol,
      HttpStatusCode status)
    {
      this.Protocol     = protocol;
      this.Status       = (int)status;
      this.ReasonPhrase = HTTPUtils.GetReasonPhrase(this.Status);
      this.Headers      = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      this.Body         = null;
      if (status==HttpStatusCode.Unauthorized && !Headers.ContainsKey("WWW-Authenticate")) {
        Headers.Add("WWW-Authenticate", "Basic realm=\"PeerCastStation\"");
      }
    }

    public HTTPResponse(
      string protocol,
      HttpStatusCode status,
      IDictionary<string, string> headers)
    {
      this.Protocol     = protocol;
      this.Status       = (int)status;
      this.ReasonPhrase = HTTPUtils.GetReasonPhrase(this.Status);
      this.Headers      = new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);
      this.Body         = null;
      if (status==HttpStatusCode.Unauthorized && !Headers.ContainsKey("WWW-Authenticate")) {
        Headers.Add("WWW-Authenticate", "Basic realm=\"PeerCastStation\"");
      }
    }

    public HTTPResponse(
      string protocol,
      HttpStatusCode status,
      IDictionary<string, string> headers,
      string body)
    {
      this.Protocol     = protocol;
      this.Status       = (int)status;
      this.ReasonPhrase = HTTPUtils.GetReasonPhrase(this.Status);
      this.Headers      = new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);
      var mem = new MemoryStream();
      using (var writer=new StreamWriter(mem)) {
        writer.Write(body);
      }
      this.Body         = mem.ToArray();
      if (!Headers.ContainsKey("Content-Type")) {
        Headers.Add("Content-Type", "text/plain");
      }
      if (status==HttpStatusCode.Unauthorized && !Headers.ContainsKey("WWW-Authenticate")) {
        Headers.Add("WWW-Authenticate", "Basic realm=\"PeerCastStation\"");
      }
    }

    public byte[] GetBytes()
    {
      var header = new System.Text.StringBuilder($"{Protocol} {Status} {ReasonPhrase}\r\n");
      if (!Headers.ContainsKey("Content-Type")) {
        header.AppendFormat("{0}: {1}\r\n", "Content-Type", "text/plain");
      }
      if (!Headers.ContainsKey("Content-Length") && Body!=null) {
        header.AppendFormat("{0}: {1}\r\n", "Content-Length", Body.Length);
      }
      foreach (var param in Headers) {
        header.AppendFormat("{0}: {1}\r\n", param.Key, param.Value);
      }
      header.Append("\r\n");
      var mem = new MemoryStream();
      using (var writer=new StreamWriter(mem)) {
        writer.Write(header.ToString());
      }
      if (Body!=null) {
        return mem.ToArray().Concat(Body).ToArray();
      }
      else {
        return mem.ToArray();
      }
    }

  }

}
