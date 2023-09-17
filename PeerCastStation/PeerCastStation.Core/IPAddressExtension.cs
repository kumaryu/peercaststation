using System;
using System.Net;
using System.Net.Sockets;

namespace PeerCastStation.Core
{
  public enum NetworkLocality {
    Undefined = -1,
    Loopback = 0,
    Local = 1,
    Global = 2,
  }

  public static class IPAddressExtension
  {
    static public AddressFamily GetAddressFamily(this NetworkType type)
    {
      switch (type) {
      case NetworkType.IPv4: return AddressFamily.InterNetwork;
      case NetworkType.IPv6: return AddressFamily.InterNetworkV6;
      default: throw new ArgumentException("Not supported network type", "type");
      }
    }

    static public NetworkType GetNetworkType(this AddressFamily family)
    {
      return family switch {
        AddressFamily.InterNetwork => NetworkType.IPv4,
        AddressFamily.InterNetworkV6 => NetworkType.IPv6,
        _ => throw new ArgumentException($"Unsupported address family {family}", nameof(family))
      };
    }

    static public bool IsIPv6UniqueLocal(this IPAddress addr)
    {
      if (addr.AddressFamily!=System.Net.Sockets.AddressFamily.InterNetworkV6) return false;
      var bytes = addr.GetAddressBytes();
      return bytes[0]==0xfc || bytes[0]==0xfd;
    }

    static public bool IsSiteLocal(this IPAddress addr)
    {
      if (addr.IsIPv4MappedToIPv6) {
        return IsSiteLocal(new IPAddress(new ReadOnlySpan<byte>(addr.GetAddressBytes(), 12, 4)));
      }
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
          addr.IsIPv6UniqueLocal() ||
          addr.IsIPv6Teredo ||
          addr.Equals(IPAddress.IPv6Loopback);
      default:
        return false;
      }
    }

    static public NetworkLocality GetAddressLocality(this IPAddress addr)
    {
      if (addr.IsIPv4MappedToIPv6) {
        return GetAddressLocality(new IPAddress(new ReadOnlySpan<byte>(addr.GetAddressBytes(), 12, 4)));
      }
      switch (addr.AddressFamily) {
      case System.Net.Sockets.AddressFamily.InterNetwork:
        if (addr.Equals(IPAddress.Any) || addr.Equals(IPAddress.None) || addr.Equals(IPAddress.Broadcast)) return NetworkLocality.Undefined;
        if (addr.Equals(IPAddress.Loopback)) return NetworkLocality.Loopback;
        if (IsSiteLocal(addr)) return NetworkLocality.Local;
        return NetworkLocality.Global;
      case System.Net.Sockets.AddressFamily.InterNetworkV6:
        if (addr.Equals(IPAddress.IPv6Any) || addr.Equals(IPAddress.IPv6None)) return NetworkLocality.Undefined;
        if (addr.Equals(IPAddress.IPv6Loopback)) return NetworkLocality.Loopback;
        if (IsSiteLocal(addr)) return NetworkLocality.Local;
        return NetworkLocality.Global;
      default:
        return NetworkLocality.Undefined;
      }
    }

  }
}
