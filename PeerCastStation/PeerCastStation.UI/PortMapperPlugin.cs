using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using PeerCastStation.Core;
using PeerCastStation.UI.PortMapper;

namespace PeerCastStation.UI
{
  [Plugin]
  public class PortMapperPlugin
    : PluginBase
  {
    public override string Name {
      get { return "PortMapperPlugin"; }
    }

    private bool enabled = false;
    public bool Enabled {
      get { return enabled; }
      set {
        if (enabled==value) return;
        enabled = value;
        if (Application==null) return;
        if (enabled) {
          OnStart();
        }
        else {
          OnStop();
        }
        SaveSettings();
      }
    }

    public IEnumerable<IPAddress> GetExternalAddresses()
    {
      if (monitor==null || !enabled) {
        return Enumerable.Empty<System.Net.IPAddress>();
      }
      else {
        return monitor.GetExternalAddresses();
      }
    }

    protected override void OnAttach()
    {
      base.OnAttach();
      var settings = Application.Settings.Get<PortMapperSettings>();
      enabled = settings.Enabled;
    }

    private void SaveSettings()
    {
      if (Application==null) return;
      var settings = Application.Settings.Get<PortMapperSettings>();
      settings.Enabled = enabled;
      Application.SaveSettings();
    }

    private PortMapperMonitor monitor;

    protected override void OnStart()
    {
      base.OnStart();
      if (enabled) {
        monitor = new PortMapperMonitor(Application.PeerCast);
        Application.PeerCast.AddChannelMonitor(monitor);
      }
      Task.Run(async () => {
        if (enabled) {
          await DiscoverAsync().ConfigureAwait(false);
        }
        var results = await CheckPortAsync().ConfigureAwait(false);
        foreach (var result in results) {
          if (!result.Success) continue;
          if (result.IsOpen) {
            Application.PeerCast.SetPortStatus(result.LocalAddress, result.GlobalAddress, PortStatus.Open);
          }
          else {
            Application.PeerCast.SetPortStatus(result.LocalAddress, result.GlobalAddress, PortStatus.Firewalled);
          }
        }
      });
    }

    protected override void OnStop()
    {
      if (monitor!=null) {
        Application.PeerCast.RemoveChannelMonitor(monitor);
        monitor.Dispose();
        monitor = null;
      }
      base.OnStop();
    }

    private Task discoverTask = Task.Delay(0);
    public Task DiscoverAsync()
    {
      if (monitor==null || !enabled) return Task.Delay(0);
      if (!discoverTask.IsCompleted) return discoverTask;
      discoverTask = monitor.DiscoverAsync();
      return discoverTask;
    }

    private async Task<PortCheckResult[]> CheckPortAsync()
    {
      var checker = Application.Plugins.GetPlugin<PCPPortCheckerPlugin>();
      if (checker==null) return new PortCheckResult[0];
      return await checker.CheckAsync().ConfigureAwait(false);
    }

  }

  [PecaSettings]
  public class PortMapperSettings
  {
    public bool Enabled { get; set; }

    public PortMapperSettings()
    {
      Enabled = true;
    }
  }

  public class PortMapperMonitor
    : IPeerCastMonitor,
      IDisposable
  {
    private class NatDevice
    {
      public INatDevice Device { get; private set; }
      public IPAddress ExternalIPAddress { get; private set; }
      public Task MapAsync(MappingProtocol protocol, int port, TimeSpan lifetime, CancellationToken cancel_token)
      {
        return Device.MapAsync(protocol, port, lifetime, cancel_token);
      }
      public Task UnmapAsync(MappingProtocol protocol, int port, CancellationToken cancel_token)
      {
        return Device.UnmapAsync(protocol, port, cancel_token);
      }
      public NatDevice(INatDevice device, IPAddress external_ipaddress)
      {
        this.Device = device;
        this.ExternalIPAddress = external_ipaddress;
      }

      public override bool Equals(object obj)
      {
        if (obj==null) return false;
        if (ReferenceEquals(obj, this)) return true;
        return this.Device.Equals(((NatDevice)obj).Device);
      }

      public override int GetHashCode()
      {
        return this.Device.GetHashCode();
      }
    }

    private ISet<NatDevice> devices = new HashSet<NatDevice>();
    private IEnumerable<int> ports   = Enumerable.Empty<int>();
    private System.Diagnostics.Stopwatch renewTimer = new System.Diagnostics.Stopwatch();
    private PeerCast peerCast;
    private NatDeviceDiscoverer discoverer = new NatDeviceDiscoverer();
    private CancellationTokenSource cancelSource = new CancellationTokenSource();
    private Logger logger = new Logger(typeof(PortMapperMonitor));

    public PortMapperMonitor(PeerCast peercast)
    {
      peerCast = peercast;
      renewTimer.Start();
    }

    public Task DiscoverAsync()
    {
      return DiscoverAsync(cancelSource.Token);
    }

    private async Task DiscoverAsync(CancellationToken cancel_token)
    {
      cancel_token.ThrowIfCancellationRequested();
      try {
        var new_devices = new HashSet<NatDevice>(await Task.WhenAll(
          (await discoverer.DiscoverAsync(cancel_token).ConfigureAwait(false))
          .Select(async dev => new NatDevice(dev, await dev.GetExternalAddressAsync(cancel_token).ConfigureAwait(false)))
        ).ConfigureAwait(false));
        foreach (var device in new_devices) {
          if (devices.Contains(device)) continue;
          foreach (var port in ports) {
            AddPortOnDevice(device, port);
          }
        }
        devices = new_devices;
      }
      catch (Exception e) {
        logger.Info(e);
        throw;
      }
    }

    public void Dispose()
    {
      renewTimer.Stop();
      Clear();
      cancelSource.CancelAfter(1000);
      devices = new HashSet<NatDevice>();
    }

    public IEnumerable<IPAddress> GetExternalAddresses()
    {
      return devices
        .Select(dev => dev.ExternalIPAddress)
        .Where(addr => addr!=null)
        .ToArray();
    }

    public void Clear()
    {
      foreach (var port in ports) {
        RemovePort(port);
      }
      ports = Enumerable.Empty<int>();
    }

    private void AddPortOnDevice(NatDevice device, int port)
    {
      device.MapAsync(MappingProtocol.TCP, port, TimeSpan.FromSeconds(7200), cancelSource.Token);
      device.MapAsync(MappingProtocol.UDP, port, TimeSpan.FromSeconds(7200), cancelSource.Token);
    }

    private void RemovePortOnDevice(NatDevice device, int port)
    {
      device.UnmapAsync(MappingProtocol.TCP, port, cancelSource.Token);
      device.UnmapAsync(MappingProtocol.UDP, port, cancelSource.Token);
    }

    private void AddPort(int port)
    {
      foreach (var device in devices) {
        AddPortOnDevice(device, port);
      }
    }

    private void RemovePort(int port)
    {
      foreach (var device in devices) {
        RemovePortOnDevice(device, port);
      }
    }

    private void RenewPort(int port)
    {
      foreach (var device in devices) {
        AddPortOnDevice(device, port);
      }
    }

    public void OnTimer()
    {
      lock (ports) {
        var current_ports = peerCast.OutputListeners
          .Where(listener  => (listener.GlobalOutputAccepts & OutputStreamType.All)!=0)
          .Select(listener => listener.LocalEndPoint.Port).ToArray();
        var added_ports    = current_ports.Except(ports).ToArray();
        var removed_ports  = ports.Except(current_ports).ToArray();
        var existing_ports = current_ports.Intersect(ports).ToArray();
        foreach (var port in removed_ports) {
          RemovePort(port);
        }
        foreach (var port in added_ports) {
          AddPort(port);
        }
        if (renewTimer.ElapsedMilliseconds>=60000) {
          foreach (var port in existing_ports) {
            RenewPort(port);
          }
          renewTimer.Restart();
        }
        ports = current_ports;
      }
    }

  }

}
