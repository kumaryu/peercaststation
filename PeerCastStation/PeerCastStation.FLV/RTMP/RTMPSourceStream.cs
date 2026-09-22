using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using PeerCastStation.Core;

namespace PeerCastStation.FLV.RTMP
{
  public class RTMPSourceStreamFactory
    : SourceStreamFactoryBase
  {
    public RTMPSourceStreamFactory(PeerCast peercast)
      : base(peercast)
    {
    }

    public override string Name {
      get { return "RTMP Source"; }
    }

    public override string Scheme {
      get { return "rtmp"; }
    }

    public override SourceStreamType Type {
      get { return SourceStreamType.Broadcast; }
    }

    public override Uri DefaultUri {
      get { return new Uri("rtmp://localhost/live/livestream"); }
    }

    public override bool IsContentReaderRequired {
      get { return false; }
    }

    public override ISourceStream Create(Channel channel, Uri source, IContentReader reader)
    {
      return new RTMPSourceStream(PeerCast, channel, source);
    }
  }

  public class RTMPSourceConnection
    : SourceConnectionBase
  {
    public RTMPSourceConnection(PeerCast peercast, Channel channel, Uri source_uri, bool use_content_bitrate)
      : base(peercast, channel, source_uri)
    {
      IContentSink sink = new ChannelContentSink(channel, use_content_bitrate);
      sink =
        System.Text.RegularExpressions.Regex.Matches(source_uri.Query, @"(&|\?)([^&=]+)=([^&=]+)")
          .Cast<System.Text.RegularExpressions.Match>()
          .Where(param => Uri.UnescapeDataString(param.Groups[2].Value).ToLowerInvariant()=="filters")
          .SelectMany(param => Uri.UnescapeDataString(param.Groups[3].Value).Split(','))
          .Select(name => PeerCast.ContentFilters.FirstOrDefault(filter => filter.Name.ToLowerInvariant()==name.ToLowerInvariant()))
          .NotNull()
          .Aggregate(sink, (r,filter) => filter.Activate(r));
      this.flvBuffer = new FLVContentBuffer(channel, new AsynchronousContentSink(sink));
      this.useContentBitrate = use_content_bitrate;
    }

    private FLVContentBuffer flvBuffer;
    private bool useContentBitrate;

    public override ConnectionInfo GetConnectionInfo()
    {
      ConnectionStatus status;
      switch (state) {
      case ConnectionState.Waiting:   status = ConnectionStatus.Connecting; break;
      case ConnectionState.Connected: status = ConnectionStatus.Connecting; break;
      case ConnectionState.Receiving: status = ConnectionStatus.Connected; break;
      case ConnectionState.Error:     status = ConnectionStatus.Error; break;
      default:                        status = ConnectionStatus.Idle; break;
      }
      IPEndPoint? endpoint = null;
      if (connection!=null) {
        endpoint = connection.RemoteEndPoint;
      }
      return new ConnectionInfoBuilder {
        ProtocolName     = "RTMP Source",
        Type             = ConnectionType.Source,
        Status           = status,
        RemoteName       = SourceUri.ToString(),
        RemoteEndPoint   = endpoint,
        RemoteHostStatus = (endpoint!=null && endpoint.Address.IsSiteLocal()) ? RemoteHostStatus.Local : RemoteHostStatus.None,
        ContentPosition  = flvBuffer.Position,
        RecvRate         = RecvRate,
        SendRate         = SendRate,
        AgentName        = clientName,
      }.Build();
    }

    private enum ConnectionState {
      Waiting,
      Connected,
      Receiving,
      Error,
      Closed,
    };
    private ConnectionState state = ConnectionState.Waiting;
    private string clientName = "";

    private IEnumerable<IPEndPoint> GetBindAddresses(Uri uri)
    {
      IEnumerable<IPAddress> addresses;
      if (uri.HostNameType==UriHostNameType.IPv4 ||
          uri.HostNameType==UriHostNameType.IPv6) {
        addresses = new IPAddress[] { IPAddress.Parse(uri.Host) };
      }
      else {
        try {
          addresses = Dns.GetHostAddresses(uri.DnsSafeHost);
        }
        catch (SocketException) {
          return Enumerable.Empty<IPEndPoint>();
        }
      }
      return addresses.Select(addr => new IPEndPoint(addr, uri.Port<0 ? 1935 : uri.Port));
    }

    protected override async Task<SourceConnectionClient?> DoConnect(Uri source, CancellationToken cancellationToken)
    {
      TcpClient? client = null;
      var bind_addr = GetBindAddresses(source);
      if (bind_addr.Count()==0) {
        this.state = ConnectionState.Error;
        throw new BindErrorException(String.Format("Cannot resolve bind address: {0}", source.DnsSafeHost));
      }
      var listeners = bind_addr.Select(addr => { 
        var listener = new TcpListener(addr);
        if (addr.AddressFamily==AddressFamily.InterNetworkV6) {
          listener.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, true);
        }
        return listener;
      }).ToArray();
      try {
        var cancel_task = cancellationToken.CreateCancelTask<TcpClient>();
        var tasks = listeners.Select(listener => {
          listener.Start(1);
          Logger.Debug("Listening on {0}", listener.LocalEndpoint);
          return listener.AcceptTcpClientAsync();
        }).Concat(Enumerable.Repeat(cancel_task, 1)).ToArray();
        var result = await Task.WhenAny(tasks).ConfigureAwait(false);
        if (!result.IsCanceled) {
          client = result.Result;
          Logger.Debug("Client accepted");
        }
        else {
          Logger.Debug("Listen cancelled");
        }
      }
      catch (SocketException) {
        this.state = ConnectionState.Error;
        throw new BindErrorException(String.Format("Cannot bind address: {0}", bind_addr));
      }
      finally {
        foreach (var listener in listeners) {
          listener.Stop();
        }
      }
      if (client!=null) {
        var c = new SourceConnectionClient(client);
        c.Stream.CloseTimeout = 0;
        return c;
      }
      else {
        return null;
      }
    }

    protected override async Task DoProcess(SourceConnectionClient connection, CancellationToken cancellationToken)
    {
      this.state = ConnectionState.Waiting;
      var rtmpConnection = new RTMPPublishConnection(connection.Stream, connection.Stream, flvBuffer);
      rtmpConnection.Started += (sender, e) => {
        this.state = ConnectionState.Receiving;
      };
      rtmpConnection.Stopped += (sender, e) => {
        Stop(StopReason.OffAir);
      };
      try {
        await rtmpConnection.Run(cancellationToken);
        this.state = ConnectionState.Closed;
      }
      catch (IOException e) {
        if (!cancellationToken.IsCancellationRequested && !IsStopped) {
          Logger.Error(e);
          Stop(StopReason.ConnectionError);
          this.state = ConnectionState.Error;
        }
      }
      catch (OperationCanceledException e) {
        if (!cancellationToken.IsCancellationRequested && !IsStopped) {
          Logger.Error(e);
        }
        this.state = ConnectionState.Closed;
      }
    }
  }

  public class RTMPSourceStream
    : SourceStreamBase
  {
    public RTMPSourceStream(PeerCast peercast, Channel channel, Uri source_uri)
      : base(peercast, channel, source_uri)
    {
      this.UseContentBitrate = channel.ChannelInfo==null || channel.ChannelInfo.Bitrate==0;
    }

    public bool UseContentBitrate { get; private set; }

    public override SourceStreamType Type {
      get { return SourceStreamType.Broadcast; }
    }

    public override ConnectionInfo GetConnectionInfo()
    {
      var connInfo = sourceConnection.GetConnectionInfo();
      if (connInfo!=null) {
        return connInfo;
      }
      else {
        ConnectionStatus status;
        switch (StoppedReason) {
        case StopReason.UserReconnect: status = ConnectionStatus.Connecting; break;
        case StopReason.UserShutdown:  status = ConnectionStatus.Idle; break;
        default:                       status = ConnectionStatus.Error; break;
        }
        string client_name = "";
        return new ConnectionInfoBuilder {
          ProtocolName     = "RTMP Source",
          Type             = ConnectionType.Source,
          Status           = status,
          RemoteName       = SourceUri.ToString(),
          RemoteEndPoint   = null,
          RemoteHostStatus = RemoteHostStatus.None,
          AgentName        = client_name,
        }.Build();
      }
    }

    protected override ISourceConnection CreateConnection(Uri source_uri)
    {
      return new RTMPSourceConnection(PeerCast, Channel, source_uri, UseContentBitrate);
    }

    protected override void OnConnectionStopped(ISourceConnection connection, ConnectionStoppedArgs args)
    {
      switch (args.Reason) {
      case StopReason.UserReconnect:
      case StopReason.UserShutdown:
      case StopReason.NoHost:
        break;
      default:
        args.Delay = 3000;
        args.Reconnect = true;
        break;
      }
    }

  }

  [Plugin]
  class RTMPSourceStreamPlugin
    : PluginBase
  {
    override public string Name { get { return "RTMP Source"; } }

    private RTMPSourceStreamFactory? factory;
    override protected void OnAttach(PeerCastApplication app)
    {
      if (factory==null) factory = new RTMPSourceStreamFactory(app.PeerCast);
      app.PeerCast.SourceStreamFactories.Add(factory);
    }

    override protected void OnDetach(PeerCastApplication app)
    {
      var f = Interlocked.Exchange(ref factory, null);
      if (f!=null) {
        app.PeerCast.SourceStreamFactories.Remove(f);
      }
    }
  }

}
