using PeerCastStation.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.HTTP
{
  public class HTTPPushSourceStreamFactory
    : SourceStreamFactoryBase
  {
    public HTTPPushSourceStreamFactory(PeerCast peercast)
      : base(peercast)
    {
    }

    public override string Name {
      get { return "HTTP Push Source"; }
    }

    public override string Scheme {
      get { return "http"; }
    }

    public override SourceStreamType Type {
      get { return SourceStreamType.Broadcast; }
    }

    public override Uri DefaultUri {
      get { return new Uri("http://localhost:8080/"); }
    }

    public override bool IsContentReaderRequired {
      get { return true; }
    }

    public override ISourceStream Create(Channel channel, Uri source, IContentReader reader)
    {
      return new HTTPPushSourceStream(PeerCast, channel, source, reader);
    }
  }

  public class HTTPPushSourceConnection
    : SourceConnectionBase
  {
    public HTTPPushSourceConnection(PeerCast peercast, Channel channel, Uri source_uri, IContentReader content_reader, bool use_content_bitrate)
      : base(peercast, channel, source_uri)
    {
      this.ContentReader = content_reader;
      this.contentSink = new BufferedContentSink(new ChannelContentSink(channel, use_content_bitrate));
    }
    private BufferedContentSink contentSink;

    public IContentReader ContentReader { get; private set; }

    private class ConnectionStoppedExcception : ApplicationException {}
    private class BindErrorException
      : ApplicationException
    {
      public BindErrorException(string message)
        : base(message)
      {
      }
    }

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
      IPEndPoint endpoint = null;
      if (this.connection?.Client?.Connected ?? false) {
        endpoint = (IPEndPoint)this.connection.Client.Client.RemoteEndPoint;
      }
      return new ConnectionInfoBuilder {
        ProtocolName     = "HTTP Push Source",
        Type             = ConnectionType.Source,
        Status           = status,
        RemoteName       = SourceUri.ToString(),
        RemoteEndPoint   = endpoint,
        RemoteHostStatus = (endpoint!=null && endpoint.Address.IsSiteLocal()) ? RemoteHostStatus.Local : RemoteHostStatus.None,
        ContentPosition  = contentSink.LastContent?.Position ?? 0,
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

    protected override async Task<SourceConnectionClient> DoConnect(Uri source, CancellationToken cancellationToken)
    {
      TcpClient client = null;
      var bind_addr = GetBindAddresses(source);
      if (bind_addr.Count()==0) {
        this.state = ConnectionState.Error;
        throw new BindErrorException(String.Format("Cannot resolve bind address: {0}", source.DnsSafeHost));
      }
      var listeners = bind_addr.Select(addr => new TcpListener(addr)).ToArray();
      try {
        var cancel_task = cancellationToken.CreateCancelTask<TcpClient>();
        var tasks = listeners.Select(listener => {
          listener.Start(1);
          Logger.Debug("Listening on {0}", listener.LocalEndpoint);
          return listener.AcceptTcpClientAsync();
        }).Concat(Enumerable.Repeat(cancel_task, 1)).ToArray();
        var result = await Task.WhenAny(tasks);
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
        return new SourceConnectionClient(client);
      }
      else {
        return null;
      }
    }

    private bool chunked = false;
    private async Task Handshake(CancellationToken cancel_token)
    {
      var request = await HTTPRequestReader.ReadAsync(connection.Stream, cancel_token);
      if (request==null) new HTTPError(HttpStatusCode.BadRequest);
      if (request.Method!="POST") new HTTPError(HttpStatusCode.MethodNotAllowed);
      Logger.Debug("POST requested");

      string encodings;
      if (request.Headers.TryGetValue("TRANSFER-ENCODING", out encodings)) {
        var codings = encodings.Split(',')
          .Select(token => token.Trim())
          .Distinct()
          .ToArray();
        if (codings.Length>1 ||
            codings[0].ToLowerInvariant()!="chunked") {
          new HTTPError(HttpStatusCode.NotImplemented);
        }
        chunked = true;
        Logger.Debug("chunked encoding");
      }

      if (request.Headers.ContainsKey("CONTENT-TYPE")) {
        Logger.Debug("Content-Type: {0}", request.Headers["CONTENT-TYPE"]);
      }

      if (!request.Headers.TryGetValue("USER-AGENT", out clientName)) {
        clientName = "";
      }
    }

    Stream GetChunkedStream(Stream stream)
    {
      return chunked ? new HTTPChunkedContentStream(stream) : stream;
    }

    private async Task ReadContents(CancellationToken cancel_token)
    {
      this.state = ConnectionState.Connected;
      var stream = GetChunkedStream(connection.Stream);
      this.state = ConnectionState.Receiving;
      await ContentReader.ReadAsync(contentSink, stream, cancel_token);
      Stop(StopReason.OffAir);
    }

    protected override async Task DoProcess(CancellationToken cancellationToken)
    {
      this.state = ConnectionState.Waiting;
      try {
        if (connection!=null && !IsStopped) {
          await Handshake(cancellationToken);
          await ReadContents(cancellationToken);
        }
        this.state = ConnectionState.Closed;
      }
      catch (HTTPError e) {
        await connection.Stream.WriteUTF8Async(HTTPUtils.CreateResponseHeader(e.StatusCode));
        Stop(StopReason.BadAgentError);
        this.state = ConnectionState.Error;
      }
      catch (BindErrorException e) {
        Logger.Error(e);
        Stop(StopReason.NoHost);
        this.state = ConnectionState.Error;
      }
      catch (IOException e) {
        Logger.Error(e);
        Stop(StopReason.ConnectionError);
        this.state = ConnectionState.Error;
      }
      catch (ConnectionStoppedExcception) {
        this.state = ConnectionState.Closed;
      }
    }

    protected override void DoPost(Host from, Atom packet)
    {
      //Do nothing
    }

  }

  public class HTTPPushSourceStream
    : SourceStreamBase
  {
    public IContentReader ContentReader { get; private set; }

    public HTTPPushSourceStream(PeerCast peercast, Channel channel, Uri source_uri, IContentReader content_reader)
      : base(peercast, channel, source_uri)
    {
      this.UseContentBitrate = channel.ChannelInfo==null || channel.ChannelInfo.Bitrate==0;
      this.ContentReader = content_reader;
    }

    public bool UseContentBitrate { get; private set; }

    public override SourceStreamType Type {
      get { return SourceStreamType.Broadcast; }
    }

    public override ConnectionInfo GetConnectionInfo()
    {
      if (sourceConnection!=null) {
        return sourceConnection.GetConnectionInfo();
      }
      else {
        ConnectionStatus status;
        switch (StoppedReason) {
        case StopReason.UserReconnect: status = ConnectionStatus.Connecting; break;
        case StopReason.UserShutdown:  status = ConnectionStatus.Idle; break;
        default:                       status = ConnectionStatus.Error; break;
        }
        IPEndPoint endpoint = null;
        string client_name = "";

        return new ConnectionInfoBuilder {
          ProtocolName     = "RTMP Source",
          Type             = ConnectionType.Source,
          Status           = status,
          RemoteName       = SourceUri.ToString(),
          RemoteEndPoint   = endpoint,
          RemoteHostStatus = RemoteHostStatus.None,
          AgentName        = client_name,
        }.Build();
      }
    }

    protected override ISourceConnection CreateConnection(Uri source_uri)
    {
      return new HTTPPushSourceConnection(PeerCast, Channel, source_uri, ContentReader, UseContentBitrate);
    }

    protected override void OnConnectionStopped(ISourceConnection connection, StopReason reason)
    {
      switch (reason) {
      case StopReason.UserReconnect:
        break;
      case StopReason.UserShutdown:
        Stop(reason);
        break;
      case StopReason.NoHost:
        Stop(reason);
        break;
      default:
        Reconnect();
        break;
      }
    }

  }

  [Plugin]
  class HTTPPushSourceStreamPlugin
    : PluginBase
  {
    override public string Name { get { return "HTTP Push Source"; } }

    private HTTPPushSourceStreamFactory factory;
    override protected void OnAttach()
    {
      if (factory==null) factory = new HTTPPushSourceStreamFactory(Application.PeerCast);
      Application.PeerCast.SourceStreamFactories.Add(factory);
    }

    override protected void OnDetach()
    {
      Application.PeerCast.SourceStreamFactories.Remove(factory);
    }
  }

}
