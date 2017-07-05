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

    protected override ISourceStream CreateSourceStream(Uri source_uri)
    {
      var source_factory = PeerCast.SourceStreamFactories
        .Where(factory => (factory.Type & SourceStreamType.Relay)!=0)
        .FirstOrDefault(factory => source_uri.Scheme==factory.Scheme);
      if (source_factory==null) {
        logger.Error("Protocol `{0}' is not found", source_uri.Scheme);
        throw new ArgumentException(String.Format("Protocol `{0}' is not found", source_uri.Scheme));
      }
      return source_factory.Create(this, source_uri);
    }

  }

}
