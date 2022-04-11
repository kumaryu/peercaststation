
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PeerCastStation.Core
{
  public class LoopbackSourceConnection
    : ISourceConnection,
      IContentSink
  {
    public LoopbackSourceConnection(
        PeerCast peercast,
        Channel channel,
        Uri source_uri,
        Channel? source_channel)
    {
      PeerCast = peercast;
      Channel = channel;
      SourceUri = source_uri;
      SourceChannel = source_channel;
      if (SourceChannel!=null) {
        channelContentSink = new ChannelContentSink(Channel, true);
      }
    }

    private TaskCompletionSource<StopReason> taskSource = new TaskCompletionSource<StopReason>();
    private IContentSink? channelContentSink = null;

    public PeerCast PeerCast { get; private set; }
    public Channel Channel { get; private set; }
    public Uri SourceUri { get; private set; }
    public Channel? SourceChannel { get; private set; }

    public StopReason StoppedReason { get; private set; }

    public bool IsStopped {
      get { return StoppedReason!=StopReason.None; }
    }

    public float SendRate {
      get {
        return SourceChannel?.SourceStream?.GetConnectionInfo()?.SendRate ?? 0.0f;
      }
    }
    public float RecvRate {
      get {
        return SourceChannel?.SourceStream?.GetConnectionInfo()?.RecvRate ?? 0.0f;
      }
    }

    public ConnectionInfo GetConnectionInfo()
    {
      var source_connection_info = SourceChannel?.SourceStream?.GetConnectionInfo();
      ConnectionStatus status;
      switch (StoppedReason) {
      case StopReason.None:
        if (SourceChannel!=null) {
          status = ConnectionStatus.Connected;
        }
        else {
          status = ConnectionStatus.Error;
        }
        break;
      case StopReason.OffAir:
      case StopReason.UserReconnect:
      case StopReason.UserShutdown:
        status = ConnectionStatus.Idle;
        break;
      default:
        status = ConnectionStatus.Error;
        break;
      }
      return new ConnectionInfoBuilder {
        ProtocolName     = "Loopback Source",
        Type             = ConnectionType.Source,
        Status           = status,
        RemoteName       = SourceUri.ToString(),
        RemoteEndPoint   = null,
        RemoteHostStatus = RemoteHostStatus.Local,
        ContentPosition  = SourceChannel?.ContentPosition ?? 0,
        RecvRate         = source_connection_info?.RecvRate ?? 0.0f,
        SendRate         = source_connection_info?.SendRate ?? 0.0f,
        AgentName        = "",
      }.Build();
    }

    public void Post(Host from, Atom packet)
    {
    }

    public void OnChannelInfo(ChannelInfo channel_info)
    {
      var new_channel_info = new AtomCollection(channel_info.Extra);
      if (Channel.ChannelInfo!=null) {
        if (Channel.ChannelInfo.Name!=null) {
          new_channel_info.SetChanInfoName(Channel.ChannelInfo.Name);
        }
        if (Channel.ChannelInfo.Genre!=null) {
          new_channel_info.SetChanInfoGenre(Channel.ChannelInfo.Genre);
        }
      }
      channelContentSink?.OnChannelInfo(new ChannelInfo(new_channel_info));
    }

    public void OnChannelTrack(ChannelTrack channel_track)
    {
      channelContentSink?.OnChannelTrack(channel_track);
    }

    public void OnContent(Content content)
    {
      channelContentSink?.OnContent(content);
    }

    public void OnContentHeader(Content content_header)
    {
      channelContentSink?.OnContentHeader(content_header);
    }

    public void OnStop(StopReason reason)
    {
      channelContentSink?.OnStop(reason);
      taskSource.TrySetResult(reason);
    }

    public Task<StopReason> Run()
    {
      if (SourceChannel!=null) {
        SourceChannel.AddContentSink(this);
        return taskSource.Task.ContinueWith(prev => {
          SourceChannel.RemoveContentSink(this);
          StoppedReason = prev.Result;
          return prev.Result;
        });
      }
      else {
        StoppedReason = StopReason.NoHost;
        taskSource.TrySetResult(StopReason.NoHost);
        return taskSource.Task;
      }
    }

    public void Stop(StopReason reason)
    {
      taskSource.TrySetResult(reason);
    }
  }

  public class LoopbackSourceStream
    : SourceStreamBase
  {
    public LoopbackSourceStream(PeerCast peercast, Channel channel, Uri source_uri)
      : base(peercast, channel, source_uri)
    {
    }

    public override SourceStreamType Type {
      get { return SourceStreamType.Broadcast; }
    }

    public override ConnectionInfo GetConnectionInfo()
    {
      return
        sourceConnection.GetConnectionInfo() ??
        new ConnectionInfoBuilder {
          ProtocolName     = "Loopback Source",
          Type             = ConnectionType.Source,
          Status           = ConnectionStatus.Idle,
          RemoteName       = SourceUri.ToString(),
          RemoteEndPoint   = null,
          RemoteHostStatus = RemoteHostStatus.Local,
          ContentPosition  = 0,
          RecvRate         = RecvRate,
          SendRate         = SendRate,
          AgentName        = "",
        }.Build();
    }

    private Channel? GetSourceChannel(Uri source_uri)
    {
      var md = System.Text.RegularExpressions.Regex.Match(source_uri.AbsolutePath, @"([0-9a-zA-Z]{32})");
      if (!md.Success) return null;
      var channel_id = Guid.Parse(md.Groups[1].Value);
      return PeerCast.Channels.FirstOrDefault(c => c.ChannelID==channel_id);
    }

    protected override ISourceConnection CreateConnection(Uri source_uri)
    {
      return new LoopbackSourceConnection(PeerCast, Channel, source_uri, GetSourceChannel(source_uri));
    }

  }

  public class LoopbackSourceStreamFactory
    : SourceStreamFactoryBase
  {
    public LoopbackSourceStreamFactory(PeerCast peercast)
      : base(peercast)
    {
    }

    public override string Name {
      get { return "他のチャンネル"; }
    }

    public override string Scheme {
      get { return "loopback"; }
    }

    public override SourceStreamType Type {
      get { return SourceStreamType.Broadcast; }
    }

    public override Uri DefaultUri {
      get { return new Uri("loopback:00000000000000000000000000000000"); }
    }

    public override bool IsContentReaderRequired {
      get { return false; }
    }

    public override ISourceStream Create(Channel channel, Uri source, IContentReader reader)
    {
      return new LoopbackSourceStream(PeerCast, channel, source);
    }

  }

  [Plugin]
  class LoopbackSourceStreamPlugin
    : PluginBase
  {
    override public string Name { get { return "Loopback Source"; } }

    private LoopbackSourceStreamFactory? factory = null;
    override protected void OnAttach(PeerCastApplication app)
    {
      if (factory==null) {
        factory = new LoopbackSourceStreamFactory(app.PeerCast);
      }
      app.PeerCast.SourceStreamFactories.Add(factory);
    }

    override protected void OnDetach(PeerCastApplication app)
    {
      if (factory!=null) {
        app.PeerCast.SourceStreamFactories.Remove(factory);
      }
    }
  }

}
