using System;
using System.Linq;
using System.Net;

namespace PeerCastStation.Core
{
  public class BroadcastChannel
    : Channel
  {
    public override bool IsBroadcasting { get { return true; } }
    public override EndPoint? TrackerEndPoint {
      get {
        return
          PeerCast.GetGlobalEndPoint(Network, OutputStreamType.Relay) ??
          PeerCast.GetLocalEndPoint(Network, OutputStreamType.Relay);
      }
    }

    public IContentReaderFactory ContentReaderFactory { get; private set; }

    public BroadcastChannel(
        IPeerCast peercast,
        NetworkType network,
        Guid channel_id,
        ChannelInfo channel_info,
        ChannelTrack channel_track,
        IContentReaderFactory content_reader_factory)
      : base(peercast, network, channel_id)
    {
      this.ChannelInfo = channel_info;
      this.ChannelTrack = channel_track;
      this.ContentReaderFactory = content_reader_factory;
    }

    protected override ISourceStream CreateSourceStream(ISourceStreamFactory source_stream_factory, Uri source_uri)
    {
      var content_reader = ContentReaderFactory.Create(this);
      return source_stream_factory.Create(this, source_uri, content_reader);
    }

    static public Guid CreateChannelID(Guid bcid, NetworkType network, string channel_name, string genre, string source)
    {
      var stream = new System.IO.MemoryStream();
      using (var writer = new System.IO.BinaryWriter(stream)) {
        var bcid_hash = System.Security.Cryptography.SHA512.Create().ComputeHash(bcid.ToByteArray());
        if (network!=NetworkType.IPv4) {
          writer.Write((int)network);
        }
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
