using System;
using System.IO;
using System.Net;
using System.Linq;
using PeerCastStation.Core;
using PeerCastStation.HTTP;
using PeerCastStation.Core.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using Owin;
using Microsoft.Owin;

namespace PeerCastStation.UI.HTTP
{
  public static class HTMLHostOwinApp
  {
    class HostApp
    {
      public string LocalPath { get; private set; }
      public HostApp(string localPath)
      {
        LocalPath = localPath;
      }

      class FileDesc
      {
        public string MimeType { get; set; }
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

      private FileDesc GetFileDesc(string ext)
      {
        if (FileDescriptions.TryGetValue(ext, out var res)) {
          return res;
        }
        else {
          return FileDescriptions[""];
        }
      }

      public async Task Invoke(IOwinContext ctx)
      {
        var cancel_token = ctx.Request.CallCancelled;
        var localpath = Path.GetFullPath(Path.Combine(LocalPath, ctx.Request.Path.Value.Substring(1)));
        if (Directory.Exists(localpath)) {
          localpath = Path.Combine(localpath, "index.html");
          if (!File.Exists(localpath)) {
            ctx.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            return;
          }
        }
        if (File.Exists(localpath)) {
          var contents = File.ReadAllBytes(localpath);
          var content_desc = GetFileDesc(Path.GetExtension(localpath));
          ctx.Response.ContentType = content_desc.MimeType;
          ctx.Response.ContentLength = contents.LongLength;
          var acinfo = ctx.GetAccessControlInfo();
          if (acinfo?.AuthenticationKey!=null) {
            ctx.Response.Headers.Append("Set-Cookie", "auth=" + HTTPUtils.CreateAuthorizationToken(acinfo.AuthenticationKey));
          }
          await ctx.Response.WriteAsync(contents, cancel_token).ConfigureAwait(false);
        }
        else {
          ctx.Response.StatusCode = (int)HttpStatusCode.NotFound;
        }
      }

    }

    private static async Task InvokeRedirect(IOwinContext ctx)
    {
      var cancel_token = ctx.Request.CallCancelled;
      ctx.Response.ContentType = "text/plain;charset=utf-8";
      ctx.Response.Headers.Set("Location", "/html/index.html");
      var acinfo = ctx.GetAccessControlInfo();
      if (acinfo?.AuthenticationKey!=null) {
        ctx.Response.Headers.Append("Set-Cookie", "auth=" + HTTPUtils.CreateAuthorizationToken(acinfo.AuthenticationKey));
      }
      ctx.Response.StatusCode = (int)HttpStatusCode.Moved;
      await ctx.Response.WriteAsync("Moving...", cancel_token).ConfigureAwait(false);
    }

    public static void BuildPath(IAppBuilder builder, string mappath, string localpath)
    {
      builder.Map(mappath, sub => {
        sub.MapMethod("GET", withmethod => {
          withmethod.UseAuth(OutputStreamType.Interface);
          withmethod.Run(new HostApp(localpath).Invoke);
        });
      });
    }

    public static void BuildApp(IAppBuilder builder, string basepath)
    {
      BuildPath(builder, "/html", Path.Combine(basepath, "html"));
      BuildPath(builder, "/help", Path.Combine(basepath, "help"));
      BuildPath(builder, "/Content", Path.Combine(basepath, "Content"));
      BuildPath(builder, "/Scripts", Path.Combine(basepath, "Scripts"));
      builder.MapWhen(ctx => ctx.Request.Path.Value=="/", sub => {
        sub.MapMethod("GET", withmethod => {
          withmethod.UseAuth(OutputStreamType.Interface);
          withmethod.Run(InvokeRedirect);
        });
      });
    }
  }

  [Plugin]
  public class HTMLHost
    : PluginBase
  {
    override public string Name { get { return "HTTP File Host UI"; } }
    private IDisposable appRegistration = null;

    protected override void OnStart()
    {
      var owin = Application.Plugins.OfType<OwinHostPlugin>().FirstOrDefault();
      appRegistration = owin?.OwinHost?.Register(builder => HTMLHostOwinApp.BuildApp(builder, this.Application.BasePath));
    }

    protected override void OnStop()
    {
      appRegistration?.Dispose();
      appRegistration = null;
    }

  }
}

