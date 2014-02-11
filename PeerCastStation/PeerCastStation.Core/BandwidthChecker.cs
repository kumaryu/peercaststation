using System;
using System.Net;
using System.Linq;

namespace PeerCastStation.Core
{
  public class BandwidthCheckCompletedEventArgs
    : EventArgs
  {
    public bool     Success     { get; private set; }
    public int      DataSize    { get; private set; }
    public TimeSpan ElapsedTime { get; private set; }
    public int Bitrate {
      get { return (int)(DataSize*8 / Math.Max(ElapsedTime.TotalSeconds, 0.001)); }
    }

    public BandwidthCheckCompletedEventArgs(bool success, int data_size, TimeSpan elapsed)
      : base()
    {
      this.Success     = success;
      this.DataSize    = data_size;
      this.ElapsedTime = elapsed;
    }
  }
  public delegate void BandwidthCheckCompletedEventHandler(object sender, BandwidthCheckCompletedEventArgs args);

  public class BandwidthChecker
  {
    public static readonly int DataSize = 256*1024;
    public static readonly int Tries    = 4;
    public Uri Target { get; private set; }
    public BandwidthChecker(Uri target_uri)
    {
      this.Target = target_uri;
    }

    private struct BandwidthCheckResult
    {
      public bool     Succeeded;
      public TimeSpan ElapsedTime;
    }

    public void Run()
    {
      var ctx = System.Threading.SynchronizationContext.Current;
      System.Threading.ThreadPool.QueueUserWorkItem(state => {
        var client = new WebClient();
        var rand   = new Random();
        var data   = new byte[DataSize];
        rand.NextBytes(data);
        var results = new BandwidthCheckResult[Tries];
        for (var i=0; i<Tries; i++) {
          try {
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            var response_body = client.UploadData(Target, data);
            stopwatch.Stop();
            results[i].ElapsedTime = stopwatch.Elapsed;
            results[i].Succeeded = true;
          }
          catch (WebException) {
            results[i].Succeeded = false;
          }
        }
        if (BandwidthCheckCompleted!=null) {
          var success = results.Count(r => r.Succeeded)>0;
          var average_seconds = results.Average(r => r.ElapsedTime.TotalSeconds);
          ctx.Post(s => {
            BandwidthCheckCompleted(
              this,
              new BandwidthCheckCompletedEventArgs(success, DataSize, TimeSpan.FromSeconds(average_seconds)));
          }, null);
        }
      });
    }

    public event BandwidthCheckCompletedEventHandler BandwidthCheckCompleted;
  }

}
