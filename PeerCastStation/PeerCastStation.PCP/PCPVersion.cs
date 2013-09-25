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
    public static readonly short  ServantVersionEXNumber = 153;

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
