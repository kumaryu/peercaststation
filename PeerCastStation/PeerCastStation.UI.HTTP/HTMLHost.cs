using System;
using System.IO;
using System.Net;
using System.Linq;
using PeerCastStation.Core;
using PeerCastStation.Core.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Diagnostics.CodeAnalysis;

namespace PeerCastStation.UI.HTTP
{
  public static class HTMLHostOwinApp
  {
    public class HostApp
    {
      public string LocalPath { get; private set; }
      public HostApp(string localPath)
      {
        LocalPath = localPath;
      }

      record FileDesc(string MimeType);
      private static readonly Dictionary<string, FileDesc> FileDescriptions = new Dictionary<string, FileDesc> {
        { ".html", new ("text/html") },
        { ".htm",  new ("text/html") },
        { ".txt",  new ("text/plain") },
        { ".xml",  new ("text/xml") },
        { ".json", new ("application/json") },
        { ".css",  new ("text/css") },
        { ".js",   new ("application/javascript") },
        { ".bmp",  new ("image/bmp") },
        { ".png",  new ("image/png") },
        { ".jpg",  new ("image/jpeg") },
        { ".gif",  new ("image/gif") },
        { ".svg",  new ("image/svg+xml") },
        { ".swf",  new ("application/x-shockwave-flash") },
        { ".xap",  new ("application/x-silverlight-app") },
        { "",      new ("application/octet-stream") },
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

      [return:NotNullIfNotNull("a")]
      [return:NotNullIfNotNull("b")]
      private string? CombinePath(string? a, string? b)
      {
        if (String.IsNullOrEmpty(a)) return b;
        if (String.IsNullOrEmpty(b)) return a;
        if (b[0]=='/' || b[0]=='\\') {
          return Path.Combine(a, b.Substring(1));
        }
        else {
          return Path.Combine(a, b);
        }
      }

      public async Task Invoke(OwinEnvironment ctx)
      {
        var cancel_token = ctx.Request.CallCancelled;
        var localpath = Path.GetFullPath(CombinePath(LocalPath, ctx.Request.Path));
        if (Directory.Exists(localpath)) {
          localpath = Path.Combine(localpath, "index.html");
          if (!File.Exists(localpath)) {
            ctx.Response.StatusCode = HttpStatusCode.Forbidden;
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
            ctx.Response.Headers.Add("Set-Cookie", $"auth={acinfo.AuthenticationKey.GetToken()}; Path=/");
          }
          await ctx.Response.WriteAsync(contents, cancel_token).ConfigureAwait(false);
        }
        else {
          ctx.Response.StatusCode = HttpStatusCode.NotFound;
        }
      }

    }

    private static string BuildIndexTXTEntry(PeerCast peercast, Channel channel)
    {
      var endpoint = channel.TrackerEndPoint;
      var enc_channel_name = Uri.EscapeDataString(channel.ChannelInfo.Name ?? "");
      var uptime = (int)channel.Uptime.TotalMinutes;
      var columns = new string[] {
        channel.ChannelInfo.Name ?? "",  //1 CHANNEL_NAME チャンネル名
        channel.ChannelID.ToString("N").ToUpperInvariant(),  //2 ID ID ユニーク値16進数32桁、制限チャンネルは全て0埋め
        endpoint?.ToString() ?? "",  //3 TIP TIP ポートも含む。Push配信時はブランク、制限チャンネルは127.0.0.1
        channel.ChannelInfo.URL ?? "", //4 CONTACT_URL コンタクトURL 基本的にURL、任意の文字列も可 CONTACT_URL
        channel.ChannelInfo.Genre ?? "", //5 GENRE ジャンル
        channel.ChannelInfo.Desc ?? "", //6 DETAIL 詳細
        channel.TotalDirects.ToString(),  //7 LISTENER_NUM Listener数 -1は非表示、-1未満はサーバのメッセージ。ブランクもあるかも
        channel.TotalRelays.ToString(), //8 RELAY_NUM Relay数 同上 
        channel.ChannelInfo.Bitrate.ToString(),  //9 BITRATE Bitrate 単位は kbps 
        channel.ChannelInfo.ContentType ?? "",  //10 TYPE Type たぶん大文字 
        channel.ChannelTrack.Creator ?? "", //11 TRACK_ARTIST トラック アーティスト 
        channel.ChannelTrack.Album ?? "", //12 TRACK_ALBUM トラック アルバム 
        channel.ChannelTrack.Name ?? "", //13 TRACK_TITLE トラック タイトル 
        channel.ChannelTrack.URL ?? "", //14 TRACK_CONTACT_URL トラック コンタクトURL 基本的にURL、任意の文字列も可 
        enc_channel_name, //15 ENC_CHANNEL_NAME エンコード済みチャンネル名 URLエンコード(UTF-8)
        $"{uptime/60}:{uptime%60:D2}", //16 BROADCAST_TIME 配信時間 000〜99999 
        "",          //17 STATUS ステータス 特殊なステータス disconnectしばらく情報の更新が無い、port0Push配信 又はアイコン
        channel.ChannelInfo.Comment ?? "", //18 COMMENT コメント 
        "0",         //19 DIRECT ダイレクトの有無 0固定
      }.Select(str => {
        str = System.Text.RegularExpressions.Regex.Replace(str, "&", "&amp;");
        str = System.Text.RegularExpressions.Regex.Replace(str, "<", "&lt;");
        str = System.Text.RegularExpressions.Regex.Replace(str, ">", "&gt;");
        return str;
      });
      return String.Join("<>", columns);
    }

    private static async Task InvokeIndexTXT(OwinEnvironment ctx)
    {
      var cancel_token = ctx.Request.CallCancelled;
      ctx.Response.ContentType = "text/plain;charset=utf-8";
      var acinfo = ctx.GetAccessControlInfo();
      if (acinfo?.AuthenticationKey!=null) {
        ctx.Response.Headers.Add("Set-Cookie", $"auth={acinfo.AuthenticationKey.GetToken()}; Path=/");
      }
      var peercast = ctx.GetPeerCast();
      if (peercast==null) {
        await ctx.Response.WriteAsync("", cancel_token).ConfigureAwait(false);
      }
      else {
        var indextxt = String.Join("\r\n", peercast.Channels.Select(c => BuildIndexTXTEntry(peercast, c)));
        await ctx.Response.WriteAsync(indextxt, cancel_token).ConfigureAwait(false);
      }
    }

    private static async Task InvokeRedirect(OwinEnvironment ctx)
    {
      var cancel_token = ctx.Request.CallCancelled;
      ctx.Response.ContentType = "text/plain;charset=utf-8";
      ctx.Response.Headers.Set("Location", "/html/index.html");
      var acinfo = ctx.GetAccessControlInfo();
      if (acinfo?.AuthenticationKey!=null) {
        ctx.Response.Headers.Add("Set-Cookie", $"auth={acinfo.AuthenticationKey.GetToken()}; Path=/");
      }
      ctx.Response.StatusCode = HttpStatusCode.Moved;
      await ctx.Response.WriteAsync("Moving...", cancel_token).ConfigureAwait(false);
    }

    public static void BuildPath(IAppBuilder builder, string mappath, OutputStreamType accepts, string localpath)
    {
      builder.MapGET(mappath, sub => {
        sub.UseAuth(accepts);
        sub.Run(new HostApp(localpath).Invoke);
      });
    }

    public static void BuildApp(IAppBuilder builder, string basepath)
    {
      builder.MapGET("/html/index.txt", sub => {
        sub.UseAuth(OutputStreamType.Interface | OutputStreamType.Play);
        sub.Run(InvokeIndexTXT);
      });
      BuildPath(builder, "/html/play.html", OutputStreamType.Interface | OutputStreamType.Play, Path.Combine(basepath, "html/play.html"));
      BuildPath(builder, "/html/player.html", OutputStreamType.Interface | OutputStreamType.Play, Path.Combine(basepath, "html/player.html"));
      BuildPath(builder, "/html/js", OutputStreamType.Interface | OutputStreamType.Play, Path.Combine(basepath, "html/js"));
      BuildPath(builder, "/html/css", OutputStreamType.Interface | OutputStreamType.Play, Path.Combine(basepath, "html/css"));
      BuildPath(builder, "/html/images", OutputStreamType.Interface | OutputStreamType.Play, Path.Combine(basepath, "html/images"));
      BuildPath(builder, "/html", OutputStreamType.Interface, Path.Combine(basepath, "html"));
      BuildPath(builder, "/help", OutputStreamType.Interface, Path.Combine(basepath, "help"));
      BuildPath(builder, "/Content", OutputStreamType.Interface | OutputStreamType.Play, Path.Combine(basepath, "Content"));
      BuildPath(builder, "/Scripts", OutputStreamType.Interface | OutputStreamType.Play, Path.Combine(basepath, "Scripts"));
      builder.MapGET(new Regex("^/?$"), sub => {
        sub.UseAuth(OutputStreamType.Interface);
        sub.Run(InvokeRedirect);
      });
    }
  }

  [Plugin]
  public class HTMLHost
    : PluginBase
  {
    override public string Name { get { return "HTTP File Host UI"; } }
    private IDisposable? appRegistration = null;

    protected override void OnStart(PeerCastApplication app)
    {
      var owin = app.Plugins.OfType<OwinHostPlugin>().FirstOrDefault();
      appRegistration = owin?.OwinHost?.Register(builder => HTMLHostOwinApp.BuildApp(builder, app.BasePath));
    }

    protected override void OnStop()
    {
      appRegistration?.Dispose();
      appRegistration = null;
    }

  }
}

