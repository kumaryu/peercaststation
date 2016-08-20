using PeerCastStation.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.UI.HTTP
{
  internal class OWINEnv
  {
    public IDictionary<string, object> Environment { get; private set; }
    public OWINEnv(IDictionary<string, object> env)
    {
      this.Environment = env;
    }

    public string RequestMethod { get { return (string)this.Environment["owin.RequestMethod"]; } }
    public string RequestScheme { get { return (string)this.Environment["owin.RequestScheme"]; } }
    public string RequestPathBase { get { return (string)this.Environment["owin.RequestPathBase"]; } }
    public string RequestPath     { get { return (string)this.Environment["owin.RequestPath"]; } }
    public string RequestQueryString { get { return (string)this.Environment["owin.RequestQueryString"]; } }
    public string RequestProtocol { get { return (string)this.Environment["owin.RequestProtocol"]; } }
    public IDictionary<string, string[]> RequestHeaders { get { return (IDictionary<string,string[]>)this.Environment["owin.RequestHeaders"]; } }
    public Stream RequestBody { get { return (Stream)this.Environment["owin.RequestBody"]; } }
    public CancellationToken CallCanlelled { get { return (CancellationToken)this.Environment["owin.CallCancelled"]; } }
    public string Version { get { return (string)this.Environment["owin.Version"]; } }
    public AccessControlInfo AccessControlInfo { get { return (AccessControlInfo)this.Environment["peercast.AccessControlInfo"]; } }
    public IDictionary<string, string[]> ResponseHeaders { get { return (IDictionary<string,string[]>)this.Environment["owin.ResponseHeaders"]; } }
    public Stream ResponseBody { get { return (Stream)this.Environment["owin.ResponseBody"]; } }
    public int ResponseStatusCode {
      get { return (int)this.Environment["owin.ResponseStatusCode"]; }
      set { this.Environment["owin.ResponseStatusCode"] = value; }
    }
    public string ResponseReasonPhrase {
      get { return (string)this.Environment["owin.ResponseReasonPhrase"]; }
      set { this.Environment["owin.ResponseReasonPhrase"] = value; }
    }
    public string ResponseProtocol {
      get { return (string)this.Environment["owin.ResponseProtocol"]; }
      set { this.Environment["owin.ResponseProtocol"] = value; }
    }

    public Dictionary<string, string> RequestParameters {
      get {
        var parameters = new Dictionary<string, string>();
        foreach (Match param in Regex.Matches(this.RequestQueryString, @"(&|\?)([^&=]+)=([^&=]+)")) {
          parameters.Add(
            Uri.UnescapeDataString(param.Groups[2].Value).ToLowerInvariant(),
            Uri.UnescapeDataString(param.Groups[3].Value));
        }
        return parameters;
      }
    }

    public Dictionary<string, string> RequestCookies {
      get {
        var cookies = new Dictionary<string, string>();
        string[] cookie_headers;
        if (!this.RequestHeaders.TryGetValue("COOKIE", out cookie_headers)) return cookies;
        foreach (var pair in cookie_headers.SelectMany(v => v.Split(';'))) {
          var md = Regex.Match(pair, @"^([A-Za-z0-9!#$%^&*_\-+|~`'"".]+)=(.*)$");
          if (md.Success) {
            cookies.Add(md.Groups[1].Value, md.Groups[2].Value);
          }
        }
        return cookies;
      }
    }

    public void AddResponseHeader(string key, string value)
    {
      var headers = this.ResponseHeaders;
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

    public void SetResponseHeader(string key, string value)
    {
      var headers = this.ResponseHeaders;
      string[] v;
      if (!headers.TryGetValue(key, out v)) {
        headers.Add(key, new string[] { value });
      }
    }

    public string GetAuthorizationToken()
    {
      String result = null;
      if (this.RequestHeaders.ContainsKey("AUTHORIZATION")) {
        var md = System.Text.RegularExpressions.Regex.Match(
          this.RequestHeaders["AUTHORIZATION"][0],
          @"\s*BASIC (\S+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (md.Success) {
          result = md.Groups[1].Value;
        }
      }
      if (result==null) {
        this.RequestParameters.TryGetValue("auth", out result);
      }
      if (result==null) {
        this.RequestCookies.TryGetValue("auth", out result);
      }
      return result;
    }

    public void SetResponseStatusCode(HttpStatusCode status_code)
    {
      this.ResponseStatusCode = (int)status_code;
    }

    public async Task SetResponseBodyAsync(string str, CancellationToken cancel_token)
    {
      SetResponseHeader("Content-Type", "text/plain; charset=utf-8");
      await this.ResponseBody.WriteUTF8Async(str, cancel_token);
    }

  }

}
