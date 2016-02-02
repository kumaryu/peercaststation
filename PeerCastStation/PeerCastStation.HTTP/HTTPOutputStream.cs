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
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Threading;
using System.Text.RegularExpressions;
using PeerCastStation.Core;

namespace PeerCastStation.HTTP
{
  /// <summary>
  ///クライアントからのHTTPリクエスト内容を保持するクラスです
  /// </summary>
  public class HTTPRequest
  {
    /// <summary>
    /// HTTPメソッドを取得および設定します
    /// </summary>
    public string Method { get; private set; }
    /// <summary>
    /// リクエストされたUriを取得および設定します
    /// </summary>
    public Uri Uri     { get; private set; }
    /// <summary>
    /// リクエストヘッダの値のコレクション取得します
    /// </summary>
    public Dictionary<string, string> Headers { get; private set; }
    public Dictionary<string, string> Parameters { get; private set; }
    public Dictionary<string, string> Cookies { get; private set; }

    /// <summary>
    /// HTTPリクエスト文字列からHTTPRequestオブジェクトを構築します
    /// </summary>
    /// <param name="requests">行毎に区切られたHTTPリクエストの文字列表現</param>
    public HTTPRequest(IEnumerable<string> requests)
    {
      Headers    = new Dictionary<string, string>();
      Parameters = new Dictionary<string, string>();
      Cookies    = new Dictionary<string, string>();
      string host = "localhost";
      string path = "/";
      foreach (var req in requests) {
        Match match = null;
        if ((match = Regex.Match(req, @"^(\w+) +(\S+) +HTTP/1.\d$", RegexOptions.IgnoreCase)).Success) {
          this.Method = match.Groups[1].Value.ToUpper();
          path = match.Groups[2].Value;
        }
        else if ((match = Regex.Match(req, @"^Host:(.+)$", RegexOptions.IgnoreCase)).Success) {
          host = match.Groups[1].Value.Trim();
          Headers["HOST"] = host;
        }
        else if ((match = Regex.Match(req, @"^Cookie:(\s*)(.+)(\s*)$", RegexOptions.IgnoreCase)).Success) {
          foreach (var pair in match.Groups[2].Value.Split(';')) {
            var md = Regex.Match(pair, @"^([A-Za-z0-9!#$%^&*_\-+|~`'"".]+)=(.*)$");
            if (md.Success) {
              Cookies.Add(md.Groups[1].Value, md.Groups[2].Value);
            }
          }
        }
        else if ((match = Regex.Match(req, @"^(\S*):(.+)$", RegexOptions.IgnoreCase)).Success) {
          Headers[match.Groups[1].Value.ToUpper()] = match.Groups[2].Value.Trim();
        }
      }
      Uri uri;
      if (Uri.TryCreate("http://" + host + path, UriKind.Absolute, out uri)) {
        this.Uri = uri;
        foreach (Match param in Regex.Matches(uri.Query, @"(&|\?)([^&=]+)=([^&=]+)")) {
          this.Parameters.Add(
            Uri.UnescapeDataString(param.Groups[2].Value).ToLowerInvariant(),
            Uri.UnescapeDataString(param.Groups[3].Value));
        }
      }
      else {
        this.Uri = null;
      }
    }
  }

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
    /// <exception cref="EndOfStreamException">
    /// HTTPリクエストの終端より前に解析ストリームの末尾に到達した
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// HTTPリクエストが不正な形式であった
    /// </exception>
    public static HTTPRequest Read(Stream stream)
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
      var req = new HTTPRequest(requests);
      if (req.Uri==null) throw new InvalidDataException();
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
        return new HTTPOutputStream(PeerCast, input_stream, output_stream, remote_endpoint, access_control, channel, request);
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
      HTTPRequest res = null;
      var stream = new MemoryStream(header);
      try {
        res = HTTPRequestReader.Read(stream);
      }
      catch (EndOfStreamException) {
      }
      catch (InvalidDataException) {
      }
      stream.Close();
      return res;
    }
  }

  /// <summary>
  /// HTTPで視聴出力をするクラスです
  /// </summary>
  public class HTTPOutputStream
    : OutputStreamBase
  {
    private HTTPRequest request;
    private Content headerPacket = null;
    private List<Content> contentPacketQueue = new List<Content>();
    private Content sentPacket;

    /// <summary>
    /// 元になるストリーム、チャンネル、リクエストからHTTPOutputStreamを初期化します
    /// </summary>
    /// <param name="peercast">所属するPeerCast</param>
    /// <param name="input_stream">元になる受信ストリーム</param>
    /// <param name="output_stream">元になる送信ストリーム</param>
    /// <param name="remote_endpoint">接続先のアドレス</param>
    /// <param name="access_control">接続可否および認証の情報</param>
    /// <param name="channel">所属するチャンネル。無い場合はnull</param>
    /// <param name="request">クライアントからのリクエスト</param>
    public HTTPOutputStream(
      PeerCast peercast,
      Stream input_stream,
      Stream output_stream,
      EndPoint remote_endpoint,
      AccessControlInfo access_control,
      Channel channel,
      HTTPRequest request)
      : base(peercast, input_stream, output_stream, remote_endpoint, access_control, channel, null)
    {
      Logger.Debug("Initialized: Channel {0}, Remote {1}, Request {2} {3}",
        channel!=null ? channel.ChannelID.ToString("N") : "(null)",
        remote_endpoint,
        request.Method,
        request.Uri);
      this.request = request;
    }

    private void OnContentChanged(object sender, EventArgs args)
    {
      lock (contentPacketQueue) {
        headerPacket = Channel.ContentHeader;
        if (headerPacket==null) return;
        if (contentPacketQueue.Count>0) {
          var last_packet = contentPacketQueue[contentPacketQueue.Count-1];
          contentPacketQueue.AddRange(Channel.Contents.GetNewerContents(headerPacket.Stream, last_packet.Timestamp, last_packet.Position));
        }
        else if (sentPacket!=null) {
          contentPacketQueue.AddRange(Channel.Contents.GetNewerContents(headerPacket.Stream, sentPacket.Timestamp, sentPacket.Position));
        }
        else {
          contentPacketQueue.AddRange(Channel.Contents.GetNewerContents(headerPacket.Stream, headerPacket.Timestamp, headerPacket.Position));
        }
      }
    }

    protected override int GetUpstreamRate()
    {
      return Channel.ChannelInfo.Bitrate;
    }

    public override string ToString()
    {
      string user_agent = "";
      if (request.Headers.ContainsKey("USER-AGENT")) {
        user_agent = request.Headers["USER-AGENT"];
      }
      return String.Format(
        "HTTP Direct {0} ({1}) {2}kbps",
        RemoteEndPoint,
        user_agent,
        (int)(RecvRate+SendRate)*8/1000);
    }

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
    protected virtual BodyType GetBodyType()
    {
      if (Channel==null || Channel.Status==SourceStreamStatus.Error) {
        return BodyType.None;
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

    private IPlayList CreateDefaultPlaylist()
    {
      bool mms = 
        Channel.ChannelInfo.ContentType=="WMV" ||
        Channel.ChannelInfo.ContentType=="WMA" ||
        Channel.ChannelInfo.ContentType=="ASX";
      if (mms) return new ASXPlayList();
      else     return new M3UPlayList();
    }

    private IPlayList CreatePlaylist()
    {
      var fmt = GetPlaylistFormat();
      if (String.IsNullOrEmpty(fmt)) {
        return CreateDefaultPlaylist();
      }
      else {
        switch (fmt.ToLowerInvariant()) {
        case "asx": return new ASXPlayList();
        case "m3u": return new M3UPlayList();
        default:    return CreateDefaultPlaylist();
        }
      }
    }

    /// <summary>
    /// HTTPのレスポンスヘッダを作成して取得します
    /// </summary>
    /// <returns>
    /// コンテント毎のHTTPレスポンスヘッダ
    /// </returns>
    protected string CreateResponseHeader()
    {
      if (Channel==null) {
        return "HTTP/1.0 404 NotFound\r\n";
      }
      switch (GetBodyType()) {
      case BodyType.None:
        return "HTTP/1.0 404 NotFound\r\n";
      case BodyType.Content:
        {
          bool mms = 
            Channel.ChannelInfo.ContentType=="WMV" ||
            Channel.ChannelInfo.ContentType=="WMA" ||
            Channel.ChannelInfo.ContentType=="ASX";
          if (mms) {
            return
              "HTTP/1.0 200 OK\r\n"                         +
              "Server: Rex/9.0.2980\r\n"                    +
              "Cache-Control: no-cache\r\n"                 +
              "Pragma: no-cache\r\n"                        +
              "Pragma: features=\"broadcast,playlist\"\r\n" +
              "Content-Type: application/x-mms-framed\r\n";
          }
          else {
            return
              "HTTP/1.0 200 OK\r\n"        +
              "Content-Type: "             +
              Channel.ChannelInfo.MIMEType +
              "\r\n";
          }
        }
      case BodyType.Playlist:
        {
          var pls = CreatePlaylist();
          return String.Format(
            "HTTP/1.0 200 OK\r\n"             +
            "Server: {0}\r\n"                 +
            "Cache-Control: private\r\n"      +
            "Content-Disposition: inline\r\n" +
            "Connection: close\r\n"           +
            "Content-Type: {1}\r\n",
            PeerCast.AgentName,
            pls.MIMEType);
        }
      default:
        return "HTTP/1.0 404 NotFound\r\n";
      }
    }

    /// <summary>
    /// ストリームにプレイリストを出力します
    /// </summary>
    protected void WritePlayList()
    {
      var pls = CreatePlaylist();
      pls.Channels.Add(Channel);
      var baseuri = new Uri(
        new Uri(request.Uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.UserInfo, UriFormat.UriEscaped)),
        "stream/");
      Send(pls.CreatePlayList(baseuri));
    }

    /// <summary>
    /// ストリームにHTTPレスポンスのボディ部分を出力します
    /// </summary>
    protected virtual void WriteResponseBody()
    {
      Logger.Debug("Sending Contents");
      Content sentHeader = null;
      SetState(() => {
        switch (GetBodyType()) {
        case BodyType.None:
          OnWriteResponseBodyCompleted();
          break;
        case BodyType.Content:
          if (sentHeader!=headerPacket) {
            sentHeader = headerPacket;
            sentPacket = sentHeader;
            if (headerPacket!=null) {
              Send(headerPacket.Data);
              Logger.Debug("Sent ContentHeader pos {0}", sentHeader.Position);
            }
          }
          if (sentHeader!=null) {
            lock (contentPacketQueue) {
              foreach (var c in contentPacketQueue) {
                if (HasError) break;
                if (c.Timestamp>sentPacket.Timestamp ||
                    (c.Timestamp==sentPacket.Timestamp && c.Position>sentPacket.Position)) {
                  Send(c.Data);
                  sentPacket = c;
                }
              }
              contentPacketQueue.Clear();
            }
          }
          break;
        case BodyType.Playlist:
          Logger.Debug("Sending Playlist");
          WritePlayList();
          OnWriteResponseBodyCompleted();
          break;
        }
      });
    }

    protected virtual void OnWriteResponseBodyCompleted()
    {
      Stop();
    }

    /// <summary>
    /// チャンネルのContentTypeが取得できるか10秒たつまで待ちます。
    /// </summary>
    protected void WaitChannel()
    {
      var started = Environment.TickCount;
      SetState(() => {
        if (!IsStopped &&
            Channel!=null &&
            Environment.TickCount-started<10000 &&
            (Channel.ChannelInfo.ContentType==null ||
             Channel.ChannelInfo.ContentType=="")) {
          //Do nothing
        }
        else {
          if (Channel!=null) {
            Logger.Debug("ContentType: {0}", Channel.ChannelInfo.ContentType);
          }
          OnWaitChannelCompleted();
        }
      });
    }

    protected void Unauthorized()
    {
      SetState(() => {
        var response_header = HTTPUtils.CreateResponseHeader(
          HttpStatusCode.Unauthorized,
          new Dictionary<string, string>());
        var bytes = System.Text.Encoding.UTF8.GetBytes(response_header);
        Send(bytes);
        Logger.Debug("Header: {0}", response_header);
        Stop();
      });
    }

    protected virtual void OnWaitChannelCompleted()
    {
      if (!IsStopped) {
        WriteResponseHeader();
      }
    }

    /// <summary>
    /// ストリームにHTTPレスポンスヘッダを出力します
    /// </summary>
    protected void WriteResponseHeader()
    {
      SetState(() => {
        var response_header = CreateResponseHeader();
        var bytes = System.Text.Encoding.UTF8.GetBytes(response_header + "\r\n");
        Send(bytes);
        Logger.Debug("Header: {0}", response_header);
        OnWriteResponseHeaderCompleted();
      });
    }

    protected virtual void OnWriteResponseHeaderCompleted()
    {
      if (request.Method=="GET") {
        WriteResponseBody();
      }
      else {
        Stop();
      }
    }

    Action state = null;
    private void SetState(Action state)
    {
      this.state = state;
    }

    protected override void OnIdle()
    {
      if (this.state!=null) this.state();
      base.OnIdle();
    }

    /// <summary>
    /// ストリームにレスポンスを出力します
    /// </summary>
    protected override void OnStarted()
    {
      Logger.Debug("Starting");
      if (HTTPUtils.CheckAuthorization(request, AccessControl.AuthenticationKey)) {
        if (this.Channel!=null) {
          this.Channel.ContentChanged += OnContentChanged;
          OnContentChanged(this, new EventArgs());
        }
        WaitChannel();
      }
      else {
        Unauthorized();
      }
    }

    protected override void OnStopped()
    {
      base.OnStopped();
      if (this.Channel!=null) {
        this.Channel.ContentChanged -= OnContentChanged;
      }
      Logger.Debug("Finished");
    }

    /// <summary>
    /// OutputStreamの種別を取得します。常にOutputStreamType.Playを返します
    /// </summary>
    public override OutputStreamType OutputStreamType
    {
      get { return OutputStreamType.Play; }
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
      return new ConnectionInfo(
        "HTTP Direct",
        ConnectionType.Direct,
        status,
        RemoteEndPoint.ToString(),
        (IPEndPoint)RemoteEndPoint,
        IsLocal ? RemoteHostStatus.Local : RemoteHostStatus.None,
        sentPacket!=null ? sentPacket.Position : 0,
        RecvRate,
        SendRate,
        null,
        null,
        user_agent);
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
