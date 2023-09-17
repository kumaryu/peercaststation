using System;
using PeerCastStation.Core;

namespace PeerCastStation.PCP
{
  static class ChannelExtensions
  {
    public static int GetPCPVersion(this Channel channel)
    {
      return PCPVersion.Default.GetPCPVersionForNetworkType(channel.Network);
    }

  }
}
