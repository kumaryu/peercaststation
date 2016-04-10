using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace PeerCastStation.Core
{
  public interface ISourceConnection
  {
    Uri        SourceUri     { get; }
    StopReason StoppedReason { get; }
    bool       IsStopped     { get; }
    float      SendRate      { get; }
    float      RecvRate      { get; }

    ConnectionInfo GetConnectionInfo();
    Task<StopReason> Run();
    void Post(Host from, Atom packet);
    void Stop(StopReason reason);
  }

  public class ChannelContentSink
    : IContentSink
  {
    public Channel Channel     { get; private set; }
    public Content LastContent { get; private set; }
    public bool    UseContentBitrate { get; private set; }
    public ChannelContentSink(Channel channel, bool use_content_bitrate)
    {
      this.Channel = channel;
      this.LastContent = null;
      this.UseContentBitrate = use_content_bitrate;
    }

    public void OnChannelInfo(ChannelInfo channel_info)
    {
      this.Channel.ChannelInfo = MergeChannelInfo(Channel.ChannelInfo, channel_info);
    }

    public void OnChannelTrack(ChannelTrack channel_track)
    {
      this.Channel.ChannelTrack = MergeChannelTrack(Channel.ChannelTrack, channel_track);
    }

    public void OnContent(Content content)
    {
      this.Channel.Contents.Add(content);
      this.LastContent = content;
    }

    public void OnContentHeader(Content content_header)
    {
      this.Channel.ContentHeader = content_header;
      this.Channel.Contents.Clear();
      this.LastContent = content_header;
    }

    private ChannelInfo MergeChannelInfo(ChannelInfo a, ChannelInfo b)
    {
      var base_atoms = new AtomCollection(a.Extra);
      var new_atoms  = new AtomCollection(b.Extra);
      if (!UseContentBitrate) {
        new_atoms.RemoveByName(Atom.PCP_CHAN_INFO_BITRATE);
      }
      base_atoms.Update(new_atoms);
      return new ChannelInfo(base_atoms);
    }

    private ChannelTrack MergeChannelTrack(ChannelTrack a, ChannelTrack b)
    {
      var base_atoms = new AtomCollection(a.Extra);
      base_atoms.Update(b.Extra);
      return new ChannelTrack(base_atoms);
    }

  }

  public abstract class SourceConnectionBase
    : ISourceConnection
  {
    protected ConnectionStatus Status { get; set; }

    public PeerCast   PeerCast { get; private set; }
    public Channel    Channel { get; private set; }
    public Uri        SourceUri { get; private set; }
    public StopReason StoppedReason { get; private set; }
    public float      SendRate { get { return connection!=null ? connection.Stream.WriteRate : 0; } }
    public float      RecvRate { get { return connection!=null ? connection.Stream.ReadRate  : 0; } }

    private CancellationTokenSource isStopped = new CancellationTokenSource();
    public bool IsStopped {
      get { return isStopped.IsCancellationRequested; }
    }
    protected CancellationToken StoppedCancellationToken {
      get { return isStopped.Token; }
    }

    protected Logger Logger { get; private set; }

    protected class SourceConnectionClient
      : IDisposable
    {
      public TcpClient Client { get; private set; }
      public ConnectionStream Stream { get; private set; }
      public SourceConnectionClient(TcpClient client)
      {
        this.Client = client;
        var stream = client.GetStream();
        this.Stream = new ConnectionStream(stream, stream);
      }

      private IPEndPoint remoteEndPoint = null;
      public IPEndPoint RemoteEndPoint {
        get {
          if (remoteEndPoint!=null) {
            return remoteEndPoint;
          }
          else if (this.Client.Connected) {
            remoteEndPoint = this.Client.Client.RemoteEndPoint as IPEndPoint;
            return remoteEndPoint;
          }
          else {
            return null;
          }
        }
      }

      public void Dispose()
      {
        remoteEndPoint = null;
        this.Stream.Close();
        this.Client.Close();
      }
    }
    protected SourceConnectionClient connection;

    public SourceConnectionBase(
        PeerCast peercast,
        Channel  channel,
        Uri      source_uri)
    {
      this.PeerCast      = peercast;
      this.Channel       = channel;
      this.SourceUri     = source_uri;
      this.StoppedReason = StopReason.None;
      this.Logger        = new Logger(this.GetType());
      this.Status        = ConnectionStatus.Idle;
    }

    protected virtual void OnStarted()
    {
    }

    protected virtual void OnStopped()
    {
    }

    public async Task<StopReason> Run()
    {
      this.Status = ConnectionStatus.Connecting;
      try {
        connection = await DoConnect(SourceUri, isStopped.Token);
      }
      catch (OperationCanceledException) {
        connection = null;
      }
      if (connection==null) {
        Stop(StopReason.ConnectionError);
      }
      if (!IsStopped) {
        OnStarted();
        try {
          await DoProcess(isStopped.Token);
        }
        catch (OperationCanceledException) {
        }
        OnStopped();
      }
      if (connection!=null) {
        await DoClose(connection);
      }
      return StoppedReason;
    }

    public void Post(Host from, Atom packet)
    {
      if (IsStopped) return;
      DoPost(from, packet);
    }

    public void Stop()
    {
      Stop(StopReason.UserShutdown);
    }

    public virtual void Stop(StopReason reason)
    {
      if (reason==StopReason.None) throw new ArgumentException("Invalid value", "reason");
      if (IsStopped) return;
      StoppedReason = reason;
      isStopped.Cancel();
    }

    protected abstract Task<SourceConnectionClient> DoConnect(Uri source, CancellationToken cancellationToken);

    protected virtual async Task DoClose(SourceConnectionClient connection)
    {
      await connection.Stream.FlushAsync();
      connection.Dispose();
      Logger.Debug("closed");
      this.Status = ConnectionStatus.Error;
    }

    protected virtual void DoPost(Host from, Atom packet)
    {
    }

    protected abstract Task DoProcess(CancellationToken cancellationToken);

    public abstract ConnectionInfo GetConnectionInfo();
  }

}
