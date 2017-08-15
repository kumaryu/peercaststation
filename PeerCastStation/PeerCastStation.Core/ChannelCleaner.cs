using System;
using System.Collections.Generic;
using System.Linq;
using PeerCastStation.Core;

namespace PeerCastStation
{
  public class ChannelCleaner
    : IChannelMonitor
  {
    private Dictionary<Channel, int> inactiveChannels  = new Dictionary<Channel,int>();
    private PeerCast peerCast;
    public ChannelCleaner(PeerCast peercast)
    {
      this.peerCast = peercast;
    }

    [PecaSettings]
    public enum CleanupMode {
      None         = 0,
      Disconnected = 1,
      NotRelaying  = 2,
      NotPlaying   = 3,
    }
    static private CleanupMode mode = CleanupMode.Disconnected;
    static public CleanupMode Mode {
      get { return mode; }
      set { mode = value; }
    }
    static private int inactiveLimit = 1800000;
    static public int InactiveLimit {
      get { return inactiveLimit; }
      set { inactiveLimit = value; }
    }

    private void CleanupChannels(Func<Channel, bool> predicate)
    {
      if (inactiveLimit<1) return;
      var channels = peerCast.Channels;
      foreach (var channel in channels) {
        if (channel.IsBroadcasting) continue;
        if (predicate(channel)) {
          int time;
          if (inactiveChannels.TryGetValue(channel, out time)) {
            if (Environment.TickCount-time>inactiveLimit) {
              peerCast.CloseChannel(channel);
              inactiveChannels.Remove(channel);
            }
          }
          else {
            inactiveChannels.Add(channel, Environment.TickCount);
          }
        }
        else {
          inactiveChannels.Remove(channel);
        }
      }
      foreach (var channel in inactiveChannels.Keys.ToArray()) {
        if (!channels.Contains(channel)) {
          inactiveChannels.Remove(channel);
        }
      }
    }

    public void OnTimer()
    {
      switch (mode) {
      case CleanupMode.None:
        break;
      case CleanupMode.Disconnected:
        CleanupChannels(channel => {
          return channel.Status==SourceStreamStatus.Idle ||
                 channel.Status==SourceStreamStatus.Error;
        });
        break;
      case CleanupMode.NotRelaying:
        CleanupChannels(channel => {
          return channel.LocalDirects==0 &&
                 channel.LocalRelays==0;
        });
        break;
      case CleanupMode.NotPlaying:
        CleanupChannels(channel => {
          return channel.LocalDirects==0;
        });
        break;
      }
    }
  }

}
