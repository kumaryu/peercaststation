using System;
using System.Collections.Generic;
using System.Linq;
using PeerCastStation.Core;

namespace PeerCastStation.App
{
  public class ChannelNotifier
    : IPeerCastMonitor
  {
    private static TimeSpan messageExpires = TimeSpan.FromMinutes(1);
    public static TimeSpan MessageExpires {
      get { return messageExpires; }
      set { messageExpires = value; }
    }
    private System.Diagnostics.Stopwatch messageExpireTimer = new System.Diagnostics.Stopwatch();
    private NotificationMessage? lastMessage = null;
    private PeerCastApplication app;
    public ChannelNotifier(PeerCastApplication app)
    {
      this.app = app;
      this.messageExpireTimer.Start();
    }

    class Monitor
      : IChannelMonitor
    {
      public Channel Channel { get; }
      public ChannelNotifier Owner { get; }
      public Monitor(ChannelNotifier owner, Channel channel)
      {
        Owner = owner;
        Channel = channel;
      }

      public void OnContentChanged(ChannelContentType channelContentType)
      {
      }

      public void OnNodeChanged(ChannelNodeAction action, Host node)
      {
      }

      public void OnStopped(StopReason reason)
      {
        Owner.OnChannelClosed(Channel, reason);
      }
    }

    private void OnChannelClosed(Channel channel, StopReason reason)
    {
      switch (reason) {
      case StopReason.OffAir: {
          var msg = new NotificationMessage(
            channel.ChannelInfo.Name,
            "チャンネルが終了しました",
            NotificationMessageType.Info);
          NotifyMessage(msg);
        }
        break;
      case StopReason.NoHost:
      case StopReason.ConnectionError: {
          var msg = new NotificationMessage(
            channel.ChannelInfo.Name,
            "チャンネルに接続できませんでした",
            NotificationMessageType.Error);
          NotifyMessage(msg);
        }
        break;
      }
    }

    private void NotifyMessage(NotificationMessage msg)
    {
      lock (messageExpireTimer) {
        if (messageExpireTimer.Elapsed>=MessageExpires) {
          lastMessage = null;
          messageExpireTimer.Reset();
          messageExpireTimer.Start();
        }
        if (lastMessage==null || !lastMessage.Equals(msg)) {
          app.ShowNotificationMessage(msg);
          lastMessage = msg;
        }
      }
    }

    public void OnTimer()
    {
    }

    public void OnChannelChanged(PeerCastChannelAction action, Channel channel)
    {
      switch (action) {
      case PeerCastChannelAction.Added:
        channel.AddMonitor(new Monitor(this, channel));
        break;
      }
    }

  }

}
