using System;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.Core
{
  public class AsynchronousContentSink
    : IContentSink
  {
    public IContentSink TargetSink { get; private set; }
    private Task lastTask = Task.Delay(0);
    private CancellationTokenSource taskAbortedSource = new CancellationTokenSource();
    public Exception Exception { get; private set; } = null;
    public bool IsFaulted { get { return this.Exception!=null; } }
    
    public AsynchronousContentSink(IContentSink target_sink)
    {
      this.TargetSink = target_sink;
    }

    private void DispatchSinkEvent(Action action)
    {
      lastTask = lastTask.ContinueWith(prev => {
        try {
          action();
        }
        catch (Exception e) {
          this.Exception = e;
          taskAbortedSource.Cancel();
        }
      }, taskAbortedSource.Token);
    }

    public void OnChannelInfo(ChannelInfo channel_info)
    {
      DispatchSinkEvent(() => this.TargetSink.OnChannelInfo(channel_info));
    }

    public void OnChannelTrack(ChannelTrack channel_track)
    {
      DispatchSinkEvent(() => this.TargetSink.OnChannelTrack(channel_track));
    }

    public void OnContent(Content content)
    {
      DispatchSinkEvent(() => this.TargetSink.OnContent(content));
    }

    public void OnContentHeader(Content content_header)
    {
      DispatchSinkEvent(() => this.TargetSink.OnContentHeader(content_header));
    }

    public void OnStop(StopReason reason)
    {
      DispatchSinkEvent(() => this.TargetSink.OnStop(reason));
    }
  }
}
