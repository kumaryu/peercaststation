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
        return headers.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
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

    private static readonly Regex RequestLineRegex = new Regex(@"^(\w+) +(\S+) +(HTTP/1\.\d)$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
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
      foreach (var req in requests) {
        Match match = null;
        if ((match = OtherHeaderRegex.Match(req)).Success) {
          headers.Add(match.Groups[1].Value, match.Groups[2].Value.Trim());
        }
      }
      Headers = headers;
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
