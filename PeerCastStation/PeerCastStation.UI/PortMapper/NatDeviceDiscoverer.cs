using System;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.UI.PortMapper
{
  public enum MappingProtocol {
    TCP,
    UDP,
  }

  public class PortMappingException
    : ApplicationException
  {
  }

  public class MappedPort
  {
    public INatDevice Device { get; private set; }
    public MappingProtocol Protocol { get; private set; }
    public int InternalPort { get; private set; }
    public int ExternalPort { get; private set; }
    public DateTime Expiration { get; private set; }

    public MappedPort(
        INatDevice device,
        MappingProtocol protocol,
        int internal_port,
        int external_port,
        DateTime expiration)
    {
      this.Device       = device;
      this.Protocol     = protocol;
      this.InternalPort = internal_port;
      this.ExternalPort = external_port;
      this.Expiration   = expiration;
    }
  }

  public interface INatDevice
  {
    string Name { get; }
    Task<MappedPort> MapAsync(MappingProtocol protocol, int port, TimeSpan lifetime, CancellationToken cancel_token);
    Task UnmapAsync(MappingProtocol protocol, int port, CancellationToken cancel_token);
    Task<IPAddress> GetExternalAddressAsync(CancellationToken cancel_token);
  }

  public interface INatDeviceDiscoverer
  {
    Task<IEnumerable<INatDevice>> DiscoverAsync(CancellationToken cancel_token);
  }

  public class NatDeviceDiscoverer
  {
    public IEnumerable<INatDeviceDiscoverer> Discoverers { get; private set; }

    public NatDeviceDiscoverer()
    {
      this.Discoverers = new INatDeviceDiscoverer[] {
        new NatPmpDeviceDiscoverer(),
        new UPnPWANConnectionServiceDiscoverer(),
      };
    }

    public async Task<IEnumerable<INatDevice>> DiscoverAsync(CancellationToken cancel_token)
    {
      var results = Enumerable.Empty<INatDevice>();
      foreach (var discoverer in this.Discoverers) {
        results = results.Concat(await discoverer.DiscoverAsync(cancel_token).ConfigureAwait(false));
      }
      return results;
    }
  }

  internal static class BinaryAccessor
  {
    static public byte GetByte(byte[] bytes, int offset)
    {
      return bytes[offset];
    }

    static public void PutByte(byte[] bytes, int offset, byte value)
    {
      bytes[offset] = value;
    }

    static public int GetUInt16BE(byte[] bytes, int offset)
    {
      return (bytes[offset]<<8) | bytes[offset+1];
    }

    static public void PutUInt16BE(byte[] bytes, int offset, int value)
    {
      bytes[offset+0] = (byte)((value & 0xFF00) >> 8);
      bytes[offset+1] = (byte)(value & 0x00FF);
    }

    static public long GetUInt32BE(byte[] bytes, int offset)
    {
      return
        ((uint)bytes[offset+0]<<24) |
        ((uint)bytes[offset+1]<<16) |
        ((uint)bytes[offset+2]<<8) |
        ((uint)bytes[offset+3]);
    }

    static public void PutUInt32BE(byte[] bytes, int offset, long value)
    {
      bytes[offset+0] = (byte)((value & 0xFF000000) >> 24);
      bytes[offset+1] = (byte)((value & 0x00FF0000) >> 16);
      bytes[offset+2] = (byte)((value & 0x0000FF00) >> 8);
      bytes[offset+3] = (byte)(value & 0x000000FF);
    }

    static public IPAddress GetIPv4AddressBE(byte[] bytes, int offset)
    {
      return new IPAddress(new byte[] { 
        bytes[offset+0],
        bytes[offset+1],
        bytes[offset+2],
        bytes[offset+3],
      });
    }

  }

}

