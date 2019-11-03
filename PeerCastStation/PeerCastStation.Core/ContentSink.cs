using System;
using System.IO;

namespace PeerCastStation.Core
{
  public class BufferedContentSink
    : IContentSink
  {
    public IContentSink BaseSink { get; private set; }
    public Content LastContent { get; private set; }
    private RateCounter packetRateCounter = new RateCounter(1000);
    public float PacketRate { get { return packetRateCounter.Rate; } }
    public BufferedContentSink(IContentSink base_sink)
    {
      this.BaseSink = base_sink;
      this.LastContent = null;
    }

    public void OnChannelInfo(ChannelInfo channel_info)
    {
      this.BaseSink.OnChannelInfo(channel_info);
    }

    public void OnChannelTrack(ChannelTrack channel_track)
    {
      this.BaseSink.OnChannelTrack(channel_track);
    }

    public void OnStop(StopReason reason)
    {
      this.BaseSink.OnStop(reason);
    }

    private class ContentBuilder
    {
      private bool empty = true;
      private int stream;
      private long position;
      private PCPChanPacketContinuation contFlag;
      private TimeSpan timestamp;
      private TimeSpan lastTimestamp;
      private MemoryStream dataBuffer = new MemoryStream(16*1024);
      public ContentBuilder()
      {
      }

      public bool Append(Content content)
      {
        if (empty) {
          stream = content.Stream;
          position = content.Position;
          contFlag = content.ContFlag;
          timestamp = content.Timestamp;
          lastTimestamp = timestamp;
          dataBuffer.SetLength(0);
          dataBuffer.Write(content.Data, 0, content.Data.Length);
          empty = false;
          return true;
        }
        else if (stream!=content.Stream ||
                 contFlag!=content.ContFlag ||
                 position+dataBuffer.Length!=content.Position ||
                 Math.Abs((content.Timestamp-lastTimestamp).TotalMilliseconds)>100.0 ||
                 dataBuffer.Length+content.Data.Length>8*1024) {
          return false;
        }
        else {
          lastTimestamp = content.Timestamp;
          dataBuffer.Write(content.Data, 0, content.Data.Length);
          return true;
        }
      }

      public Content ToContent()
      {
        if (empty) return null;
        dataBuffer.Flush();
        return new Content(stream, timestamp, position, dataBuffer.ToArray(), contFlag);
      }

      public void Clear()
      {
        empty = true;
      }

    }

    private ContentBuilder builder = new ContentBuilder();
    public void OnContent(Content content)
    {
      while (!builder.Append(content)) {
        Flush();
      }
      this.LastContent = content;
    }

    private void Flush()
    {
      var content = builder.ToContent();
      if (content!=null) {
        this.BaseSink.OnContent(content);
        packetRateCounter.Add(1);
      }
      builder.Clear();
    }

    public void OnContentHeader(Content content_header)
    {
      this.Flush();
      this.BaseSink.OnContentHeader(content_header);
      this.LastContent = content_header;
    }

  }

  public class ChannelContentSink
    : IContentSink
  {
    public Channel Channel     { get; private set; }
    public Content LastContent { get; private set; }
    public bool    UseContentBitrate { get; private set; }
    private RateCounter packetRateCounter = new RateCounter(1000);
    public float PacketRate { get { return packetRateCounter.Rate; } }
    public ChannelContentSink(Channel channel, bool use_content_bitrate)
    {
      this.Channel = channel;
      this.LastContent = null;
      this.UseContentBitrate = use_content_bitrate;
    }

    public void OnChannelInfo(ChannelInfo channel_info)
    {
      this.Channel.ChannelInfo = MergeChannelInfo(Channel.ChannelInfo, channel_info);
    }

    public void OnChannelTrack(ChannelTrack channel_track)
    {
      this.Channel.ChannelTrack = MergeChannelTrack(Channel.ChannelTrack, channel_track);
    }

    public void OnContent(Content content)
    {
      this.Channel.Contents.Add(content);
      packetRateCounter.Add(1);
      this.LastContent = content;
    }

    public void OnContentHeader(Content content_header)
    {
      this.Channel.ContentHeader = content_header;
      this.Channel.Contents.Clear();
      this.LastContent = content_header;
    }

    public void OnStop(StopReason reason)
    {
    }

    private ChannelInfo MergeChannelInfo(ChannelInfo a, ChannelInfo b)
    {
      var base_atoms = new AtomCollection(a.Extra);
      var new_atoms  = new AtomCollection(b.Extra);
      if (!UseContentBitrate) {
        new_atoms.RemoveByName(Atom.PCP_CHAN_INFO_BITRATE);
      }
      base_atoms.Update(new_atoms);
      return new ChannelInfo(base_atoms);
    }

    private ChannelTrack MergeChannelTrack(ChannelTrack a, ChannelTrack b)
    {
      var base_atoms = new AtomCollection(a.Extra);
      base_atoms.Update(b.Extra);
      return new ChannelTrack(base_atoms);
    }

  }

}
