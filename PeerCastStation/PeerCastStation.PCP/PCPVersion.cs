using PeerCastStation.Core;

namespace PeerCastStation.PCP
{
  public class PCPVersion
  {
    public static PCPVersion Default = new PCPVersion();

    public int    DefaultPort            { get; init; } = 7144;
    public int    ServantVersion         { get; init; } = 1218;
    public int    ServantVersionVP       { get; init; } = 27;
    public byte[] ServantVersionEXPrefix { get; init; } = new byte[] { (byte)'S', (byte)'T' };
    public short  ServantVersionEXNumber { get; init; } = 510;
    public int    ProtocolVersionIPv4    { get; init; } = 1;
    public int    ProtocolVersionIPv6    { get; init; } = 100;

    public int GetPCPVersionForNetworkType(NetworkType type)
    {
      switch (type) {
      case NetworkType.IPv6:
        return ProtocolVersionIPv6;
      case NetworkType.IPv4:
      default:
        return ProtocolVersionIPv4;
      }
    }

    public void SetHeloVersion(IAtomCollection helo)
    {
      helo.SetHeloVersion(ServantVersion);
    }

    public void SetHostVersion(IAtomCollection host)
    {
      host.SetHostVersion(ServantVersion);
      host.SetHostVersionVP(ServantVersionVP);
      host.SetHostVersionEXPrefix(ServantVersionEXPrefix);
      host.SetHostVersionEXNumber(ServantVersionEXNumber);
    }

    public void SetBcstVersion(IAtomCollection bcst)
    {
      bcst.SetBcstVersion(ServantVersion);
      bcst.SetBcstVersionVP(ServantVersionVP);
      bcst.SetBcstVersionEXPrefix(ServantVersionEXPrefix);
      bcst.SetBcstVersionEXNumber(ServantVersionEXNumber);
    }
  }

}

