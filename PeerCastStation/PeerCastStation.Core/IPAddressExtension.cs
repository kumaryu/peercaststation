using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace PeerCastStation.Core
{
  public static class IPAddressExtension
  {
    static public bool IsSiteLocal(this IPAddress addr)
    {
      switch (addr.AddressFamily) {
      case System.Net.Sockets.AddressFamily.InterNetwork:
        var addr_bytes = addr.GetAddressBytes();
        return
          addr_bytes[0] == 10 ||
          addr_bytes[0] == 127 ||
          addr_bytes[0] == 169 && addr_bytes[1] == 254 ||
          addr_bytes[0] == 172 && (addr_bytes[1]&0xF0) == 16 ||
          addr_bytes[0] == 192 && addr_bytes[1] == 168;
      case System.Net.Sockets.AddressFamily.InterNetworkV6:
        return
          addr.IsIPv6LinkLocal ||
          addr.IsIPv6SiteLocal ||
          addr==IPAddress.IPv6Loopback;
      default:
        return false;
      }
    }

    static public int GetAddressLocality(this IPAddress addr)
    {
      switch (addr.AddressFamily) {
      case System.Net.Sockets.AddressFamily.InterNetwork:
        if (addr==IPAddress.Any || addr==IPAddress.None || addr==IPAddress.Broadcast) return -1;
        if (addr==IPAddress.Loopback) return 0;
        if (IsSiteLocal(addr)) return 1;
        return 2;
      case System.Net.Sockets.AddressFamily.InterNetworkV6:
        if (addr==IPAddress.IPv6Any || addr==IPAddress.IPv6None) return -1;
        if (addr==IPAddress.IPv6Loopback) return 0;
        if (IsSiteLocal(addr)) return 1;
        return 2;
      default:
        return -1;
      }
    }

  }
}
