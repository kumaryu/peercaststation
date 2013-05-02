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
        else if ((match = Regex.Match(req, @"^x-peercast-pcp:\s*(\d+)\s*$", RegexOptions.IgnoreCase)).Success) {
          this.PCPVersion = Int32.Parse(match.Groups[1].Value);
        }
        else if ((match = Regex.Match(req, @"^x-peercast-pos:\s*(\d+)\s*$", RegexOptions.IgnoreCase)).Success) {
          this.StreamPos = Int64.Parse(match.Groups[1].Value);
        }
        else if ((match = Regex.Match(req, @"^User-Agent:\s*(.*)\s*$", RegexOptions.IgnoreCase)).Success) {
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
    : OutputStreamFactoryBase
  {
    /// <summary>
    /// プロトコル名を取得します。常に"PCP"を返します
    /// </summary>
    public override string Name
    {
      get { return "PCP"; }
    }

    public override OutputStreamType OutputStreamType
    {
      get { return OutputStreamType.Relay; }
    }

    /// <summary>
    /// 出力ストリームを作成します
    /// </summary>
    /// <param name="input_stream">元になる受信ストリーム</param>
    /// <param name="output_stream">元になる送信ストリーム</param>
    /// <param name="remote_endpoint">接続先。無ければnull</param>
    /// <param name="channel_id">所属するチャンネルID。</param>
    /// <param name="header">クライアントからのリクエスト</param>
    /// <returns>
    /// 作成できた場合はPCPOutputStreamのインスタンス。
    /// headerが正しく解析できなかった場合はnull
    /// </returns>
    public override IOutputStream Create(
      Stream input_stream,
      Stream output_stream,
      EndPoint remote_endpoint,
      Guid channel_id,
      byte[] header)
    {
      var request = ParseRequest(header);
      if (request!=null) {
        var channel = this.PeerCast.RequestChannel(channel_id, null, false);
        return new PCPOutputStream(this.PeerCast, input_stream, output_stream, remote_endpoint, channel, request);
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
    public override Guid? ParseChannelID(byte[] header)
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

    /// <summary>
    /// ファクトリオブジェクトを初期化します
    /// </summary>
    /// <param name="peercast">所属するPeerCastオブジェクト</param>
    public PCPOutputStreamFactory(PeerCast peercast)
      : base(peercast)
    {
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
    : OutputStreamBase
  {
    public override OutputStreamType OutputStreamType
    {
      get { return OutputStreamType.Relay; }
    }

    public const int PCP_VERSION = 1218;

    public Host Downhost       { get; protected set; }
    public string UserAgent    { get; protected set; }
    public bool IsRelayFull    { get; protected set; }
    public bool IsChannelFound { get; protected set; }
    private System.Threading.AutoResetEvent changedEvent = new System.Threading.AutoResetEvent(true);

    protected override int GetUpstreamRate()
    {
      return Channel.ChannelInfo.Bitrate;
    }

    public override string ToString()
    {
      if (Downhost!=null) {
        var relay_status = "　";
        if (Downhost.IsReceiving) {
          if (Downhost.IsRelayFull) {
            if (Downhost.RelayCount>0) {
              relay_status = "○";
            }
            else if (Downhost.IsFirewalled) {
              relay_status = "×";
            }
            else {
              relay_status = "△";
            }
          }
          else {
            relay_status = "◎";
          }
        }
        else {
          relay_status = "■";
        }
        return String.Format("{4} PCP Relay {0}({1}) [{2}/{3}] {5}kbps",
          RemoteEndPoint,
          UserAgent,
          Downhost.DirectCount,
          Downhost.RelayCount,
          relay_status,
          (int)(RecvRate+SendRate)*8/1000);
      }
      else {
        return String.Format("PCP Relay {0}({1}) {2}kbps",
          RemoteEndPoint,
          relayRequest.UserAgent,
          (int)(RecvRate+SendRate)*8/1000);
      }
    }

    public override ConnectionInfo GetConnectionInfo()
    {
      ConnectionStatus status = ConnectionStatus.Connected;
      if (IsStopped) {
        status = HasError ? ConnectionStatus.Error : ConnectionStatus.Idle;
      }
      var host_status = RemoteHostStatus.None;
      if (IsLocal) host_status |= RemoteHostStatus.Local;
      var relay_count  = 0;
      var direct_count = 0;
      if (Downhost!=null) {
        if (Downhost.IsFirewalled) host_status |= RemoteHostStatus.Firewalled;
        if (Downhost.IsRelayFull)  host_status |= RemoteHostStatus.RelayFull;
        if (Downhost.IsReceiving)  host_status |= RemoteHostStatus.Receiving;
        relay_count  = Downhost.RelayCount;
        direct_count = Downhost.DirectCount;
      }
      return new ConnectionInfo(
        "PCP Relay",
        ConnectionType.Relay,
        status,
        RemoteEndPoint.ToString(),
        (IPEndPoint)RemoteEndPoint,
        host_status,
        lastContent!=null ? lastContent.Position : 0,
        RecvRate,
        SendRate,
        relay_count,
        direct_count,
        relayRequest.UserAgent);
    }

    private RelayRequest relayRequest;

    public PCPOutputStream(
      PeerCast peercast,
      Stream input_stream,
      Stream output_stream,
      EndPoint remote_endpoint,
      Channel channel,
      RelayRequest request)
      : base(peercast, input_stream, output_stream, remote_endpoint, channel, null)
    {
      Logger.Debug("Initialized: Channel {0}, Remote {1}, Request {2} {3} ({4} {5})",
        channel!=null ? channel.ChannelID.ToString("N") : "(null)",
        remote_endpoint,
        request.Uri,
        request.StreamPos,
        request.PCPVersion,
        request.UserAgent);
      this.Downhost = null;
      this.UserAgent = request.UserAgent;
      this.IsChannelFound = channel!=null && channel.Status==SourceStreamStatus.Receiving;
      this.IsRelayFull = channel!=null ? !channel.IsRelayable(this) : false;
      this.relayRequest = request;
    }

    protected string CreateRelayResponse()
    {
      if (!IsChannelFound) {
        return String.Format(
          "HTTP/1.0 404 Not Found.\r\n" +
          "\r\n");
      }
      else {
        var status = IsRelayFull ? "503 Temporary Unavailable." : "200 OK";
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
          Channel.ChannelInfo.Bitrate,
          Channel.ChannelInfo.Genre ?? "",
          Channel.ChannelInfo.Desc ?? "",
          Channel.ChannelInfo.URL ?? "",
          Channel.ChannelID.ToString("N").ToUpper());
      }
    }

    protected void SendRelayResponse()
    {
      var response = CreateRelayResponse();
      Send(System.Text.Encoding.UTF8.GetBytes(response));
      Logger.Debug("SendingRelayResponse: {0}", response);
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

    protected IEnumerable<Atom> CreateContentHeaderPacket(Channel channel, Content content)
    {
      var chan = new AtomCollection();
      chan.SetChanID(channel.ChannelID);
      var chan_pkt = new AtomCollection();
      chan_pkt.SetChanPktType(Atom.PCP_CHAN_PKT_HEAD);
      chan_pkt.SetChanPktPos((uint)(content.Position & 0xFFFFFFFFU));
      chan_pkt.SetChanPktData(content.Data);
      chan.SetChanPkt(chan_pkt);
      chan.SetChanInfo(channel.ChannelInfo.Extra);
      chan.SetChanTrack(channel.ChannelTrack.Extra);
      Logger.Debug("Sending Header: {0}", content.Position);
      return Enumerable.Repeat(new Atom(Atom.PCP_CHAN, chan), 1);
    }

    private Atom CreateContentBodyPacket(Channel channel, long pos, IEnumerable<byte> data)
    {
      var chan = new AtomCollection();
      chan.SetChanID(channel.ChannelID);
      var chan_pkt = new AtomCollection();
      chan_pkt.SetChanPktType(Atom.PCP_CHAN_PKT_DATA);
      chan_pkt.SetChanPktPos((uint)(pos & 0xFFFFFFFFU));
      chan_pkt.SetChanPktData(data.ToArray());
      chan.SetChanPkt(chan_pkt);
      return new Atom(Atom.PCP_CHAN, chan);
    }

    public static readonly int MaxBodyLength = 15*1024;
    protected IEnumerable<Atom> CreateContentBodyPacket(Channel channel, Content content)
    {
      if (content.Data.Length>MaxBodyLength) {
        return Enumerable.Range(0, (content.Data.Length+MaxBodyLength-1)/MaxBodyLength).Select(i =>
          CreateContentBodyPacket(
            channel,
            i*MaxBodyLength+content.Position,
            content.Data.Skip(i*MaxBodyLength).Take(MaxBodyLength))
        );
      }
      else {
        return Enumerable.Repeat(CreateContentBodyPacket(channel, content.Position, content.Data), 1);
      }
    }

    protected IEnumerable<Atom> CreateContentPackets(Channel channel, ref Content lastHeader, ref Content lastContent)
    {
      var atoms = Enumerable.Empty<Atom>();
      if (channel.ContentHeader!=null &&
          (lastHeader==null || channel.ContentHeader.Position!=lastHeader.Position)) {
        lastHeader = channel.ContentHeader;
        if (lastContent!=null && lastContent.Position<lastHeader.Position) {
          lastContent = lastHeader;
        }
        atoms = atoms.Concat(CreateContentHeaderPacket(channel, lastHeader));
      }
      if (lastHeader!=null) {
        Content content;
        if (lastContent!=null) {
          content = channel.Contents.NextOf(lastContent.Stream, lastContent.Timestamp, lastContent.Position);
          if (content!=null && lastContent.Position+lastContent.Data.LongLength<content.Position) {
            Logger.Info("Content Skipped {0} expected but was {1}",
              lastContent.Position+lastContent.Data.LongLength,
              content.Position);
          }
        }
        else if (relayRequest.StreamPos.HasValue && relayRequest.StreamPos.Value>lastHeader.Position) {
          content = channel.Contents.FindNextByPosition(lastHeader.Stream, relayRequest.StreamPos.Value-1) ??
                    channel.Contents.GetNewest(lastHeader.Stream);
        }
        else {
          content = channel.Contents.GetNewest(lastHeader.Stream);
        }
        if (content!=null) {
          lastContent = content;
          atoms = atoms.Concat(CreateContentBodyPacket(channel, content));
        }
      }
      return atoms;
    }

    protected virtual void SendRelayBody(ref Content lastHeader, ref Content lastContent)
    {
      if (IsContentChanged()) {
        bool sent = true;
        while (sent) {
          sent = false;
          int cnt = 0;
          foreach (var atom in CreateContentPackets(Channel, ref lastHeader, ref lastContent)) {
            sent = true;
            Send(atom);
            cnt++;
          }
        }
      }
    }

    protected override void OnStarted()
    {
      base.OnStarted();
      Logger.Debug("Starting");
      if (Channel!=null) {
        Channel.ContentChanged += new EventHandler(Channel_ContentChanged);
      }
      SendRelayResponse();
    }

    private Content lastHeader = null;
    private Content lastContent = null;
    protected override void OnIdle()
    {
      base.OnIdle();
      if (IsChannelFound) {
        Atom atom = null;
        while ((atom = RecvAtom())!=null) {
          ProcessAtom(atom);
        }
        if (Downhost!=null && !IsRelayFull) {
          SendRelayBody(ref lastHeader, ref lastContent);
        }
      }
      else {
        Stop(StopReason.None);
      }
    }

    protected override void OnStopped()
    {
      if (Channel!=null) {
        Channel.ContentChanged -= Channel_ContentChanged;
      }
      base.OnStopped();
      Logger.Debug("Finished");
    }

    protected override void DoPost(Host from, Atom packet)
    {
      if (Downhost!=null && Downhost!=from) {
        Send(packet);
      }
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
      Logger.Debug("Ping requested. Try to ping: {0}({1})", target, remote_session_id);
      bool result = false;
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
        if (res.Name==Atom.PCP_OLEH) {
          var session_id = res.Children.GetHeloSessionID();
          if (session_id.HasValue && session_id.Value==remote_session_id) {
            Logger.Debug("Ping succeeded");
            result = true;
          }
          else {
            Logger.Debug("Ping failed. Remote SessionID mismatched");
          }
        }
        AtomWriter.Write(stream, new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT));
        stream.Close();
        client.Close();
      }
      catch (System.Net.Sockets.SocketException e) {
        Logger.Debug("Ping failed");
        Logger.Debug(e);
      }
      catch (EndOfStreamException e) {
        Logger.Debug("Ping failed");
        Logger.Debug(e);
      }
      catch (System.IO.IOException io_error) {
        Logger.Debug("Ping failed");
        Logger.Debug(io_error);
        if (!(io_error.InnerException is System.Net.Sockets.SocketException)) {
          throw;
        }
      }
      return result;
    }

    public virtual bool IsPingTarget(IPAddress address)
    {
      return !Utils.IsSiteLocal(address);
    }

    private IEnumerable<Host> SelectSourceHosts(IPEndPoint endpoint)
    {
      var rnd = new Random();
      return Channel.Nodes.OrderByDescending(n =>
        ( n.GlobalEndPoint.Address.Equals(endpoint.Address) ? 8000 : 0) +
        (!n.IsRelayFull ? 4000 : 0) +
        ( n.IsReceiving ? 2000 : 0) +
        (Math.Max(10-n.Hops, 0)*100) +
        (n.RelayCount*10) +
        rnd.NextDouble()
      ).Take(8);
    }

    protected virtual void OnPCPHelo(Atom atom)
    {
      if (Downhost!=null) return;
      Logger.Debug("Helo received");
      var session_id = atom.Children.GetHeloSessionID();
      int remote_port = 0;
      if (session_id!=null) {
        var host = new HostBuilder();
        host.SessionID = session_id.Value;
        var port = atom.Children.GetHeloPort();
        var ping = atom.Children.GetHeloPing();
        if (port!=null) {
          remote_port = port.Value;
        }
        else if (ping!=null) {
          if (IsPingTarget(((IPEndPoint)RemoteEndPoint).Address) &&
              PingHost(new IPEndPoint(((IPEndPoint)RemoteEndPoint).Address, ping.Value), session_id.Value)) {
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
          var ip = new IPEndPoint(((IPEndPoint)RemoteEndPoint).Address, remote_port);
          if (host.GlobalEndPoint==null || !host.GlobalEndPoint.Equals(ip)) {
            host.GlobalEndPoint = ip;
          }
        }
        host.IsFirewalled = remote_port==0;
        host.Extra.Update(atom.Children);
        Downhost = host.ToHost();
        UserAgent = atom.Children.GetHeloAgent() ?? UserAgent;
      }
      var oleh = new AtomCollection();
      if (RemoteEndPoint!=null && RemoteEndPoint.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork) {
        oleh.SetHeloRemoteIP(((IPEndPoint)RemoteEndPoint).Address);
      }
      oleh.SetHeloAgent(PeerCast.AgentName);
      oleh.SetHeloSessionID(PeerCast.SessionID);
      oleh.SetHeloRemotePort(remote_port);
      oleh.SetHeloVersion(PCP_VERSION);
      Send(new Atom(Atom.PCP_OLEH, oleh));
      if (Downhost==null) {
        Logger.Info("Helo has no SessionID");
        //セッションIDが無かった
        Stop(StopReason.NotIdentifiedError);
      }
      else if ((Downhost.Extra.GetHeloVersion() ?? 0)<1200) {
        Logger.Info("Helo version {0} is too old", Downhost.Extra.GetHeloVersion() ?? 0);
        //クライアントバージョンが無かった、もしくは古すぎ
        Stop(StopReason.BadAgentError);
      }
      else if (IsRelayFull) {
        Logger.Debug("Handshake succeeded {0}({1}) but relay is full", Downhost.GlobalEndPoint, Downhost.SessionID.ToString("N"));
        //次に接続するホストを送ってQUIT
        foreach (var node in SelectSourceHosts((IPEndPoint)RemoteEndPoint)) {
          var host_atom = new AtomCollection(node.Extra);
          Atom ip = host_atom.FindByName(Atom.PCP_HOST_IP);
          while (ip!=null) {
            host_atom.Remove(ip);
            ip = host_atom.FindByName(Atom.PCP_HOST_IP);
          }
          Atom port = host_atom.FindByName(Atom.PCP_HOST_PORT);
          while (port!=null) {
            host_atom.Remove(port);
            port = host_atom.FindByName(Atom.PCP_HOST_PORT);
          }
          host_atom.SetHostSessionID(node.SessionID);
          var globalendpoint = node.GlobalEndPoint ?? new IPEndPoint(IPAddress.Any, 0);
          host_atom.AddHostIP(globalendpoint.Address);
          host_atom.AddHostPort(globalendpoint.Port);
          var localendpoint  = node.LocalEndPoint ?? new IPEndPoint(IPAddress.Any, 0);
          host_atom.AddHostIP(localendpoint.Address);
          host_atom.AddHostPort(localendpoint.Port);
          host_atom.SetHostNumRelays(node.RelayCount);
          host_atom.SetHostNumListeners(node.DirectCount);
          host_atom.SetHostChannelID(Channel.ChannelID);
          host_atom.SetHostFlags1(
            (node.IsFirewalled ? PCPHostFlags1.Firewalled : PCPHostFlags1.None) |
            (node.IsTracker ? PCPHostFlags1.Tracker : PCPHostFlags1.None) |
            (node.IsRelayFull ? PCPHostFlags1.None : PCPHostFlags1.Relay) |
            (node.IsDirectFull ? PCPHostFlags1.None : PCPHostFlags1.Direct) |
            (node.IsReceiving ? PCPHostFlags1.Receiving : PCPHostFlags1.None) |
            (node.IsControlFull ? PCPHostFlags1.None : PCPHostFlags1.ControlIn));
          Send(new Atom(Atom.PCP_HOST, host_atom));
          Logger.Debug("Sending Node: {0}({1})", globalendpoint, node.SessionID.ToString("N"));
        }
        Stop(StopReason.UnavailableError);
      }
      else {
        Logger.Debug("Handshake succeeded {0}({1})", Downhost.GlobalEndPoint, Downhost.SessionID.ToString("N"));
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
          ttl>0) {
        Logger.Debug("Relaying BCST TTL: {0}, Hops: {1}", ttl, hops);
        var bcst = new AtomCollection(atom.Children);
        bcst.SetBcstTTL((byte)(ttl - 1));
        bcst.SetBcstHops((byte)(hops + 1));
        Channel.Broadcast(Downhost, new Atom(atom.Name, bcst), group.Value);
      }
      if (dest==null || dest==PeerCast.SessionID) {
        Logger.Debug("Processing BCST({0})", dest==null ? "(null)" : dest.Value.ToString("N"));
        foreach (var c in atom.Children) ProcessAtom(c);
      }
    }

    protected virtual void OnPCPHost(Atom atom)
    {
      var session_id = atom.Children.GetHostSessionID();
      if (session_id!=null) {
        var node = Channel.Nodes.FirstOrDefault(x => x.SessionID.Equals(session_id));
        HostBuilder host = new HostBuilder(node);
        if (node==null) {
          host.SessionID = (Guid)session_id;
        }
        host.Extra.Update(atom.Children);
        host.DirectCount = atom.Children.GetHostNumListeners() ?? 0;
        host.RelayCount = atom.Children.GetHostNumRelays() ?? 0;
        var flags1 = atom.Children.GetHostFlags1();
        if (flags1 != null) {
          host.IsFirewalled  = (flags1.Value & PCPHostFlags1.Firewalled) != 0;
          host.IsTracker     = (flags1.Value & PCPHostFlags1.Tracker) != 0;
          host.IsRelayFull   = (flags1.Value & PCPHostFlags1.Relay) == 0;
          host.IsDirectFull  = (flags1.Value & PCPHostFlags1.Direct) == 0;
          host.IsReceiving   = (flags1.Value & PCPHostFlags1.Receiving) != 0;
          host.IsControlFull = (flags1.Value & PCPHostFlags1.ControlIn) == 0;
        }

        var endpoints = atom.Children.GetHostEndPoints();
        if (endpoints.Length>0 && (host.GlobalEndPoint==null || !host.GlobalEndPoint.Equals(endpoints[0]))) {
          host.GlobalEndPoint = endpoints[0];
        }
        if (endpoints.Length>1 && (host.LocalEndPoint==null || !host.LocalEndPoint.Equals(endpoints[1]))) {
          host.LocalEndPoint = endpoints[1];
        }
        Logger.Debug("Updating Node: {0}/{1}({2})", host.GlobalEndPoint, host.LocalEndPoint, host.SessionID.ToString("N"));
        Channel.AddNode(host.ToHost());
        if (Downhost.SessionID==host.SessionID) Downhost = host.ToHost();
      }
    }

    protected virtual void OnPCPQuit(Atom atom)
    {
      Logger.Debug("Quit Received: {0}", atom.GetInt32());
      Stop(StopReason.None);
    }

    protected override void DoStop(OutputStreamBase.StopReason reason)
    {
      switch (reason) {
      case StopReason.None:
        break;
      case StopReason.Any:
        Send(new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT));
        break;
      case StopReason.BadAgentError:
        Send(new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT + Atom.PCP_ERROR_BADAGENT));
        break;
      case StopReason.ConnectionError:
        Send(new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT + Atom.PCP_ERROR_READ));
        break;
      case StopReason.NotIdentifiedError:
        Send(new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT + Atom.PCP_ERROR_NOTIDENTIFIED));
        break;
      case StopReason.UnavailableError:
        Send(new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT + Atom.PCP_ERROR_UNAVAILABLE));
        break;
      case StopReason.OffAir:
        Send(new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT + Atom.PCP_ERROR_OFFAIR));
        break;
      case StopReason.UserShutdown:
        Send(new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT + Atom.PCP_ERROR_SHUTDOWN));
        break;
      }
      base.DoStop(reason);
    }
  }

  [Plugin]
  class PCPOutputStreamPlugin
    : PluginBase
  {
    override public string Name { get { return "PCP Output"; } }

    private PCPOutputStreamFactory factory;
    override protected void OnAttach()
    {
      if (factory==null) factory = new PCPOutputStreamFactory(Application.PeerCast);
      Application.PeerCast.OutputStreamFactories.Add(factory);
    }

    override protected void OnDetach()
    {
      Application.PeerCast.OutputStreamFactories.Remove(factory);
    }
  }
}
