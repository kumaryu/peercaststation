using System;
using PeerCastStation.Core;

namespace PeerCastStation.PCP
{
  public static class PCPVersion
  {
    public static readonly int    DefaultPort = 7144;
    public static readonly int    ServantVersion         = 1218;
    public static readonly int    ServantVersionVP       = 27;
    public static readonly byte[] ServantVersionEXPrefix = new byte[] { (byte)'S', (byte)'T' };
    public static readonly short  ServantVersionEXNumber = 310;
    public static readonly int    ProtocolVersionIPv4    = 1;
    public static readonly int    ProtocolVersionIPv6    = 100;

    public static int GetPCPVersionForNetworkType(NetworkType type)
    {
      switch (type) {
      case NetworkType.IPv6:
        return ProtocolVersionIPv6;
      case NetworkType.IPv4:
      default:
        return ProtocolVersionIPv4;
      }
    }

    public static void SetHeloVersion(IAtomCollection helo)
    {
      helo.SetHeloVersion(ServantVersion);
    }

    public static void SetHostVersion(IAtomCollection host)
    {
      host.SetHostVersion(ServantVersion);
      host.SetHostVersionVP(ServantVersionVP);
      host.SetHostVersionEXPrefix(ServantVersionEXPrefix);
      host.SetHostVersionEXNumber(ServantVersionEXNumber);
    }

    public static void SetBcstVersion(IAtomCollection bcst)
    {
      bcst.SetBcstVersion(ServantVersion);
      bcst.SetBcstVersionVP(ServantVersionVP);
      bcst.SetBcstVersionEXPrefix(ServantVersionEXPrefix);
      bcst.SetBcstVersionEXNumber(ServantVersionEXNumber);
    }
  }
}
