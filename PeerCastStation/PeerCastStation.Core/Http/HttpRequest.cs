using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PeerCastStation.Core.Http
{
  public class HttpRequest
  {
    /// <summary>
    /// HTTPメソッドを取得および設定します
    /// </summary>
    public string Method { get; private set; }
    public string PathAndQuery { get; private set; }
    public string Protocol { get; private set; }
    /// <summary>
    /// リクエストされたUriを取得および設定します
    /// </summary>
    private Uri uri = null;
    public Uri Uri {
      get {
        if (uri==null) {
          string host;
          if (!Headers.TryGetValue("HOST", out host)) {
            host = "localhost";
          }
          Uri.TryCreate("http://" + host + PathAndQuery, UriKind.Absolute, out uri);
        }
        return uri;
      }
    }

    public class RequestHeaders
    {
      private Dictionary<string, List<string>> headers = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

      public bool TryGetValue(string name, out string[] value)
      {
        if (headers.TryGetValue(name, out var lst) && lst.Count>0) {
          value = lst.ToArray();
          return true;
        }
        else {
          value = default(string[]);
          return false;
        }
      }

      public bool ContainsKey(string name)
      {
        return headers.ContainsKey(name);
      }

      public bool TryGetValue(string name, out string value)
      {
        if (headers.TryGetValue(name, out var lst) && lst.Count>0) {
          value = lst[lst.Count-1];
          return true;
        }
        else {
          value = default(string);
          return false;
        }
      }

      public void Add(string name, string value)
      {
        if (headers.TryGetValue(name, out var lst)) {
          lst.Add(value);
        }
        else {
          headers.Add(name, new List<string> { value });
        }
      }

      public void Add(string name, IEnumerable<string> value)
      {
        if (headers.TryGetValue(name, out var lst)) {
          lst.AddRange(value);
        }
        else {
          headers.Add(name, new List<string>(value));
        }
      }

      public void Set(string name, string value)
      {
        headers[name] = new List<string> { value };
      }

      public void Set(string name, IEnumerable<string> value)
      {
        headers[name] = new List<string>(value);
      }

      public IDictionary<string, string[]> ToDictionary()
      {
        return headers.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
      }
    }

    /// <summary>
    /// リクエストヘッダの値のコレクション取得します
    /// </summary>
    public RequestHeaders Headers { get; private set; }

    private string path = null;
    public string Path {
      get {
        if (path==null) {
          var idx = PathAndQuery.IndexOf('?');
          if (idx>=0) {
            path = PathAndQuery.Substring(0, idx);
          }
          else {
            path = PathAndQuery;
          }
        }
        return path;
      }
    }

    public string QueryString {
      get {
        var idx = PathAndQuery.IndexOf('?');
        if (idx>=0) {
          return PathAndQuery.Substring(idx+1);
        }
        else {
          return String.Empty;
        }
      }
    }

    private static readonly Regex QueryParameterRegex = new Regex(@"(&|\?)([^&=]+)=([^&=]+)", RegexOptions.Compiled);
    private Dictionary<string, string> queryParameters = null;
    public IReadOnlyDictionary<string, string> QueryParameters {
      get {
        var p = queryParameters;
        if (p==null) {
          p = new Dictionary<string, string>();
          var idx = PathAndQuery.IndexOf('?');
          if (idx>=0) {
            var query = PathAndQuery.Substring(idx);
            foreach (Match param in QueryParameterRegex.Matches(query)) {
              p.Add(
                Uri.UnescapeDataString(param.Groups[2].Value).ToLowerInvariant(),
                Uri.UnescapeDataString(param.Groups[3].Value));
            }
          }
          queryParameters = p;
        }
        return p;
      }
    }

    public IReadOnlyDictionary<string, string> Cookies { get; private set; }
    public IReadOnlyList<string> Pragmas {
      get {
        if (Headers.TryGetValue("Pragma", out string[] value)) {
          return value;
        }
        else {
          return emptyPragmas;
        }
      }
    }
    private static readonly string[] emptyPragmas = new string[0];

    public bool KeepAlive {
      get {
        switch (Protocol) {
        case "HTTP/1.1":
          {
            if (Headers.TryGetValue("CONNECTION", out string value) && StringComparer.OrdinalIgnoreCase.Equals(value, "close")) {
              return false;
            }
            else {
              return true;
            }
          }
        case "HTTP/1.0":
          return Headers.ContainsKey("Keep-Alive");
        default:
          return false;
        }
      }
    }

    public bool ChunkedEncoding {
      get {
        string value;
        if (Headers.TryGetValue("TRANSFER-ENCODING", out value)) {
          return value.ToUpperInvariant().Contains("CHUNKED");
        }
        else {
          return false;
        }
      }
    }

    private static readonly Regex RequestLineRegex = new Regex(@"^(\w+) +(\S+) +(HTTP/1\.\d)$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex CookieHeaderRegex = new Regex(@"^Cookie:(\s*)(.+)(\s*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex CookieEntryRegex = new Regex(@"^([A-Za-z0-9!#$%^&*_\-+|~`'"".]+)=(.*)$", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex PragmaHeaderRegex = new Regex(@"^Pragma:(\s*)(.+)(\s*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex OtherHeaderRegex = new Regex(@"^(\S*):(.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    /// <summary>
    /// HTTPリクエスト文字列からHTTPRequestオブジェクトを構築します
    /// </summary>
    /// <param name="requests">行毎に区切られたHTTPリクエストの文字列表現</param>
    public HttpRequest(HttpRequestLine reqLine, IEnumerable<string> requests)
    {
      Method = reqLine.Method;
      Protocol = reqLine.Protocol;
      PathAndQuery = reqLine.PathAndQuery;
      var headers = new RequestHeaders();
      var cookies = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
      foreach (var req in requests) {
        Match match = null;
        if ((match = CookieHeaderRegex.Match(req)).Success) {
          foreach (var pair in match.Groups[2].Value.Split(';')) {
            var md = CookieEntryRegex.Match(pair);
            if (md.Success) {
              cookies.Add(md.Groups[1].Value, md.Groups[2].Value);
            }
          }
        }
        else if ((match = PragmaHeaderRegex.Match(req)).Success) {
          headers.Add("Pragma", match.Groups[1].Value.Split(',').Select(token => token.Trim().ToLowerInvariant()));
        }
        else if ((match = OtherHeaderRegex.Match(req)).Success) {
          headers.Add(match.Groups[1].Value, match.Groups[2].Value.Trim());
        }
      }
      Headers = headers;
      Cookies = cookies;
    }

    public class HttpRequestLine {
      public string Method { get; private set; }
      public string PathAndQuery { get; private set; }
      public string Protocol { get; private set; }
      public HttpRequestLine(string method, string pathAndQuery, string protocol)
      {
        this.Method = method;
        this.PathAndQuery = pathAndQuery;
        this.Protocol = protocol;
      }
    }

    public static HttpRequest ParseRequest(IEnumerable<string> lines)
    {
      var reqLine = ParseRequestLine(lines.First());
      if (reqLine==null) return null;
      return new HttpRequest(reqLine, lines.Skip(1));
    }

    public static HttpRequestLine ParseRequestLine(string line)
    {
      var match = RequestLineRegex.Match(line);
      if (match.Success) {
        return new HttpRequestLine(
          match.Groups[1].Value.ToUpperInvariant(),
          match.Groups[2].Value,
          match.Groups[3].Value.ToUpperInvariant());
      }
      else {
        return null;
      }
    }

  }

}
