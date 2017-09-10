using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using PeerCastStation.Core;
using System.Net.Sockets;

namespace PeerCastStation.UI
{
  [Plugin]
  public class PCPPortCheckerPlugin
    : PluginBase
  {
    public override string Name {
      get { return "PCPPortCheckerPlugin"; }
    }

    public Uri TargetUriV4 { get; set; }
    public Uri TargetUriV6 { get; set; }
    public PCPPortCheckerPlugin()
    {
    }

    protected override void OnStart()
    {
      var target_uri = TargetUriV4;
      if (target_uri==null &&
          AppSettingsReader.TryGetUri("PCPPortChecker", out target_uri)) {
        TargetUriV4 = target_uri;
      }
      if (TargetUriV6==null &&
          AppSettingsReader.TryGetUri("PCPPortCheckerV6", out target_uri)) {
        TargetUriV6 = target_uri;
      }
      base.OnStart();
      CheckAsync()
        .ContinueWith(prev => {
          if (prev.IsCanceled || prev.IsFaulted) return;
          foreach (var result in prev.Result) {
            if (!result.Success) continue;
            if (result.IsOpen) {
              this.Application.PeerCast.SetPortStatus(result.LocalAddress.AddressFamily, PortStatus.Open);
            }
            else {
              this.Application.PeerCast.SetPortStatus(result.LocalAddress.AddressFamily, PortStatus.Firewalled);
            }
          }
        });
    }

    public async Task<PortCheckResult> CheckAsyncV4()
    {
      var peercast = Application.PeerCast;
      var endpoints = peercast.OutputListeners
        .Where( listener => (listener.GlobalOutputAccepts & OutputStreamType.Relay)!=0)
        .Where( listener => listener.LocalEndPoint.AddressFamily==AddressFamily.InterNetwork)
        .Select(listener => listener.LocalEndPoint);
      var checker = new PCPPortChecker(peercast.SessionID, TargetUriV4, endpoints);
      return await checker.RunAsync();
    }

    public async Task<PortCheckResult> CheckAsyncV6()
    {
      var peercast = Application.PeerCast;
      var endpoints = peercast.OutputListeners
        .Where( listener => (listener.GlobalOutputAccepts & OutputStreamType.Relay)!=0)
        .Where( listener => listener.LocalEndPoint.AddressFamily==AddressFamily.InterNetworkV6)
        .Select(listener => listener.LocalEndPoint);
      var checker = new PCPPortChecker(peercast.SessionID, TargetUriV6, endpoints);
      return await checker.RunAsync();
    }

    public async Task<PortCheckResult[]> CheckAsync()
    {
      return await Task.WhenAll(CheckAsyncV4(), CheckAsyncV6());
    }
  }

  public class PortCheckResult
  {
    public bool      Success      { get; private set; }
    public int[]     Ports        { get; private set; }
    public IPAddress LocalAddress { get; private set; }
    public TimeSpan  ElapsedTime  { get; private set; }
    public bool IsOpen { get { return Ports.Length>0; } }

    public PortCheckResult(bool success, IPAddress address, int[] ports, TimeSpan elapsed)
    {
      this.Success      = success;
      this.LocalAddress = address;
      this.Ports        = ports;
      this.ElapsedTime  = elapsed;
    }
  }

  public class PCPPortChecker
  {
    public Guid  InstanceId { get; private set; }
    public Uri   Target     { get; private set; }
    public IPEndPoint[] EndPoints { get; private set; }
    public PCPPortChecker(Guid instance_id, Uri target_uri, IEnumerable<IPEndPoint> endpoints)
    {
      this.InstanceId = instance_id;
      this.Target     = target_uri;
      this.EndPoints  = endpoints.ToArray();
    }

    public async Task<PortCheckResult> RunAsync(IPEndPoint endpoint)
    {
      var client = new WebClient();
      var data   = new JObject();
      data["instanceId"] = InstanceId.ToString("N");
      data["ports"] = new JArray(EndPoints.Select(ep => ep.Port));
      var succeeded = false;
      var stopwatch = new System.Diagnostics.Stopwatch();
      string response_body = null;
      try {
        stopwatch.Start();
        var body = System.Text.Encoding.UTF8.GetBytes(data.ToString());
        response_body = System.Text.Encoding.UTF8.GetString(
          await client.UploadDataTaskAsync(Target, body)
        );
        stopwatch.Stop();
        succeeded = true;
        var response = JToken.Parse(response_body);
        var response_ports = response["ports"].Select(token => (int)token);
        return new PortCheckResult(
            succeeded,
            endpoint.Address,
            response_ports.ToArray(),
            stopwatch.Elapsed);
      }
      catch (WebException) {
        succeeded = false;
        return new PortCheckResult(
            succeeded,
            endpoint.Address,
            new int[0],
            stopwatch.Elapsed);
      }
      catch (Newtonsoft.Json.JsonReaderException) {
        succeeded = false;
        return new PortCheckResult(
            succeeded,
            endpoint.Address,
            new int[0],
            stopwatch.Elapsed);
      }

    }
    
    public async Task<PortCheckResult> RunAsync()
    {
      var client = new WebClient();
      var data   = new JObject();
      data["instanceId"] = InstanceId.ToString("N");
      data["ports"] = new JArray(EndPoints.Select(ep => ep.Port));
      var succeeded = false;
      var stopwatch = new System.Diagnostics.Stopwatch();
      string response_body = null;
      try {
        stopwatch.Start();
        var body = System.Text.Encoding.UTF8.GetBytes(data.ToString());
        response_body = System.Text.Encoding.UTF8.GetString(
          await client.UploadDataTaskAsync(Target, body)
        );
        stopwatch.Stop();
        succeeded = true;
        var response = JToken.Parse(response_body);
        var response_ports = response["ports"].Select(token => (int)token);
        return new PortCheckResult(
            succeeded,
            EndPoints.First().Address,
            response_ports.ToArray(),
            stopwatch.Elapsed);
      }
      catch (WebException) {
        succeeded = false;
        return new PortCheckResult(
            succeeded,
            EndPoints.First().Address,
            new int[0],
            stopwatch.Elapsed);
      }
      catch (Newtonsoft.Json.JsonReaderException) {
        succeeded = false;
        return new PortCheckResult(
            succeeded,
            EndPoints.First().Address,
            new int[0],
            stopwatch.Elapsed);
      }
    }
  }

}
