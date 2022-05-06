using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using PeerCastStation.Core;
using System.Net.Sockets;
using System.Threading;

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
            return Task.FromResult(new PortCheckResult(false, group.Key, null, Array.Empty<int>(), TimeSpan.Zero));
          }
        })
      ).ConfigureAwait(false);
    }
  }

  public class PortCheckResult
  {
    public bool       Success      { get; private set; }
    public int[]      Ports        { get; private set; }
    public IPAddress  LocalAddress { get; private set; }
    public IPAddress? GlobalAddress { get; private set; }
    public TimeSpan   ElapsedTime  { get; private set; }
    public bool IsOpen { get { return Ports.Length>0; } }

    public PortCheckResult(bool success, IPAddress address, IPAddress? globalAddress, int[] ports, TimeSpan elapsed)
    {
      this.Success       = success;
      this.LocalAddress  = address;
      this.GlobalAddress = globalAddress;
      this.Ports         = ports;
      this.ElapsedTime   = elapsed;
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
      var client = new WebClient();
      var data   = new JObject();
      data["instanceId"] = InstanceId.ToString("N");
      data["ports"] = new JArray(this.Ports);
      var succeeded = false;
      var stopwatch = new System.Diagnostics.Stopwatch();
      var cancel = new CancellationTokenSource();
      string response_body;
      try {
        stopwatch.Start();
        var body = System.Text.Encoding.UTF8.GetBytes(data.ToString());
        using (cancel.Token.Register(() => client.CancelAsync())) {
          cancel.CancelAfter(5000);
          response_body = System.Text.Encoding.UTF8.GetString(
            await client.UploadDataTaskAsync(Target, body).ConfigureAwait(false)
          );
        }
        stopwatch.Stop();
        succeeded = true;
        var response = JObject.Parse(response_body);
        var response_ip = response.GetValueAsIPAddess("ip");
        var response_ports =
          response
          .GetValueAsArray("ports")
          ?.Select(token => token.AsInt())
          ?.Where(v => v.HasValue)
          ?.Select(v => v!.Value);
        return new PortCheckResult(
            succeeded,
            LocalAddress,
            response_ip,
            response_ports?.ToArray() ?? Array.Empty<int>(),
            stopwatch.Elapsed);
      }
      catch (WebException) {
        succeeded = false;
        return new PortCheckResult(
            succeeded,
            LocalAddress,
            null,
            new int[0],
            stopwatch.Elapsed);
      }
      catch (Newtonsoft.Json.JsonReaderException) {
        succeeded = false;
        return new PortCheckResult(
            succeeded,
            LocalAddress,
            null,
            new int[0],
            stopwatch.Elapsed);
      }
    }
  }

}
