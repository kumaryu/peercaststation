using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PeerCastStation.Core;
using System.IO;
using System.Threading;

namespace PeerCastStation.HTTP
{
  public struct HLSSegment {
    public readonly int Index;
    public readonly byte[]? Data;
    public readonly double Duration;
    public HLSSegment(int index, byte[]? data, double duration)
    {
      Index = index;
      Data = data;
      Duration = duration;
    }
  }

  class HTTPLiveStreamingSegmenter
    : IContentSink
  {
    private class SegmentList
    {
      private HTTPLiveStreamingSegmenter owner;
      private Content header;
      private MemoryStream segmentBuffer = new MemoryStream();
      private Ringbuffer<HLSSegment> segments = new Ringbuffer<HLSSegment>(5);
      private TaskCompletionSource<Ringbuffer<HLSSegment>> readyEvent = new TaskCompletionSource<Ringbuffer<HLSSegment>>();
      private bool keyframeFound = false;
      private double? lastPcr = null;
      private bool completed = false;

      public SegmentList(HTTPLiveStreamingSegmenter owner, Content header)
      {
        this.owner = owner;
        this.header = header;
        segmentBuffer.Write(header.Data.Span);
      }

      private void FlushSegment(double duration)
      {
        var newbuf = new MemoryStream();
        newbuf.Write(header.Data.Span);
        var buf = Interlocked.Exchange(ref segmentBuffer, newbuf);
        buf.Close();
        lock (segments) {
          segments.Add(owner.AllocateSegment(buf.ToArray(), duration));
          readyEvent.TrySetResult(segments);
        }
      }

      public void AddContent(Content content)
      {
        if (completed) return;
        int r = 0;
        var bytes188 = new byte[188];
        while (r<content.Data.Length) {
          content.Data.Slice(r, 188).CopyTo(new Memory<byte>(bytes188));
          var tsPacket = new TSPacket(bytes188);
          if (tsPacket.keyframe) {
            if (lastPcr.HasValue) {
              var duration = tsPacket.program_clock_reference - lastPcr.Value;
              FlushSegment(duration);
            }
            keyframeFound = true;
            lastPcr = tsPacket.program_clock_reference;
          }
          if (keyframeFound) {
            segmentBuffer.Write(bytes188, 0, 188);
            if (segmentBuffer.Length > 8 * 1024 * 1024) {
              throw new Exception("Buffer Overflow");
            }
          }
          r += 188;
        }
      }

      public void Complete()
      {
        lock (segments) {
          segments.Add(new HLSSegment(segments.LastOrDefault().Index, null, 0.0));
          readyEvent.TrySetResult(segments);
          completed = true;
        }
      }

      public async Task<IList<HLSSegment>> GetSegmentsAsync(CancellationToken cancellationToken)
      {
        var task = readyEvent.Task;
        var result = await Task.WhenAny(task, cancellationToken.CreateCancelTask<Ringbuffer<HLSSegment>>()).ConfigureAwait(false);
        var segments = await result.ConfigureAwait(false);
        lock (segments) {
          return segments.ToArray();
        }
      }
    }

    class WaitableContainer<T>
    {
      private TaskCompletionSource<bool> initializedTask = new TaskCompletionSource<bool>();
      private T? value;
      public async Task<T?> GetAsync(CancellationToken cancellationToken)
      {
        var result = await Task.WhenAny(
          initializedTask.Task,
          cancellationToken.CreateCancelTask<bool>()
          ).ConfigureAwait(false);
        if (await result.ConfigureAwait(false)) {
          return value;
        }
        else {
          return default;
        }
      }

      public void Set(T value)
      {
        this.value = value;
        initializedTask.TrySetResult(true);
      }

      public T? Peek()
      {
        return value;
      }
    }

    private double targetDuration = 2.0;
    public double TargetDuration { get { return targetDuration; } }
    protected Logger Logger { get; private set; } = new Logger(nameof(HTTPLiveStreamingSegmenter));
    private int segmentIndex = 1;

    private WaitableContainer<SegmentList> segments = new WaitableContainer<SegmentList>();

    private void InterlockedMax(ref double target, double duration)
    {
    retry:
      var val = target;
      if (val<duration) {
        if (Interlocked.CompareExchange(ref target, duration, val)!=val) {
          goto retry;
        }
      }
    }

    private HLSSegment AllocateSegment(byte[] data, double duration)
    {
      var index = Interlocked.Increment(ref segmentIndex);
      InterlockedMax(ref targetDuration, duration);
      Logger.Debug("HLSSegment: index:{0} duration:{1}", index, duration);
      return new HLSSegment(index, data, duration);
    }

    public void OnChannelInfo(ChannelInfo channel_info)
    {
    }

    public void OnChannelTrack(ChannelTrack channel_track)
    {
    }

    public void OnContent(Content content)
    {
      segments.Peek()?.AddContent(content);
    }

    public void OnContentHeader(Content content_header)
    {
      segments.Set(new SegmentList(this, content_header));
    }

    public void OnStop(StopReason reason)
    {
      segments.Peek()?.Complete();
    }

    public async Task<IList<HLSSegment>> GetSegmentsAsync(CancellationToken cancellationToken)
    {
      var lst = await segments.GetAsync(cancellationToken).ConfigureAwait(false);
      if (lst!=null) {
        return await lst.GetSegmentsAsync(CancellationToken.None).ConfigureAwait(false);
      }
      else {
        return new HLSSegment[0];
      }
    }

  }
}


