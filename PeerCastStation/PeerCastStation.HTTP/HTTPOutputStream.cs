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

    /// <summary>
    /// HTTPリクエスト文字列からHTTPRequestオブジェクトを構築します
    /// </summary>
    /// <param name="requests">行毎に区切られたHTTPリクエストの文字列表現</param>
    public HTTPRequest(IEnumerable<string> requests)
    {
      Headers = new Dictionary<string, string>();
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
          Headers["Host"] = host;
        }
        else if ((match = Regex.Match(req, @"^(\S*):\s*(\S*)\s*$", RegexOptions.IgnoreCase)).Success) {
          Headers[match.Groups[1].Value] = match.Groups[2].Value;
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
    : OutputStreamFactoryBase
  {
    /// <summary>
    /// プロトコル名を取得します。常に"HTTP"を返します
    /// </summary>
    public override string Name
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
    public override IOutputStream Create(Stream stream, EndPoint remote_endpoint, Guid channel_id, byte[] header)
    {
      var request = ParseRequest(header);
      if (request!=null) {
        Channel channel = null;
        Uri tracker = CreateTrackerUri(channel_id, request.Uri);
        channel = PeerCast.RequestChannel(channel_id, tracker, true);
        return new HTTPOutputStream(PeerCast, stream, remote_endpoint, channel, request);
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
    private long sentPosition = -1;

    /// <summary>
    /// 元になるストリーム、チャンネル、リクエストからHTTPOutputStreamを初期化します
    /// </summary>
    /// <param name="peercast">所属するPeerCast</param>
    /// <param name="stream">元になるストリーム</param>
    /// <param name="is_local">接続先がローカルネットワーク内かどうか</param>
    /// <param name="channel">所属するチャンネル。無い場合はnull</param>
    /// <param name="request">クライアントからのリクエスト</param>
    public HTTPOutputStream(PeerCast peercast, Stream stream, EndPoint remote_endpoint, Channel channel, HTTPRequest request)
      : base(peercast, stream, remote_endpoint, channel) 
    {
      Logger.Debug("Initialized: Channel {0}, Remote {1}, Request {2} {3}",
        channel!=null ? channel.ChannelID.ToString("N") : "(null)",
        remote_endpoint,
        request.Method,
        request.Uri);
      this.request = request;
      if (this.Channel!=null) {
        this.Channel.ContentChanged += (sender, e) => {
          lock (contentPacketQueue) {
            headerPacket = channel.ContentHeader;
            if (contentPacketQueue.Count>0) {
              contentPacketQueue.AddRange(channel.Contents.GetNewerContents(contentPacketQueue[contentPacketQueue.Count-1].Position));
            }
            else {
              contentPacketQueue.AddRange(channel.Contents.GetNewerContents(sentPosition));
            }
          }
        };
      }
    }

    protected override int GetUpstreamRate()
    {
      return Channel.ChannelInfo.Bitrate;
    }

    public override string ToString()
    {
      string user_agent = "";
      if (request.Headers.ContainsKey("User-Agent")) {
        user_agent = request.Headers["User-Agent"];
      }
      return String.Format("HTTP Direct {0} ({1})", RemoteEndPoint, user_agent);
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
              "Pragme: no-cache\r\n"                        +
              "Pragme: features=\"broadcast,playlist\"\r\n" +
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
          bool mms = 
            Channel.ChannelInfo.ContentType=="WMV" ||
            Channel.ChannelInfo.ContentType=="WMA" ||
            Channel.ChannelInfo.ContentType=="ASX";
          IPlayList pls;
          if (mms) {
            pls = new ASXPlayList();
          }
          else {
            pls = new PLSPlayList();
          }
          pls.Channels.Add(Channel);
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
      bool mms = 
        Channel.ChannelInfo.ContentType=="WMV" ||
        Channel.ChannelInfo.ContentType=="WMA" ||
        Channel.ChannelInfo.ContentType=="ASX";
      IPlayList pls;
      if (mms) {
        pls = new ASXPlayList();
      }
      else {
        pls = new PLSPlayList();
      }
      pls.Channels.Add(Channel);
      var baseuri = new Uri(
        new Uri(request.Uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.UserInfo, UriFormat.UriEscaped)),
        "stream/");
      var bytes = System.Text.Encoding.UTF8.GetBytes(pls.CreatePlayList(baseuri));
      Stream.Write(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// ストリームにHTTPレスポンスのボディ部分を出力します
    /// </summary>
    protected virtual void WriteResponseBody()
    {
      Logger.Debug("Sending Contents");
      Content sentHeader = null;
      headerPacket = null;
      sentPosition = -1;
      SetState(() => {
        switch (GetBodyType()) {
        case BodyType.None:
          OnWriteResponseBodyCompleted();
          break;
        case BodyType.Content:
          if (sentHeader!=headerPacket) {
            if (WriteContentHeader(headerPacket)) {
              sentHeader = headerPacket;
              sentPosition = sentHeader.Position;
              Logger.Debug("Sent ContentHeader pos {0}", sentHeader.Position);
            }
          }
          if (sentHeader!=null) {
            lock (contentPacketQueue) {
              foreach (var c in contentPacketQueue) {
                if (c.Position>sentPosition) {
                  if (WriteContent(c)) {
                    sentPosition = c.Position;
                  }
                  else {
                    break;
                  }
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
            (Channel.Status==SourceStreamStatus.Connecting ||
             Channel.Status==SourceStreamStatus.Searching ||
             Channel.Status==SourceStreamStatus.Idle ||
             Channel.ChannelInfo.ContentType==null ||
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
        Stream.Write(bytes, 0, bytes.Length);
        Logger.Debug("Header: {0}", response_header);
        OnWriteResponseHeaderCompleted();
      });
    }

    protected virtual void OnWriteResponseHeaderCompleted()
    {
      if (request.Method=="GET") {
        WriteResponseBody();
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
      WaitChannel();
    }

    protected override void OnStopped()
    {
      base.OnStopped();
      Logger.Debug("Finished");
    }

    protected virtual void OnError()
    {
      Stop();
    }

    /// <summary>
    /// チャンネルコンテントのヘッダをストリームに出力します
    /// </summary>
    /// <param name="header">出力するヘッダ</param>
    /// <returns>
    /// ヘッダが出力できた場合はtrue、それ以外はfalse
    /// </returns>
    protected virtual bool WriteContentHeader(Content header)
    {
      if (header!=null) {
        if (WriteBytes(header.Data)) {
          return true;
        }
        else {
          OnError();
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
    /// <returns>出力した場合はtrue、失敗した場合はfalse</returns>
    protected bool WriteContent(Content content)
    {
      if (content!=null) {
        if (WriteBytes(content.Data)) {
          return true;
        }
        else {
          OnError();
          return false;
        }
      }
      else {
        return false;
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
        Stream.Write(bytes, 0, bytes.Length);
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
    /// OutputStreamの種別を取得します。常にOutputStreamType.Playを返します
    /// </summary>
    public override OutputStreamType OutputStreamType
    {
      get { return OutputStreamType.Play; }
    }
  }
}
