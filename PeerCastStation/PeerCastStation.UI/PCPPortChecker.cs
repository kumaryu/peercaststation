using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Newtonsoft.Json.Linq;

namespace PeerCastStation.UI
{
  public class PortCheckCompletedEventArgs
    : EventArgs
  {
    public bool     Success     { get; private set; }
    public int[]    Ports       { get; private set; }
    public TimeSpan ElapsedTime { get; private set; }

    public PortCheckCompletedEventArgs(bool success, int[] ports, TimeSpan elapsed)
      : base()
    {
      this.Success     = success;
      this.Ports       = ports;
      this.ElapsedTime = elapsed;
    }
  }
  public delegate void PortCheckCompletedEventHandler(object sender, PortCheckCompletedEventArgs args);

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

    public void RunAsync()
    {
      var ctx = System.Threading.SynchronizationContext.Current;
      System.Threading.ThreadPool.QueueUserWorkItem(state => {
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
          response_body = System.Text.Encoding.UTF8.GetString(client.UploadData(Target, body));
          stopwatch.Stop();
          succeeded = true;
          var response = JToken.Parse(response_body);
          var response_ports = response["ports"].Select(token => (int)token);
          if (PortCheckCompleted!=null) {
            ctx.Post(s => {
              PortCheckCompleted(
                this,
                new PortCheckCompletedEventArgs(
                  succeeded,
                  response_ports.ToArray(),
                  stopwatch.Elapsed));
            }, null);
          }
        }
        catch (WebException) {
          succeeded = false;
          if (PortCheckCompleted!=null) {
            ctx.Post(s => {
              PortCheckCompleted(
                this,
                new PortCheckCompletedEventArgs(
                  succeeded,
                  null,
                  stopwatch.Elapsed));
            }, null);
          }
        }

      });
    }

    public void Run()
    {
      var ctx = System.Threading.SynchronizationContext.Current;
      var client = new WebClient();
      var data   = new JObject();
      data["instanceId"] = InstanceId.ToString("N");
      data["ports"] = new JArray(Ports);
      var succeeded = false;
      var stopwatch = new System.Diagnostics.Stopwatch();
      int[] response_ports = null;
      try {
        stopwatch.Start();
        var body = System.Text.Encoding.UTF8.GetBytes(data.ToString());
        var response_body = System.Text.Encoding.UTF8.GetString(client.UploadData(Target, body));
        var response = JToken.Parse(response_body);
        response_ports = response["ports"].Select(token => (int)token).ToArray();
        stopwatch.Stop();
        succeeded = true;
      }
      catch (WebException) {
        succeeded = false;
      }
      if (PortCheckCompleted!=null) {
        PortCheckCompleted(
          this,
          new PortCheckCompletedEventArgs(
            succeeded,
            response_ports,
            stopwatch.Elapsed));
      }
    }

    public event PortCheckCompletedEventHandler PortCheckCompleted;
  }

}
