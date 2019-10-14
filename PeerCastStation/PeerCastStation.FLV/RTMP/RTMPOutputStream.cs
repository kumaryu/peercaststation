using System;
using System.Linq;
using System.Threading.Tasks;
using PeerCastStation.Core;
using System.Threading;

namespace PeerCastStation.FLV.RTMP
{
  public class RTMPOutputStream
    : IOutputStream
  {
    private PeerCast peerCast;
    private ConnectionStream inputStream;
    private ConnectionStream outputStream;
    private AccessControlInfo accessControl;
    private RTMPPlayConnection connection;
    private CancellationTokenSource isStopped = new CancellationTokenSource();
    private Channel channel;

    public RTMPOutputStream(
        PeerCast peercast,
        ConnectionStream connection,
        AccessControlInfo access_control,
        Guid channel_id)
    {
      connection.ReadTimeout = Timeout.Infinite;
      connection.WriteTimeout = 10000;
      this.peerCast       = peercast;
      this.inputStream    = connection;
      this.outputStream   = connection;
      this.accessControl  = access_control;
      this.connection = new RTMPPlayConnection(this, this.inputStream, this.outputStream);
    }

    public ConnectionInfo GetConnectionInfo()
    {
      return new ConnectionInfoBuilder {
        ProtocolName     = "RTMP Output",
        Type             = ConnectionType.Direct,
        Status           = ConnectionStatus.Connected,
        RemoteName       = inputStream.RemoteEndPoint.ToString(),
        RemoteEndPoint   = inputStream.RemoteEndPoint as System.Net.IPEndPoint,
        RemoteHostStatus = RemoteHostStatus.Receiving | (IsLocal ? RemoteHostStatus.Local : RemoteHostStatus.None),
        ContentPosition  = connection.ContentPosition,
        RecvRate         = (float)this.inputStream.ReadRate,
        SendRate         = (float)this.outputStream.WriteRate,
        AgentName        = connection.ClientName
      }.Build();
    }

    public OutputStreamType OutputStreamType {
      get { return Core.OutputStreamType.Play; }
    }

    public Channel RequestChannel(Guid channel_id, Uri tracker_uri)
    {
      var channel = peerCast.RequestChannel(channel_id, tracker_uri, true);
      this.channel = channel;
      if (channel!=null && channel.IsPlayable(IsLocal)) {
        channel.AddOutputStream(this);
        return channel;
      }
      else {
        return null;
      }
    }

    public bool IsLocal {
      get {
        var endpoint = inputStream.RemoteEndPoint as System.Net.IPEndPoint;
        if (endpoint==null) return false;
        return endpoint.Address.IsSiteLocal();
      }
    }

    public int UpstreamRate {
      get {
        if (IsLocal || channel==null) return 0;
        return channel.ChannelInfo.Bitrate;
      }
    }

    public async Task<HandlerResult> Start(CancellationToken cancellationToken)
    {
      using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, isStopped.Token)) {
        try {
          await connection.Run(cts.Token).ConfigureAwait(false);
        }
        finally {
          this.channel?.RemoveOutputStream(this);
        }
        return HandlerResult.Close;
      }
    }

    public void OnBroadcast(Host from, Atom packet)
    {
    }

    private StopReason stopReason = StopReason.None;
    public void OnStopped(StopReason reason)
    {
      stopReason = reason;
      isStopped.Cancel();
    }

    public bool CheckAuthotization(string auth)
    {
      if (!accessControl.AuthorizationRequired || accessControl.AuthenticationKey==null) return true;
      if (auth==null) return false;
      var authorized = false;
      try {
        var authorization = System.Text.Encoding.ASCII.GetString(Convert.FromBase64String(auth)).Split(':');
        if (authorization.Length>=2) {
          var user = authorization[0];
          var pass = String.Join(":", authorization.Skip(1).ToArray());
          authorized = accessControl.CheckAuthorization(user, pass);
        }
      }
      catch (FormatException) {
      }
      catch (ArgumentException) {
      }
      return authorized;
    }

  }

  public class RTMPOutputStreamFactory
    : OutputStreamFactoryBase
  {
    public RTMPOutputStreamFactory(PeerCast peercast)
      : base(peercast)
    {
    }

    public override string Name
    {
      get { return "RTMP Output"; }
    }

    public override OutputStreamType OutputStreamType
    {
      get { return Core.OutputStreamType.Play; }
    }

    public override IOutputStream Create(
      ConnectionStream connection,
      AccessControlInfo access_control,
      Guid channel_id)
    {
      return new RTMPOutputStream(
        PeerCast,
        connection,
        access_control,
        channel_id
      );
    }

    public override Guid? ParseChannelID(byte[] header)
    {
      if (header.Length>0 && header[0]==0x03) {
        return Guid.Empty;
      }
      else {
        return null;
      }
    }
  }

	[Plugin]
	public class RTMPOutputStreamPlugin
		: PluginBase
	{
		override public string Name { get { return "RTMP Output Stream"; } }

		private RTMPOutputStreamFactory factory;
		override protected void OnAttach()
		{
			if (factory==null) factory = new RTMPOutputStreamFactory(Application.PeerCast);
			Application.PeerCast.OutputStreamFactories.Add(factory);
		}

		override protected void OnDetach()
		{
			Application.PeerCast.OutputStreamFactories.Remove(factory);
		}
	}
}

