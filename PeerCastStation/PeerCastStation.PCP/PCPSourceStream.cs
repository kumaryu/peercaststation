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
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PeerCastStation.Core;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace PeerCastStation.PCP
{
  public class PCPSourceStreamFactory
    : SourceStreamFactoryBase
  {
    public PCPSourceStreamFactory(PeerCast peercast)
      : base(peercast)
    {
    }

    public override string Name { get { return "pcp"; } }
    public override string Scheme { get { return "pcp"; } }

    public override SourceStreamType Type {
      get { return SourceStreamType.Relay; }
    }

    public override Uri? DefaultUri {
      get { return null; }
    }

    public override bool IsContentReaderRequired {
      get { return false; }
    }

    //ハンドシェイク終了まで一定時間で終わらなかったらタイムアウトする
    //PeerCastのポート開放チェックが最悪15秒かかるのでそれより短くはしないこと
    public int PCPHandshakeTimeout { get; set; } = 18000;

    public override ISourceStream Create(Channel channel, Uri tracker)
    {
      var strm = new PCPSourceStream(PeerCast, channel, tracker);
      strm.PCPHandshakeTimeout = PCPHandshakeTimeout;
      return strm;
    }
  }

  public class RelayRequestResponse
  {
    public int     StatusCode  { get; set; }
    public int?    PCPVersion  { get; set; }
    public string? ContentType { get; set; }
    public long?   StreamPos   { get; set; }
    public string? Server      { get; set; }
    public RelayRequestResponse(IEnumerable<string> responses)
    {
      this.PCPVersion = null;
      this.ContentType = null;
      this.StreamPos = null;
      foreach (var res in responses) {
        Match match;
        if ((match = Regex.Match(res, @"^HTTP/1.\d (\d+) .*$")).Success) {
          this.StatusCode = Convert.ToInt32(match.Groups[1].Value);
        }
        if ((match = Regex.Match(res, @"Content-Type:\s*(\S+)\s*$")).Success) {
          this.ContentType = match.Groups[1].Value;
        }
        if ((match = Regex.Match(res, @"x-peercast-pcp:\s*(\d+)\s*$")).Success) {
          this.PCPVersion = Convert.ToInt32(match.Groups[1].Value);
        }
        if ((match = Regex.Match(res, @"x-peercast-pos:\s*(\d+)\s*$")).Success) {
          this.StreamPos = Convert.ToInt64(match.Groups[1].Value);
        }
        if ((match = Regex.Match(res, @"Server:\s*(.*)\s*$")).Success) {
          this.Server = match.Groups[1].Value;
        }
      }
    }
  }

  public class PCPSourceConnection
    : SourceConnectionBase,
      IChannelMonitor
  {
    private TcpClient? client = null;
    private RelayRequestResponse? relayResponse = null;
    private Host? uphost = null;
    private RemoteHostStatus remoteType = RemoteHostStatus.None;
    private EndPoint? remoteHost = null;
    private IContentSink contentSink;
    private Content?     lastHeader = null;
    private ChannelInfo lastInfo;
    private OutputListener? listener = null;

    public PCPSourceConnection(
        PeerCast peercast,
        Channel  channel,
        Uri      source_uri,
        RemoteHostStatus remote_type)
      : base(peercast, channel, source_uri)
    {
      remoteType = remote_type;
      contentSink = new AsynchronousContentSink(new ChannelContentSink(channel, true));
      lastInfo = Channel.ChannelInfo;
    }

    protected override void OnStarted(SourceConnectionClient connection)
    {
      Logger.Debug("Started");
      listener = PeerCast.FindListener(
        connection.RemoteEndPoint?.Address,
        OutputStreamType.Relay | OutputStreamType.Metadata);
      lastInfo = Channel.ChannelInfo;
      lastHeader = Channel.ContentHeader;
      Channel.AddMonitor(this);
      base.OnStarted(connection);
    }

    protected override void OnStopped()
    {
      Channel.RemoveMonitor(this);
      Logger.Debug("Finished");
      base.OnStopped();
    }

    protected override async Task<SourceConnectionClient?> DoConnect(Uri source, CancellationToken cancel_token)
    {
      TcpClient? client = null;
      try {
        var port = source.Port<0 ? PCPVersion.DefaultPort : source.Port;
        if (source.HostNameType==UriHostNameType.IPv4 ||
            source.HostNameType==UriHostNameType.IPv6) {
          var addr = IPAddress.Parse(source.Host);
          client = new TcpClient(addr.AddressFamily);
          using (var cs = CancellationTokenSource.CreateLinkedTokenSource(cancel_token)) {
            cs.CancelAfter(3000);
            var task = await Task.WhenAny(
              cs.Token.CreateCancelTask(),
              client.ConnectAsync(addr, port)
            ).ConfigureAwait(false);
            await task.ConfigureAwait(false);
          }
        }
        else {
          client = new TcpClient(Channel.NetworkAddressFamily);
          using (var cs = CancellationTokenSource.CreateLinkedTokenSource(cancel_token)) {
            cs.CancelAfter(3000);
            var task = await Task.WhenAny(
              cs.Token.CreateCancelTask(),
              client.ConnectAsync(source.DnsSafeHost, port)
            ).ConfigureAwait(false);
            await task.ConfigureAwait(false);
          }
        }
        var connection = new SourceConnectionClient(client);
        connection.Stream.ReadTimeout  = 30000;
        connection.Stream.WriteTimeout = 8000;
        remoteHost = new DnsEndPoint(source.Host, port);
        Logger.Debug("Connected: {0}", source);
        this.client = client;
        return connection;
      }
      catch (OperationCanceledException) {
        client?.Close();
        Logger.Debug("Connection Cancelled: {0}", source);
        return null;
      }
      catch (SocketException e) {
        Logger.Debug("Connection Failed: {0}", source);
        Logger.Debug(e);
        return null;
      }
    }

    protected override void DoPost(SourceConnectionClient connection, Host? from, Atom packet)
    {
      if (uphost==from) return;
      if (postedAtoms.TryAdd(packet)) {
        postedAtomsEvent.Release();
      }
    }

    private BlockingCollection<Atom> postedAtoms = new BlockingCollection<Atom>(110);
    private SemaphoreSlim postedAtomsEvent = new SemaphoreSlim(0);
    private async Task ProcessPost(SourceConnectionClient connection, CancellationToken cancel_token)
    {
      while (!cancel_token.IsCancellationRequested) {
        int delay = 0;
        await postedAtomsEvent.WaitAsync(cancel_token).ConfigureAwait(false);
        while (postedAtoms.TryTake(out var atom) && !cancel_token.IsCancellationRequested) {
          await Task.Delay(delay).ConfigureAwait(false);
          delay = Math.Min(delay+10, 90);
          try {
            await connection.Stream.WriteAsync(atom, cancel_token).ConfigureAwait(false);
          }
          catch (IOException e) {
            Logger.Info(e);
            Stop(StopReason.ConnectionError);
          }
          catch (Exception e) {
            Logger.Info(e);
          }
        }
      }
    }

    //ハンドシェイク終了まで一定時間で終わらなかったらタイムアウトする
    //PeerCastのポート開放チェックが最悪15秒かかるのでそれより短くはしないこと
    public int PCPHandshakeTimeout { get; set; } = 18000;
    protected override async Task DoProcess(SourceConnectionClient connection, CancellationToken cancel_token)
    {
      try {
        this.Status = ConnectionStatus.Connecting;
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancel_token);
        cts.CancelAfter(PCPHandshakeTimeout);
        var relayResponse = await ProcessRelayRequest(connection, cts.Token).ConfigureAwait(false);
        if (relayResponse==null || cts.IsCancellationRequested) goto Stopped;
        await ProcessHandshake(connection, cts.Token).ConfigureAwait(false);
        if (cts.IsCancellationRequested) goto Stopped;
        if (relayResponse.StatusCode==503) {
          await ProcessHosts(connection, cts.Token).ConfigureAwait(false);
        }
        if (relayResponse.StatusCode!=503) {
          this.Status = ConnectionStatus.Connected;
          await Task.WhenAll(
            ProcessBody(connection, cancel_token),
            ProcessPost(connection, cancel_token)
          ).ConfigureAwait(false);
        }
      }
      catch (OperationCanceledException) {
      }
Stopped:
      Logger.Debug("Disconnected");
    }

    private async Task<RelayRequestResponse> ReadRequestResponseAsync(Stream stream, CancellationToken cancel_token)
    {
      string? line = null;
      var responses = new List<string>();
      var buf = new List<byte>();
      while (line!="") {
        var value = await stream.ReadByteAsync(cancel_token).ConfigureAwait(false);
        if (value<0) throw new IOException();
        buf.Add((byte)value);
        if (buf.Count>=2 && buf[buf.Count-2] == '\r' && buf[buf.Count-1] == '\n') {
          line = System.Text.Encoding.UTF8.GetString(buf.ToArray(), 0, buf.Count - 2);
          if (line!="") responses.Add(line);
          buf.Clear();
        }
      }
      return new RelayRequestResponse(responses);
    }

    private async Task<RelayRequestResponse?> ProcessRelayRequest(SourceConnectionClient connection, CancellationToken cancel_token)
    {
      Logger.Debug("Sending Relay request: /channel/{0}", Channel.ChannelID.ToString("N"));
      var host_header = remoteHost!=null ? $"Host:{remoteHost}\r\n" : "";
      if (remoteHost is DnsEndPoint dnsendpoint) {
        host_header = $"Host:{dnsendpoint.Host}:{dnsendpoint.Port}\r\n";
      }
      var req = System.Text.Encoding.UTF8.GetBytes(
        $"GET /channel/{Channel.ChannelID.ToString("N")} HTTP/1.0\r\n" +
        host_header +
        $"User-Agent:{PeerCast.AgentName}\r\n" +
        $"x-peercast-pcp:{Channel.GetPCPVersion()}\r\n" +
        $"x-peercast-pos:{Channel.ContentPosition}\r\n" +
        $"\r\n"
      );
      try {
        await connection.Stream.WriteAsync(req, cancel_token).ConfigureAwait(false);
        relayResponse = await ReadRequestResponseAsync(connection.Stream, cancel_token).ConfigureAwait(false);
        Logger.Debug("Relay response: {0}", relayResponse.StatusCode);
        if (relayResponse.StatusCode==200 || relayResponse.StatusCode==503) {
          return relayResponse;
        }
        else {
          Logger.Info("Server responses {0} to GET {1}", relayResponse.StatusCode, SourceUri.PathAndQuery);
          Stop(relayResponse.StatusCode==404 ? StopReason.OffAir : StopReason.ConnectionError);
        }
        return relayResponse;
      }
      catch (IOException e) {
        Logger.Info(e);
        Stop(StopReason.ConnectionError);
        return null;
      }
    }

    private Atom CreatePCPHelo()
    {
      var helo = new AtomCollection();
      helo.SetHeloAgent(PeerCast.AgentName);
      helo.SetHeloSessionID(PeerCast.SessionID);
      switch (listener?.Status ?? PortStatus.Firewalled) {
      case PortStatus.Open:
        helo.SetHeloPort(listener!.LocalEndPoint.Port);
        break;
      case PortStatus.Firewalled:
        break;
      case PortStatus.Unknown:
        helo.SetHeloPing(listener!.LocalEndPoint.Port);
        break;
      }
      PCPVersion.SetHeloVersion(helo);
      return new Atom(Atom.PCP_HELO, helo);
    }

    private async Task ProcessHandshake(SourceConnectionClient connection, CancellationToken cancel_token)
    {
      Logger.Debug("Handshake Started");
      var helo = CreatePCPHelo();
      try {
        await connection.Stream.WriteAsync(helo, cancel_token).ConfigureAwait(false);
        var handshake_finished = false;
        while (!handshake_finished) {
          cancel_token.ThrowIfCancellationRequested();
          var atom = await connection.Stream.ReadAtomAsync(cancel_token).ConfigureAwait(false);
          if (atom.Name==Atom.PCP_OLEH) {
            OnPCPOleh(connection, atom);
            Logger.Debug("Handshake Finished");
            handshake_finished = true;
          }
          else if (atom.Name==Atom.PCP_OLEH) {
            OnPCPQuit(atom);
          }
          else {
            //Ignore other packets
          }
        }
      }
      catch (InvalidDataException e) {
        Logger.Error(e);
        Stop(StopReason.ConnectionError);
      }
      catch (IOException e) {
        Logger.Info(e);
        Stop(StopReason.ConnectionError);
      }
    }

    private async Task ProcessBody(SourceConnectionClient connection, CancellationToken cancel_token)
    {
      try {
        BroadcastHostInfo();
        try {
          while (!cancel_token.IsCancellationRequested) {
            if (CheckHostInfoUpdate()) {
              BroadcastHostInfo();
            }
            var atom = await connection.Stream.ReadAtomAsync(cancel_token).ConfigureAwait(false);
            ProcessAtom(atom);
          }
        }
        catch (OperationCanceledException) {
        }
        try {
          using (var cts=new CancellationTokenSource(3000)) {
            //QUIT送るのに3秒でタイムアウトする
            //cancel_tokenはキャンセルされているので使わない
            await SendQuit(connection.Stream, StopReason.UserShutdown, cts.Token).ConfigureAwait(false);
          }
        }
        catch (OperationCanceledException) {
        }
      }
      catch (InvalidDataException e) {
        Logger.Error(e);
        Stop(StopReason.ConnectionError);
      }
      catch (IOException e) {
        Logger.Info(e);
        Stop(StopReason.ConnectionError);
      }
      catch (Exception e) {
        Logger.Error(e);
        Stop(StopReason.NotIdentifiedError);
      }
    }

    private static readonly ID4[] hostPackets = new []{ Atom.PCP_QUIT, Atom.PCP_HOST };
    private async Task ProcessHosts(SourceConnectionClient connection, CancellationToken cancel_token)
    {
      try {
        while (!cancel_token.IsCancellationRequested) {
          var atom = await connection.Stream.ReadAtomAsync(cancel_token).ConfigureAwait(false);
          ProcessAtom(atom, hostPackets);
        }
      }
      catch (InvalidDataException e) {
        Logger.Error(e);
        Stop(StopReason.ConnectionError);
      }
      catch (IOException e) {
        Logger.Info(e);
        Stop(StopReason.ConnectionError);
      }
    }

    /// <summary>
    /// 現在のチャンネルとPeerCastの状態からHostパケットを作ります
    /// </summary>
    /// <returns>作ったPCP_HOSTパケット</returns>
    private Atom CreatePCPHOST(SourceConnectionClient connection)
    {
      var host = new AtomCollection();
      host.SetHostChannelID(Channel.ChannelID);
      host.SetHostSessionID(PeerCast.SessionID);
      var globalendpoint = listener?.GlobalEndPoint;
      if (globalendpoint!=null) {
        host.AddHostIP(globalendpoint.Address);
        host.AddHostPort(globalendpoint.Port);
      }
      var localendpoint = listener?.LocalEndPoint;
      if (localendpoint!=null) {
        host.AddHostIP(localendpoint.Address);
        host.AddHostPort(localendpoint.Port);
      }
      host.SetHostNumListeners(Channel.LocalDirects);
      host.SetHostNumRelays(Channel.LocalRelays);
      host.SetHostUptime(Channel.Uptime);
      var oldest = Channel.Contents.Oldest;
      if (oldest!=null) {
        host.SetHostOldPos((uint)(oldest.Position & 0xFFFFFFFFU));
      }
      var newest = Channel.Contents.Newest;
      if (newest!=null) {
        host.SetHostNewPos((uint)(newest.Position & 0xFFFFFFFFU));
      }
      PCPVersion.SetHostVersion(host);
      host.SetHostFlags1(
        (Channel.IsRelayable(false) ? PCPHostFlags1.Relay : 0) |
        (Channel.IsPlayable(false) ? PCPHostFlags1.Direct : 0) |
        (listener?.Status!=PortStatus.Open ? PCPHostFlags1.Firewalled : 0) |
        (RecvRate>0 ? PCPHostFlags1.Receiving : 0));
      if (connection.RemoteEndPoint!=null) {
        host.SetHostUphostIP(connection.RemoteEndPoint.Address);
        host.SetHostUphostPort(connection.RemoteEndPoint.Port);
      }
      return new Atom(Atom.PCP_HOST, host);
    }

    /// <summary>
    /// 指定したパケットを含むブロードキャストパケットを作成します
    /// </summary>
    /// <param name="group">配送先グループ</param>
    /// <param name="packet">配送するパケット</param>
    /// <returns>作成したPCP_BCSTパケット</returns>
    private Atom CreatePCPBCST(BroadcastGroup group, Atom packet)
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

    class LocalHostInfo
    {
      public Timestamp Timestamp;
      public int LocalDirects;
      public int LocalRelays;
    }
    LocalHostInfo lastHostInfo = new LocalHostInfo();

    private bool CheckHostInfoUpdate()
    {
      return lastHostInfo.LocalDirects!=Channel.LocalDirects ||
             lastHostInfo.LocalRelays!=Channel.LocalRelays ||
             (Timestamp.Now-lastHostInfo.Timestamp).TotalMilliseconds>=120000;
    }

    private void BroadcastHostInfo()
    {
      if (connection==null) return;
      Channel.Broadcast(null, CreatePCPBCST(BroadcastGroup.Trackers, CreatePCPHOST(connection)), BroadcastGroup.Trackers);
      var hostInfo = new LocalHostInfo {
        Timestamp = Timestamp.Now,
        LocalDirects = Channel.LocalDirects,
        LocalRelays = Channel.LocalRelays,
      };
      lastHostInfo = hostInfo;
    }

    protected bool ProcessAtom(Atom atom)
    {
      if (atom==null) return true;
      else if (atom.Name==Atom.PCP_OK)         { OnPCPOk(atom);        return true; }
      else if (atom.Name==Atom.PCP_CHAN)       { OnPCPChan(atom);      return true; }
      else if (atom.Name==Atom.PCP_CHAN_PKT)   { OnPCPChanPkt(atom);   return true; }
      else if (atom.Name==Atom.PCP_CHAN_INFO)  { OnPCPChanInfo(atom);  return true; }
      else if (atom.Name==Atom.PCP_CHAN_TRACK) { OnPCPChanTrack(atom); return true; }
      else if (atom.Name==Atom.PCP_BCST)       { OnPCPBcst(atom);      return true; }
      else if (atom.Name==Atom.PCP_HOST)       { OnPCPHost(atom);      return true; }
      else if (atom.Name==Atom.PCP_QUIT)       { OnPCPQuit(atom);      return false; }
      return true;
    }

    protected bool ProcessAtom(Atom atom, ID4[] accepts)
    {
      if (atom==null) return true;
      else if (!accepts.Contains(atom.Name)) return true;
      else if (atom.Name==Atom.PCP_OK)         { OnPCPOk(atom);        return true; }
      else if (atom.Name==Atom.PCP_CHAN)       { OnPCPChan(atom);      return true; }
      else if (atom.Name==Atom.PCP_CHAN_PKT)   { OnPCPChanPkt(atom);   return true; }
      else if (atom.Name==Atom.PCP_CHAN_INFO)  { OnPCPChanInfo(atom);  return true; }
      else if (atom.Name==Atom.PCP_CHAN_TRACK) { OnPCPChanTrack(atom); return true; }
      else if (atom.Name==Atom.PCP_BCST)       { OnPCPBcst(atom);      return true; }
      else if (atom.Name==Atom.PCP_HOST)       { OnPCPHost(atom);      return true; }
      else if (atom.Name==Atom.PCP_QUIT)       { OnPCPQuit(atom);      return false; }
      return true;
    }

    protected void OnPCPOleh(SourceConnectionClient connection, Atom atom)
    {
      if (atom.Children==null) {
        throw new InvalidDataException($"{atom.Name} has no children.");
      }
      var rip = atom.Children.GetHeloRemoteIP();
      if (rip!=null && Channel.NetworkAddressFamily==rip.AddressFamily) {
        var global_addr = listener?.GlobalAddress;
        if (global_addr==null ||
            global_addr.GetAddressLocality()<=rip.GetAddressLocality()) {
          if (listener!=null) {
            listener.GlobalAddress = rip;
          }
        }
      }
      var port = atom.Children.GetHeloPort();
      if (port.HasValue && listener!=null) {
        listener.Status = port.Value!=0 ? PortStatus.Open : PortStatus.Firewalled;
      }
      var sid = atom.Children.GetHeloSessionID();
      if (sid.HasValue) {
        var host = new HostBuilder();
        host.SessionID      = sid.Value;
        host.GlobalEndPoint = connection.RemoteEndPoint;
        uphost = host.ToHost();
      }
    }

    protected void OnPCPOk(Atom atom)
    {
    }

    protected void OnPCPChan(Atom atom)
    {
      if (atom.Children==null) {
        throw new InvalidDataException($"{atom.Name} has no children.");
      }
      foreach (var c in atom.Children) {
        ProcessAtom(c);
      }
    }

    private ChannelInfo ResetContentType(ChannelInfo channel_info, Content content_header)
    {
      var content_type = channel_info.ContentType;
      if (content_type==null || content_type=="UNKNOWN") {
        var (found, contentType, mimeType) =
          PeerCast.ContentReaderFactories
          .Select(factory => (factory.TryParseContentType(content_header.Data.ToArray(), out var ctype, out var mtype), ctype, mtype))
          .FirstOrDefault(v => v.Item1, (false, "", ""));
        if (found) {
          var new_info = new AtomCollection(channel_info.Extra);
          if (contentType!=null) {
            new_info.SetChanInfoType(contentType);
          }
          if (mimeType!=null) {
            new_info.SetChanInfoStreamType(mimeType);
          }
          channel_info = new ChannelInfo(new_info);
        }
      }
      return channel_info;
    }

    private int streamIndex = -1;
    private DateTime streamOrigin;
    private long     lastPosition = 0;
    protected void OnPCPChanPkt(Atom atom)
    {
      if (atom.Children==null) {
        throw new InvalidDataException($"{atom.Name} has no children.");
      }
      var pkt_type = atom.Children.GetChanPktType();
      var pkt_data = atom.Children.GetChanPktData();
      var pkt_cont = atom.Children.GetChanPktCont();
      if (pkt_type!=null && pkt_data!=null) {
        if (pkt_type==Atom.PCP_CHAN_PKT_TYPE_HEAD) {
          long pkt_pos = atom.Children.GetChanPktPos() ?? 0;
          streamIndex = Channel.GenerateStreamID();
          streamOrigin = DateTime.Now;
          var header = new Content(streamIndex, TimeSpan.Zero, pkt_pos, pkt_data, pkt_cont);
          var info   = ResetContentType(lastInfo, header);
          contentSink.OnContentHeader(header);
          contentSink.OnChannelInfo(info);
          lastHeader = header;
          lastInfo   = info;
          lastPosition = pkt_pos;
        }
        else if (pkt_type==Atom.PCP_CHAN_PKT_TYPE_DATA) {
          var pkt_pos = atom.Children.GetChanPktPos();
          if (pkt_pos!=null) {
            contentSink.OnContent(new Content(streamIndex, DateTime.Now-streamOrigin, pkt_pos.Value, pkt_data, pkt_cont));
            lastPosition = pkt_pos.Value;
          }
        }
        else if (pkt_type==Atom.PCP_CHAN_PKT_TYPE_META) {
        }
      }
    }

    protected void OnPCPChanInfo(Atom atom)
    {
      if (atom.Children==null) {
        throw new InvalidDataException($"{atom.Name} has no children.");
      }
      var channel_info = new ChannelInfo(atom.Children);
      if (lastHeader!=null) {
        channel_info = ResetContentType(channel_info, lastHeader);
      }
      contentSink.OnChannelInfo(channel_info);
      lastInfo = channel_info;
      BroadcastHostInfo();
    }

    protected void OnPCPChanTrack(Atom atom)
    {
      if (atom.Children==null) {
        throw new InvalidDataException($"{atom.Name} has no children.");
      }
      contentSink.OnChannelTrack(new ChannelTrack(atom.Children));
    }

    protected void OnPCPBcst(Atom atom)
    {
      if (atom.Children==null) {
        throw new InvalidDataException($"{atom.Name} has no children.");
      }
      var dest = atom.Children.GetBcstDest();
      if (dest==null || dest==PeerCast.SessionID) {
        foreach (var c in atom.Children) ProcessAtom(c);
      }
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
        var bcst = new AtomCollection(atom.Children);
        bcst.SetBcstTTL((byte)(ttl - 1));
        bcst.SetBcstHops((byte)(hops + 1));
        Channel.Broadcast(uphost, new Atom(atom.Name, bcst), group.Value);
      }
    }

    private void OnPCPHost(Atom atom)
    {
      if (atom.Children==null) {
        throw new InvalidDataException($"{atom.Name} has no children.");
      }
      var session_id = atom.Children.GetHostSessionID();
      if (session_id!=null) {
        var node = Channel.SourceNodes.FirstOrDefault(x => x.SessionID.Equals(session_id));
        var host = new HostBuilder(node);
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
        Channel.AddSourceNode(host.ToHost());
      }
    }

    protected void OnPCPQuit(Atom atom)
    {
      if (atom.GetInt32()==Atom.PCP_ERROR_QUIT+Atom.PCP_ERROR_UNAVAILABLE) {
        Stop(StopReason.UnavailableError);
      }
      else {
        Stop(StopReason.OffAir);
      }
    }

    private async Task SendQuit(Stream stream, StopReason code, CancellationToken cancellationToken)
    {
      switch (code) {
      case StopReason.None:
        break;
      case StopReason.Any:
        await stream.WriteAsync(new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT), cancellationToken).ConfigureAwait(false);
        break;
      case StopReason.SendTimeoutError:
        await stream.WriteAsync(new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT + Atom.PCP_ERROR_SKIP), cancellationToken).ConfigureAwait(false);
        break;
      case StopReason.BadAgentError:
        await stream.WriteAsync(new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT + Atom.PCP_ERROR_BADAGENT), cancellationToken).ConfigureAwait(false);
        break;
      case StopReason.ConnectionError:
        await stream.WriteAsync(new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT + Atom.PCP_ERROR_READ), cancellationToken).ConfigureAwait(false);
        break;
      case StopReason.NotIdentifiedError:
        await stream.WriteAsync(new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT + Atom.PCP_ERROR_NOTIDENTIFIED), cancellationToken).ConfigureAwait(false);
        break;
      case StopReason.UnavailableError:
        await stream.WriteAsync(new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT + Atom.PCP_ERROR_UNAVAILABLE), cancellationToken).ConfigureAwait(false);
        break;
      case StopReason.OffAir:
        await stream.WriteAsync(new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT + Atom.PCP_ERROR_OFFAIR), cancellationToken).ConfigureAwait(false);
        break;
      case StopReason.UserShutdown:
        await stream.WriteAsync(new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT + Atom.PCP_ERROR_SHUTDOWN), cancellationToken).ConfigureAwait(false);
        break;
      }
    }

    public override ConnectionInfo GetConnectionInfo()
    {
      var server_name = "";
      if (relayResponse!=null && relayResponse.Server!=null) {
        server_name = relayResponse.Server;
      }
      var remote = remoteType;
      var remote_endpoint = connection!=null ? connection.RemoteEndPoint : null;
      if (remote_endpoint!=null && remote_endpoint.Address.IsSiteLocal()) remote |= RemoteHostStatus.Local;
      var remote_name = String.Format(
        "{0}:{1}",
        SourceUri.Host,
        SourceUri.IsDefaultPort ? PCPVersion.DefaultPort : SourceUri.Port);
      return new ConnectionInfoBuilder {
        ProtocolName     = "PCP Source",
        Type             = ConnectionType.Source,
        Status           = this.Status,
        RemoteName       = remote_name,
        RemoteEndPoint   = remote_endpoint,
        RemoteHostStatus = remote,
        RemoteSessionID  = uphost?.SessionID,
        ContentPosition  = lastPosition,
        RecvRate         = RecvRate,
        SendRate         = SendRate,
        AgentName        = server_name,
      }.Build();
    }

    public void OnContentChanged(ChannelContentType channelContentType)
    {
      if (CheckHostInfoUpdate()) {
        BroadcastHostInfo();
      }
    }

    public void OnNodeChanged(ChannelNodeAction action, Host node)
    {
      if (CheckHostInfoUpdate()) {
        BroadcastHostInfo();
      }
    }

    public void OnStopped(StopReason reason)
    {
    }
  }

  public class PCPSourceStream
    : SourceStreamBase
  {
    //ハンドシェイク終了まで一定時間で終わらなかったらタイムアウトする
    //PeerCastのポート開放チェックが最悪15秒かかるのでそれより短くはしないこと
    public int PCPHandshakeTimeout { get; set; } = 18000;

    private class IgnoredNodeCollection
    {
      private Dictionary<Uri, TimeSpan> ignoredNodes = new Dictionary<Uri, TimeSpan>();
      private TimeSpan threshold;
      private Stopwatch timer = new Stopwatch();
      public IgnoredNodeCollection(TimeSpan threshold)
      {
        this.threshold = threshold;
        timer.Start();
      }

      public void Add(Uri uri)
      {
        ignoredNodes[uri] = timer.Elapsed;
      }

      public bool Contains(Uri uri)
      {
        if (ignoredNodes.ContainsKey(uri)) {
          var tick = timer.Elapsed;
          if (tick - ignoredNodes[uri] <= threshold) {
            return true;
          }
          else {
            ignoredNodes.Remove(uri);
            return false;
          }
        }
        else {
          return false;
        }
      }

      public void Clear()
      {
        ignoredNodes.Clear();
      }

      public ICollection<Uri> Nodes { get { return ignoredNodes.Keys; } }
    }
    private static readonly TimeSpan ignoredTime = TimeSpan.FromMilliseconds(180000); //ms
    private IgnoredNodeCollection ignoredNodes = new IgnoredNodeCollection(ignoredTime);

    private bool IsIgnored(Uri uri)
    {
      lock (ignoredNodes) { 
        return ignoredNodes.Contains(uri);
      }
    }

    private IEnumerable<Host> GetConnectableNodes()
    {
      lock (ignoredNodes) { 
        return Channel.SourceNodes
          .Where(h => !ignoredNodes.Contains(CreateHostUri(h)));
      }
    }

    /// <summary>
    /// 指定したノードが接続先として選択されないように保持します。
    /// 一度無視されたノードは一定時間経過した後、再度選択されるようになります
    /// </summary>
    /// <param name="uri">接続先として選択されないようにするノードのURI</param>
    protected override void IgnoreSourceHost(Uri uri)
    {
      lock (ignoredNodes) {
        Logger.Debug("Host {0} is ignored", uri);
        ignoredNodes.Add(uri);
      }
    }

    /// <summary>
    /// 全てのノードを接続先として選択可能にします
    /// </summary>
    private void ClearIgnored()
    {
      lock (ignoredNodes) {
        ignoredNodes.Clear();
      }
    }

    public PCPSourceStream(PeerCast peercast, Channel channel, Uri source_uri)
      : base(peercast, channel, source_uri)
    {
      Logger.Debug("Initialized: Channel {0}, Source {1}",
        channel!=null ? channel.ChannelID.ToString("N") : "(null)",
        source_uri);
    }

    private Uri CreateHostUri(Host host)
    {
      EndPoint? endpoint = IsSiteLocal(host) ? host.LocalEndPoint : host.GlobalEndPoint;
      return new Uri($"pcp://{endpoint}/channel/{Channel.ChannelID.ToString("N").ToUpperInvariant()}");
    }

    private bool IsSiteLocal(Host node)
    {
      if (node.GlobalEndPoint!=null) {
        return PeerCast.OutputListeners.Any(listener => node.GlobalEndPoint.Equals(listener.GlobalEndPoint));
      }
      else {
        return true;
      }
    }

    protected override Uri? SelectSourceHost()
    {
      var rnd = new Random();
      var res = GetConnectableNodes().OrderByDescending(n =>
        (IsSiteLocal(n) ? 8000 : 0) +
        rnd.NextDouble() * (
          ( n.IsReceiving ? 4000 : 0) +
          (!n.IsRelayFull ? 2000 : 0) +
          (Math.Max(10-n.Hops, 0)*100) +
          (n.RelayCount*10)
        )
      ).DefaultIfEmpty().First();
      if (res!=null) {
        var uri = CreateHostUri(res);
        Logger.Debug("{0} is selected to source.", uri);
        return uri;
      }
      else if (!IsIgnored(this.SourceUri)) {
        Logger.Debug("Tracker {0} is selected to source.", this.SourceUri);
        return this.SourceUri;
      }
      else {
        return null;
      }
    }

    public override ConnectionInfo GetConnectionInfo()
    {
      var connInfo = sourceConnection.GetConnectionInfo();
      if (connInfo!=null) {
        return connInfo;
      }
      else {
        ConnectionStatus status;
        switch (StoppedReason) {
        case StopReason.UserReconnect:
          status = ConnectionStatus.Connecting;
          break;
        case StopReason.UserShutdown:
        case StopReason.None:
          status = ConnectionStatus.Idle;
          break;
        default:
          status = ConnectionStatus.Error;
          break;
        }
        return new ConnectionInfoBuilder {
          ProtocolName     = "PCP Source",
          Type             = ConnectionType.Source,
          Status           = status,
        }.Build();
      }
    }

    protected override ISourceConnection CreateConnection(Uri source_uri)
    {
      var conn = new PCPSourceConnection(
        PeerCast,
        Channel,
        source_uri,
        source_uri==this.SourceUri ? RemoteHostStatus.Tracker : RemoteHostStatus.None
      );
      conn.PCPHandshakeTimeout = PCPHandshakeTimeout;
      return conn;
    }

    protected override void OnConnectionStopped(ISourceConnection connection, ConnectionStoppedArgs args)
    {
      switch (args.Reason) {
      case StopReason.UnavailableError:
        args.IgnoreSource = connection.SourceUri;
        args.Reconnect = true;
        break;
      case StopReason.ConnectionError:
      case StopReason.OffAir:
        if (connection.SourceUri!=this.SourceUri) {
          args.IgnoreSource = connection.SourceUri;
          args.Reconnect = true;
        }
        break;
      }
    }

    protected override void DoReconnect()
    {
      if (this.sourceConnection.SourceUri!=this.SourceUri && this.sourceConnection.SourceUri!=null) {
        IgnoreSourceHost(this.sourceConnection.SourceUri);
      }
      base.DoReconnect();
    }

    public override SourceStreamType Type {
      get { return SourceStreamType.Relay; }
    }
  }

  [Plugin]
  class PCPSourceStreamPlugin
    : PluginBase
  {
    override public string Name { get { return "PCP Source"; } }

    private PCPSourceStreamFactory? factory;
    override protected void OnAttach(PeerCastApplication app)
    {
      if (factory==null) factory = new PCPSourceStreamFactory(app.PeerCast);
      app.PeerCast.SourceStreamFactories.Add(factory);
    }

    override protected void OnDetach(PeerCastApplication app)
    {
      if (factory!=null) {
        app.PeerCast.SourceStreamFactories.Remove(factory);
      }
    }
  }
}
