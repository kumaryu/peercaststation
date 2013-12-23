using System;
using System.Linq;

namespace PeerCastStation.Core
{
  public class BroadcastChannel
    : Channel
  {
    public override bool IsBroadcasting { get { return true; } }
    public ISourceStreamFactory SourceStreamFactory { get; private set; }
    public IContentReaderFactory ContentReaderFactory { get; private set; }

    public BroadcastChannel(
        PeerCast peercast,
        Guid channel_id,
        ChannelInfo channel_info,
        ISourceStreamFactory source_stream_factory,
        IContentReaderFactory content_reader_factory)
      : base(peercast, channel_id)
    {
      this.ChannelInfo = channel_info;
      this.SourceStreamFactory = source_stream_factory;
      this.ContentReaderFactory = content_reader_factory;
    }

    public override void Start(Uri source_uri)
    {
      var source_factory = PeerCast.SourceStreamFactories.FirstOrDefault(factory => source_uri.Scheme==factory.Scheme);
      if (source_factory==null) {
        logger.Error("Protocol `{0}' is not found", source_uri.Scheme);
        throw new ArgumentException(String.Format("Protocol `{0}' is not found", source_uri.Scheme));
      }
      var content_reader = ContentReaderFactory.Create(this);
      var source_stream = source_factory.Create(this, source_uri, content_reader);
      this.Start(source_uri, source_stream);
    }

  }

}
