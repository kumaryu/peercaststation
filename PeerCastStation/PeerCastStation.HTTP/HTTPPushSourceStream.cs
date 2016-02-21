using PeerCastStation.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

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
      this.useContentBitrate = use_content_bitrate;
    }

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
    private TcpClient client;
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
      IPEndPoint endpoint = null;
      if (client!=null && client.Connected) {
        endpoint = (IPEndPoint)client.Client.RemoteEndPoint;
      }
      return new ConnectionInfo(
        "HTTP Push Source",
        ConnectionType.Source,
        status,
        SourceUri.ToString(),
        endpoint,
        (endpoint!=null && endpoint.Address.IsSiteLocal()) ? RemoteHostStatus.Local : RemoteHostStatus.None,
        lastPosition,
        RecvRate,
        SendRate,
        null,
        null,
        clientName);
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

    private T WaitAndProcessEvents<T>(IEnumerable<T> targets, Func<T, WaitHandle> get_waithandle)
      where T : class
    {
      var target_ary = targets.ToArray();
      var handles = new WaitHandle[] { SyncContext.EventHandle }
        .Concat(target_ary.Select(t => get_waithandle(t)))
        .ToArray();
      bool event_processed = false;
      T signaled = null;
      while (!IsStopped && signaled==null) {
        var idx = WaitHandle.WaitAny(handles);
        if (idx==0) {
          SyncContext.ProcessAll();
          event_processed = true;
        }
        else {
          signaled = target_ary[idx-1];
        }
      }
      if (!event_processed) {
        SyncContext.ProcessAll();
      }
      return signaled;
    }

    protected override StreamConnection DoConnect(Uri source)
    {
      TcpClient client = null;
      var bind_addr = GetBindAddresses(source);
      if (bind_addr==null || bind_addr.Count()==0) {
        throw new BindErrorException(String.Format("Cannot resolve bind address: {0}", source.DnsSafeHost));
      }
      var listeners = bind_addr.Select(addr => new TcpListener(addr)).ToArray();
      try {
        var async_results = listeners.Select(listener => {
          listener.Start(1);
          Logger.Debug("Listening on {0}", listener.LocalEndpoint);
          return new {
            Listener    = listener,
            AsyncResult = listener.BeginAcceptTcpClient(null, null),
          };
        }).ToArray();
        var result = WaitAndProcessEvents(
          async_results,
          async_result => async_result.AsyncResult.AsyncWaitHandle);
        if (result!=null) {
          client = result.Listener.EndAcceptTcpClient(result.AsyncResult);
          Logger.Debug("Client accepted");
        }
      }
      catch (SocketException) {
        throw new BindErrorException(String.Format("Cannot bind address: {0}", bind_addr));
      }
      finally {
        foreach (var listener in listeners) {
          listener.Stop();
        }
      }
      if (client!=null) {
        this.client = client;
        return new StreamConnection(client.GetStream(), client.GetStream());
      }
      else {
        return null;
      }
    }

    protected override void DoClose(StreamConnection connection)
    {
      this.connection.Close();
      this.client.Close();
      Logger.Debug("Closed");
    }

    public override void Run()
    {
      this.state = ConnectionState.Waiting;
      try {
        OnStarted();
        if (connection!=null && !IsStopped) {
          Handshake();
          DoProcess();
        }
        this.state = ConnectionState.Closed;
      }
      catch (BindErrorException e) {
        Logger.Error(e);
        DoStop(StopReason.NoHost);
        this.state = ConnectionState.Error;
      }
      catch (IOException e) {
        Logger.Error(e);
        DoStop(StopReason.ConnectionError);
        this.state = ConnectionState.Error;
      }
      catch (ConnectionStoppedExcception) {
        this.state = ConnectionState.Closed;
      }
      SyncContext.ProcessAll();
      OnStopped();
    }

    private bool chunked = false;
    private bool Handshake()
    {
      HTTPRequest request = null;
      RecvUntil(stream => {
        try {
          request = HTTPRequestReader.Read(stream);
        }
        catch (EndOfStreamException) {
          return false;
        }
        return true;
      });
      if (request==null) new HTTPError(HttpStatusCode.BadRequest);
      if (request.Method!="POST") new HTTPError(HttpStatusCode.MethodNotAllowed);

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
      }

      Logger.Debug("Handshake succeeded");
      return true;
    }

    Stream GetChunkedStream(Stream stream)
    {
      if (!chunked) return stream;
      var pos = stream.Position;
      MemoryStream substream = null;
      var bytes = new List<byte>();
      while (stream.Position<stream.Length) {
        var b = stream.ReadByte();
        if (b=='\r') {
          b = stream.ReadByte();
          if (b<0) {
            if (substream!=null) {
              substream.Position = 0;
            }
            stream.Position = pos;
            return substream;
          }
          if (b=='\n') {
            var line = System.Text.Encoding.ASCII.GetString(bytes.ToArray());
            bytes.Clear();
            Logger.Debug(line);
            if (line!="") {
              int len;
              if (Int32.TryParse(
                  line.Split(';')[0],
                  System.Globalization.NumberStyles.AllowHexSpecifier,
                  System.Globalization.CultureInfo.InvariantCulture.NumberFormat,
                  out len)) {
                if (len==0) {
                  if (substream!=null) {
                    substream.Position = 0;
                  }
                  stream.Position = pos;
                  return substream ?? stream;
                }
                if (stream.Length-stream.Position<len+2) {
                  if (substream!=null) {
                    substream.Position = 0;
                  }
                  stream.Position = pos;
                  return substream;
                }
                else {
                  var buf = new byte[len];
                  stream.Read(buf, 0, len);
                  if (substream==null) {
                    substream = new MemoryStream();
                  }
                  substream.Write(buf, 0, len);
                  stream.ReadByte(); // \r
                  stream.ReadByte(); // \n
                  pos = stream.Position;
                }
              }
              else {
                throw new HTTPError(HttpStatusCode.BadRequest);
              }
            }
          }
          else {
            bytes.Add((byte)'\r');
            bytes.Add((byte)b);
          }
        }
        else {
          bytes.Add((byte)b);
        }
      }
      if (substream!=null) {
        substream.Position = 0;
      }
      stream.Position = pos;
      return substream;
    }

    long lastPosition = 0;
    protected override void DoProcess()
    {
      this.state = ConnectionState.Connected;
      while (!IsStopped) {
        RecvUntil(stream => {
          stream = GetChunkedStream(stream);
          if (stream==null) return false;
          if (stream.Length<=0) return true;
          this.state = ConnectionState.Receiving;
          var data = this.ContentReader.Read(stream);
          if (data.ChannelInfo!=null) {
            Channel.ChannelInfo = UpdateChannelInfo(Channel.ChannelInfo, data.ChannelInfo);
          }
          if (data.ChannelTrack!=null) {
            Channel.ChannelTrack = UpdateChannelTrack(Channel.ChannelTrack, data.ChannelTrack);
          }
          if (data.ContentHeader!=null) {
            Channel.ContentHeader = data.ContentHeader;
            Channel.Contents.Clear();
            lastPosition = data.ContentHeader.Position;
          }
          if (data.Contents!=null) {
            foreach (var content in data.Contents) {
              Channel.Contents.Add(content);
              lastPosition = content.Position;
            }
          }
          return true;
        });
      }
      Stop(StopReason.OffAir);
    }

    protected override void DoPost(Host from, Atom packet)
    {
      //Do nothing
    }

    protected void Recv(byte[] dst, int offset, int len)
    {
      if (len==0) return;
      RecvUntil(stream => stream.Read(dst, offset, len)>=len);
    }

    protected void RecvUntil(Func<Stream,bool> proc)
    {
      WaitAndProcessEvents(connection.ReceiveWaitHandle, stopped => {
        if (stopped) {
          throw new ConnectionStoppedExcception();
        }
        else if (connection.Recv(proc)) {
          return null;
        }
        else {
          return connection.ReceiveWaitHandle;
        }
      });
    }

    protected byte[] Recv(int len)
    {
      var dst = new byte[len];
      Recv(dst, 0, len);
      return dst;
    }

    protected void Send(Action<Stream> proc)
    {
      connection.Send(proc);
    }

    protected void Send(byte[] data, int offset, int len)
    {
      connection.Send(data, offset, len);
    }

    protected void Send(byte[] data)
    {
      Send(data, 0, data.Length);
    }

    protected bool WaitAndProcessEvents(WaitHandle wait_handle, Func<bool,WaitHandle> on_signal)
    {
      var handles = new WaitHandle[] {
        SyncContext.EventHandle,
        null,
      };
      bool event_processed = false;
      while (wait_handle!=null) {
        handles[1] = wait_handle;
        var idx = WaitHandle.WaitAny(handles);
        if (idx==0) {
          SyncContext.ProcessAll();
          if (IsStopped) {
            wait_handle = on_signal(IsStopped);
          }
          event_processed = true;
        }
        else {
          wait_handle = on_signal(IsStopped);
        }
      }
      if (!event_processed) {
        SyncContext.ProcessAll();
      }
      return true;
    }

    private ChannelInfo UpdateChannelInfo(ChannelInfo a, ChannelInfo b)
    {
      var base_atoms = new AtomCollection(a.Extra);
      var new_atoms  = new AtomCollection(b.Extra);
      if (!useContentBitrate) {
        new_atoms.RemoveByName(Atom.PCP_CHAN_INFO_BITRATE);
      }
      base_atoms.Update(new_atoms);
      return new ChannelInfo(base_atoms);
    }

    private ChannelTrack UpdateChannelTrack(ChannelTrack a, ChannelTrack b)
    {
      var base_atoms = new AtomCollection(a.Extra);
      base_atoms.Update(b.Extra);
      return new ChannelTrack(base_atoms);
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
        return new ConnectionInfo(
          "RTMP Source",
          ConnectionType.Source,
          status,
          SourceUri.ToString(),
          endpoint,
          RemoteHostStatus.None,
          null,
          null,
          null,
          null,
          null,
          client_name);
      }
    }

    protected override ISourceConnection CreateConnection(Uri source_uri)
    {
      return new HTTPPushSourceConnection(PeerCast, Channel, source_uri, ContentReader, UseContentBitrate);
    }

    protected override void OnConnectionStopped(SourceStreamBase.ConnectionStoppedEvent msg)
    {
      switch (msg.StopReason) {
      case StopReason.UserReconnect:
        break;
      case StopReason.UserShutdown:
        Stop(msg.StopReason);
        break;
      case StopReason.NoHost:
        Stop(msg.StopReason);
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
