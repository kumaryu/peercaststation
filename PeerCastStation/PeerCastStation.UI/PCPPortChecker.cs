using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using PeerCastStation.Core;
using System.Net.Sockets;
using System.Threading;
using System.Net.Http;

namespace PeerCastStation.UI
{
  [Plugin]
  public class PCPPortCheckerPlugin
    : PluginBase
  {
    public override string Name {
      get { return "PCPPortCheckerPlugin"; }
    }

    public Uri? TargetUriV4 { get; private set; }
    public Uri? TargetUriV6 { get; private set; }
    public PCPPortCheckerPlugin()
    {
      if (AppSettingsReader.TryGetUri("PCPPortChecker", out var target_uri)) {
        TargetUriV4 = target_uri;
      }
      else {
        TargetUriV4 = null;
      }
      if (AppSettingsReader.TryGetUri("PCPPortCheckerV6", out target_uri)) {
        TargetUriV6 = target_uri;
      }
      else {
        TargetUriV6 = null;
      }
    }

    public async Task<PortCheckResult[]> CheckAsync(PeerCast peercast)
    {
      var endpoints = peercast.OutputListeners
        .Where( listener => (listener.GlobalOutputAccepts & OutputStreamType.Relay)!=0)
        .Select(listener => listener.LocalEndPoint)
        .GroupBy(ep => ep.Address);
      return await Task.WhenAll(
        endpoints.Select(group => {
          var target = TargetUriV4;
          switch (group.Key.AddressFamily) {
          case AddressFamily.InterNetworkV6:
            target = TargetUriV6;
            break;
          case AddressFamily.InterNetwork:
          default:
            target = TargetUriV4;
            break;
          }
          if (target!=null) {
            var checker = new PCPPortChecker(peercast.SessionID, target, group.Key, group.Select(ep => ep.Port));
            return checker.RunAsync();
          }
          else {
            return Task.FromResult(new PortCheckResult(new InvalidOperationException($"PortCheck uri for {group.Key.AddressFamily} is empty"), group.Key, TimeSpan.Zero));
          }
        })
      ).ConfigureAwait(false);
    }
  }

  public class PortCheckResult
  {
    public bool       Success       { get; }
    public int[]      Ports         { get; }
    public IPAddress  LocalAddress  { get; }
    public IPAddress? GlobalAddress { get; }
    public TimeSpan   ElapsedTime   { get; }
    public Exception? Exception     { get; }
    public bool IsOpen { get { return Ports.Length>0; } }

    public PortCheckResult(IPAddress localAddress, IPAddress? globalAddress, int[] ports, TimeSpan elapsed)
    {
      this.Success       = true;
      this.LocalAddress  = localAddress;
      this.GlobalAddress = globalAddress;
      this.Ports         = ports;
      this.ElapsedTime   = elapsed;
      this.Exception     = null;
    }

    public PortCheckResult(Exception exception, IPAddress localAddress, TimeSpan elapsed)
    {
      this.Success       = false;
      this.LocalAddress  = localAddress;
      this.GlobalAddress = null;
      this.Ports         = Array.Empty<int>();
      this.ElapsedTime   = TimeSpan.Zero;
      this.Exception     = exception;
    }
  }

  public class PCPPortChecker
  {
    public Guid  InstanceId { get; private set; }
    public Uri   Target     { get; private set; }
    public IPAddress LocalAddress { get; private set; }
    public int[] Ports { get; private set; }
    public PCPPortChecker(Guid instance_id, Uri target_uri, IPAddress local_address, IEnumerable<int> ports)
    {
      this.InstanceId = instance_id;
      this.Target     = target_uri;
      this.LocalAddress = local_address;
      this.Ports = ports.ToArray();
    }

    public async Task<PortCheckResult> RunAsync()
    {
      var client = new HttpClient();
      var data   = new JObject();
      data["instanceId"] = InstanceId.ToString("N");
      data["ports"] = new JArray(this.Ports);
      var stopwatch = new System.Diagnostics.Stopwatch();
      using var cancel = new CancellationTokenSource();
      string response_body;
      try {
        stopwatch.Start();
        cancel.CancelAfter(5000);
        var rsp = await client.PostAsync(Target, new StringContent(data.ToString(), System.Text.Encoding.UTF8), cancel.Token).ConfigureAwait(false);
        rsp.EnsureSuccessStatusCode();
        response_body = await rsp.Content.ReadAsStringAsync(cancel.Token).ConfigureAwait(false);
        stopwatch.Stop();
        var response = JObject.Parse(response_body);
        var response_ip = response.GetValueAsIPAddess("ip");
        var response_ports =
          response
          .GetValueAsArray("ports")
          ?.Select(token => token.AsInt())
          ?.Where(v => v.HasValue)
          ?.Select(v => v!.Value);
        return new PortCheckResult(
            LocalAddress,
            response_ip,
            response_ports?.ToArray() ?? Array.Empty<int>(),
            stopwatch.Elapsed);
      }
      catch (Exception ex) {
        return new PortCheckResult(
            ex,
            LocalAddress,
            stopwatch.Elapsed);
      }
    }
  }

}
