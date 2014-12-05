using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Nat;
using PeerCastStation.Core;

namespace PeerCastStation.UI
{
	[Plugin]
	public class PortMapper
		: PluginBase
	{
		public override string Name {
			get { return "PortMapper"; }
		}

		private bool enabled = false;
		public bool Enabled {
			get { return enabled; }
			set {
				if (enabled!=value) {
					enabled = value;
					if (Application==null) return;
					if (enabled) {
						if (monitor==null) OnStart();
						else               monitor.OnTimer();
					}
					else {
						if (monitor!=null) monitor.Clear();
					}
					SaveSettings();
				}
			}
		}

		public IEnumerable<System.Net.IPAddress> GetExternalAddresses()
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
			if (!enabled) return;
			monitor = new PortMapperMonitor(Application.PeerCast);
			Application.PeerCast.AddChannelMonitor(monitor);
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
		: IChannelMonitor,
		  IDisposable
	{
		private List<INatDevice> devices = new List<INatDevice>();
		private List<int>        ports   = new List<int>();
		private System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
		private PeerCast peerCast;

		public PortMapperMonitor(PeerCast peercast)
		{
			peerCast = peercast;
			NatUtility.DeviceFound += NatUtility_DeviceFound;
			NatUtility.DeviceLost  += NatUtility_DeviceLost;
			NatUtility.StartDiscovery();
			timer.Start();
		}

		public void Dispose()
		{
			timer.Stop();
			NatUtility.StopDiscovery();
			NatUtility.DeviceFound -= NatUtility_DeviceFound;
			NatUtility.DeviceLost  -= NatUtility_DeviceLost;
			lock (ports) {
				lock (devices) {
					foreach (var port in ports) {
						foreach (var device in devices) {
							Mapping mapping_tcp = new Mapping(Protocol.Tcp, port, port, 7200);
							Mapping mapping_udp = new Mapping(Protocol.Udp, port, port, 7200);
							device.BeginDeletePortMap(mapping_tcp, OnPortMapDeleted, device);
							device.BeginDeletePortMap(mapping_udp, OnPortMapDeleted, device);
						}
					}
					devices.Clear();
				}
				ports.Clear();
			}
		}

		public IList<System.Net.IPAddress> GetExternalAddresses()
		{
			lock (devices) {
				return devices.Select(dev => dev.GetExternalIP()).ToArray();
			}
		}

		public void Clear()
		{
			lock (ports) {
				lock (devices) {
					foreach (var port in ports) {
						foreach (var device in devices) {
							Mapping mapping_tcp = new Mapping(Protocol.Tcp, port, port, 7200);
							Mapping mapping_udp = new Mapping(Protocol.Udp, port, port, 7200);
							device.BeginDeletePortMap(mapping_tcp, OnPortMapDeleted, device);
							device.BeginDeletePortMap(mapping_udp, OnPortMapDeleted, device);
						}
					}
				}
				ports.Clear();
			}
		}

		private void AddPort(int port)
		{
			lock (ports) {
				if (ports.Any(p => p==port)) return;
				ports.Add(port);
			}
			lock (devices) {
				Mapping mapping_tcp = new Mapping(Protocol.Tcp, port, port, 7200);
				Mapping mapping_udp = new Mapping(Protocol.Udp, port, port, 7200);
				foreach (var device in devices) {
					device.BeginCreatePortMap(mapping_tcp, OnPortMapCreated, device);
					device.BeginCreatePortMap(mapping_udp, OnPortMapCreated, device);
				}
			}
		}

		private void OnPortMapCreated(IAsyncResult ar)
		{
			try {
				((INatDevice)ar.AsyncState).EndCreatePortMap(ar);
			}
			catch (MappingException) {
			}
		}

		private void RemovePort(int port)
		{
			lock (ports) {
				if (!ports.Remove(port)) return;
			}
			lock (devices) {
				foreach (var device in devices) {
					Mapping mapping_tcp = new Mapping(Protocol.Tcp, port, port, 7200);
					Mapping mapping_udp = new Mapping(Protocol.Udp, port, port, 7200);
					device.BeginDeletePortMap(mapping_tcp, OnPortMapDeleted, device);
					device.BeginDeletePortMap(mapping_udp, OnPortMapDeleted, device);
				}
			}
		}

		private void OnPortMapDeleted(IAsyncResult ar)
		{
			try {
				((INatDevice)ar.AsyncState).EndDeletePortMap(ar);
			}
			catch (MappingException) {
			}
		}

		private void RenewPort(int port)
		{
			lock (devices) {
				foreach (var device in devices) {
					Mapping mapping_tcp = new Mapping(Protocol.Tcp, port, port, 7200);
					Mapping mapping_udp = new Mapping(Protocol.Udp, port, port, 7200);
					device.BeginCreatePortMap(mapping_tcp, OnPortMapDeleted, device);
					device.BeginCreatePortMap(mapping_udp, OnPortMapDeleted, device);
				}
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
				foreach (var port in added_ports) {
					AddPort(port);
				}
				foreach (var port in removed_ports) {
					RemovePort(port);
				}
				if (timer.ElapsedMilliseconds<=60000) return;
				foreach (var port in existing_ports) {
					RenewPort(port);
				}
			}
		}

		private void NatUtility_DeviceFound(object sender, DeviceEventArgs e)
		{
			lock (devices) {
				devices.Add(e.Device);
			}
			lock (ports) {
				foreach (var port in ports) {
					Mapping mapping_tcp = new Mapping(Protocol.Tcp, port, port, 7200);
					Mapping mapping_udp = new Mapping(Protocol.Udp, port, port, 7200);
					e.Device.BeginCreatePortMap(mapping_tcp, OnPortMapCreated, e.Device);
					e.Device.BeginCreatePortMap(mapping_udp, OnPortMapCreated, e.Device);
				}
			}
		}

		private void NatUtility_DeviceLost(object sender, DeviceEventArgs e)
		{
			lock (devices) {
				devices.Remove(e.Device);
			}
		}

	}

}
