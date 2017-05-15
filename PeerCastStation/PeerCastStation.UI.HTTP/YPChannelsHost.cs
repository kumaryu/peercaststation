using PeerCastStation.Core;
using PeerCastStation.HTTP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.UI.HTTP
{
  [Plugin]
  class YPChannelsHost
    : PluginBase
  {
    override public string Name { get { return "YP Channels Host"; } }

    public YPChannelsHost()
    {
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
        applications.Add(owinhost.AddApplication("/ypchannels/index.txt", PathParameters.None, OnProcess));
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

    public Task<IEnumerable<IYellowPageChannel>> GetYPChannels()
    {
      var channel_list = Application.Plugins.FirstOrDefault(plugin => plugin is YPChannelList) as YPChannelList;
      if (channel_list==null) return Task.FromResult(Enumerable.Empty<IYellowPageChannel>());
      return channel_list.UpdateAsync();
    }

    public string FormatUptime(int? sec)
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

    public string ChannelsToIndex(IEnumerable<IYellowPageChannel> channels)
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
              System.Net.WebUtility.UrlEncode(channel.Name),
              FormatUptime(channel.Uptime),
              "click",
              channel.Comment,
              "0",
            }.Select(str => System.Net.WebUtility.HtmlEncode(str))
          )
        )
      ) + "\n";
    }

    private async Task SendResponseChannelList(OWINEnv env, CancellationToken cancel_token)
    {
      var channel_list = await GetYPChannels();
      var contents     = System.Text.Encoding.UTF8.GetBytes(ChannelsToIndex(channel_list));
      env.SetResponseHeader("Content-Type", "text/plain;charset=utf-8");
      env.SetResponseHeader("Content-Length", contents.Length.ToString());
      if (env.AccessControlInfo.AuthenticationKey!=null) {
        env.SetResponseHeader("Set-Cookie", "auth=" + HTTPUtils.CreateAuthorizationToken(env.AccessControlInfo.AuthenticationKey));
      }
      if (env.RequestMethod=="GET") {
        await env.ResponseBody.WriteAsync(contents, 0, contents.Length, cancel_token);
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
        await SendResponseChannelList(env, cancel_token);
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
