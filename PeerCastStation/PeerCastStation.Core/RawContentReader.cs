using System;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;

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

    public async Task ReadAsync(IContentSink sink, Stream stream, CancellationToken cancel_token)
    {
      long pos = 0;
      var streamIndex = Channel.GenerateStreamID();
      var streamOrigin = DateTime.Now;
      sink.OnContentHeader(new Content(streamIndex, TimeSpan.Zero, pos, new byte[] { }, PCPChanPacketContinuation.None));
      var channel_info = new AtomCollection(Channel.ChannelInfo.Extra);
      channel_info.SetChanInfoType("RAW");
      channel_info.SetChanInfoStreamType("application/octet-stream");
      channel_info.SetChanInfoStreamExt("");
      sink.OnChannelInfo(new ChannelInfo(channel_info));

      bool eof = false;
      do {
        var buf = new byte[8192];
        var sz = await stream.ReadAsync(buf, 0, buf.Length, cancel_token).ConfigureAwait(false);
        if (sz>0) {
          sink.OnContent(new Content(streamIndex, DateTime.Now-streamOrigin, pos, buf.Take(sz).ToArray(), PCPChanPacketContinuation.None));
          pos += sz;
        }
        else {
          eof = true;
        }
      } while (!eof);
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

    public bool TryParseContentType(byte[] header, [NotNullWhen(true)] out string? content_type, [NotNullWhen(true)] out string? mime_type)
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

    private RawContentReaderFactory? factory = null;
    override protected void OnAttach(PeerCastApplication app)
    {
      if (factory==null) factory = new RawContentReaderFactory();
      app.PeerCast.ContentReaderFactories.Add(factory);
    }

    override protected void OnDetach(PeerCastApplication app)
    {
      if (factory!=null) {
        app.PeerCast.ContentReaderFactories.Remove(factory);
      }
    }
  }
}
