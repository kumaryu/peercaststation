using System;
using System.IO;
using System.Net;
using System.Linq;
using PeerCastStation.Core;
using PeerCastStation.HTTP;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace PeerCastStation.UI.HTTP
{
  [Plugin]
  public class HTMLHost
    : PluginBase
  {
    class FileDesc
    {
      public string MimeType   { get; set; }
    }
    private static readonly Dictionary<string, FileDesc> FileDescriptions = new Dictionary<string, FileDesc> {
      { ".html", new FileDesc { MimeType="text/html" } },
      { ".htm",  new FileDesc { MimeType="text/html" } },
      { ".txt",  new FileDesc { MimeType="text/plain" } },
      { ".xml",  new FileDesc { MimeType="text/xml" } },
      { ".json", new FileDesc { MimeType="application/json" } },
      { ".css",  new FileDesc { MimeType="text/css" } },
      { ".js",   new FileDesc { MimeType="application/javascript" } },
      { ".bmp",  new FileDesc { MimeType="image/bmp" } },
      { ".png",  new FileDesc { MimeType="image/png" } },
      { ".jpg",  new FileDesc { MimeType="image/jpeg" } },
      { ".gif",  new FileDesc { MimeType="image/gif" } },
      { ".svg",  new FileDesc { MimeType="image/svg+xml" } },
      { ".swf",  new FileDesc { MimeType="application/x-shockwave-flash" } },
      { ".xap",  new FileDesc { MimeType="application/x-silverlight-app" } },
      { "",      new FileDesc { MimeType="application/octet-stream" } },
    };

    override public string Name { get { return "HTTP File Host UI"; } }
    public SortedList<string, string> VirtualPhysicalPathMap { get { return virtualPhysicalPathMap; } }
    private SortedList<string, string> virtualPhysicalPathMap = new SortedList<string,string>();

    public HTMLHost()
    {
      var basepath = Path.GetFullPath(Path.GetDirectoryName(typeof(HTMLHost).Assembly.Location));
      virtualPhysicalPathMap.Add("/html/", Path.Combine(basepath, "html"));
      virtualPhysicalPathMap.Add("/help/", Path.Combine(basepath, "help"));
      virtualPhysicalPathMap.Add("/Content/", Path.Combine(basepath, "Content"));
      virtualPhysicalPathMap.Add("/Scripts/", Path.Combine(basepath, "Scripts"));
    }

    override protected void OnAttach()
    {
    }

    private List<OWINApplication> applications = new List<OWINApplication>();
    protected override void OnStart()
    {
      base.OnStart();
      var owinhost =
        Application.PeerCast.OutputStreamFactories.FirstOrDefault(factory => factory is OWINHostOutputStreamFactory) as OWINHostOutputStreamFactory;
      if (owinhost!=null) {
        applications.AddRange(
          virtualPhysicalPathMap.Keys.Select(path =>
            owinhost.AddApplication(path, PathParameters.Any, OnProcess)));
        applications.Add(owinhost.AddApplication("/", PathParameters.None, OnRedirect));
      }
    }

    protected override void OnStop()
    {
      var owinhost =
        Application.PeerCast.OutputStreamFactories.FirstOrDefault(factory => factory is OWINHostOutputStreamFactory) as OWINHostOutputStreamFactory;
      if (owinhost!=null) {
        foreach (var app in applications) {
          owinhost.RemoveApplication(app);
        }
      }
      base.OnStop();
    }

    override protected void OnDetach()
    {
    }

    private class OWINEnv
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

    }

    private string GetPhysicalPath(OWINEnv env)
    {
      string physical_path;
      if (virtualPhysicalPathMap.TryGetValue(env.RequestPathBase, out physical_path)) {
        return Path.GetFullPath(Path.Combine(physical_path, env.RequestPath));
      }
      else {
        return null;
      }
    }

    private FileDesc GetFileDesc(string ext)
    {
      FileDesc res;
      if (FileDescriptions.TryGetValue(ext, out res)) {
        return res;
      }
      else {
        return FileDescriptions[""];
      }
    }

    private async Task SendResponseMoveToIndex(OWINEnv env, CancellationToken cancel_token)
    {
      var content = System.Text.Encoding.UTF8.GetBytes("Moving...");
      env.SetResponseHeader("Content-Type",   "text/plain");
      env.SetResponseHeader("Content-Length", content.Length.ToString());
      env.SetResponseHeader("Location",       "/html/index.html");
      if (env.AccessControlInfo.AuthenticationKey!=null) {
        env.SetResponseHeader("Set-Cookie", "auth=" + HTTPUtils.CreateAuthorizationToken(env.AccessControlInfo.AuthenticationKey));
      }
      env.ResponseStatusCode = (int)HttpStatusCode.Moved;
      if (env.RequestMethod=="GET") {
        await env.ResponseBody.WriteAsync(content, 0, content.Length, cancel_token);
      }
    }

    private async Task SendResponseFileContent(OWINEnv env, CancellationToken cancel_token)
    {
      var localpath = GetPhysicalPath(env);
      if (localpath==null) throw new HTTPError(HttpStatusCode.Forbidden);
      if (Directory.Exists(localpath)) {
        localpath = Path.Combine(localpath, "index.html");
        if (!File.Exists(localpath)) throw new HTTPError(HttpStatusCode.Forbidden);
      }
      if (File.Exists(localpath)) {
        var contents = File.ReadAllBytes(localpath);
        var content_desc = GetFileDesc(Path.GetExtension(localpath));
        env.SetResponseHeader("Content-Type",   content_desc.MimeType);
        env.SetResponseHeader("Content-Length", contents.Length.ToString());
        if (env.AccessControlInfo.AuthenticationKey!=null) {
          env.SetResponseHeader("Set-Cookie", "auth=" + HTTPUtils.CreateAuthorizationToken(env.AccessControlInfo.AuthenticationKey));
        }
        if (env.RequestMethod=="GET") {
          await env.ResponseBody.WriteAsync(contents, 0, contents.Length, cancel_token);
        }
      }
      else {
        throw new HTTPError(HttpStatusCode.NotFound);
      }
    }

    private async Task OnProcess(IDictionary<string, object> owinenv)
    {
      var env = new OWINEnv(owinenv);
      var cancel_token = env.CallCanlelled;
      try {
        if (!HTTPUtils.CheckAuthorization(env.GetAuthorizationToken(), env.AccessControlInfo)) {
          throw new HTTPError(HttpStatusCode.Unauthorized);
        }
        if (env.RequestMethod!="HEAD" && env.RequestMethod!="GET") {
          throw new HTTPError(HttpStatusCode.MethodNotAllowed);
        }
        await SendResponseFileContent(env, cancel_token);
      }
      catch (HTTPError err) {
        env.ResponseStatusCode = (int)err.StatusCode;
      }
      catch (UnauthorizedAccessException) {
        env.ResponseStatusCode = (int)HttpStatusCode.Forbidden;
      }
    }

    private async Task OnRedirect(IDictionary<string, object> owinenv)
    {
      var env = new OWINEnv(owinenv);
      var cancel_token = env.CallCanlelled;
      try {
        if (!HTTPUtils.CheckAuthorization(env.GetAuthorizationToken(), env.AccessControlInfo)) {
          throw new HTTPError(HttpStatusCode.Unauthorized);
        }
        if (env.RequestMethod!="HEAD" && env.RequestMethod!="GET") {
          throw new HTTPError(HttpStatusCode.MethodNotAllowed);
        }
        await SendResponseMoveToIndex(env, cancel_token);
      }
      catch (HTTPError err) {
        env.ResponseStatusCode = (int)err.StatusCode;
      }
      catch (UnauthorizedAccessException) {
        env.ResponseStatusCode = (int)HttpStatusCode.Forbidden;
      }
    }

  }
}
