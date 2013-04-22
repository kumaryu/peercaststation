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
    private Dictionary<Channel, int> noPlayingChannels = new Dictionary<Channel,int>();
    private PeerCast peerCast;
    public ChannelCleaner(PeerCast peercast)
    {
      this.peerCast = peercast;
    }

    static private int inactiveLimit = 1800000;
    static public int InactiveLimit {
      get { return inactiveLimit; }
      set { inactiveLimit = value; }
    }

    static private int noPlayingLimit = 0;
    static public int NoPlayingLimit {
      get { return noPlayingLimit; }
      set { noPlayingLimit = value; }
    }

    private void CleanupChannels(Dictionary<Channel,int> times, int limit, Func<Channel, bool> predicate)
    {
      if (limit<1) return;
      var channels = peerCast.Channels;
      foreach (var channel in channels) {
        if (predicate(channel)) {
          int time;
          if (times.TryGetValue(channel, out time)) {
            if (Environment.TickCount-time>limit) {
              peerCast.CloseChannel(channel);
              times.Remove(channel);
            }
          }
          else {
            times.Add(channel, Environment.TickCount);
          }
        }
        else {
          times.Remove(channel);
        }
      }
      foreach (var channel in times.Keys.ToArray()) {
        if (!channels.Contains(channel)) {
          times.Remove(channel);
        }
      }
    }

    public void OnTimer()
    {
      CleanupChannels(inactiveChannels, InactiveLimit, channel => {
        return channel.Status==SourceStreamStatus.Idle ||
               channel.Status==SourceStreamStatus.Error;
      });
      CleanupChannels(noPlayingChannels, NoPlayingLimit, channel => {
        return channel.LocalDirects==0 &&
               channel.BroadcastID!=Guid.Empty;
      });
    }
  }

}
