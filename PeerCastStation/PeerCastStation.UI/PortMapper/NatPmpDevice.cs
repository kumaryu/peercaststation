using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;

namespace PeerCastStation.UI.PortMapper
{
  public class NatPmpDevice
    : INatDevice
  {
    const int  PMPPort           = 5351;
    const byte PMPVersion        = 0;
    const byte PMPOpExternalPort = 0;
    const byte PMPOpResultExternalPort = 128+0;
    const byte PMPOpMapTcp       = 2;
    const byte PMPOpResultMapTcp = 128+2;
    const byte PMPOpMapUdp       = 1;
    const byte PMPOpResultMapUdp = 128+1;
    const int PMPTries = 4;

    public string Name { get { return this.DeviceAddress.ToString(); } }
    public IPAddress DeviceAddress { get; private set; }
    public long LastTimestamp { get; private set; }
    public NatPmpDevice(IPAddress device_address)
    {
      this.DeviceAddress = device_address;
      this.LastTimestamp = -1;
    }

    private async Task<MappedPort> MapAsyncInternal(
        MappingProtocol protocol,
        int port,
        int lifetime,
        CancellationToken cancel_token)
    {
      int tries = 1;
    retry:
      cancel_token.ThrowIfCancellationRequested();
      using (var client = new UdpClient()) {
        var cancel_source = CancellationTokenSource.CreateLinkedTokenSource(
          new CancellationTokenSource(250*tries).Token,
          cancel_token);
        var cancel = cancel_source.Token;
        cancel.Register(() => client.Close(), false);
        try {
          var bytes = new byte[12];
          BinaryAccessor.PutByte(bytes, 0, PMPVersion);
          switch (protocol) {
          case MappingProtocol.TCP:
            BinaryAccessor.PutByte(bytes, 1, PMPOpMapTcp);
            break;
          case MappingProtocol.UDP:
            BinaryAccessor.PutByte(bytes, 1, PMPOpMapUdp);
            break;
          }
          BinaryAccessor.PutUInt16BE(bytes, 2, 0);
          BinaryAccessor.PutUInt16BE(bytes, 4, port);
          BinaryAccessor.PutUInt16BE(bytes, 6, port);
          BinaryAccessor.PutUInt32BE(bytes, 8, lifetime);

          await client.SendAsync(bytes, bytes.Length, new IPEndPoint(this.DeviceAddress, PMPPort));
          var msg = await client.ReceiveAsync();
          if (!msg.RemoteEndPoint.Address.Equals(this.DeviceAddress) || msg.Buffer.Length<16) {
            if (tries++<PMPTries) goto retry;
            throw new PortMappingException();
          }
          var ver    = BinaryAccessor.GetByte(msg.Buffer, 0);
          var opcode = BinaryAccessor.GetByte(msg.Buffer, 1);
          var err    = BinaryAccessor.GetUInt16BE(msg.Buffer, 2);
          var time   = BinaryAccessor.GetUInt32BE(msg.Buffer, 4);
          var internal_port = BinaryAccessor.GetUInt16BE(msg.Buffer, 8);
          var external_port = BinaryAccessor.GetUInt16BE(msg.Buffer, 10);
          var mapped_lifetime = BinaryAccessor.GetUInt32BE(msg.Buffer, 12);
          if (ver!=PMPVersion) {
            if (tries++<PMPTries) goto retry;
            throw new PortMappingException();
          }
          switch (protocol) {
          case MappingProtocol.TCP:
            if (opcode!=PMPOpResultMapTcp) {
              if (tries++<PMPTries) goto retry;
              throw new PortMappingException();
            }
            break;
          case MappingProtocol.UDP:
            if (opcode!=PMPOpResultMapUdp) {
              if (tries++<PMPTries) goto retry;
              throw new PortMappingException();
            }
            break;
          }
          if (err!=0) {
            if (tries++<PMPTries) goto retry;
            throw new PortMappingException();
          }
          this.LastTimestamp = time;
          return new MappedPort(
            this,
            protocol,
            internal_port,
            external_port,
            DateTime.Now.AddSeconds(mapped_lifetime));
        }
        catch (SocketException) { }
        catch (ObjectDisposedException) { }
        if (tries++<PMPTries) goto retry;
        throw new PortMappingException();
      }
    }

    public async Task UnmapAsync(
        MappingProtocol protocol,
        int port,
        CancellationToken cancel_token)
    {
      try {
        await MapAsyncInternal(protocol, port, 0, cancel_token);
      }
      catch (PortMappingException) {
      }
    }

    public Task<MappedPort> MapAsync(
        MappingProtocol protocol,
        int port,
        TimeSpan lifetime,
        CancellationToken cancel_token)
    {
      return MapAsyncInternal(
        protocol,
        port,
        lifetime.TotalSeconds<=0 ? 7200 : (int)lifetime.TotalSeconds,
        cancel_token);
    }

    public async Task<IPAddress> GetExternalAddressAsync(CancellationToken cancel_token)
    {
      cancel_token.ThrowIfCancellationRequested();
      int tries = 1;
    retry:
      using (var client = new UdpClient()) {
        var cancel_source = CancellationTokenSource.CreateLinkedTokenSource(
          new CancellationTokenSource(250*tries).Token,
          cancel_token);
        var cancel = cancel_source.Token;
        cancel.Register(() => client.Close(), false);
        try {
          var bytes = new byte[] { PMPVersion, PMPOpExternalPort };
          await client.SendAsync(bytes, bytes.Length, new IPEndPoint(this.DeviceAddress, PMPPort));
          var msg = await client.ReceiveAsync();
          if (!msg.RemoteEndPoint.Address.Equals(this.DeviceAddress) || msg.Buffer.Length<12) {
            if (tries++<PMPTries) goto retry;
            return null;
          }
          var ver    = BinaryAccessor.GetByte(msg.Buffer, 0);
          var opcode = BinaryAccessor.GetByte(msg.Buffer, 1);
          var err    = BinaryAccessor.GetUInt16BE(msg.Buffer, 2);
          var time   = BinaryAccessor.GetUInt32BE(msg.Buffer, 4);
          var external_ip = BinaryAccessor.GetIPv4AddressBE(msg.Buffer, 8);
          if (ver!=PMPVersion || opcode!=PMPOpResultExternalPort || err!=0) {
            if (tries++<PMPTries) goto retry;
            return null;
          }
          this.LastTimestamp = time;
          return external_ip;
        }
        catch (SocketException) { }
        catch (ObjectDisposedException) { }
        if (tries++<PMPTries) goto retry;
        return null;
      }
    }

  }

  public class NatPmpDeviceDiscoverer
    : INatDeviceDiscoverer
  {
    private IEnumerable<IPAddress> GetGatewayAddresses()
    {
      return NetworkInterface.GetAllNetworkInterfaces()
        .Where(intf => intf.OperationalStatus==OperationalStatus.Up)
        .Select(intf => intf.GetIPProperties())
        .Where(ipprop => ipprop.UnicastAddresses.Count>0)
        .SelectMany(ipprop => ipprop.GatewayAddresses.Select(addr => addr.Address))
        .Distinct();
    }

    public async Task<IEnumerable<INatDevice>> DiscoverAsync(CancellationToken cancel_token)
    {
      var devices = new List<NatPmpDevice>();
      foreach (var gateway in GetGatewayAddresses()) {
        var dev = new NatPmpDevice(gateway);
        var external_address = await dev.GetExternalAddressAsync(cancel_token);
        if (external_address!=null) devices.Add(dev);
      }
      return devices;
    }

  }

}
