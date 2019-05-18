// PeerCastStation, a P2P streaming servent.
// Copyright (C) 2011 Ryuichi Sakamoto (kumaryu@kumaryu.net)
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
using System;
using System.Linq;
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Threading;
using System.Text.RegularExpressions;
using PeerCastStation.Core;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace PeerCastStation.HTTP
{
  /// <summary>
  /// ストリームからHTTPリクエストを読み取るクラスです
  /// </summary>
  public static class HTTPRequestReader
  {
    /// <summary>
    /// ストリームからHTTPリクエストを読み取り解析します
    /// </summary>
    /// <param name="stream">読み取り元のストリーム</param>
    /// <returns>解析済みHTTPRequest</returns>
    public static HTTPRequest Read(Stream stream)
    {
      string line = null;
      var requests = new List<string>();
      var buf = new List<byte>();
      while (line!="") {
        var value = stream.ReadByte();
        if (value<0) return null;
        buf.Add((byte)value);
        if (buf.Count >= 2 && buf[buf.Count - 2] == '\r' && buf[buf.Count - 1] == '\n') {
          line = System.Text.Encoding.UTF8.GetString(buf.ToArray(), 0, buf.Count - 2);
          if (line!="") requests.Add(line);
          buf.Clear();
        }
      }
      var req = new HTTPRequest(requests);
      if (req.Uri==null) return null;
      return req;
    }

    public static async Task<HTTPRequest> ReadAsync(Stream stream, CancellationToken cancel_token)
    {
      string line = null;
      var requests = new List<string>();
      var buf = new List<byte>();
      while (line!="") {
        var value = await stream.ReadByteAsync(cancel_token).ConfigureAwait(false);
        if (value<0) return null;
        buf.Add((byte)value);
        if (buf.Count >= 2 && buf[buf.Count - 2] == '\r' && buf[buf.Count - 1] == '\n') {
          line = System.Text.Encoding.UTF8.GetString(buf.ToArray(), 0, buf.Count - 2);
          if (line!="") requests.Add(line);
          buf.Clear();
        }
      }
      var req = new HTTPRequest(requests);
      if (req.Uri==null) return null;
      return req;
    }

  }

  /// <summary>
  /// HTTPで視聴出力をするHTTPOutputStreamを作成するクラスです
  /// </summary>
  public class HTTPOutputStreamFactory
    : OutputStreamFactoryBase
  {
    /// <summary>
    /// プロトコル名を取得します。常に"HTTP"を返します
    /// </summary>
    public override string Name
    {
      get { return "HTTP"; }
    }

    public override OutputStreamType OutputStreamType
    {
      get { return OutputStreamType.Play; }
    }

    private string ParseEndPoint(string text)
    {
      var ipv4port = Regex.Match(text, @"\A(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}):(\d{1,5})\z");
      var ipv6port = Regex.Match(text, @"\A\[([a-fA-F0-9:]+)\]:(\d{1,5})\z");
      var hostport = Regex.Match(text, @"\A([a-zA-Z](?:[a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z](?:[a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?)*):(\d{1,5})\z");
      var ipv4addr = Regex.Match(text, @"\A(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})\z");
      var ipv6addr = Regex.Match(text, @"\A([a-fA-F0-9:.]+)\z");
      var hostaddr = Regex.Match(text, @"\A([a-zA-Z](?:[a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z](?:[a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?)*)\z");
      if (ipv4port.Success) {
        IPAddress addr;
        int port;
        if (IPAddress.TryParse(ipv4port.Groups[1].Value, out addr) &&
            addr.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork &&
            Int32.TryParse(ipv4port.Groups[2].Value, out port) &&
            0<port && port<=65535) {
          return new IPEndPoint(addr, port).ToString();
        }
      }
      if (ipv6port.Success) {
        IPAddress addr;
        int port;
        if (IPAddress.TryParse(ipv6port.Groups[1].Value, out addr) &&
            addr.AddressFamily==System.Net.Sockets.AddressFamily.InterNetworkV6 &&
            Int32.TryParse(ipv6port.Groups[2].Value, out port) &&
            0<port && port<=65535) {
          return new IPEndPoint(addr, port).ToString();
        }
      }
      if (hostport.Success) {
        string host = hostport.Groups[1].Value;
        int port;
        if (Int32.TryParse(hostport.Groups[2].Value, out port) && 0<port && port<=65535) {
          return String.Format("{0}:{1}", host, port);
        }
      }
      if (ipv4addr.Success) {
        IPAddress addr;
        if (IPAddress.TryParse(ipv4addr.Groups[1].Value, out addr) &&
            addr.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork) {
          return addr.ToString();
        }
      }
      if (ipv6addr.Success) {
        IPAddress addr;
        if (IPAddress.TryParse(ipv6addr.Groups[1].Value, out addr) &&
            addr.AddressFamily==System.Net.Sockets.AddressFamily.InterNetworkV6) {
          return String.Format("[{0}]", addr.ToString());
        }
      }
      if (hostaddr.Success) {
        string host = hostaddr.Groups[1].Value;
        return host;
      }
      return null;
    }

    private Uri CreateTrackerUri(Guid channel_id, Uri request_uri)
    {
      string tip = null;
      foreach (Match param in Regex.Matches(request_uri.Query, @"(&|\?)([^&=]+)=([^&=]+)")) {
        if (Uri.UnescapeDataString(param.Groups[2].Value)=="tip") {
          tip = Uri.UnescapeDataString(param.Groups[3].Value);
          break;
        }
      }
      return OutputStreamBase.CreateTrackerUri(channel_id, tip);
    }

    /// <summary>
    /// 出力ストリームを作成します
    /// </summary>
    /// <param name="input_stream">元になる受信ストリーム</param>
    /// <param name="output_stream">元になる送信ストリーム</param>
    /// <param name="remote_endpoint">接続先。無ければnull</param>
    /// <param name="channel_id">所属するチャンネルのチャンネルID</param>
    /// <param name="header">クライアントからのリクエスト</param>
    /// <returns>
    /// 作成できた場合はHTTPOutputStreamのインスタンス。
    /// headerが正しく解析できなかった場合はnull
    /// </returns>
    public override IOutputStream Create(
      Stream input_stream,
      Stream output_stream,
      EndPoint local_endpoint,
      EndPoint remote_endpoint,
      AccessControlInfo access_control,
      Guid channel_id,
      byte[] header)
    {
      var request = ParseRequest(header);
      if (request!=null) {
        Channel channel = null;
        Uri tracker = CreateTrackerUri(channel_id, request.Uri);
        channel = PeerCast.RequestChannel(channel_id, tracker, true);
        return new HTTPOutputStream(PeerCast, input_stream, output_stream, local_endpoint, remote_endpoint, access_control, channel, request);
      }
      else {
        return null;
      }
    }

    /// <summary>
    /// クライアントからのリクエストを解析しチャンネルIDを取得します
    /// </summary>
    /// <param name="header">クライアントからのリクエスト</param>
    /// <returns>
    /// リクエストが解析できてチャンネルIDを取り出せた場合はチャンネルID。
    /// それ以外の場合はnull
    /// </returns>
    /// <remarks>
    /// HTTPのGETまたはHEADリクエストでパスが
    /// /stream/チャンネルID
    /// /pls/チャンネルID
    /// のいずれかで始まる場合のみチャンネルIDを抽出します
    /// </remarks>
    public override Guid? ParseChannelID(byte[] header)
    {
      var request = ParseRequest(header);
      if (request!=null &&
          (request.Method=="GET" || request.Method=="HEAD") &&
          request.Uri!=null) {
        Match match = null;
        if ((match = Regex.Match(request.Uri.AbsolutePath, @"^/(stream/|pls/)([0-9A-Fa-f]{32}).*$")).Success) {
          return new Guid(match.Groups[2].Value);
        }
      }
      return null;
    }

    /// <summary>
    /// ファクトリオブジェクトを初期化します
    /// </summary>
    /// <param name="peercast">所属するPeerCastオブジェクト</param>
    public HTTPOutputStreamFactory(PeerCast peercast)
      : base(peercast)
    {
    }

    /// <summary>
    /// HTTPリクエストを解析します
    /// </summary>
    /// <param name="header">リクエスト</param>
    /// <returns>
    /// 解析できた場合はHTTPRequest、それ以外はnull
    /// </returns>
    private HTTPRequest ParseRequest(byte[] header)
    {
      using (var stream=new MemoryStream(header)) {
        return HTTPRequestReader.Read(stream);
      }
    }
  }

  /// <summary>
  /// HTTPで視聴出力をするクラスです
  /// </summary>
  public class HTTPOutputStream
    : OutputStreamBase,
      IContentSink
  {
    private HTTPRequest request;
    public bool IsPlayable { get; private set; }

    static HTTPOutputStream()
    {
      var chaninfo = new AtomCollection();
      chaninfo.SetChanInfoBitrate(0);
      chaninfo.SetChanInfoType("RAW");
      chaninfo.SetChanInfoStreamType("application/octet-stream");
      chaninfo.SetChanInfoStreamExt("");
    }

    /// <summary>
    /// 元になるストリーム、チャンネル、リクエストからHTTPOutputStreamを初期化します
    /// </summary>
    /// <param name="peercast">所属するPeerCast</param>
    /// <param name="input_stream">元になる受信ストリーム</param>
    /// <param name="output_stream">元になる送信ストリーム</param>
    /// <param name="remote_endpoint">接続待ち受けをしたアドレス</param>
    /// <param name="remote_endpoint">接続先のアドレス</param>
    /// <param name="access_control">接続可否および認証の情報</param>
    /// <param name="channel">所属するチャンネル。無い場合はnull</param>
    /// <param name="request">クライアントからのリクエスト</param>
    public HTTPOutputStream(
      PeerCast peercast,
      Stream input_stream,
      Stream output_stream,
      EndPoint local_endpoint,
      EndPoint remote_endpoint,
      AccessControlInfo access_control,
      Channel channel,
      HTTPRequest request)
      : base(peercast, input_stream, output_stream, local_endpoint, remote_endpoint, access_control, channel, null)
    {
      Logger.Debug("Initialized: Channel {0}, Remote {1}, Request {2} {3}",
        channel!=null ? channel.ChannelID.ToString("N") : "(null)",
        remote_endpoint,
        request.Method,
        request.Uri);
      this.request = request;
      IsPlayable = channel!=null ? channel.IsPlayable(this) : false;
    }

    private bool IsChannelASF {
      get {
        return
          channelInfo.ContentType=="WMV" ||
          channelInfo.ContentType=="WMA" ||
          channelInfo.ContentType=="ASX";
      }
    }

    private bool IsM3u8Play {
      get {
        if (Regex.IsMatch(request.Uri.AbsolutePath, @"^/pls/([0-9A-Fa-f]{32})\.m3u8.*$")) {
            return true;
        }
        var fmt = GetPlaylistFormat();
        return (fmt != null) && fmt.ToLowerInvariant().Equals("m3u8");
      }
    }

    private enum RequestType {
      Unknown,
      HttpGet,
      HttpHead,
      WMSPDescribe,
      WMSPPlay,
    }

    private RequestType RequestMode {
      get {
        switch (request.Method) {
        case "HEAD":
          return RequestType.HttpHead;
        case "GET":
          if (IsChannelASF) {
            if (request.Pragmas.Contains("xplaystrm=1")) {
              return RequestType.WMSPPlay;
            }
            else {
              return RequestType.WMSPDescribe;
            }
          }
          else {
            return RequestType.HttpGet;
          }
        default:
          return RequestType.Unknown;
        }

      }
    }

    class WaitableQueue<T>
    {
      private SemaphoreSlim locker = new SemaphoreSlim(0);
      private ConcurrentQueue<T> queue = new ConcurrentQueue<T>();

      public void Enqueue(T value)
      {
        queue.Enqueue(value);
        locker.Release();
      }

      public async Task<T> DequeueAsync(CancellationToken cancellationToken)
      {
        await locker.WaitAsync(cancellationToken).ConfigureAwait(false);
        T result;
        while (!queue.TryDequeue(out result)) {
          await locker.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        return result;
      }
    }

    private WaitableQueue<Packet> contentPacketQueue = new WaitableQueue<Packet>();
    private Content headerContent = null;
    private Content lastPacket = null;
    private ChannelInfo channelInfo = null;

    /// <summary>
    /// 出力する内容を表します
    /// </summary>
    public enum BodyType {
      /// <summary>
      /// 内容無し
      /// </summary>
      None,
      /// <summary>
      /// ストリームコンテント
      /// </summary>
      Content,
      /// <summary>
      /// HLSセグメント
      /// </summary>
      Segment,
      /// <summary>
      /// プレイリスト
      /// </summary>
      Playlist,
    }

    /// <summary>
    /// リクエストと所属するチャンネルの有無から出力すべき内容を取得します
    /// </summary>
    /// <returns>
    /// 所属するチャンネルが無いかエラー状態の場合およびリクエストパスがstreamでもplsでも無い場合はBodyType.None、
    /// パスが/stream/で始まる場合はBodyType.Content、
    /// パスが/pls/で始まる場合はBodyType.Playlist
    /// </returns>
    private BodyType GetBodyType()
    {
      if (Channel==null || Channel.Status==SourceStreamStatus.Error || !IsPlayable) {
        return BodyType.None;
      }
      else if (Regex.IsMatch(request.Uri.AbsolutePath, @"^/stream/[0-9A-Fa-f]{32}_([0-9]{5}).*$")) {
        return BodyType.Segment;
      }
      else if (Regex.IsMatch(request.Uri.AbsolutePath, @"^/stream/[0-9A-Fa-f]{32}.*$")) {
        return BodyType.Content;
      }
      else if (Regex.IsMatch(request.Uri.AbsolutePath, @"^/pls/[0-9A-Fa-f]{32}.*$")) {
        return BodyType.Playlist;
      }
      else {
        return BodyType.None;
      }
    }

    private string GetPlaylistScheme()
    {
      string scheme;
      if (request.Parameters.TryGetValue("scheme", out scheme)) {
        return scheme;
      }
      else {
        return null;
      }
    }

    private int GetHlsSegmentIndex() {
      Match match = null;
      if ((match = Regex.Match(request.Uri.AbsolutePath, @"^/stream/([0-9A-Fa-f]{32})_([0-9]{5}).*$")).Success) {
        int i;
        if(int.TryParse(match.Groups[2].Value, out i)){
          return i;
        }
      }
      return -1;
    }

    private string GetPlaylistFormat()
    {
      string fmt;
      if (request.Parameters.TryGetValue("pls", out fmt)) {
        return fmt;
      }
      else {
        return null;
      }
    }

    private IPlayList CreateDefaultPlaylist(string scheme, Channel channel)
    {
      if (IsChannelASF) {
        return new ASXPlayList(scheme, channel);
      }
      else if(IsM3u8Play) {
        return new M3U8PlayList(scheme, channel);
      }
      else {
        return new M3UPlayList(scheme, channel);
      }
    }

    private IPlayList CreatePlaylist(Channel channel)
    {
      var scheme = GetPlaylistScheme();
      var fmt = GetPlaylistFormat();
      if (String.IsNullOrEmpty(fmt)) {
        return CreateDefaultPlaylist(scheme, channel);
      }
      else {
        switch (fmt.ToLowerInvariant()) {
        case "asx": return new ASXPlayList(scheme, channel);
        case "m3u": return new M3UPlayList(scheme, channel);
        case "m3u8": return new M3U8PlayList(scheme, channel);
        default:    return CreateDefaultPlaylist(scheme, channel);
        }
      }
    }

    /// <summary>
    /// HTTPのレスポンスヘッダを作成して取得します
    /// </summary>
    /// <returns>
    /// コンテント毎のHTTPレスポンスヘッダ
    /// </returns>
    protected async Task<string> CreateResponseHeaderAsync(CancellationToken cancellationToken)
    {
      if (Channel==null) {
        return "HTTP/1.0 404 NotFound\r\n";
      }
      if (!IsPlayable) {
        return "HTTP/1.0 503 ServiceUnavailable\r\n";
      }
      switch (GetBodyType()) {
      case BodyType.None:
        return "HTTP/1.0 404 NotFound\r\n";
      case BodyType.Content:
        switch (RequestMode) {
        case RequestType.WMSPDescribe:
            return
              "HTTP/1.0 200 OK\r\n"                                +
              "Server: Rex/9.0.2980\r\n"                           +
              "Cache-Control: no-cache\r\n"                        +
              "Pragma: no-cache\r\n"                               +
              "Pragma: features=\"broadcast,playlist\"\r\n"        +
              "Content-Type: application/vnd.ms.wms-hdr.asfv1\r\n" +
              $"Content-Length: {headerContent.Data.Length}\r\n"   +
              "Access-Control-Allow-Origin: *\r\n" +
              "Connection: Keep-Alive\r\n";
        case RequestType.WMSPPlay:
            return
              "HTTP/1.0 200 OK\r\n"                         +
              "Server: Rex/9.0.2980\r\n"                    +
              "Cache-Control: no-cache\r\n"                 +
              "Pragma: no-cache\r\n"                        +
              "Pragma: features=\"broadcast,playlist\"\r\n" +
              "Access-Control-Allow-Origin: *\r\n" +
              "Content-Type: application/x-mms-framed\r\n";
        case RequestType.HttpGet:
        case RequestType.HttpHead:
        default:
          return
            $"HTTP/1.0 200 OK\r\n" +
            "Access-Control-Allow-Origin: *\r\n" +
            $"Server: {PeerCast.AgentName}\r\n" +
            $"Content-Type: {channelInfo.MIMEType}\r\n";
        }
      case BodyType.Segment:
        {
          int idx = GetHlsSegmentIndex();
          var seg = (await Channel.Hls.GetSegmentsAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault(s => s.Index==idx);
          if (seg.Data == null) {
              return "HTTP/1.0 404 NotFound\r\n";
          } else {
            return String.Format(
                "HTTP/1.0 200 OK\r\n"         +
                "Server: {0}\r\n"             +
                "Cache-Control: no-cache\r\n" +
                "Pragma: no-cache\r\n"        +
                "Content-Length: {1}\r\n"     +
                "Connection: close\r\n"       +
                "Content-Type: video/mp2t\r\n",
              PeerCast.AgentName,
              seg.Data.Length);
          }
        }
      case BodyType.Playlist:
        {
          var pls = CreatePlaylist(Channel);
          return String.Format(
            "HTTP/1.0 200 OK\r\n"             +
            "Server: {0}\r\n"                 +
            "Cache-Control: private\r\n"      +
            "Content-Disposition: inline\r\n" +
            "Connection: close\r\n"           +
            "Access-Control-Allow-Origin: *\r\n" +
            "Content-Type: {1}\r\n",
            PeerCast.AgentName,
            pls.MIMEType);
        }
      default:
        return "HTTP/1.0 404 NotFound\r\n";
      }
    }

    /// <summary>
    /// OutputStreamの種別を取得します。常にOutputStreamType.Playを返します
    /// </summary>
    public override OutputStreamType OutputStreamType
    {
      get { return OutputStreamType.Play; }
    }

    protected override int GetUpstreamRate()
    {
      return channelInfo?.Bitrate ?? 0;
    }

    public override ConnectionInfo GetConnectionInfo()
    {
      ConnectionStatus status = ConnectionStatus.Connected;
      if (IsStopped) {
        status = HasError ? ConnectionStatus.Error : ConnectionStatus.Idle;
      }
      string user_agent = "";
      if (request.Headers.ContainsKey("USER-AGENT")) {
        user_agent = request.Headers["USER-AGENT"];
      }
      return new ConnectionInfoBuilder() {
        ProtocolName     = "HTTP Direct",
        Type             = ConnectionType.Direct,
        Status           = status,
        RemoteName       = RemoteEndPoint.ToString(),
        RemoteEndPoint   = (IPEndPoint)RemoteEndPoint,
        RemoteHostStatus = IsLocal ? RemoteHostStatus.Local : RemoteHostStatus.None,
        ContentPosition  = lastPacket!=null ? lastPacket.Position : 0,
        RecvRate         = Connection.ReadRate,
        SendRate         = Connection.WriteRate,
        AgentName        = user_agent,
      }.Build();
    }

    public async Task WaitChannelReceived(CancellationToken cancellationToken)
    {
      if (Channel==null) return;
      await Task.WhenAny(
        Task.Delay(10000),
        this.channelInfoReadyTaskSource.Task,
        cancellationToken.CreateCancelTask()).ConfigureAwait(false);
      if (channelInfo==null) {
        throw new HTTPError(HttpStatusCode.ServiceUnavailable);
      }
      Logger.Debug("ContentType: {0}", channelInfo.ContentType);
    }

    private async Task SendResponseHeader(CancellationToken cancellationToken)
    {
      var response_header = await CreateResponseHeaderAsync(cancellationToken).ConfigureAwait(false);
      var bytes = System.Text.Encoding.UTF8.GetBytes(response_header + "\r\n");
      await Connection.WriteAsync(bytes).ConfigureAwait(false);
      Logger.Debug("Header: {0}", response_header);
    }

    private class Packet {
      public enum ContentType {
        Header,
        Body,
      }

      public ContentType Type { get; private set; }
      public Content Content { get; private set; }
      public Packet(ContentType type, Content content)
      {
        this.Type = type;
        this.Content = content;
      }
    }

    private Task<Packet> GetPacket(CancellationToken cancel_token)
    {
      return contentPacketQueue.DequeueAsync(cancel_token);
    }

    private async Task SendHeaderContent(CancellationToken cancel_token)
    {
      Logger.Debug("Sending Contents");
      try {
        Packet packet = null;
        do {
          packet = await GetPacket(cancel_token).ConfigureAwait(false);
          if (packet.Type!=Packet.ContentType.Header || packet.Content==null) continue;
          await Connection.WriteAsync(packet.Content.Data, cancel_token).ConfigureAwait(false);
          Logger.Debug("Sent ContentHeader pos {0}", packet.Content.Position);
        }
        while (!IsStopped && packet.Type!=Packet.ContentType.Header || packet.Content==null);
      }
      catch (OperationCanceledException) {
      }
    }

    private async Task SendContents(CancellationToken cancel_token)
    {
      Logger.Debug("Sending Contents");
      Content sent_header = null;
      Content sent_packet = null;
      try {
        while (!IsStopped) {
          var packet = await GetPacket(cancel_token).ConfigureAwait(false);
          switch (packet.Type) {
          case Packet.ContentType.Header:
            if (sent_header!=packet.Content && packet.Content!=null) {
              await Connection.WriteAsync(packet.Content.Data, cancel_token).ConfigureAwait(false);
              Logger.Debug("Sent ContentHeader pos {0}", packet.Content.Position);
              sent_header = packet.Content;
              sent_packet = packet.Content;
            }
            break;
          case Packet.ContentType.Body:
            if (sent_header==null) continue;
            var c = packet.Content;
            if (c.Timestamp>sent_packet.Timestamp ||
                (c.Timestamp==sent_packet.Timestamp && c.Position>sent_packet.Position)) {
              await Connection.WriteAsync(c.Data, cancel_token).ConfigureAwait(false);
              sent_packet = c;
            }
            break;
          }
        }
      }
      catch (OperationCanceledException) {
      }
    }

    private async Task SendSegmentContents(CancellationToken cancel_token)
    {
      Logger.Debug("Sending Contents");
      try {
        int idx = GetHlsSegmentIndex();
        var seg = (await Channel.Hls.GetSegmentsAsync(cancel_token).ConfigureAwait(false)).FirstOrDefault(s => s.Index==idx);
        if (seg.Data!=null) {
          await Connection.WriteAsync(seg.Data, cancel_token).ConfigureAwait(false);
        }
      }
      catch (OperationCanceledException) {
      }
    }

    private async Task SendPlaylist(CancellationToken cancel_token)
    {
      Logger.Debug("Sending Playlist");
      var pls = CreatePlaylist(Channel);
      try {
        using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancel_token)) {
          cts.CancelAfter(TimeSpan.FromSeconds(10));
          var baseuri = new Uri(
            new Uri(request.Uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.UserInfo, UriFormat.UriEscaped)),
            "stream/");

          if (AccessControlInfo.AuthenticationKey!=null) {
            var parameters = new Dictionary<string, string>() {
              { "auth", HTTPUtils.CreateAuthorizationToken(AccessControlInfo.AuthenticationKey) },
            };
            var playlist = await pls.CreatePlayListAsync(baseuri, parameters, cts.Token).ConfigureAwait(false);
            await Connection.WriteAsync(playlist, cancel_token).ConfigureAwait(false);
          }
          else {
            var playlist = await pls.CreatePlayListAsync(baseuri, Enumerable.Empty<KeyValuePair<string,string>>(), cts.Token).ConfigureAwait(false);
            await Connection.WriteAsync(playlist, cancel_token).ConfigureAwait(false);
          }
        }
      }
      catch (OperationCanceledException) {
        cancel_token.ThrowIfCancellationRequested();
        throw new HTTPError(HttpStatusCode.ServiceUnavailable);
      }
    }

    private async Task SendReponseBodyHeaderOnly(CancellationToken cancel_token)
    {
      switch (GetBodyType()) {
      case BodyType.None:
        break;
      case BodyType.Content:
        await SendHeaderContent(cancel_token).ConfigureAwait(false);
        break;
      case BodyType.Segment:
        break;
      case BodyType.Playlist:
        await SendPlaylist(cancel_token).ConfigureAwait(false);
        break;
      }
    }

    private async Task SendReponseBody(CancellationToken cancel_token)
    {
      switch (GetBodyType()) {
      case BodyType.None:
        break;
      case BodyType.Content:
        await SendContents(cancel_token).ConfigureAwait(false);
        break;
      case BodyType.Segment:
        await SendSegmentContents(cancel_token).ConfigureAwait(false);
        break;
      case BodyType.Playlist:
        await SendPlaylist(cancel_token).ConfigureAwait(false);
        break;
      }
    }

    private async Task Unauthorized()
    {
      var response_header = HTTPUtils.CreateResponseHeader(HttpStatusCode.Unauthorized, new Dictionary<string,string>());
      await Connection.WriteAsync(System.Text.Encoding.UTF8.GetBytes(response_header)).ConfigureAwait(false);
      Logger.Debug("Header: {0}", response_header);
    }

    private async Task SendErrorResponse(HttpStatusCode code)
    {
      var response_header = HTTPUtils.CreateResponseHeader(code, new Dictionary<string,string>());
      await Connection.WriteAsync(System.Text.Encoding.UTF8.GetBytes(response_header)).ConfigureAwait(false);
      Logger.Debug("Header: {0}", response_header);
    }

    IContentSink sink = null;
    protected override Task OnStarted(CancellationToken cancel_token)
    {
      if (this.Channel!=null) {
        if (IsM3u8Play && Channel.Hls == null) {
          HTTPLiveStreamingSegmenter hls = new HTTPLiveStreamingSegmenter(Channel);
          Channel.Hls = hls;
        }

        string filters;
        sink = this;
        if (request.Parameters.TryGetValue("filters", out filters)) {
          sink =
            filters.Split(',')
            .Select(name => PeerCast.ContentFilters.FirstOrDefault(filter => filter.Name.ToLowerInvariant()==name.ToLowerInvariant()))
            .Where(filter => filter!=null)
            .Aggregate(sink, (r,filter) => filter.Activate(r));
        }
        this.Channel.AddContentSink(sink);
      }
      return base.OnStarted(cancel_token);
    }

    protected override Task OnStopped(CancellationToken cancel_token)
    {
      if (this.Channel!=null) {
        sink.OnStop(StopReason.OffAir);
        this.Channel.RemoveContentSink(sink);
      }
      return base.OnStopped(cancel_token);
    }

    protected override async Task<StopReason> DoProcess(CancellationToken cancel_token)
    {
      try {
        if (!HTTPUtils.CheckAuthorization(request, AccessControlInfo)) {
          throw new HTTPError(HttpStatusCode.Unauthorized);
        }
        await WaitChannelReceived(cancel_token).ConfigureAwait(false);
        await SendResponseHeader(cancel_token).ConfigureAwait(false);
        switch (RequestMode) {
        case RequestType.WMSPDescribe:
          await SendReponseBodyHeaderOnly(cancel_token).ConfigureAwait(false);
          break;
        case RequestType.HttpGet:
        case RequestType.WMSPPlay:
        default:
          await SendReponseBody(cancel_token).ConfigureAwait(false);
          break;
        }
        return StopReason.OffAir;
      }
      catch (HTTPError err) {
        await SendErrorResponse(err.StatusCode).ConfigureAwait(false);
        return StopReason.OffAir;
      }
    }

    TaskCompletionSource<ChannelInfo> channelInfoReadyTaskSource = new TaskCompletionSource<ChannelInfo>();
    public void OnChannelInfo(ChannelInfo channel_info)
    {
      this.channelInfo = channel_info;
      if (channel_info!=null) {
        this.channelInfoReadyTaskSource.TrySetResult(channel_info);
      }
    }

    public void OnChannelTrack(ChannelTrack channel_track)
    {
    }

    public void OnContentHeader(Content content_header)
    {
      if (headerContent!=content_header) {
        headerContent = content_header;
        lastPacket = headerContent;
        contentPacketQueue.Enqueue(new Packet(Packet.ContentType.Header, content_header));
      }
    }

    public void OnContent(Content content)
    {
      if (headerContent!=null) {
        contentPacketQueue.Enqueue(new Packet(Packet.ContentType.Body, content));
        lastPacket = content;
      }
    }

    public void OnStop(StopReason reason)
    {
    }
  }

  [Plugin]
  class HTTPOutputStreamPlugin
    : PluginBase
  {
    override public string Name { get { return "HTTP Output"; } }

    private HTTPOutputStreamFactory factory;
    override protected void OnAttach()
    {
      if (factory==null) factory = new HTTPOutputStreamFactory(Application.PeerCast);
      Application.PeerCast.OutputStreamFactories.Add(factory);
    }

    override protected void OnDetach()
    {
      Application.PeerCast.OutputStreamFactories.Remove(factory);
    }
  }
}
