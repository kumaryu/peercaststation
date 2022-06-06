using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Threading;

namespace PeerCastStation.Core.Http
{
  class OwinHostOutputStream
    : OutputStreamBase
  {
    private OwinHost owinHost;
    public OwinHostOutputStream(PeerCast peercast, OwinHost host, ConnectionStream connection, AccessControlInfo access_control, Channel? channel)
      : base(peercast, connection, access_control, channel)
    {
      owinHost = host;
    }

    public override OutputStreamType OutputStreamType {
      get { return OutputStreamType.Interface; }
    }

    public override ConnectionInfo GetConnectionInfo()
    {
      return new ConnectionInfo("HTTP", ConnectionType.Interface, ConnectionStatus.Connected, RemoteEndPoint?.ToString(), RemoteEndPoint as IPEndPoint, RemoteHostStatus.None, null, null, null, null, null, null, "Owin");
    }

    protected override async Task<StopReason> DoProcess(CancellationToken cancel_token)
    {
      try {
        var keep_count = 1000;
        while (!cancel_token.IsCancellationRequested && keep_count-->0) {
          HttpRequest? req;
          using (var requestTimeout=CancellationTokenSource.CreateLinkedTokenSource(cancel_token))
          using (var reader=new HttpRequestReader(Connection, true)) {
            requestTimeout.CancelAfter(7000);
            req = await reader.ReadAsync(requestTimeout.Token).ConfigureAwait(false);
            if (req==null) {
              return StopReason.OffAir;
            }
          }
          IPEndPoint localEndPoint = LocalEndPoint as IPEndPoint ?? new IPEndPoint(IPAddress.Any, 0);
          IPEndPoint remoteEndPoint = RemoteEndPoint as IPEndPoint ?? new IPEndPoint(IPAddress.Any, 0);
          var ctx = new OwinContext(PeerCast, req, Connection, localEndPoint, remoteEndPoint, AccessControlInfo);
          try {
            await ctx.Invoke(owinHost.OwinApp, cancel_token).ConfigureAwait(false);
          }
          catch (Exception ex) {
            Logger.Error(ex);
            return StopReason.NotIdentifiedError;
          }
          if (!ctx.IsKeepAlive) {
            break;
          }
        }
      }
      catch (OperationCanceledException) {
      }
      return StopReason.OffAir;
    }
  }

  public class OwinHostOutputStreamFactory
    : OutputStreamFactoryBase
  {
    public override string Name { get { return nameof(OwinHostOutputStreamFactory); } }

    public override int Priority { get { return 10; } }

    public override OutputStreamType OutputStreamType {
      get { throw new NotSupportedException(); }
    }

    private OwinHost owinHost;

    public OwinHostOutputStreamFactory(PeerCast peerCast, OwinHost host)
      : base(peerCast)
    {
      owinHost = host;
    }

    public override IOutputStream Create(ConnectionStream connection, AccessControlInfo access_control, Guid channel_id)
    {
      var channel = channel_id!=Guid.Empty ? PeerCast.Channels.FirstOrDefault(c => c.ChannelID==channel_id) : null;
      return new OwinHostOutputStream(PeerCast, owinHost, connection, access_control, channel);
    }

    public override Guid? ParseChannelID(byte[] header, AccessControlInfo acinfo)
    {
      var idx = Array.IndexOf(header, (byte)'\r');
      if (idx<0 ||
          idx==header.Length-1 ||
          header[idx+1]!='\n') {
        return null;
      }
      try {
        var reqline = HttpRequest.ParseRequestLine(System.Text.Encoding.ASCII.GetString(header, 0, idx));
        if (reqline!=null) {
          return Guid.Empty;
        }
        else {
          return null;
        }
      }
      catch (ArgumentException) {
        return null;
      }
    }

    public override Guid? ParseChannelID(byte[] header)
    {
      throw new NotSupportedException();
    }

  }

  [Plugin(PluginType.Protocol, PluginPriority.Higher)]
  public class OwinHostPlugin
    : PluginBase
  {
    private Logger logger = new Logger(typeof(OwinHostPlugin));
    public OwinHost? OwinHost { get; private set; } = null;
    private OwinHostOutputStreamFactory? factory = null;

    public override string Name {
      get { return nameof(OwinHostPlugin); }
    }

    protected override void OnAttach(PeerCastApplication app)
    {
      var peercast = app.PeerCast;
      OwinHost = new OwinHost(app, peercast);
      if (factory==null) {
        factory = new OwinHostOutputStreamFactory(peercast, OwinHost);
      }
      peercast.OutputStreamFactories.Add(factory);
    }

    protected override void OnDetach(PeerCastApplication app)
    {
      OwinHost = null;
      if (factory!=null) {
        app.PeerCast.OutputStreamFactories.Remove(factory);
      }
    }

    protected override void OnStart()
    {
    }

    protected override void OnStop()
    {
      if (OwinHost!=null) {
        OwinHost.Dispose();
      }
    }
  }

}

