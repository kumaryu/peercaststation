using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Diagnostics.CodeAnalysis;

namespace PeerCastStation.HTTP
{
  /// <summary>
  ///クライアントからのHTTPリクエスト内容を保持するクラスです
  /// </summary>
  public class HTTPRequest
  {
    /// <summary>
    /// HTTPメソッドを取得および設定します
    /// </summary>
    public string Method { get; private set; }
    public string Protocol { get; private set; }
    /// <summary>
    /// リクエストされたUriを取得および設定します
    /// </summary>
    public Uri? Uri { get; private set; }
    /// <summary>
    /// リクエストヘッダの値のコレクション取得します
    /// </summary>
    public Dictionary<string, string> Headers { get; private set; }
    public Dictionary<string, string> Parameters { get; private set; }
    public Dictionary<string, string> Cookies { get; private set; }
    public IList<string> Pragmas { get; private set; }
    private static readonly string[] emptyPragmas = new string[0];

    public bool KeepAlive {
      get {
        switch (Protocol) {
        case "HTTP/1.1":
          {
            if (Headers.TryGetValue("CONNECTION", out var value) && value=="close") {
              return false;
            }
            else {
              return true;
            }
          }
        case "HTTP/1.0":
          {
            if (Headers.TryGetValue("CONNECTION", out var value) && value=="keep-alive") {
              return true;
            }
            else {
              return false;
            }
          }
        default:
          return false;
        }
      }
    }

    public bool ChunkedEncoding {
      get {
        if (Headers.TryGetValue("Transfer-Encoding", out var value)) {
          return value.Contains("chunked");
        }
        else {
          return false;
        }
      }
    }

    private HTTPRequest(
      string method,
      string protocol,
      Uri uri,
      Dictionary<string, string> headers,
      Dictionary<string, string> parameters,
      Dictionary<string, string> cookies,
      IList<string> pragmas)
    {
      Method = method;
      Protocol = protocol;
      Uri = Uri;
      Headers = headers;
      Parameters = parameters;
      Cookies = cookies;
      Pragmas = pragmas;
    }

    /// <summary>
    /// HTTPリクエスト文字列からHTTPRequestオブジェクトを構築します
    /// </summary>
    /// <param name="requests">行毎に区切られたHTTPリクエストの文字列表現</param>
    /// <param name="request">パースされたHTTPRequestオブジェクト</param>
    /// <returns>パースできたら true 、それ以外は false</returns>
    public static bool TryParse(IEnumerable<string> requests, [NotNullWhen(true)] out HTTPRequest? request)
    {
      var headers    = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      var parameters = new Dictionary<string, string>();
      var cookies    = new Dictionary<string, string>();
      List<string>? pragmas = null;
      string host = "localhost";
      HTTPRequestLine? requestLine = null;
      foreach (var req in requests) {
        if (requestLine==null) {
          requestLine = ParseRequestLine(req);
          if (requestLine==null) {
            request = null;
            return false;
          }
        }
        else if (String.IsNullOrEmpty(req)) {
          break;
        }
        else {
          Match match;
          if ((match = Regex.Match(req, @"^Host:(.+)$", RegexOptions.IgnoreCase)).Success) {
            host = match.Groups[1].Value.Trim();
            headers["HOST"] = host;
          }
          else if ((match = Regex.Match(req, @"^Cookie:(\s*)(.+)(\s*)$", RegexOptions.IgnoreCase)).Success) {
            foreach (var pair in match.Groups[2].Value.Split(';')) {
              var md = Regex.Match(pair, @"^([A-Za-z0-9!#$%^&*_\-+|~`'"".]+)=(.*)$");
              if (md.Success) {
                cookies.Add(md.Groups[1].Value, md.Groups[2].Value);
              }
            }
          }
          else if ((match = Regex.Match(req, @"^Pragma:(.+)$", RegexOptions.IgnoreCase)).Success) {
            if (pragmas==null) {
              pragmas = new List<string>();
            }
            pragmas.AddRange(match.Groups[1].Value.Split(',').Select(token => token.Trim().ToLowerInvariant()));
          }
          else if ((match = Regex.Match(req, @"^(\S*):(.+)$", RegexOptions.IgnoreCase)).Success) {
            headers[match.Groups[1].Value.ToUpper()] = match.Groups[2].Value.Trim();
          }
        }
      }
      if (requestLine==null) {
        request = null;
        return false;
      }
      if (Uri.TryCreate("http://" + host + requestLine.Path, UriKind.Absolute, out var uri)) {
        foreach (Match param in Regex.Matches(uri.Query, @"(&|\?)([^&=]+)=([^&=]+)")) {
          parameters.Add(
            Uri.UnescapeDataString(param.Groups[2].Value).ToLowerInvariant(),
            Uri.UnescapeDataString(param.Groups[3].Value));
        }
        request = new HTTPRequest(
          requestLine.Method,
          requestLine.Protocol,
          uri,
          headers,
          parameters,
          cookies,
          pragmas?.ToArray() ?? emptyPragmas);
        return true;
      }
      else {
        request = null;
        return false;
      }
    }

    private static readonly Regex RequestLineRegex = new Regex(@"^(\w+) +(\S+) +(HTTP/1.\d)$", RegexOptions.IgnoreCase);
    public class HTTPRequestLine {
      public string Method   { get; private set; }
      public string Path     { get; private set; }
      public string Protocol { get; private set; }
      public HTTPRequestLine(string method, string path, string protocol)
      {
        this.Method   = method;
        this.Path     = path;
        this.Protocol = protocol;
      }
    }

    public static HTTPRequestLine? ParseRequestLine(string line)
    {
      var match = RequestLineRegex.Match(line);
      if (match.Success) {
        return new HTTPRequestLine(
          match.Groups[1].Value.ToUpper(),
          match.Groups[2].Value,
          match.Groups[3].Value);
      }
      else {
        return null;
      }
    }

  }

}
