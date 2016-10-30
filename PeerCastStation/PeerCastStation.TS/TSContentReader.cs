using System;
using System.IO;
using PeerCastStation.Core;
using System.Threading;
using System.Threading.Tasks;

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

    public string Name { get { return "MPEG-TS (TS)"; } }
    public Channel Channel { get; private set; }
    private float? recvRate = 0;
    private MemoryStream cache = new MemoryStream();
    
    public async Task ReadAsync(IContentSink sink, Stream stream, CancellationToken cancel_token)
    {
      int streamIndex = -1;
      DateTime streamOrigin = DateTime.Now;

      streamIndex = Channel.GenerateStreamID();
      streamOrigin = DateTime.Now;
      sink.OnContentHeader(new Content(streamIndex, TimeSpan.Zero, Channel.ContentPosition, new byte[] {}));
      
      var eof = false;
      try
      {
        while (!eof)
        {
          byte[] bytes = null;
          TSPacket packet = null;
          try
          {
            bytes = await ReadBytesAsync(stream, cancel_token);
          }
          catch (EndOfStreamException)
          {
            eof = true;
            continue;
          }

          for (int i = 0; i < bytes.Length/188; i++)
          {
            byte[] bytes188 = new byte[188];
            Array.Copy(bytes, 188 * i, bytes188, 0, 188);
            packet = new TSPacket(bytes188);

            if (packet.sync_byte != 0x47) throw new Exception();
            if (packet.payload_unit_start_indicator > 0)
            {
              byte[] contentData;
              TryParseContent(packet, out contentData);
              if(contentData!=null) {
                sink.OnContent(new Content(streamIndex, DateTime.Now - streamOrigin, Channel.ContentPosition, contentData));
                UpdateRecvRate(sink);
              }
            }
            if (!addCache(bytes188)) throw new Exception();
          }
        }
      }
      catch (Exception) {
      }
    }

    private void UpdateRecvRate(IContentSink sink) {
      var bitrate = (int)((Channel.SourceStream.GetConnectionInfo().RecvRate ?? 0)*8/1000);

      if(recvRate*1.2 < bitrate) {
        recvRate = bitrate;

        var info = new AtomCollection(Channel.ChannelInfo.Extra);
        info.SetChanInfoType("TS");
        info.SetChanInfoStreamType("video/mp2ts");
        info.SetChanInfoStreamExt(".ts");
        info.SetChanInfoBitrate((int)bitrate);
        sink.OnChannelInfo(new ChannelInfo(info));
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
      if (cache.Length < 7144) return false;
      if (packet.video_block) {
        cache.Close();
        data = cache.ToArray();
        cache = new MemoryStream();
        return true;
      }
      return false;
    }
    
    private async Task<byte[]> ReadBytesAsync(Stream stream, CancellationToken cancel_token)
    {
      var bytes = await stream.ReadBytesAsync(188*10, cancel_token);
      if (bytes.Length < 188*10) throw new EndOfStreamException();
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
        mime_type    = "video/mp2ts";
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
