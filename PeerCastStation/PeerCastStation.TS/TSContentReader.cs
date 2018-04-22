using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PeerCastStation.Core;

namespace PeerCastStation.TS
{
  public class TSContentReader
    : IContentReader
  {
    private static readonly Logger logger = new Logger(typeof(TSContentReader));

    public TSContentReader(Channel channel)
    {
      this.Channel = channel;
    }

    public string Name { get { return "MPEG-2 TS (TS)"; } }
    public Channel Channel { get; private set; }
    private double recvRate = 0;
    private int patID = 0;
    private int pmtID = -1;
    private int pcrPID = -1;
    private MemoryStream head = new MemoryStream();
    private MemoryStream cache = new MemoryStream();
    private class RateCounter {
      public double lastPCR   = Double.MaxValue;
      public long   byteCount = 0;
    }
    private RateCounter rateCounter = new RateCounter();

    class ProgramMapTable {
      public int PCRPID { get; private set; } = -1;
      public ProgramMapTable(TSPacket pkt, byte[] packet)
      {
        int section_length = ((packet[pkt.payload_offset+1] & 0x0F)<<8 | packet[pkt.payload_offset+2]);
        if (section_length<13) {
          PCRPID = -1;
        }
        else {
          PCRPID = ((packet[pkt.payload_offset+8] & 0x1F)<<8 | packet[pkt.payload_offset+9]);
        }
      }
    }

    public async Task ReadAsync(IContentSink sink, Stream stream, CancellationToken cancel_token)
    {
      int streamIndex = -1;
      long contentPosition = 0;
      DateTime streamOrigin = DateTime.Now;
      DateTime latestContentTime = DateTime.Now;
      byte[] bytes188 = new byte[188];
      byte[] latestHead = new byte[0];
      byte[] contentData = null;

      streamIndex = Channel.GenerateStreamID();
      streamOrigin = DateTime.Now;
      sink.OnContentHeader(new Content(streamIndex, TimeSpan.Zero, contentPosition, new byte[] {}));
      
      try
      {
        while (!cancel_token.IsCancellationRequested)
        {
          bytes188 = await stream.ReadBytesAsync(188, cancel_token).ConfigureAwait(false);
          TSPacket packet = new TSPacket(bytes188);
          if (packet.sync_byte != 0x47) throw new Exception();
          if (packet.payload_unit_start_indicator > 0)
          {
            if (packet.PID == patID)
            {
              pmtID = packet.PMTID;
              head = new MemoryStream();
              if(!addHead(bytes188)) throw new Exception();
              continue;
            }
            if (packet.PID == pmtID)
            {
              var pmt = new ProgramMapTable(packet, bytes188);
              pcrPID = pmt.PCRPID;
              if(!addHead(bytes188)) throw new Exception();
              head.Close();
              byte[] newHead = head.ToArray();
              if(!Enumerable.SequenceEqual(newHead, latestHead))
              {
                streamIndex = Channel.GenerateStreamID();
                contentPosition = 0;
                sink.OnContentHeader(new Content(streamIndex, DateTime.Now - streamOrigin, contentPosition, newHead));
                contentPosition += newHead.Length;
                latestHead = newHead;
              }
              continue;
            }
            if (packet.PID==pcrPID && packet.program_clock_reference>0.0) {
              if (packet.program_clock_reference<rateCounter.lastPCR) {
                rateCounter.lastPCR = packet.program_clock_reference;
                rateCounter.byteCount = 0;
                recvRate = 0.0;
              }
              else if (rateCounter.lastPCR+10.0<packet.program_clock_reference) {
                var bitrate = 8*rateCounter.byteCount / (packet.program_clock_reference - rateCounter.lastPCR);
                UpdateRecvRate(sink, bitrate);
                rateCounter.lastPCR = packet.program_clock_reference;
                rateCounter.byteCount = 0;
              }
            }
            if ((DateTime.Now - latestContentTime).Milliseconds > 50) {
              TryParseContent(packet, out contentData);
              if(contentData!=null) {
                sink.OnContent(new Content(streamIndex, DateTime.Now - streamOrigin, contentPosition, contentData));
                contentPosition += contentData.Length;
                latestContentTime = DateTime.Now;
              }
            }
          }
          if (!addCache(bytes188)) throw new Exception();
          rateCounter.byteCount += 188;
        }
      }
      catch (EndOfStreamException)
      { }
      catch (Exception)
      { }
    }

    private void UpdateRecvRate(IContentSink sink, double bitrate) {
      if (recvRate==0.0 || (recvRate*1.2<bitrate && bitrate<recvRate*10.0)) {
        recvRate = bitrate;

        var info = new AtomCollection(Channel.ChannelInfo.Extra);
        info.SetChanInfoType("TS");
        info.SetChanInfoStreamType("video/mp2t");
        info.SetChanInfoStreamExt(".ts");
        info.SetChanInfoBitrate((int)bitrate/1000);
        sink.OnChannelInfo(new ChannelInfo(info));
      }
    }

    private bool addHead(byte[] bytes) {
      if (head.Length < 1024 * 1024) {
        //continuity_counter = 0
        bytes[3] = (byte)(bytes[3] & 0xF0); 
        head.Write(bytes, 0, bytes.Length);
        return true;
      }
      else {
        return false;
      }
    }

    private bool addCache(byte[] bytes) {
      if (cache.Length < 8 * 1024 * 1024) {
        cache.Write(bytes, 0, bytes.Length);
        return true;
      }
      else {
        return false;
      }
    }
    
    private bool TryParseContent(TSPacket packet, out byte[] data) {
      data = null;
      if (packet.video_block || packet.audio_block) {
        cache.Close();
        data = cache.ToArray();
        cache = new MemoryStream();
        return true;
      }
      return false;
    }

  }

  public class TSContentReaderFactory
    : IContentReaderFactory
  {
    public string Name { get { return "MPEG-2 TS (TS)"; } }

    public IContentReader Create(Channel channel)
    {
      return new TSContentReader(channel);
    }

    public bool TryParseContentType(byte[] header, out string content_type, out string mime_type)
    {
      if (header.Length>=188 && header[0]==0x47) {
        content_type = "TS";
        mime_type    = "video/mp2t";
        return true;
      }
      else {
        content_type = null;
        mime_type    = null;
        return false;
      }
    }
  }

  [Plugin]
  public class TSContentReaderPlugin
    : PluginBase
  {
    override public string Name { get { return "TS Content Reader"; } }

    private TSContentReaderFactory factory;
    override protected void OnAttach()
    {
      if (factory == null) factory = new TSContentReaderFactory();
      Application.PeerCast.ContentReaderFactories.Add(factory);
    }

    override protected void OnDetach()
    {
      Application.PeerCast.ContentReaderFactories.Remove(factory);
    }
  }
}
