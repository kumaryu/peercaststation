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
