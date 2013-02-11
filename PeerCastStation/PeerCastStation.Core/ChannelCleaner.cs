using System;
using System.Collections.Generic;
using System.Linq;
using PeerCastStation.Core;

namespace PeerCastStation
{
  public class ChannelCleaner
    : IChannelMonitor
  {
    private Dictionary<Channel, int> inactiveChannels = new Dictionary<Channel,int>();
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

    public void OnTimer()
    {
      if (InactiveLimit<1) return;
      var channels = peerCast.Channels;
      foreach (var channel in channels) {
        if (channel.Status==SourceStreamStatus.Idle ||
            channel.Status==SourceStreamStatus.Error) {
          int inactivetime;
          if (inactiveChannels.TryGetValue(channel, out inactivetime)) {
            if (Environment.TickCount-inactivetime>InactiveLimit) {
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
  }

}
