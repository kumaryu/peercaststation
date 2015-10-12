using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.Core
{
  /// <summary>
  /// 読み取ったデータをそのままコンテントとして流すクラスです
  /// </summary>
  public class RawContentReader
    : IContentReader
  {
    public RawContentReader(Channel channel)
    {
      this.Channel = channel;
    }

    private int streamIndex = -1;
    private DateTime streamOrigin;
    public ParsedContent Read(Stream stream)
    {
      if (stream.Length-stream.Position<=0) throw new EndOfStreamException();
      var res = new ParsedContent();
      var pos = Channel.ContentPosition;
      if (Channel.ContentHeader==null) {
        streamIndex = Channel.GenerateStreamID();
        streamOrigin = DateTime.Now;
        res.ContentHeader = new Content(streamIndex, TimeSpan.Zero, pos, new byte[] { });
        var channel_info = new AtomCollection(Channel.ChannelInfo.Extra);
        channel_info.SetChanInfoType("RAW");
        channel_info.SetChanInfoStreamType("application/octet-stream");
        channel_info.SetChanInfoStreamExt("");
        res.ChannelInfo = new ChannelInfo(channel_info);
      }
      res.Contents = new List<Content>();
      while (stream.Length-stream.Position>0) {
        var bytes = new byte[Math.Min(8192, stream.Length-stream.Position)];
        var sz = stream.Read(bytes, 0, bytes.Length);
        if (sz>0) {
          Array.Resize(ref bytes, sz);
          res.Contents.Add(new Content(streamIndex, DateTime.Now-streamOrigin, pos, bytes));
          pos += sz;
        }
      }
      return res;
    }

    public Task<ParsedContent> ReadAsync(Stream stream, CancellationToken cancel_token)
    {
      throw new NotImplementedException();
    }

    public string  Name    { get { return "RAW"; } }
    public Channel Channel { get; private set; }
  }

  /// <summary>
  /// 読み取ったデータをそのままコンテントとして流すRawContentReaderのファクトリクラスです
  /// </summary>
  public class RawContentReaderFactory
    : IContentReaderFactory
  {
    public string Name
    {
      get { return "RAW"; }
    }

    public IContentReader Create(Channel channel)
    {
      return new RawContentReader(channel);
    }

    public bool TryParseContentType(byte[] header, out string content_type, out string mime_type)
    {
      content_type = null;
      mime_type = null;
      return false;
    }
  }

  [Plugin(PluginPriority.Lower)]
  public class RawContentReaderPlugin
    : PluginBase
  {
    override public string Name { get { return "Raw Content Reader"; } }

    private RawContentReaderFactory factory;
    override protected void OnAttach()
    {
      if (factory==null) factory = new RawContentReaderFactory();
      Application.PeerCast.ContentReaderFactories.Add(factory);
    }

    override protected void OnDetach()
    {
      Application.PeerCast.ContentReaderFactories.Remove(factory);
    }
  }
}
