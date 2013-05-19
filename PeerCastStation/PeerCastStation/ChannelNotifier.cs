using System;
using System.Collections.Generic;
using System.Linq;
using PeerCastStation.Core;

namespace PeerCastStation
{
  public class ChannelNotifier
    : IChannelMonitor
  {
    private Dictionary<Channel, int> inactiveChannels  = new Dictionary<Channel,int>();
    private PeerCastApplication app;
    public ChannelNotifier(PeerCastApplication app)
    {
      this.app = app;
      this.app.PeerCast.ChannelAdded   += (sender, args) => {
        args.Channel.Closed += OnChannelClosed;
      };
      this.app.PeerCast.ChannelRemoved += (sender, args) => {
        args.Channel.Closed -= OnChannelClosed;
      };
    }

    public void OnChannelClosed(object sender, StreamStoppedEventArgs args)
    {
      var channel = (Channel)sender;
      switch (args.StopReason) {
      case StopReason.OffAir: {
          var msg = new NotificationMessage(
            channel.ChannelInfo.Name,
            "チャンネルが終了しました",
            NotificationMessageType.Info);
          foreach (var ui in this.app.Plugins.Where(p => p is IUserInterfacePlugin)) {
            ((IUserInterfacePlugin)ui).ShowNotificationMessage(msg);
          }
        }
        break;
      case StopReason.NoHost:
      case StopReason.ConnectionError: {
          var msg = new NotificationMessage(
            channel.ChannelInfo.Name,
            "チャンネルに接続できませんでした",
            NotificationMessageType.Error);
          foreach (var ui in this.app.Plugins.Where(p => p is IUserInterfacePlugin)) {
            ((IUserInterfacePlugin)ui).ShowNotificationMessage(msg);
          }
        }
        break;
      }
    }

    public void OnTimer()
    {
    }
  }
}
