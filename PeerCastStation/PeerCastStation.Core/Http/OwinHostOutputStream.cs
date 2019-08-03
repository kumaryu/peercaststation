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
    public OwinHostOutputStream(PeerCast peercast, OwinHost host, Stream input_stream, Stream output_stream, EndPoint local_endpoint, EndPoint remote_endpoint, AccessControlInfo access_control, Channel channel, byte[] header)
      : base(peercast, input_stream, output_stream, local_endpoint, remote_endpoint, access_control, channel, header)
    {
      owinHost = host;
    }

    public override OutputStreamType OutputStreamType {
      get { return OutputStreamType.Interface; }
    }

    public override ConnectionInfo GetConnectionInfo()
    {
      return new ConnectionInfo("HTTP", ConnectionType.Interface, ConnectionStatus.Connected, RemoteEndPoint.ToString(), RemoteEndPoint as IPEndPoint, RemoteHostStatus.None, null, null, null, null, null, null, "Owin");
    }

    protected override async Task<StopReason> DoProcess(CancellationToken cancel_token)
    {
      try {
        var keep_count = 1000;
        while (!cancel_token.IsCancellationRequested && keep_count-->0) {
          HttpRequest req;
          using (var requestTimeout=CancellationTokenSource.CreateLinkedTokenSource(cancel_token))
          using (var reader=new HttpRequestReader(Connection, true)) {
            requestTimeout.CancelAfter(7000);
            req = await reader.ReadAsync(requestTimeout.Token).ConfigureAwait(false);
            if (req==null) {
              return StopReason.OffAir;
            }
          }
          var ctx = new OwinContext(PeerCast, req, Connection, LocalEndPoint as IPEndPoint, RemoteEndPoint as IPEndPoint, AccessControlInfo);
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

    public override IOutputStream Create(Stream input_stream, Stream output_stream, EndPoint local_endpoint, EndPoint remote_endpoint, AccessControlInfo access_control, Guid channel_id, byte[] header)
    {
      var channel = channel_id!=Guid.Empty ? PeerCast.Channels.FirstOrDefault(c => c.ChannelID==channel_id) : null;
      return new OwinHostOutputStream(PeerCast, owinHost, input_stream, output_stream, local_endpoint, remote_endpoint, access_control, channel, header);
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
    public OwinHost OwinHost { get; private set; }
    private OwinHostOutputStreamFactory factory;

    public override string Name {
      get { return nameof(OwinHostPlugin); }
    }

    protected override void OnAttach()
    {
      OwinHost = new OwinHost(Application, Application.PeerCast);
      if (factory==null) {
        factory = new OwinHostOutputStreamFactory(Application.PeerCast, OwinHost);
      }
      Application.PeerCast.OutputStreamFactories.Add(factory);
    }

    protected override void OnDetach()
    {
      OwinHost = null;
      Application.PeerCast.OutputStreamFactories.Remove(factory);
    }

    protected override void OnStart()
    {
    }

    protected override void OnStop()
    {
      OwinHost.Dispose();
    }
  }

}

