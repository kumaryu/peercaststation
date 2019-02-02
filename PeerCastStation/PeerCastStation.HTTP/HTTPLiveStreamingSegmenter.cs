using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PeerCastStation.Core;
using System.IO;
using System.Threading;

namespace PeerCastStation.HTTP
{
  class HTTPLiveStreamingSegmenter : IContentSink, Channel.IHTTPLiveStreaming
  {
    private class SegmentList
    {
      private HTTPLiveStreamingSegmenter owner;
      private Content header;
      private MemoryStream segmentBuffer = new MemoryStream();
      private Ringbuffer<Channel.HLSSegment> segments = new Ringbuffer<Channel.HLSSegment>(5);
      private TaskCompletionSource<Ringbuffer<Channel.HLSSegment>> readyEvent = new TaskCompletionSource<Ringbuffer<Channel.HLSSegment>>();
      private bool keyframeFound = false;
      private double? lastPcr = null;

      public SegmentList(HTTPLiveStreamingSegmenter owner, Content header)
      {
        this.owner = owner;
        this.header = header;
        segmentBuffer.Write(header.Data, 0, header.Data.Length);
      }

      private void FlushSegment(double duration)
      {
        segmentBuffer.Close();
        byte[] data = segmentBuffer.ToArray();
        segmentBuffer = new MemoryStream();
        segmentBuffer.Write(header.Data, 0, header.Data.Length);
        lock (segments) {
          segments.Add(owner.AllocateSegment(data, duration));
          readyEvent.TrySetResult(segments);
        }
      }

      public void AddContent(Content content)
      {
        int r = 0;
        var bytes188 = new byte[188];
        while (r<content.Data.Length) {
          Array.Copy(content.Data, r, bytes188, 0, 188);
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

      public async Task<IList<Channel.HLSSegment>> GetSegmentsAsync(CancellationToken cancellationToken)
      {
        var task = readyEvent.Task;
        var result = await Task.WhenAny(task, cancellationToken.CreateCancelTask<Ringbuffer<Channel.HLSSegment>>()).ConfigureAwait(false);
        var segments = await result.ConfigureAwait(false);
        lock (segments) {
          return segments.ToArray();
        }
      }
    }

    protected Logger Logger { get; private set; }
    public Channel Channel { get; private set; }
    private int SegmentIndex = 1;
    private SegmentList Segments = null;

    private Channel.HLSSegment AllocateSegment(byte[] data, double duration)
    {
      var index = SegmentIndex++;
      Logger.Debug("HLSSegment: index:{0} duration:{1}", index, duration);
      return new Channel.HLSSegment(index, data, duration);
    }

    public HTTPLiveStreamingSegmenter(Channel channel)
    {
      Logger = new Logger(this.GetType());
      this.Channel = channel;
      IContentSink sink = this;
      sink =
          "flvtots".Split(',')
          .Select(name => Channel.PeerCast.ContentFilters.FirstOrDefault(filter => filter.Name.ToLowerInvariant() == name.ToLowerInvariant()))
          .Where(filter => filter != null)
          .Aggregate(sink, (r, filter) => filter.Activate(r));
      Channel.AddContentSink(sink);
    }

    public void OnChannelInfo(ChannelInfo channel_info)
    {
    }

    public void OnChannelTrack(ChannelTrack channel_track)
    {
    }

    public void OnContent(Content content)
    {
      if (Segments==null) return;
      Segments.AddContent(content);
    }

    public void OnContentHeader(Content content_header)
    {
      Segments = new SegmentList(this, content_header);
    }

    public void OnStop(StopReason reason)
    {
      Channel.RemoveContentSink(this);
    }

    public IList<Channel.HLSSegment> GetSegments()
    {
      var lst = Segments;
      if (lst!=null) {
        return lst.GetSegmentsAsync(CancellationToken.None).Result;
      }
      else {
        return new Channel.HLSSegment[0];
      }
    }

    public Task<IList<Channel.HLSSegment>> GetSegmentsAsync(CancellationToken cancellationToken)
    {
      var lst = Segments;
      if (lst!=null) {
        return lst.GetSegmentsAsync(CancellationToken.None);
      }
      else {
        return Task.FromResult<IList<Channel.HLSSegment>>(new Channel.HLSSegment[0]);
      }
    }

  }
}

