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
using System.Collections.Generic;
using System.Linq;
using System.IO;
using PeerCastStation.Core;
using System.Text.RegularExpressions;
using System.Net;

namespace PeerCastStation.PCP
{
  /// <summary>
  ///クライアントからのリレーリクエスト内容を保持するクラスです
  /// </summary>
  public class RelayRequest
  {
    /// <summary>
    /// リクエストされたUriを取得および設定します
    /// </summary>
    public Uri Uri     { get; set; }
    /// <summary>
    /// PCPのバージョンを取得および設定します
    /// </summary>
    public int? PCPVersion { get; set; }
    /// <summary>
    /// コンテントのリレー開始位置を取得および設定します
    /// </summary>
    public long? StreamPos { get; set; }
    /// <summary>
    /// リクエスト元のUserAgentを取得および設定します
    /// </summary>
    public string UserAgent { get; set; }

    /// <summary>
    /// HTTPリクエスト文字列からRelayRequestオブジェクトを構築します
    /// </summary>
    /// <param name="requests">行毎に区切られたHTTPリクエストの文字列表現</param>
    public RelayRequest(IEnumerable<string> requests)
    {
      this.Uri = null;
      this.PCPVersion = null;
      this.StreamPos = null;
      this.UserAgent = null;
      foreach (var req in requests) {
        Match match = null;
        if ((match = Regex.Match(req, @"^GET (\S+) HTTP/1.\d$")).Success) {
          Uri uri;
          if (Uri.TryCreate(new Uri("http://localhost/"), match.Groups[1].Value, out uri)) {
            this.Uri = uri;
          }
          else {
            this.Uri = null;
          }
        }
        else if ((match = Regex.Match(req, @"^x-peercast-pcp:\s*(\d+)\s*$")).Success) {
          this.PCPVersion = Int32.Parse(match.Groups[1].Value);
        }
        else if ((match = Regex.Match(req, @"^x-peercast-pos:\s*(\d+)\s*$")).Success) {
          this.StreamPos = Int64.Parse(match.Groups[1].Value);
        }
        else if ((match = Regex.Match(req, @"^User-Agent:\s*(.*)\s*$")).Success) {
          this.UserAgent = match.Groups[1].Value;
        }
      }
    }
  }

  /// <summary>
  /// ストリームからリレーリクエストを読み取るクラスです
  /// </summary>
  public static class RelayRequestReader
  {
    /// <summary>
    /// ストリームからリレーリクエストを読み取り解析します
    /// </summary>
    /// <param name="stream">読み取り元のストリーム</param>
    /// <returns>解析済みHTTPRequest</returns>
    /// <exception cref="EndOfStreamException">
    /// リレーリクエストの終端より前に解析ストリームの末尾に到達した
    /// </exception>
    public static RelayRequest Read(Stream stream)
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
      return new RelayRequest(requests);
    }
  }

  /// <summary>
  /// PCPでリレー出力をするPCPOutputStreamを作成するクラスです
  /// </summary>
  public class PCPOutputStreamFactory
    : IOutputStreamFactory
  {
    /// <summary>
    /// プロトコル名を取得します。常に"PCP"を返します
    /// </summary>
    public string Name
    {
      get { return "PCP"; }
    }

    /// <summary>
    /// 出力ストリームを作成します
    /// </summary>
    /// <param name="stream">元になるストリーム</param>
    /// <param name="remote_endpoint">接続先。無ければnull</param>
    /// <param name="channel_id">所属するチャンネルID。</param>
    /// <param name="header">クライアントからのリクエスト</param>
    /// <returns>
    /// 作成できた場合はPCPOutputStreamのインスタンス。
    /// headerが正しく解析できなかった場合はnull
    /// </returns>
    public IOutputStream Create(Stream stream, EndPoint remote_endpoint, Guid channel_id, byte[] header)
    {
      var request = ParseRequest(header);
      if (request!=null) {
        Channel channel = null;
        peercast.SynchronizationContext.Send(dummy => {
          channel = peercast.RequestChannel(channel_id, null, false);
        }, null);
        return new PCPOutputStream(peercast, stream, remote_endpoint, channel, request);
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
    /// /channel/チャンネルID
    /// で始まる場合のみチャンネルIDを抽出します。
    /// またクライアントが要求してくるPCPのバージョンは1である必要があります
    /// </remarks>
    public Guid? ParseChannelID(byte[] header)
    {
      var request = ParseRequest(header);
      if (request!=null && request.Uri!=null && request.PCPVersion==1) {
        Match match = null;
        if ((match = Regex.Match(request.Uri.AbsolutePath, @"^/channel/([0-9A-Fa-f]{32}).*$")).Success) {
          return new Guid(match.Groups[1].Value);
        }
      }
      return null;
    }

    private PeerCast peercast;
    /// <summary>
    /// ファクトリオブジェクトを初期化します
    /// </summary>
    /// <param name="peercast">所属するPeerCastオブジェクト</param>
    public PCPOutputStreamFactory(PeerCast peercast)
    {
      this.peercast = peercast;
    }

    /// <summary>
    /// リレーリクエストを解析します
    /// </summary>
    /// <param name="header">リクエスト</param>
    /// <returns>
    /// 解析できた場合はRelayRequest、それ以外はnull
    /// </returns>
    private RelayRequest ParseRequest(byte[] header)
    {
      RelayRequest res = null;
      var stream = new MemoryStream(header);
      try {
        res = RelayRequestReader.Read(stream);
      }
      catch (EndOfStreamException) {
      }
      stream.Close();
      return res;
    }
  }

  public class PCPOutputStream
    : IOutputStream
  {
    static private Logger logger = new Logger(typeof(PCPOutputStream));
    public OutputStreamType OutputStreamType
    {
      get { return OutputStreamType.Relay; }
    }

    public const int PCP_VERSION = 1218;

    public PeerCast PeerCast   { get; private set; }
    public Stream Stream       { get; private set; }
    public Channel Channel     { get; private set; }
    public bool IsClosed       { get; private set; }
    public Host Downhost       { get; protected set; }
    public bool IsRelayFull    { get; protected set; }
    public bool IsLocal {
      get {
        var ip = remoteEndPoint as IPEndPoint;
        if (ip!=null) {
          return PeerCastStation.Core.Utils.IsSiteLocal(ip.Address);
        }
        else {
          return true;
        }
      }
    }
    private QueuedSynchronizationContext syncContext = null;
    private System.Threading.AutoResetEvent changedEvent = new System.Threading.AutoResetEvent(true);
    private EndPoint remoteEndPoint = null;

    public int UpstreamRate
    {
      get { 
        if (IsLocal || Channel==null) {
          return 0;
        }
        else {
          var chaninfo = Channel.ChannelInfo.Extra.GetChanInfo();
          if (chaninfo!=null) {
            return chaninfo.GetChanInfoBitrate() ?? 0;
          }
          else {
            return 0;
          }
        }
      }
    }

    public override string ToString()
    {
      return String.Format("PCP Relay {0} ({1})", remoteEndPoint, relayRequest.UserAgent);
    }
    private RelayRequest relayRequest;

    public PCPOutputStream(
      PeerCast peercast,
      Stream stream,
      EndPoint remote_endpoint,
      Channel channel,
      RelayRequest request)
    {
      logger.Debug("Initialized: Channel {0}, Remote {1}, Request {2} {3} ({4} {5})",
        channel!=null ? channel.ChannelInfo.ChannelID.ToString("N") : "(null)",
        remote_endpoint,
        request.Uri,
        request.StreamPos,
        request.PCPVersion,
        request.UserAgent);
      this.PeerCast = peercast;
      this.Stream = stream;
      this.Channel = channel;
      this.remoteEndPoint = remote_endpoint;
      this.Downhost = null;
      this.IsRelayFull = channel!=null ? !peercast.AccessController.IsChannelRelayable(channel, this) : false;
      this.relayRequest = request;
    }

    protected virtual string CreateRelayResponse(Channel channel, bool is_relay_full)
    {
      if (channel==null || channel.Status!=SourceStreamStatus.Recieving) {
        return String.Format(
          "HTTP/1.0 404 Not Found.\r\n" +
          "\r\n");
      }
      else {
        var status = is_relay_full ? "503 Temporary Unavailable." : "200 OK";
        var chaninfo = Channel.ChannelInfo.Extra.GetChanInfo() ?? new AtomCollection();
        return String.Format(
          "HTTP/1.0 {0}\r\n" +
          "Server: {1}\r\n" +
          "Accept-Ranges: none\r\n" +
          "x-audiocast-name: {2}\r\n" +
          "x-audiocast-bitrate: {3}\r\n" +
          "x-audiocast-genre: {4}\r\n" +
          "x-audiocast-description: {5}\r\n" +
          "x-audiocast-url: {6}\r\n" +
          "x-peercast-channelid: {7}\r\n" +
          "Content-Type:application/x-peercast-pcp\r\n" +
          "\r\n",
          status,
          PeerCast.AgentName,
          Channel.ChannelInfo.Name,
          chaninfo.GetChanInfoBitrate() ?? 0,
          chaninfo.GetChanInfoGenre() ?? "",
          chaninfo.GetChanInfoDesc() ?? "",
          chaninfo.GetChanInfoURL() ?? "",
          Channel.ChannelInfo.ChannelID.ToString("N").ToUpper());
      }
    }

    protected virtual void SendRelayResponse()
    {
      var response = CreateRelayResponse(Channel, IsRelayFull);
      Send(System.Text.Encoding.UTF8.GetBytes(response));
      logger.Debug("SendingRelayResponse: {0}", response);
    }

    private void Channel_ContentChanged(object sender, EventArgs args)
    {
      SetContentChanged();
    }

    protected void SetContentChanged()
    {
      this.changedEvent.Set();
    }

    protected bool IsContentChanged()
    {
      return changedEvent.WaitOne(1);
    }

    protected static Atom CreateContentHeaderPacket(Channel channel, Content content)
    {
      var chan = new AtomCollection();
      chan.SetChanID(channel.ChannelInfo.ChannelID);
      var chan_pkt = new AtomCollection();
      chan_pkt.SetChanPktType(Atom.PCP_CHAN_PKT_HEAD);
      chan_pkt.SetChanPktPos((uint)(content.Position & 0xFFFFFFFFU));
      chan_pkt.SetChanPktData(content.Data);
      chan.SetChanPkt(chan_pkt);
      chan.Update(channel.ChannelInfo.Extra);
      logger.Debug("Sending Header: {0}", content.Position);
      return new Atom(Atom.PCP_CHAN, chan);
    }

    protected static Atom CreateContentBodyPacket(Channel channel, Content content)
    {
      var chan = new AtomCollection();
      chan.SetChanID(channel.ChannelInfo.ChannelID);
      var chan_pkt = new AtomCollection();
      chan_pkt.SetChanPktType(Atom.PCP_CHAN_PKT_DATA);
      chan_pkt.SetChanPktPos((uint)(content.Position & 0xFFFFFFFFU));
      chan_pkt.SetChanPktData(content.Data);
      chan.SetChanPkt(chan_pkt);
      return new Atom(Atom.PCP_CHAN, chan);
    }

    protected static Atom CreateContentPacket(Channel channel, ref long? header_pos, ref long? content_pos)
    {
      if (channel.ContentHeader!=null &&
          (!header_pos.HasValue || channel.ContentHeader.Position!=header_pos.Value)) {
        header_pos = channel.ContentHeader.Position;
        if (content_pos.HasValue && content_pos.Value<header_pos.Value) {
          content_pos = header_pos;
        }
        return CreateContentHeaderPacket(channel, channel.ContentHeader);
      }
      else if (header_pos.HasValue) {
        Content content;
        if (content_pos.HasValue) {
          content = channel.Contents.NextOf(content_pos.Value);
        }
        else {
          content = channel.Contents.Newest;
        }
        if (content!=null) {
          content_pos = content.Position;
          return CreateContentBodyPacket(channel, content);
        }
        else {
          return null;
        }
      }
      else {
        return null;
      }
    }

    protected virtual void SendRelayBody(ref long? header_pos, ref long? content_pos)
    {
      if (IsContentChanged()) {
        bool sent = true;
        while (sent) {
          var atom = CreateContentPacket(Channel, ref header_pos, ref content_pos);
          if (atom!=null) {
            sent = true;
            Send(atom);
          }
          else {
            sent = false;
          }
        }
      }
    }

    public void Start()
    {
      logger.Debug("Starting");
      if (this.syncContext == null) {
        this.syncContext = new QueuedSynchronizationContext();
        System.Threading.SynchronizationContext.SetSynchronizationContext(this.syncContext);
      }
      if (Channel!=null) {
        Channel.ContentChanged += new EventHandler(Channel_ContentChanged);
      }
      StartReceive();
      SendRelayResponse();
      if (Channel!=null) {
        long? header_pos  = null;
        long? content_pos = null;
        while (!IsClosed) {
          Atom atom = null;
          while ((atom = RecvAtom())!=null) {
            ProcessAtom(atom);
          }
          if (Downhost!=null) {
            SendRelayBody(ref header_pos, ref content_pos);
          }
          ProcessSend();
          syncContext.ProcessAll();
        }
      }
      while (ProcessSend()) {
        syncContext.ProcessAll();
      }
      Close();
      syncContext.ProcessAll();
      logger.Debug("Finished");
    }

    private bool Recv(Action<Stream> proc)
    {
      bool res = false;
      recvStream.Seek(0, SeekOrigin.Begin);
      try {
        proc(recvStream);
        recvStream = dropStream(recvStream);
        res = true;
      }
      catch (EndOfStreamException) {
      }
      return res;
    }

    private Atom RecvAtom()
    {
      Atom res = null;
      if (recvStream.Length>=8 && Recv(s => { res = AtomReader.Read(s); })) {
        return res;
      }
      else {
        return null;
      }
    }

    public void Post(Host from, Atom packet)
    {
      if (syncContext!=null) {
        syncContext.Post(x => {
          if (Downhost!=null && Downhost!=from) {
            Send(packet);
          }
        }
        , null);
      }
      else {
        if (Downhost!=null && Downhost!=from) {
          Send(packet);
        }
      }
    }

    private void DoClose()
    {
      IsClosed = true;
      if (sendResult!=null) {
        try {
          Stream.EndWrite(sendResult);
        }
        catch (ObjectDisposedException) {}
        catch (IOException) {}
        sendResult = null;
      }
      Stream.Close();
      if (sendStream.Length>0) {
        logger.Debug("Discarded send stream length: {0}", sendStream.Length);
      }
      if (recvStream.Length>0) {
        logger.Debug("Discarded recv stream length: {0}", recvStream.Length);
      }
      sendStream.SetLength(0);
      sendStream.Position = 0;
      recvStream.SetLength(0);
      recvStream.Position = 0;
      if (Channel!=null) {
        Channel.ContentChanged -= Channel_ContentChanged;
      }
    }

    public virtual void Close()
    {
      if (syncContext!=null) {
        syncContext.Post(x => {
          DoClose();
        }, null);
      }
      else {
        DoClose();
      }
    }

    MemoryStream recvStream = new MemoryStream();
    byte[] recvBuffer = new byte[8192];
    private void StartReceive()
    {
      if (!IsClosed) {
        try {
          Stream.BeginRead(recvBuffer, 0, recvBuffer.Length, (ar) => {
            Stream s = (Stream)ar.AsyncState;
            try {
              int bytes = s.EndRead(ar);
              if (bytes > 0) {
                syncContext.Post(x => {
                  recvStream.Seek(0, SeekOrigin.End);
                  recvStream.Write(recvBuffer, 0, bytes);
                  recvStream.Seek(0, SeekOrigin.Begin);
                  StartReceive();
                }, null);
              }
            }
            catch (ObjectDisposedException) {}
            catch (IOException e) {
              logger.Error(e);
              DoClose();
            }
          }, Stream);
        }
        catch (ObjectDisposedException) {}
        catch (IOException e) {
          logger.Error(e);
          DoClose();
        }
      }
    }

    MemoryStream sendStream = new MemoryStream(8192);
    IAsyncResult sendResult = null;
    private bool ProcessSend()
    {
      bool res = false;
      if (sendResult!=null) {
        res = true;
        if (sendResult.IsCompleted) {
          try {
            Stream.EndWrite(sendResult);
          }
          catch (ObjectDisposedException) {
          }
          catch (IOException e) {
            logger.Error(e);
            DoClose();
          }
          sendResult = null;
        }
      }
      if (!IsClosed && sendResult==null && sendStream.Length>0) {
        res = true;
        var buf = sendStream.ToArray();
        sendStream.SetLength(0);
        sendStream.Position = 0;
        try {
          sendResult = Stream.BeginWrite(buf, 0, buf.Length, null, null);
        }
        catch (ObjectDisposedException) {
        }
        catch (IOException e) {
          logger.Error(e);
          DoClose();
        }
      }
      return res;
    }

    protected virtual void Send(byte[] bytes)
    {
      sendStream.Write(bytes, 0, bytes.Length);
    }

    protected virtual void Send(Atom atom)
    {
      AtomWriter.Write(sendStream, atom);
    }

    static private MemoryStream dropStream(MemoryStream s)
    {
      var res = new MemoryStream((int)Math.Max(8192, s.Length - s.Position));
      res.Write(s.GetBuffer(), (int)s.Position, (int)(s.Length - s.Position));
      res.Position = 0;
      return res;
    }

    protected virtual void ProcessAtom(Atom atom)
    {
      if (Downhost==null) {
        //HELOでセッションIDを受け取るまでは他のパケットは無視
        if (atom.Name==Atom.PCP_HELO) OnPCPHelo(atom);
      }
      else {
             if (atom.Name==Atom.PCP_HELO)       OnPCPHelo(atom);
        else if (atom.Name==Atom.PCP_OLEH)       OnPCPOleh(atom);
        else if (atom.Name==Atom.PCP_OK)         OnPCPOk(atom);
        else if (atom.Name==Atom.PCP_CHAN)       OnPCPChan(atom);
        else if (atom.Name==Atom.PCP_CHAN_PKT)   OnPCPChanPkt(atom);
        else if (atom.Name==Atom.PCP_CHAN_INFO)  OnPCPChanInfo(atom);
        else if (atom.Name==Atom.PCP_CHAN_TRACK) OnPCPChanTrack(atom);
        else if (atom.Name==Atom.PCP_BCST)       OnPCPBcst(atom);
        else if (atom.Name==Atom.PCP_HOST)       OnPCPHost(atom);
        else if (atom.Name==Atom.PCP_QUIT)       OnPCPQuit(atom);
      }
    }

    protected virtual bool PingHost(IPEndPoint target, Guid remote_session_id)
    {
      logger.Debug("Ping requested. Try to ping: {0}({1})", target, remote_session_id);
      try {
        var client = new System.Net.Sockets.TcpClient();
        client.Connect(target);
        client.ReceiveTimeout = 3000;
        client.SendTimeout    = 3000;
        var stream = client.GetStream();
        var conn = new Atom(Atom.PCP_CONNECT, 1);
        AtomWriter.Write(stream, conn);
        var helo = new AtomCollection();
        helo.SetHeloSessionID(PeerCast.SessionID);
        AtomWriter.Write(stream, new Atom(Atom.PCP_HELO, helo));
        var res = AtomReader.Read(stream);
        AtomWriter.Write(stream, new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT));
        stream.Close();
        client.Close();
        if (res.Name==Atom.PCP_OLEH) {
          var session_id = res.Children.GetHeloSessionID();
          if (session_id.HasValue && session_id.Value==remote_session_id) {
            logger.Debug("Ping succeeded");
            return true;
          }
          else {
            logger.Debug("Ping failed. Remote SessionID mismatched");
          }
        }
        return false;
      }
      catch (System.Net.Sockets.SocketException e) {
        logger.Debug("Ping failed");
        logger.Debug(e);
        return false;
      }
      catch (EndOfStreamException e) {
        logger.Debug("Ping failed");
        logger.Debug(e);
        return false;
      }
      catch (System.IO.IOException io_error) {
        logger.Debug("Ping failed");
        logger.Debug(io_error);
        if (io_error.InnerException is System.Net.Sockets.SocketException) {
          return false;
        }
        else {
          throw;
        }
      }
    }

    protected virtual void OnPCPHelo(Atom atom)
    {
      if (Downhost!=null) return;
      logger.Debug("Helo received");
      var session_id = atom.Children.GetHeloSessionID();
      short remote_port = 0;
      if (session_id!=null) {
        Downhost = new Host();
        Downhost.SessionID = session_id.Value;
        var port = atom.Children.GetHeloPort();
        var ping = atom.Children.GetHeloPing();
        if (port!=null) {
          remote_port = port.Value;
        }
        else if (ping!=null) {
          if (!Utils.IsSiteLocal(((IPEndPoint)remoteEndPoint).Address) &&
              PingHost(new IPEndPoint(((IPEndPoint)remoteEndPoint).Address, ping.Value), session_id.Value)) {
            remote_port = ping.Value;
          }
          else {
            remote_port = 0;
          }
        }
        else {
          remote_port = 0;
        }
        if (remote_port!=0)  {
          var ip = new IPEndPoint(((IPEndPoint)remoteEndPoint).Address, remote_port);
          if (Downhost.GlobalEndPoint==null || !Downhost.GlobalEndPoint.Equals(ip)) {
            Downhost.GlobalEndPoint = ip;
          }
        }
        Downhost.IsFirewalled = remote_port==0;
        Downhost.Extra.Update(atom.Children);
      }
      var oleh = new AtomCollection();
      if (remoteEndPoint!=null && remoteEndPoint.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork) {
        oleh.SetHeloRemoteIP(((IPEndPoint)remoteEndPoint).Address);
      }
      oleh.SetHeloAgent(PeerCast.AgentName);
      oleh.SetHeloSessionID(PeerCast.SessionID);
      oleh.SetHeloRemotePort(remote_port);
      oleh.SetHeloVersion(PCP_VERSION);
      Send(new Atom(Atom.PCP_OLEH, oleh));
      if (Downhost==null) {
        logger.Info("Helo has no SessionID");
        //セッションIDが無かった
        var quit = new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT+Atom.PCP_ERROR_NOTIDENTIFIED);
        Send(quit);
        Close();
      }
      else if ((Downhost.Extra.GetHeloVersion() ?? 0)<1200) {
        logger.Info("Helo version {0} is too old", Downhost.Extra.GetHeloVersion() ?? 0);
        //クライアントバージョンが無かった、もしくは古すぎ
        var quit = new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT+Atom.PCP_ERROR_BADAGENT);
        Send(quit);
        Close();
      }
      else if (IsRelayFull) {
        logger.Debug("Handshake succeeded {0}({1}) but relay is full", Downhost.GlobalEndPoint, Downhost.SessionID.ToString("N"));
        //次に接続するホストを送ってQUIT
        foreach (var node in Channel.SelectSourceNodes()) {
          var host_atom = new AtomCollection();
          host_atom.SetHostSessionID(node.SessionID);
          var globalendpoint = node.GlobalEndPoint ?? new IPEndPoint(IPAddress.Loopback, 7144);
          host_atom.AddHostIP(globalendpoint.Address);
          host_atom.AddHostPort((short)globalendpoint.Port);
          var localendpoint  = node.LocalEndPoint ?? new IPEndPoint(IPAddress.Loopback, 7144);
          host_atom.AddHostIP(localendpoint.Address);
          host_atom.AddHostPort((short)localendpoint.Port);
          host_atom.SetHostChannelID(Channel.ChannelInfo.ChannelID);
          host_atom.SetHostFlags1(
            (node.IsFirewalled ? PCPHostFlags1.Firewalled : PCPHostFlags1.None) |
            (node.IsRelayFull ? PCPHostFlags1.None : PCPHostFlags1.Relay) |
            (node.IsDirectFull ? PCPHostFlags1.None : PCPHostFlags1.Direct) |
            (node.IsReceiving ? PCPHostFlags1.Receiving : PCPHostFlags1.None));
          host_atom.Update(node.Extra);
          Send(new Atom(Atom.PCP_HOST, host_atom));
          logger.Debug("Sending Node: {0}({1})", globalendpoint, node.SessionID.ToString("N"));
        }
        var quit = new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT+Atom.PCP_ERROR_UNAVAILABLE);
        Send(quit);
        Close();
      }
      else {
        logger.Debug("Handshake succeeded {0}({1})", Downhost.GlobalEndPoint, Downhost.SessionID.ToString("N"));
        Send(new Atom(Atom.PCP_OK, (int)1));
      }
    }

    protected virtual void OnPCPOleh(Atom atom)
    {
    }

    protected virtual void OnPCPOk(Atom atom)
    {
    }

    protected virtual void OnPCPChan(Atom atom)
    {
    }

    protected virtual void OnPCPChanPkt(Atom atom)
    {
    }

    protected virtual void OnPCPChanInfo(Atom atom)
    {
    }

    protected virtual void OnPCPChanTrack(Atom atom)
    {
    }

    protected virtual void OnPCPBcst(Atom atom)
    {
      var dest = atom.Children.GetBcstDest();
      var ttl = atom.Children.GetBcstTTL();
      var hops = atom.Children.GetBcstHops();
      var from = atom.Children.GetBcstFrom();
      var group = atom.Children.GetBcstGroup();
      if (ttl != null &&
          hops != null &&
          group != null &&
          from != null &&
          dest != PeerCast.SessionID &&
          ttl>1) {
        logger.Debug("Relaying BCST TTL: {0}, Hops: {1}", ttl, hops);
        var bcst = new AtomCollection(atom.Children);
        bcst.SetBcstTTL((byte)(ttl - 1));
        bcst.SetBcstHops((byte)(hops + 1));
        Channel.Broadcast(Downhost, new Atom(atom.Name, bcst), group.Value);
      }
      if (dest==null || dest==PeerCast.SessionID) {
        logger.Debug("Processing BCST({0})", dest==null ? "(null)" : dest.Value.ToString("N"));
        foreach (var c in atom.Children) ProcessAtom(c);
      }
    }

    protected virtual void OnPCPHost(Atom atom)
    {
      var session_id = atom.Children.GetHostSessionID();
      if (session_id!=null) {
        PeerCast.SynchronizationContext.Post(dummy => {
          var node = Channel.Nodes.FirstOrDefault(x => x.SessionID.Equals(session_id));
          if (node==null) {
            node = new Host();
            node.SessionID = (Guid)session_id;
            Channel.Nodes.Add(node);
          }
          node.Extra.Update(atom.Children);
          node.DirectCount = atom.Children.GetHostNumListeners() ?? 0;
          node.RelayCount = atom.Children.GetHostNumRelays() ?? 0;
          var flags1 = atom.Children.GetHostFlags1();
          if (flags1 != null) {
            node.IsFirewalled = (flags1.Value & PCPHostFlags1.Firewalled) != 0;
            node.IsRelayFull       = (flags1.Value & PCPHostFlags1.Relay) == 0;
            node.IsDirectFull      = (flags1.Value & PCPHostFlags1.Direct) == 0;
            node.IsReceiving       = (flags1.Value & PCPHostFlags1.Receiving) != 0;
            node.IsControlFull     = (flags1.Value & PCPHostFlags1.ControlIn) == 0;
          }

          int addr_count = 0;
          var ip = new IPEndPoint(IPAddress.Any, 0);
          foreach (var c in atom.Children) {
            if (c.Name==Atom.PCP_HOST_IP) {
              IPAddress addr;
              if (c.TryGetIPv4Address(out addr)) {
                ip.Address = addr;
                if (ip.Port!=0) {
                  if (addr_count==0 && (node.GlobalEndPoint==null || !node.GlobalEndPoint.Equals(ip))) {
                    node.GlobalEndPoint = ip;
                  }
                  if (addr_count==1 && (node.LocalEndPoint==null || !node.LocalEndPoint.Equals(ip))) {
                    node.LocalEndPoint = ip;
                  }
                  ip = new IPEndPoint(IPAddress.Any, 0);
                  addr_count++;
                }
              }
            }
            else if (c.Name==Atom.PCP_HOST_PORT) {
              short port;
              if (c.TryGetInt16(out port)) {
                ip.Port = port;
                if (ip.Address!=IPAddress.Any) {
                  if (addr_count==0 && (node.GlobalEndPoint==null || !node.GlobalEndPoint.Equals(ip))) {
                    node.GlobalEndPoint = ip;
                  }
                  if (addr_count==1 && (node.LocalEndPoint==null || !node.LocalEndPoint.Equals(ip))) {
                    node.LocalEndPoint = ip;
                  }
                  ip = new IPEndPoint(IPAddress.Any, 0);
                  addr_count++;
                }
              }
            }
          }
          logger.Debug("Updating Node: {0}/{1}({2})", node.GlobalEndPoint, node.LocalEndPoint, node.SessionID.ToString("N"));
        }, null);
      }
    }

    protected virtual void OnPCPQuit(Atom atom)
    {
      logger.Debug("Quit Received: {0}", atom.GetInt32());
      Close();
    }


  }
}
