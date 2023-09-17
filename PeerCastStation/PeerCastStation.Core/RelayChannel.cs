using System;
using System.Linq;

namespace PeerCastStation.Core
{
  public class RelayChannel
    : Channel
  {
    public override bool IsBroadcasting { get { return false; } }

    public RelayChannel(IPeerCast peercast, NetworkType network, Guid channel_id)
      : base(peercast, network, channel_id)
    {
    }

    protected override ISourceStream CreateSourceStream(ISourceStreamFactory source_stream_factory, Uri source_uri)
    {
      return source_stream_factory.Create(this, source_uri);
    }

  }

}
