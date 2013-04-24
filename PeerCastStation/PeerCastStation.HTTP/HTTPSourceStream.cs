using System;
using System.Collections.Generic;
using System.Linq;
using PeerCastStation.Core;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace PeerCastStation.HTTP
{
  /// <summary>
  ///サーバからのHTTPレスポンス内容を保持するクラスです
  /// </summary>
  public class HTTPResponse
  {
    /// <summary>
    /// HTTPバージョンを取得および設定します
    /// </summary>
    public string Version { get; private set; }
    /// <summary>
    /// HTTPステータスを取得および設定します
    /// </summary>
    public int Status { get; private set; }
    /// <summary>
    /// レスポンスヘッダの値のコレクション取得します
    /// </summary>
    public Dictionary<string, string> Headers { get; private set; }

    /// <summary>
    /// HTTPレンスポンス文字列からHTTPResponseオブジェクトを構築します
    /// </summary>
    /// <param name="response">行毎に区切られたHTTPレスポンスの文字列表現</param>
    public HTTPResponse(IEnumerable<string> requests)
    {
      Headers = new Dictionary<string, string>();
      foreach (var req in requests) {
        Match match = null;
        if ((match = Regex.Match(req, @"^HTTP/(1.\d) (\d+) .*$")).Success) {
          this.Version = match.Groups[1].Value;
          this.Status = Int32.Parse(match.Groups[2].Value);
        }
        else if ((match = Regex.Match(req, @"^(\S*):\s*(.*)\s*$", RegexOptions.IgnoreCase)).Success) {
          Headers[match.Groups[1].Value] = match.Groups[2].Value;
        }
      }
    }
  }

  /// <summary>
  /// ストリームからHTTPレスポンスを読み取るクラスです
  /// </summary>
  public static class HTTPResponseReader
  {
    /// <summary>
    /// ストリームからHTTPレスポンスを読み取り解析します
    /// </summary>
    /// <param name="stream">読み取り元のストリーム</param>
    /// <returns>解析済みHTTPResponse</returns>
    /// <exception cref="EndOfStreamException">
    /// HTTPレスポンスの終端より前に解析ストリームの末尾に到達した
    /// </exception>
    public static HTTPResponse Read(Stream stream)
    {
      string line = null;
      var requests = new List<string>();
      var buf = new List<byte>();
      while (line!="") {
        var value = stream.ReadByte();
        if (value<0) {
          throw new EndOfStreamException();
        }
        buf.Add((byte)value);
        if (buf.Count >= 2 && buf[buf.Count - 2] == '\r' && buf[buf.Count - 1] == '\n') {
          line = System.Text.Encoding.UTF8.GetString(buf.ToArray(), 0, buf.Count - 2);
          if (line!="") requests.Add(line);
          buf.Clear();
        }
      }
      return new HTTPResponse(requests);
    }
  }

  public class HTTPSourceStreamFactory
    : SourceStreamFactoryBase
  {
    public override string Name { get { return "http"; } }
    public override string Scheme { get { return "http"; } }
    public override ISourceStream Create(Channel channel, Uri source, IContentReader reader)
    {
      return new HTTPSourceStream(this.PeerCast, channel, source, reader);
    }

    public HTTPSourceStreamFactory(PeerCast peercast)
      : base(peercast)
    {
    }
  }

  public class HTTPSourceStream
    : SourceStreamBase
  {
    enum State
    {
      Connecting,
      WaitResponse,
      Receiving,
      Retrying,
    };
    State state = State.Connecting;

    public IContentReader ContentReader { get; private set; }

    public HTTPSourceStream(PeerCast peercast, Channel channel, Uri source_uri, IContentReader reader)
      : base(peercast, channel, source_uri)
    {
      this.ContentReader = reader;
    }

    protected override void OnStarted()
    {
      base.OnStarted();
      this.Status = SourceStreamStatus.Connecting;
      state = State.Connecting;
    }

    protected override void OnError()
    {
      if (!HasError) {
        HasError = true;
        EndConnection();
      }
      state = State.Retrying;
    }

    private TcpClient client = null;
    protected bool StartConnection()
    {
      bool connected;
      try {
        client = new TcpClient();
        client.Connect(SourceUri.DnsSafeHost, SourceUri.Port);
        var stream = client.GetStream();
        StartConnection(stream, stream);
        connected = true;
      }
      catch (SocketException e) {
        Logger.Error(e);
        OnError();
        connected = false;
      }
      return connected;
    }

    protected override void EndConnection()
    {
      base.EndConnection();
      if (client!=null) client.Close();
      client = null;
    }

    private void OnStateConnectiong()
    {
      Status = SourceStreamStatus.Connecting;
      if (StartConnection()) {
        var host = SourceUri.DnsSafeHost;
        if (SourceUri.Port!=-1 && SourceUri.Port!=80) {
          host = String.Format("{0}:{1}", SourceUri.DnsSafeHost, SourceUri.Port);
        }
        var request = String.Format(
            "GET {0} HTTP/1.1\r\n" +
            "Host:{1}\r\n" +
            "User-Agent:WMPlayer ({2})\r\n" +
            "connection:close\r\n" +
            "\r\n",
            SourceUri.PathAndQuery,
            host,
            PeerCast.AgentName);
        Send(System.Text.Encoding.UTF8.GetBytes(request));
        state = State.WaitResponse;
        waitResponseStart = Environment.TickCount;
      }
    }

    int waitResponseStart;
    private void OnStateWaitResponse()
    {
      Status = SourceStreamStatus.Connecting;
      HTTPResponse response = null;
      if (Recv(stream => { response = HTTPResponseReader.Read(stream); })) {
        if (response.Status==200) {
          lastReceived = Environment.TickCount;
          state = State.Receiving;
        }
        else {
          Logger.Error("Server responses {0} to GET {1}", response.Status, SourceUri.PathAndQuery);
          EndConnection();
          state = State.Retrying;
        }
      }
      else if (Environment.TickCount-waitResponseStart>10000) {
        Logger.Error("Recv response timed out");
        EndConnection();
        state = State.Retrying;
      }
    }

    int lastReceived;
    long lastPosition = 0;
    private void OnStateReceiving()
    {
      Status = SourceStreamStatus.Receiving;
      Recv(stream => {
        if (stream.Length>0) {
          var data = ContentReader.Read(stream);
          if (data.ChannelInfo!=null) {
            Channel.ChannelInfo = data.ChannelInfo;
          }
          if (data.ChannelTrack!=null) {
            Channel.ChannelTrack = data.ChannelTrack;
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
          lastReceived = Environment.TickCount;
        }
      });
      if (Environment.TickCount-lastReceived>60000) {
        Logger.Error("Recv content timed out");
        OnError();
      }
    }

    int? waitRetryStart = null;
    private void OnStateRetrying()
    {
      Status = SourceStreamStatus.Searching;
      if (!waitRetryStart.HasValue) waitRetryStart = Environment.TickCount;
      if (Environment.TickCount-waitRetryStart.Value>20000) {
        state = State.Connecting;
        waitRetryStart = null;
      }
    }

    protected override void OnIdle()
    {
      base.OnIdle();
      switch (state) {
      case State.Connecting:   OnStateConnectiong(); break;
      case State.WaitResponse: OnStateWaitResponse(); break;
      case State.Receiving:    OnStateReceiving(); break;
      case State.Retrying:     OnStateRetrying(); break;
      }
    }

    protected override void DoReconnect(Uri source_uri)
    {
      base.DoReconnect(source_uri);
      if (state!=State.Retrying) {
        EndConnection();
        state = State.Retrying;
      }
    }

    public override ConnectionInfo GetConnectionInfo()
    {
      ConnectionStatus status = ConnectionStatus.Idle;
      switch (state) {
      case State.Connecting:
      case State.WaitResponse:
      case State.Retrying:
        status = ConnectionStatus.Connecting;
        break;
      case State.Receiving:
        status = ConnectionStatus.Connected;
        break;
      }
      if (IsStopped) {
        status = HasError ? ConnectionStatus.Error : ConnectionStatus.Idle;
      }
      IPEndPoint endpoint = null;
      if (client!=null && client.Connected) {
        endpoint = (IPEndPoint)client.Client.RemoteEndPoint;
      }
      return new ConnectionInfo(
        "HTTP Source",
        ConnectionType.Source,
        status,
        SourceUri.ToString(),
        endpoint,
        (endpoint!=null && Utils.IsSiteLocal(endpoint.Address)) ? RemoteHostStatus.Local : RemoteHostStatus.None,
        lastPosition,
        RecvRate,
        SendRate,
        null,
        null,
        null);
    }

    public override string ToString()
    {
      return String.Format(
        "HTTP {0} Source:{1} {2}kbps",
        Status,
        SourceUri,
        (int)(RecvRate+SendRate)*8/1000);
    }
  }

  [Plugin]
  class HTTPSourceStreamPlugin
    : PluginBase
  {
    override public string Name { get { return "HTTP Source"; } }

    private HTTPSourceStreamFactory factory;
    override protected void OnAttach()
    {
      if (factory==null) factory = new HTTPSourceStreamFactory(Application.PeerCast);
      Application.PeerCast.SourceStreamFactories.Add(factory);
    }

    override protected void OnDetach()
    {
      Application.PeerCast.SourceStreamFactories.Remove(factory);
    }
  }
}
