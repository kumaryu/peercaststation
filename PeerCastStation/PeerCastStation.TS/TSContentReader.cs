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
    private float? recvRate = 0;
    private int patID = 0;
    private int pmtID = -1;
    private MemoryStream head = new MemoryStream();
    private MemoryStream cache = new MemoryStream();
    
    public async Task ReadAsync(IContentSink sink, Stream stream, CancellationToken cancel_token)
    {
      int streamIndex = -1;
      DateTime streamOrigin = DateTime.Now;
      DateTime latestContentTime = DateTime.Now;
      byte[] bytes188 = new byte[188];
      byte[] latestHead = new byte[0];
      byte[] contentData = null;

      streamIndex = Channel.GenerateStreamID();
      streamOrigin = DateTime.Now;
      sink.OnContentHeader(new Content(streamIndex, TimeSpan.Zero, Channel.ContentPosition, new byte[] {}));
      
      try
      {
        while (!cancel_token.IsCancellationRequested)
        {
          bytes188 = ReadBytes(stream, 188);
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
              if(!addHead(bytes188)) throw new Exception();
              head.Close();
              byte[] newHead = head.ToArray();
              if(!Enumerable.SequenceEqual(newHead, latestHead))
              {
                sink.OnContentHeader(new Content(streamIndex, DateTime.Now - streamOrigin, Channel.ContentPosition, newHead));
                latestHead = newHead;                  
              }
              continue;
            }
            if ((DateTime.Now - latestContentTime).Milliseconds > 50) {
              TryParseContent(packet, out contentData);
              if(contentData!=null) {
                sink.OnContent(new Content(streamIndex, DateTime.Now - streamOrigin, Channel.ContentPosition, contentData));
                latestContentTime = DateTime.Now;
                UpdateRecvRate(sink);
              }
            }
          }
          if (!addCache(bytes188)) throw new Exception();
        }
      }
      catch (EndOfStreamException)
      { }
      catch (Exception)
      { }
    }

    private void UpdateRecvRate(IContentSink sink) {
      var bitrate = (int)((Channel.SourceStream.GetConnectionInfo().RecvRate ?? 0)*8/1000);

      if(recvRate*1.2 < bitrate) {
        recvRate = bitrate;

        var info = new AtomCollection(Channel.ChannelInfo.Extra);
        info.SetChanInfoType("TS");
        info.SetChanInfoStreamType("video/mp2t");
        info.SetChanInfoStreamExt(".ts");
        info.SetChanInfoBitrate((int)bitrate);
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
    
    private byte[] ReadBytes(Stream stream, int len)
    {
      var bytes = stream.ReadBytes(len);
      if (bytes.Length < len) throw new EndOfStreamException();
      return bytes;
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
