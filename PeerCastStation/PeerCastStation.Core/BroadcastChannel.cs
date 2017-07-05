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

    protected override ISourceStream CreateSourceStream(Uri source_uri)
    {
      var source_factory = this.SourceStreamFactory;
      if (source_factory==null) {
        source_factory = PeerCast.SourceStreamFactories.FirstOrDefault(factory => source_uri.Scheme==factory.Scheme);
        if (source_factory==null) {
          logger.Error("Protocol `{0}' is not found", source_uri.Scheme);
          throw new ArgumentException(String.Format("Protocol `{0}' is not found", source_uri.Scheme));
        }
      }
      var content_reader = ContentReaderFactory.Create(this);
      return source_factory.Create(this, source_uri, content_reader);
    }

    static public Guid CreateChannelID(Guid bcid, string channel_name, string genre, string source)
    {
      var stream = new System.IO.MemoryStream();
      using (var writer = new System.IO.BinaryWriter(stream)) {
        var bcid_hash = System.Security.Cryptography.SHA512.Create().ComputeHash(bcid.ToByteArray());
        writer.Write(bcid_hash);
        writer.Write(channel_name);
        writer.Write(genre);
        writer.Write(source);
      }
      var channel_hash = System.Security.Cryptography.MD5.Create().ComputeHash(stream.ToArray());
      return new Guid(channel_hash);
    }

  }

}
