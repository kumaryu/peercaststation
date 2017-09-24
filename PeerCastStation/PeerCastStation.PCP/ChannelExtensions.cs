using System;
using PeerCastStation.Core;

namespace PeerCastStation.PCP
{
  static class ChannelExtensions
  {
    public static int GetPCPVersion(this Channel channel)
    {
      switch (channel.Network) {
      case NetworkType.IPv6:
        return PCPVersion.ProtocolVersionIPv6;
      case NetworkType.IPv4:
      default:
        return PCPVersion.ProtocolVersionIPv4;
      }
    }

  }
}
