using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
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
    public string Method { get; set; }
    /// <summary>
    /// リクエストされたUriを取得および設定します
    /// </summary>
    public Uri Uri     { get; set; }

    /// <summary>
    /// HTTPリクエスト文字列からHTTPRequestオブジェクトを構築します
    /// </summary>
    /// <param name="requests">行毎に区切られたHTTPリクエストの文字列表現</param>
    public HTTPRequest(IEnumerable<string> requests)
    {
      string host = "localhost";
      string path = "/";
      foreach (var req in requests) {
        Match match = null;
        if ((match = Regex.Match(req, @"^(\w+) (\S+) HTTP/1.\d$")).Success) {
          this.Method = match.Groups[1].Value;
          path = match.Groups[2].Value;
        }
        else if ((match = Regex.Match(req, @"^Host:\s*(\S*)\s*$", RegexOptions.IgnoreCase)).Success) {
          host = match.Groups[1].Value;
        }
      }
      Uri uri;
      if (Uri.TryCreate("http://" + host + path, UriKind.Absolute, out uri)) {
        this.Uri = uri;
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
      return new HTTPRequest(requests);
    }
  }

  /// <summary>
  /// HTTPで視聴出力をするHTTPOutputStreamを作成するクラスです
  /// </summary>
  public class HTTPOutputStreamFactory
    : IOutputStreamFactory
  {
    /// <summary>
    /// プロトコル名を取得します。常に"HTTP"を返します
    /// </summary>
    public string Name
    {
      get { return "HTTP"; }
    }

    private Uri CreateTrackerUri(Guid channel_id, Uri request_uri)
    {
      string tip = null;
      foreach (Match param in Regex.Matches(request_uri.Query, @"(&|\?)([^&=]+)=([^&=]+)")) {
        if (param.Groups[2].Value=="tip") {
          tip = param.Groups[3].Value;
          break;
        }
      }
      if (tip!=null) {
        return new Uri(String.Format("pcp://{0}/{1}", tip, channel_id));
      }
      else {
        return null;
      }
    }

    /// <summary>
    /// 出力ストリームを作成します
    /// </summary>
    /// <param name="stream">元になるストリーム</param>
    /// <param name="remote_endpoint">接続先。無ければnull</param>
    /// <param name="channel_id">所属するチャンネルのチャンネルID</param>
    /// <param name="header">クライアントからのリクエスト</param>
    /// <returns>
    /// 作成できた場合はHTTPOutputStreamのインスタンス。
    /// headerが正しく解析できなかった場合はnull
    /// </returns>
    public IOutputStream Create(Stream stream, EndPoint remote_endpoint, Guid channel_id, byte[] header)
    {
      var request = ParseRequest(header);
      if (request!=null) {
        Channel channel = null;
        Uri tracker = CreateTrackerUri(channel_id, request.Uri);
        peercast.SynchronizationContext.Send(
          dummy => {
            channel = peercast.RequestChannel(channel_id, tracker, true);
          }, null
        );
        return new HTTPOutputStream(peercast, stream, remote_endpoint, channel, request);
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
    public Guid? ParseChannelID(byte[] header)
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

    private PeerCast peercast;
    /// <summary>
    /// ファクトリオブジェクトを初期化します
    /// </summary>
    /// <param name="peercast">所属するPeerCastオブジェクト</param>
    public HTTPOutputStreamFactory(PeerCast peercast)
    {
      this.peercast = peercast;
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
      stream.Close();
      return res;
    }
  }

  /// <summary>
  /// HTTPで視聴出力をするクラスです
  /// </summary>
  public class HTTPOutputStream
    : IOutputStream
  {
    private PeerCast peercast;
    private Stream stream;
    private Channel channel;
    private HTTPRequest request;
    private volatile bool closed = false;
    private System.Threading.AutoResetEvent changedEvent = new System.Threading.AutoResetEvent(true);

    /// <summary>
    /// 所属するPeerCastを取得します
    /// </summary>
    public PeerCast PeerCast { get { return peercast; } }
    /// <summary>
    /// 元になるストリームを取得します
    /// </summary>
    public Stream Stream { get { return stream; } }
    /// <summary>
    /// 所属するチャンネルを取得します
    /// </summary>
    public Channel Channel { get { return channel; } }
    /// <summary>
    /// ストリームが閉じられたかどうかを取得します
    /// </summary>
    public bool IsClosed { get { return closed; } }
    /// <summary>
    /// 送信先がローカルネットワークかどうかを取得します
    /// </summary>
    public bool IsLocal { get; private set; }
    /// <summary>
    /// 送信に必要な上り帯域を取得します。
    /// IsLocalがtrueの場合は0を返します。
    /// </summary>
    public int UpstreamRate {
      get
      {
        if (IsLocal) {
          return 0;
        }
        else {
          var chaninfo = channel.ChannelInfo.Extra.GetChanInfo();
          if (chaninfo!=null) {
            return chaninfo.GetChanInfoBitrate() ?? 0;
          }
          else {
            return 0;
          }
        }
      }
    }

    /// <summary>
    /// 元になるストリーム、チャンネル、リクエストからHTTPOutputStreamを初期化します
    /// </summary>
    /// <param name="peercast">所属するPeerCast</param>
    /// <param name="stream">元になるストリーム</param>
    /// <param name="is_local">接続先がローカルネットワーク内かどうか</param>
    /// <param name="channel">所属するチャンネル。無い場合はnull</param>
    /// <param name="request">クライアントからのリクエスト</param>
    public HTTPOutputStream(PeerCast peercast, Stream stream, EndPoint remote_endpoint, Channel channel, HTTPRequest request)
    {
      this.peercast = peercast;
      this.stream = stream;
      var ip = remote_endpoint as IPEndPoint;
      this.IsLocal = ip!=null ? Utils.IsSiteLocal(ip.Address) : true;
      this.channel = channel;
      this.request = request;
      if (this.channel!=null) {
        this.channel.ContentChanged += (sender, e) => {
          this.changedEvent.Set();
        };
        this.channel.Closed += (sender, e) => {
          this.closed = true;
          this.changedEvent.Set();
        };
      }
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
      if (channel==null || channel.Status==SourceStreamStatus.Error) {
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

    /// <summary>
    /// HTTPのレスポンスヘッダを作成して取得します
    /// </summary>
    /// <returns>
    /// コンテント毎のHTTPレスポンスヘッダ
    /// </returns>
    protected string CreateResponseHeader()
    {
      if (channel==null) {
        return "HTTP/1.0 404 NotFound\r\n";
      }
      switch (GetBodyType()) {
      case BodyType.None:
        return "HTTP/1.0 404 NotFound\r\n";
      case BodyType.Content:
        {
          bool mms = 
            channel.ChannelInfo.ContentType=="WMV" ||
            channel.ChannelInfo.ContentType=="WMA" ||
            channel.ChannelInfo.ContentType=="ASX";
          if (mms) {
            return
              "HTTP/1.0 200 OK\r\n"                         +
              "Server: Rex/9.0.2980\r\n"                    +
              "Cache-Control: no-cache\r\n"                 +
              "Pragme: no-cache\r\n"                        +
              "Pragme: features=\"broadcast,playlist\"\r\n" +
              "Content-Type: application/x-mms-framed\r\n";
          }
          else {
            return
              "HTTP/1.0 200 OK\r\n"        +
              "Content-Type: "             +
              channel.ChannelInfo.MIMEType +
              "\r\n";
          }
        }
      case BodyType.Playlist:
        {
          bool mms = 
            channel.ChannelInfo.ContentType=="WMV" ||
            channel.ChannelInfo.ContentType=="WMA" ||
            channel.ChannelInfo.ContentType=="ASX";
          IPlayList pls;
          if (mms) {
            pls = new ASXPlayList();
          }
          else {
            pls = new PLSPlayList();
          }
          pls.Channels.Add(channel.ChannelInfo);
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
    /// ストリームにHTTPレスポンスヘッダを出力します
    /// </summary>
    protected void WriteResponseHeader()
    {
      var response_header = CreateResponseHeader();
      var bytes = System.Text.Encoding.UTF8.GetBytes(response_header + "\r\n");
      stream.Write(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// チャンネルのコンテントが変化するかチャンネルが閉じられるまで待ちます
    /// </summary>
    protected virtual void WaitContentChanged()
    {
      changedEvent.WaitOne();
    }

    /// <summary>
    /// ストリームにプレイリストを出力します
    /// </summary>
    protected void WritePlayList()
    {
      bool mms = 
        channel.ChannelInfo.ContentType=="WMV" ||
        channel.ChannelInfo.ContentType=="WMA" ||
        channel.ChannelInfo.ContentType=="ASX";
      IPlayList pls;
      if (mms) {
        pls = new ASXPlayList();
      }
      else {
        pls = new PLSPlayList();
      }
      pls.Channels.Add(channel.ChannelInfo);
      var baseuri = new Uri(
        new Uri(request.Uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.UserInfo, UriFormat.UriEscaped)),
        "stream/");
      var bytes = System.Text.Encoding.UTF8.GetBytes(pls.CreatePlayList(baseuri));
      stream.Write(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// ストリームにHTTPレスポンスのボディ部分を出力します
    /// </summary>
    protected virtual void WriteResponseBody()
    {
      switch (GetBodyType()) {
      case BodyType.None:
        break;
      case BodyType.Content:
        bool header_sent = false;
        long last_pos = -1;
        while (!closed) {
          WaitContentChanged();
          bool sent = true;
          while (sent) {
            if (!header_sent) {
              header_sent = WriteContentHeader();
              sent = header_sent;
            }
            else {
              long new_pos = WriteContent(last_pos);
              sent = last_pos!=new_pos;
              last_pos = new_pos;
            }
          }
        }
        break;
      case BodyType.Playlist:
        WritePlayList();
        break;
      }
    }

    /// <summary>
    /// チャンネルのContentTypeが取得できるか10秒たつまで待ちます。
    /// </summary>
    protected void WaitChannel()
    {
      var timeout_count = 1000;
      while (!closed &&
             channel!=null &&
             timeout_count-->0 &&
             (channel.Status==SourceStreamStatus.Connecting ||
              channel.Status==SourceStreamStatus.Searching ||
              channel.Status==SourceStreamStatus.Idle ||
              channel.ChannelInfo.ContentType==null ||
              channel.ChannelInfo.ContentType=="")) {
        System.Threading.Thread.Sleep(10);
      }
    }

    /// <summary>
    /// ストリームにレスポンスを出力します
    /// </summary>
    public void Start()
    {
      WaitChannel();
      if (!closed) {
        WriteResponseHeader();
        if (request.Method=="GET") {
          WriteResponseBody();
        }
        this.stream.Close();
      }
    }

    /// <summary>
    /// チャンネルコンテントのヘッダをストリームに出力します
    /// </summary>
    /// <returns>
    /// ヘッダが出力できた場合はtrue、それ以外はfalse
    /// </returns>
    protected virtual bool WriteContentHeader()
    {
      if (channel.ContentHeader!=null) {
        if (WriteBytes(channel.ContentHeader.Data)) {
          return true;
        }
        else {
          closed = true;
          return false;
        }
      }
      else {
        return false;
      }
    }

    /// <summary>
    /// チャンネルコンテントのボディをストリームに出力します
    /// </summary>
    /// <param name="last_pos">前回まで出力したposition</param>
    /// <returns>今回出力したposition、出力してない場合はlast_pos</returns>
    protected long WriteContent(long last_pos)
    {
      var content = channel.Contents.NextOf(last_pos);
      if (content!=null) {
        if (WriteBytes(content.Data)) {
          return content.Position;
        }
        else {
          closed = true;
          return last_pos;
        }
      }
      else {
        return last_pos;
      }
    }

    /// <summary>
    /// ストリームにバイト列を出力します
    /// </summary>
    /// <param name="bytes">出力するバイト列</param>
    /// <returns>
    /// 出力できた場合はtrue、それ以外はfalse
    /// </returns>
    protected virtual bool WriteBytes(byte[] bytes)
    {
      try {
        stream.Write(bytes, 0, bytes.Length);
      }
      catch (IOException) {
        return false;
      }
      catch (NotSupportedException) {
        return false;
      }
      catch (ObjectDisposedException) {
        return false;
      }
      return true;
    }

    /// <summary>
    /// ブロードキャストパケットをストリームに出力します。
    /// HTTPOutputStreamではブロードキャストパケットは無視します
    /// </summary>
    /// <param name="from">送信元ホスト</param>
    /// <param name="packet">出力するパケット</param>
    public void Post(Host from, Atom packet)
    {
    }

    /// <summary>
    /// ストリームを閉じます
    /// </summary>
    public void Close()
    {
      if (!closed) {
        closed = true;
        changedEvent.Set();
        this.stream.Close();
      }
    }

    /// <summary>
    /// OutputStreamの種別を取得します。常にOutputStreamType.Playを返します
    /// </summary>
    public OutputStreamType OutputStreamType
    {
      get { return OutputStreamType.Play; }
    }
  }
}
