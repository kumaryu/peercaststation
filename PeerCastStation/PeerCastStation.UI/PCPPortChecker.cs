using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using PeerCastStation.Core;

namespace PeerCastStation.UI
{
  [Plugin]
  public class PCPPortCheckerPlugin
    : PluginBase
  {
    public override string Name {
      get { return "PCPPortCheckerPlugin"; }
    }

    public Uri TargetUri { get; set; }
    public PCPPortCheckerPlugin()
    {
    }

    protected override void OnStart()
    {
      var target_uri = TargetUri;
      if (target_uri==null &&
          AppSettingsReader.TryGetUri("PCPPortChecker", out target_uri)) {
        TargetUri = target_uri;
      }
      base.OnStart();
      CheckAsync()
        .ContinueWith(prev => {
          if (prev.IsCanceled || prev.IsFaulted) return;
          this.Application.PeerCast.IsFirewalled = !prev.Result.IsOpen;
        });
    }

    public async Task<PortCheckResult> CheckAsync()
    {
      var peercast = Application.PeerCast;
      var ports = peercast.OutputListeners
        .Where( listener => (listener.GlobalOutputAccepts & OutputStreamType.Relay)!=0)
        .Select(listener => listener.LocalEndPoint.Port);
      var checker = new PCPPortChecker(peercast.SessionID, TargetUri, ports);
      return await checker.RunAsync();
    }
  }

  public class PortCheckResult
  {
    public bool     Success     { get; private set; }
    public int[]    Ports       { get; private set; }
    public TimeSpan ElapsedTime { get; private set; }
    public bool IsOpen { get { return Success && Ports.Length>0; } }

    public PortCheckResult(bool success, int[] ports, TimeSpan elapsed)
    {
      this.Success     = success;
      this.Ports       = ports;
      this.ElapsedTime = elapsed;
    }
  }

  public class PCPPortChecker
  {
    public Guid  InstanceId { get; private set; }
    public Uri   Target     { get; private set; }
    public int[] Ports      { get; private set; }
    public PCPPortChecker(Guid instance_id, Uri target_uri, IEnumerable<int> ports)
    {
      this.InstanceId = instance_id;
      this.Target = target_uri;
      this.Ports = ports.ToArray();
    }

    public async Task<PortCheckResult> RunAsync()
    {
      var client = new WebClient();
      var data   = new JObject();
      data["instanceId"] = InstanceId.ToString("N");
      data["ports"] = new JArray(Ports);
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
            response_ports.ToArray(),
            stopwatch.Elapsed);
      }
      catch (WebException) {
        succeeded = false;
        return new PortCheckResult(
            succeeded,
            null,
            stopwatch.Elapsed);
      }
      catch (Newtonsoft.Json.JsonReaderException) {
        succeeded = false;
        return new PortCheckResult(
            succeeded,
            null,
            stopwatch.Elapsed);
      }
    }
  }

}
