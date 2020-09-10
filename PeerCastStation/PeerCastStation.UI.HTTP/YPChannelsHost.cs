using PeerCastStation.Core;
using PeerCastStation.Core.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.UI.HTTP
{
  public class YPChannelsHostOwinApp
  {
    public PeerCastApplication Application { get; private set; }
    public YPChannelsHostOwinApp(PeerCastApplication application)
    {
      Application = application;
    }

    public Task<IEnumerable<IYellowPageChannel>> GetYPChannelsAsync(CancellationToken cancellationToken)
    {
      var channel_list = Application.Plugins.FirstOrDefault(plugin => plugin is YPChannelList) as YPChannelList;
      if (channel_list==null) {
        return Task.FromResult(Enumerable.Empty<IYellowPageChannel>());
      }
      return channel_list.UpdateAsync(cancellationToken);
    }

    private static string FormatUptime(int? sec)
    {
      if (sec.HasValue) {
        var hours   = sec.Value/3600;
        var minutes = sec.Value/60%60;
        return String.Format("{0}:{1:D2}", hours, minutes);
      }
      else {
        return "";
      }
    }

    private static string ChannelsToIndex(IEnumerable<IYellowPageChannel> channels)
    {
      return String.Join("\n",
        channels.Select(channel =>
          String.Join(
            "<>",
            new string [] {
              channel.Name,
              channel.ChannelId.ToString("N").ToUpperInvariant(),
              channel.Tracker,
              channel.ContactUrl,
              channel.Genre,
              channel.Description,
              channel.Listeners.ToString(),
              channel.Relays.ToString(),
              channel.Bitrate.ToString(),
              channel.ContentType,
              channel.Artist,
              channel.TrackTitle,
              channel.Album,
              channel.TrackUrl,
              WebUtility.UrlEncode(channel.Name),
              FormatUptime(channel.Uptime),
              "click",
              channel.Comment,
              "0",
            }.Select(str => System.Net.WebUtility.HtmlEncode(str))
          )
        )
      ) + "\n";
    }

    private async Task Invoke(OwinEnvironment ctx)
    {
      var cancel_token = ctx.Request.CallCancelled;
      var channel_list = await GetYPChannelsAsync(cancel_token).ConfigureAwait(false);
      var contents     = System.Text.Encoding.UTF8.GetBytes(ChannelsToIndex(channel_list));
      ctx.Response.ContentType = "text/plain;charset=utf-8";
      ctx.Response.ContentLength = contents.LongLength;
      var acinfo = ctx.GetAccessControlInfo();
      if (acinfo?.AuthenticationKey!=null) {
        ctx.Response.Headers.Add("Set-Cookie", $"auth={acinfo.AuthenticationKey.GetToken()}; Path=/");
      }
      await ctx.Response.WriteAsync(contents, cancel_token).ConfigureAwait(false);
    }

    public static void BuildApp(IAppBuilder builder, PeerCastApplication application)
    {
      var app = new YPChannelsHostOwinApp(application);
      builder.MapGET("/ypchannels/index.txt", sub => {
        sub.UseAuth(OutputStreamType.Interface);
        sub.Run(app.Invoke);
      });
    }

  }

  [Plugin]
  public class YPChannelsHost
    : PluginBase
  {
    override public string Name { get { return "YP Channels Host"; } }

    private IDisposable appRegistration = null;

    protected override void OnStart()
    {
      var owin = Application.Plugins.OfType<OwinHostPlugin>().FirstOrDefault();
      appRegistration = owin?.OwinHost?.Register(builder => YPChannelsHostOwinApp.BuildApp(builder, Application));
    }

    protected override void OnStop()
    {
      appRegistration?.Dispose();
      appRegistration = null;
    }

  }

}
