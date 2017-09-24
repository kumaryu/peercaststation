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
using System.Threading;
using System.Threading.Tasks;

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
    public static RelayRequest Read(Stream stream)
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
      AccessControlInfo access_control,
      Guid channel_id,
      byte[] header)
    {
      var request = ParseRequest(header);
      if (request!=null) {
        var channel = this.PeerCast.RequestChannel(channel_id, null, false);
        return new PCPOutputStream(this.PeerCast, input_stream, output_stream, remote_endpoint, access_control, channel, request);
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
      if (request!=null &&
          request.Uri!=null &&
          (request.PCPVersion==PCPVersion.ProtocolVersionIPv4 || request.PCPVersion==PCPVersion.ProtocolVersionIPv6)) {
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
      using (var stream = new MemoryStream(header)) {
        return RelayRequestReader.Read(stream);
      }
    }
  }

  public class PCPOutputStream
    : OutputStreamBase
  {
    public override OutputStreamType OutputStreamType
    {
      get { return OutputStreamType.Relay; }
    }

    public Host Downhost       { get; protected set; }
    public bool IsHandshaked   { get { return Downhost!=null; } }
    public string UserAgent    { get; protected set; }
    public bool IsRelayFull    { get; protected set; }
    public bool IsChannelFound { get; protected set; }
    public bool IsProtocolMatched { get; protected set; }
    private SemaphoreSlim changedEvent = new SemaphoreSlim(1);

    protected override int GetUpstreamRate()
    {
      return Channel.ChannelInfo.Bitrate;
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
      return new ConnectionInfoBuilder {
        ProtocolName     = "PCP Relay",
        Type             = ConnectionType.Relay,
        Status           = status,
        RemoteName       = RemoteEndPoint.ToString(),
        RemoteEndPoint   = (IPEndPoint)RemoteEndPoint,
        RemoteHostStatus = host_status,
        RemoteSessionID  = Downhost?.SessionID,
        ContentPosition  = lastPosition,
        RecvRate         = Connection.ReadRate,
        SendRate         = Connection.WriteRate,
        LocalRelays      = relay_count,
        LocalDirects     = direct_count,
        AgentName        = this.UserAgent ?? "",
      }.Build();
    }

    private RelayRequest relayRequest;

    public PCPOutputStream(
      PeerCast peercast,
      Stream input_stream,
      Stream output_stream,
      EndPoint remote_endpoint,
      AccessControlInfo access_control,
      Channel channel,
      RelayRequest request)
      : base(peercast, input_stream, output_stream, remote_endpoint, access_control, channel, null)
    {
      Logger.Debug("Initialized: Channel {0}, Remote {1}, Request {2} {3} ({4} {5})",
        channel!=null ? channel.ChannelID.ToString("N") : "(null)",
        remote_endpoint,
        request.Uri,
        request.StreamPos,
        request.PCPVersion,
        request.UserAgent);
      this.Connection.ReadTimeout = 180000;
      this.Downhost = null;
      this.UserAgent = request.UserAgent;
      this.IsChannelFound = channel!=null && channel.Status==SourceStreamStatus.Receiving;
      this.IsRelayFull    = channel!=null ? !channel.MakeRelayable(this) : false;
      this.IsProtocolMatched = channel!=null ?
        (channel.Network==NetworkType.IPv6 && request.PCPVersion==PCPVersion.ProtocolVersionIPv6) ||
        (channel.Network==NetworkType.IPv4 && request.PCPVersion==PCPVersion.ProtocolVersionIPv4) : false;
      this.relayRequest   = request;
      this.UserAgent      = request.UserAgent;
    }

    protected string CreateRelayResponse()
    {
      if (!IsChannelFound) {
        return String.Format(
          "HTTP/1.0 404 Not Found.\r\n" +
          "Server: {0}\r\n" +
          "\r\n",
          PeerCast.AgentName);
      }
      else if (!IsProtocolMatched) {
        return String.Format(
          "HTTP/1.0 403 Forbidden.\r\n" +
          "Server: {0}\r\n" +
          "\r\n",
          PeerCast.AgentName);
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

    protected async Task SendRelayResponse(CancellationToken cancel_token)
    {
      var response = CreateRelayResponse();
      await Connection.WriteUTF8Async(response, cancel_token);
      Logger.Debug("SendingRelayResponse: {0}", response);
    }

    private void Channel_ContentChanged(object sender, EventArgs args)
    {
      changedEvent.Release();
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

    long lastPosition = 0;
    private async Task SendRelayBody(CancellationToken cancel_token)
    {
      Content last_header = null;
      Content last_content = null;
      while (!cancel_token.IsCancellationRequested) {
        await changedEvent.WaitAsync(cancel_token);
        bool skipped = false;
        var atoms = Enumerable.Empty<Atom>();
        if (Channel.ContentHeader!=null &&
            (last_header==null || Channel.ContentHeader.Position!=last_header.Position)) {
          last_header = Channel.ContentHeader;
          last_content = null;
          lastPosition = last_header.Position;
          atoms = atoms.Concat(CreateContentHeaderPacket(Channel, last_header));
        }
        if (last_header!=null) {
          Content content;
          if (last_content!=null) {
            content = Channel.Contents.GetNewerContent(last_content, out skipped);
            if (content!=null && skipped) {
              Logger.Error("Content Skipped: serial {0} expected but was {1}",
                last_content.Serial+1,
                content.Serial);
            }
            else if (content!=null && last_content.Position+last_content.Data.LongLength<content.Position) {
              Logger.Info("Content Skipped: position {0} expected but was {1}",
                last_content.Position+last_content.Data.LongLength,
                content.Position);
            }
          }
          else if (relayRequest.StreamPos.HasValue && relayRequest.StreamPos.Value>last_header.Position) {
            content = Channel.Contents.FindNextByPosition(last_header.Stream, relayRequest.StreamPos.Value-1);
            if (content==null || content==Channel.Contents.GetOldest(last_header.Stream)) {
              content = Channel.Contents.GetNewest(last_header.Stream);
            }
          }
          else {
            content = Channel.Contents.GetNewest(last_header.Stream);
          }
          if (content!=null) {
            last_content = content;
            lastPosition = content.Position;
            atoms = atoms.Concat(CreateContentBodyPacket(Channel, content));
          }
        }
        foreach (var atom in atoms) {
          await Connection.WriteAsync(atom, cancel_token);
        }
        if (skipped) {
          Stop(StopReason.SendTimeoutError);
        }
      }
    }

    protected override async Task OnStarted(CancellationToken cancel_token)
    {
      await base.OnStarted(cancel_token);
      if (Channel!=null) {
        Channel.ContentChanged += new EventHandler(Channel_ContentChanged);
        if (Channel.IsBroadcasting) {
          Channel.ChannelInfoChanged  += Channel_ChannelPropertyChanged;
          Channel.ChannelTrackChanged += Channel_ChannelPropertyChanged;
        }
      }
      await SendRelayResponse(cancel_token);
    }

    private void Channel_ChannelPropertyChanged(object sender, EventArgs e)
    {
      Logger.Debug("Broadcasting channel info");
      Channel.Broadcast(null, CreateBroadcastPacket(BroadcastGroup.Relays, CreateChanPacket()), BroadcastGroup.Relays);
    }

    private Atom CreateBroadcastPacket(BroadcastGroup group, Atom packet)
    {
      var bcst = new AtomCollection();
      bcst.SetBcstFrom(PeerCast.SessionID);
      bcst.SetBcstGroup(group);
      bcst.SetBcstHops(0);
      bcst.SetBcstTTL(11);
      PCPVersion.SetBcstVersion(bcst);
      bcst.SetBcstChannelID(Channel.ChannelID);
      bcst.Add(packet);
      return new Atom(Atom.PCP_BCST, bcst);
    }

    private Atom CreateChanPacket()
    {
      var chan = new AtomCollection();
      chan.SetChanID(Channel.ChannelID);
      chan.SetChanInfo(Channel.ChannelInfo.Extra);
      chan.SetChanTrack(Channel.ChannelTrack.Extra);
      return new Atom(Atom.PCP_CHAN, chan);
    }

    private async Task ReadAndProcessAtom(CancellationToken cancel_token)
    {
      while (!cancel_token.IsCancellationRequested) {
        var atom = await Connection.ReadAtomAsync(cancel_token);
        await ProcessAtom(atom, cancel_token);
      }
    }

    protected override async Task<StopReason> DoProcess(CancellationToken cancel_token)
    {
      if (!IsChannelFound || !IsProtocolMatched) return StopReason.None;
      try {
        var handshake_timeout = new CancellationTokenSource(5000);
        var unified_cancel = CancellationTokenSource.CreateLinkedTokenSource(cancel_token, handshake_timeout.Token);
        while (!IsHandshaked) {
          //Handshakeが5秒以内に完了しなければ切る
          //HELOでセッションIDを受け取るまでは他のパケットは無視
          try {
            var atom = await Connection.ReadAtomAsync(unified_cancel.Token);
            if (atom.Name==Atom.PCP_HELO) {
              await OnPCPHelo(atom, unified_cancel.Token);
            }
          }
          catch (OperationCanceledException) {
            if (handshake_timeout.IsCancellationRequested) {
              Logger.Info("Handshake timed out.");
              return StopReason.BadAgentError;
            }
            else {
              throw;
            }
          }
        }
        if (IsRelayFull) {
          return StopReason.UnavailableError;
        }
        else {
          await Task.WhenAll(
            ReadAndProcessAtom(cancel_token),
            SendRelayBody(cancel_token)
          );
        }
        return StopReason.OffAir;
      }
      catch (InvalidDataException e) {
        await OnError(e, cancel_token);
        return StopReason.NotIdentifiedError;
      }
      catch (IOException e) {
        await OnError(e, cancel_token);
        return StopReason.ConnectionError;
      }
    }

    protected override Task OnStopped(CancellationToken cancel_token)
    {
      switch (StoppedReason) {
      case StopReason.None:
        break;
      case StopReason.Any:
        Connection.WriteAsync(new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT));
        break;
      case StopReason.SendTimeoutError:
        Connection.WriteAsync(new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT + Atom.PCP_ERROR_SKIP));
        break;
      case StopReason.BadAgentError:
        Connection.WriteAsync(new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT + Atom.PCP_ERROR_BADAGENT));
        break;
      case StopReason.ConnectionError:
        Connection.WriteAsync(new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT + Atom.PCP_ERROR_READ));
        break;
      case StopReason.NotIdentifiedError:
        Connection.WriteAsync(new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT + Atom.PCP_ERROR_NOTIDENTIFIED));
        break;
      case StopReason.UnavailableError:
        {
          //次に接続するホストを送ってQUIT
          foreach (var node in SelectSourceHosts((IPEndPoint)RemoteEndPoint)) {
            if (Downhost!=null && Downhost.SessionID==node.SessionID) continue;
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
            Connection.WriteAsync(new Atom(Atom.PCP_HOST, host_atom));
            Logger.Debug("Sending Node: {0}({1})", globalendpoint, node.SessionID.ToString("N"));
          }
        }
        Connection.WriteAsync(new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT + Atom.PCP_ERROR_UNAVAILABLE));
        break;
      case StopReason.OffAir:
        Connection.WriteAsync(new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT + Atom.PCP_ERROR_OFFAIR));
        break;
      case StopReason.UserShutdown:
        Connection.WriteAsync(new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT + Atom.PCP_ERROR_SHUTDOWN));
        break;
      }

      if (Channel!=null) {
        Channel.ContentChanged -= Channel_ContentChanged;
        Channel.ChannelInfoChanged  -= Channel_ChannelPropertyChanged;
        Channel.ChannelTrackChanged -= Channel_ChannelPropertyChanged;
      }
      return base.OnStopped(cancel_token);
    }

    protected override async Task DoPost(Host from, Atom packet, CancellationToken cancel_token)
    {
      if (Downhost!=null && Downhost!=from) {
        await Connection.WriteAsync(packet, cancel_token);
      }
    }

    private async Task ProcessAtom(Atom atom, CancellationToken cancel_token)
    {
           if (atom.Name==Atom.PCP_HELO)       await OnPCPHelo(atom, cancel_token);
      else if (atom.Name==Atom.PCP_OLEH)       await OnPCPOleh(atom, cancel_token);
      else if (atom.Name==Atom.PCP_OK)         await OnPCPOk(atom, cancel_token);
      else if (atom.Name==Atom.PCP_CHAN)       await OnPCPChan(atom, cancel_token);
      else if (atom.Name==Atom.PCP_CHAN_PKT)   await OnPCPChanPkt(atom, cancel_token);
      else if (atom.Name==Atom.PCP_CHAN_INFO)  await OnPCPChanInfo(atom, cancel_token);
      else if (atom.Name==Atom.PCP_CHAN_TRACK) await OnPCPChanTrack(atom, cancel_token);
      else if (atom.Name==Atom.PCP_BCST)       await OnPCPBcst(atom, cancel_token);
      else if (atom.Name==Atom.PCP_HOST)       await OnPCPHost(atom, cancel_token);
      else if (atom.Name==Atom.PCP_QUIT)       await OnPCPQuit(atom, cancel_token);
    }

    private async Task<bool> PingHost(IPEndPoint target, Guid remote_session_id, CancellationToken cancel_token)
    {
      Logger.Debug("Ping requested. Try to ping: {0}({1})", target, remote_session_id);
      bool result = false;
      try {
        var client = new System.Net.Sockets.TcpClient(target.AddressFamily);
        client.ReceiveTimeout = 2000;
        client.SendTimeout    = 2000;
        await client.ConnectAsync(target.Address, target.Port);
        var stream = client.GetStream();
        await stream.WriteAsync(new Atom(Atom.PCP_CONNECT, 1), cancel_token);
        var helo = new AtomCollection();
        helo.SetHeloSessionID(PeerCast.SessionID);
        await stream.WriteAsync(new Atom(Atom.PCP_HELO, helo), cancel_token);
        var res = await stream.ReadAtomAsync(cancel_token);
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
        await stream.WriteAsync(new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT), cancel_token);
        stream.Close();
        client.Close();
      }
      catch (InvalidDataException e) {
        Logger.Debug("Ping failed");
        Logger.Debug(e);
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
      return !address.IsSiteLocal();
    }

    private IEnumerable<Host> SelectSourceHosts(IPEndPoint endpoint)
    {
      var rnd = new Random();
      return Channel.Nodes.OrderByDescending(n =>
        ( n.GlobalEndPoint!=null ? 16000 : 0) +
        ( n.GlobalEndPoint!=null &&
          n.GlobalEndPoint.Address.Equals(endpoint.Address) ? 8000 : 0) +
        (!n.IsRelayFull ? 4000 : 0) +
        ( n.IsReceiving ? 2000 : 0) +
        (Math.Max(10-n.Hops, 0)*100) +
        (n.RelayCount*10) +
        rnd.NextDouble()
      ).Take(8);
    }

    private async Task OnPCPHelo(Atom atom, CancellationToken cancel_token)
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
              await PingHost(new IPEndPoint(((IPEndPoint)RemoteEndPoint).Address, ping.Value), session_id.Value, cancel_token)) {
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
        var user_agent = atom.Children.GetHeloAgent();
        if (user_agent!=null) {
          UserAgent = user_agent;
        }
      }
      var oleh = new AtomCollection();
      if (RemoteEndPoint!=null && RemoteEndPoint.AddressFamily==Channel.NetworkAddressFamily) {
        oleh.SetHeloRemoteIP(((IPEndPoint)RemoteEndPoint).Address);
      }
      oleh.SetHeloAgent(PeerCast.AgentName);
      oleh.SetHeloSessionID(PeerCast.SessionID);
      oleh.SetHeloRemotePort(remote_port);
      PCPVersion.SetHeloVersion(oleh);
      await Connection.WriteAsync(new Atom(Atom.PCP_OLEH, oleh), cancel_token);
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
        Stop(StopReason.UnavailableError);
      }
      else {
        Logger.Debug("Handshake succeeded {0}({1})", Downhost.GlobalEndPoint, Downhost.SessionID.ToString("N"));
        await Connection.WriteAsync(new Atom(Atom.PCP_OK, (int)1), cancel_token);
      }
    }

    private Task OnPCPOleh(Atom atom, CancellationToken cancel_token)
    {
      return Task.Delay(0);
    }

    private Task OnPCPOk(Atom atom, CancellationToken cancel_token)
    {
      return Task.Delay(0);
    }

    private Task OnPCPChan(Atom atom, CancellationToken cancel_token)
    {
      return Task.Delay(0);
    }

    private Task OnPCPChanPkt(Atom atom, CancellationToken cancel_token)
    {
      return Task.Delay(0);
    }

    private Task OnPCPChanInfo(Atom atom, CancellationToken cancel_token)
    {
      return Task.Delay(0);
    }

    private Task OnPCPChanTrack(Atom atom, CancellationToken cancel_token)
    {
      return Task.Delay(0);
    }

    private async Task OnPCPBcst(Atom atom, CancellationToken cancel_token)
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
        foreach (var c in atom.Children) await ProcessAtom(c, cancel_token);
      }
    }

    private Task OnPCPHost(Atom atom, CancellationToken cancel_token)
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
      return Task.Delay(0);
    }

    private Task OnPCPQuit(Atom atom, CancellationToken cancel_token)
    {
      Logger.Debug("Quit Received: {0}", atom.GetInt32());
      Stop(StopReason.None);
      return Task.Delay(0);
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
