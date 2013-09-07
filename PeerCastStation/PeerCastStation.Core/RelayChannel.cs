using System;
using System.Linq;

namespace PeerCastStation.Core
{
  public class RelayChannel
    : Channel
  {
    public override bool IsBroadcasting { get { return false; } }

    public RelayChannel(PeerCast peercast, Guid channel_id)
      : base(peercast, channel_id)
    {
    }

    public override void Start(Uri source_uri)
    {
      var source_factory = PeerCast.SourceStreamFactories.FirstOrDefault(factory => source_uri.Scheme==factory.Scheme);
      if (source_factory==null) {
        logger.Error("Protocol `{0}' is not found", source_uri.Scheme);
        throw new ArgumentException(String.Format("Protocol `{0}' is not found", source_uri.Scheme));
      }
      var source_stream = source_factory.Create(this, source_uri);
      this.Start(source_uri, source_stream);
    }

  }

}
