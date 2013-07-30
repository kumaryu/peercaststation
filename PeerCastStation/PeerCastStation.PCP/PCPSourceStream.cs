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
using PeerCastStation.Core;
using System.Text.RegularExpressions;
using System.Diagnostics;

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
    public override ISourceStream Create(Channel channel, Uri tracker)
    {
      return new PCPSourceStream(PeerCast, channel, tracker);
    }
  }

  public class RelayRequestResponse
  {
    public int    StatusCode  { get; set; }
    public int?   PCPVersion  { get; set; }
    public string ContentType { get; set; }
    public long?  StreamPos   { get; set; }
    public string Server      { get; set; }
    public RelayRequestResponse(IEnumerable<string> responses)
    {
      this.PCPVersion = null;
      this.ContentType = null;
      this.StreamPos = null;
      foreach (var res in responses) {
        Match match = null;
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

  public static class RelayRequestResponseReader
  {
    public static RelayRequestResponse Read(Stream stream)
    {
      string line = null;
      var responses = new List<string>();
      var buf = new List<byte>();
      while (line!="") {
        var value = stream.ReadByte();
        if (value<0) {
          throw new EndOfStreamException();
        }
        buf.Add((byte)value);
        if (buf.Count >= 2 && buf[buf.Count - 2] == '\r' && buf[buf.Count - 1] == '\n') {
          line = System.Text.Encoding.UTF8.GetString(buf.ToArray(), 0, buf.Count - 2);
          if (line!="") responses.Add(line);
          buf.Clear();
        }
      }
      return new RelayRequestResponse(responses);
    }
  }

  public class PCPSourceConnection
    : SourceConnectionBase
  {
    private TcpClient client = null;
    private enum State {
      SendingRelayRequest,
      WaitingRelayResponse,
      SendingHandshakeRequest,
      WaitingHandshakeResponse,
      Receiving,
      Disconnected,
    }
    private State state = State.SendingRelayRequest;
    private RelayRequestResponse relayResponse = null;
    private Host uphost;
    private RemoteHostStatus remoteType = RemoteHostStatus.None;

    private IPEndPoint RemoteEndPoint {
      get {
        if (client!=null && client.Connected) {
          return (IPEndPoint)client.Client.RemoteEndPoint;
        }
        else {
          return null;
        }
      }
    }

    public PCPSourceConnection(
        PeerCast peercast,
        Channel  channel,
        Uri      source_uri,
        RemoteHostStatus remote_type)
      : base(peercast, channel, source_uri)
    {
      remoteType = remote_type;
    }

    protected override void OnStarted()
    {
      Logger.Debug("Started");
      Channel.ChannelInfoChanged  += Channel_HostInfoUpdated;
      Channel.ChannelTrackChanged += Channel_HostInfoUpdated;
      base.OnStarted();
    }

    protected override void OnStopped()
    {
      Channel.ChannelInfoChanged  -= Channel_HostInfoUpdated;
      Channel.ChannelTrackChanged -= Channel_HostInfoUpdated;
      Logger.Debug("Finished");
      base.OnStopped();
    }

    private void Channel_HostInfoUpdated(object sender, EventArgs e)
    {
      BroadcastHostInfo();
    }

    protected StreamConnection DoConnect(IPEndPoint endpoint)
    {
      try {
        client = new TcpClient();
        client.Connect(endpoint);
        var stream = client.GetStream();
        var connection = new StreamConnection(stream, stream);
        connection.ReceiveTimeout = 3000;
        connection.SendTimeout    = 3000;
        Logger.Debug("Connected: {0}", endpoint);
        return connection;
      }
      catch (SocketException e) {
        Logger.Debug("Connection Failed: {0}", endpoint);
        Logger.Debug(e);
        return null;
      }
    }

    protected override StreamConnection DoConnect(Uri source)
    {
      var port = source.Port<0 ? PCPVersion.DefaultPort : source.Port;
      IPEndPoint endpoint = null;
      try {
        var addresses = Dns.GetHostAddresses(source.DnsSafeHost);
        var addr = addresses.FirstOrDefault(x => x.AddressFamily==AddressFamily.InterNetwork);
        if (addr!=null) {
          endpoint = new IPEndPoint(addr, port);
        }
      }
      catch (ArgumentException) {}
      catch (SocketException) {}
      if (endpoint!=null) {
        return DoConnect(endpoint);
      }
      else {
        Logger.Debug("No Host Found: {0}", source);
        return null;
      }
    }

    protected override void DoClose(StreamConnection connection)
    {
      Logger.Debug("closing connection");
      connection.Close();
      Logger.Debug("closing client");
      client.Close();
      Logger.Debug("closed");
      state = State.Disconnected;
    }

    protected override void DoPost(Host from, Atom packet)
    {
      if (uphost!=from) {
        try {
          connection.Send(stream => {
            AtomWriter.Write(stream, packet);
          });
        }
        catch (IOException e) {
          Logger.Info(e);
          Stop(StopReason.ConnectionError);
        }
      }
    }

    protected override void DoProcess()
    {
      switch (state) {
      case State.SendingRelayRequest:      state = SendRelayRequest();      break;
      case State.WaitingRelayResponse:     state = WaitRelayResponse();     break;
      case State.SendingHandshakeRequest:  state = SendHandshakeRequest();  break;
      case State.WaitingHandshakeResponse: state = WaitHandshakeResponse(); break;
      case State.Receiving:                state = ReceiveBody();           break;
      case State.Disconnected: break;
      }
    }

    private State SendRelayRequest()
    {
      Logger.Debug("Sending Relay request: /channel/{0}", Channel.ChannelID.ToString("N"));
      var req = String.Format(
        "GET /channel/{0} HTTP/1.0\r\n" +
        "x-peercast-pcp:1\r\n" +
        "\r\n", Channel.ChannelID.ToString("N"));
      try {
        connection.Send(System.Text.Encoding.UTF8.GetBytes(req));
      }
      catch (IOException e) {
        Logger.Info(e);
        Stop(StopReason.ConnectionError);
        return State.Disconnected;
      }
      return State.WaitingRelayResponse;
    }

    private State WaitRelayResponse()
    {
      relayResponse = null;
      bool longresponse = false;
      try {
        connection.Recv(stream => {
          longresponse = stream.Length>=2048;
          relayResponse = RelayRequestResponseReader.Read(stream);
        });
      }
      catch (IOException) {
        Stop(StopReason.ConnectionError);
        return State.Disconnected;
      }
      if (relayResponse!=null) {
          Logger.Debug("Relay response: {0}", relayResponse.StatusCode);
        if (relayResponse.StatusCode==200 || relayResponse.StatusCode==503) {
          return State.SendingHandshakeRequest;
        }
        else {
          Logger.Error("Server responses {0} to GET {1}", relayResponse.StatusCode, SourceUri.PathAndQuery);
          Stop(relayResponse.StatusCode==404 ? StopReason.OffAir : StopReason.UnavailableError);
          return State.Disconnected;
        }
      }
      else if (longresponse) {
        Stop(StopReason.ConnectionError);
        return State.Disconnected;
      }
      else {
        return State.WaitingRelayResponse;
      }
    }

    private State SendHandshakeRequest()
    {
      Logger.Debug("Handshake Started");
      if (SendPCPHelo()) {
        return State.WaitingHandshakeResponse;
      }
      else {
        return State.Disconnected;
      }
    }

    private State WaitHandshakeResponse()
    {
      try {
        var atom = RecvAtom();
        while (atom!=null) {
          if (atom.Name==Atom.PCP_OLEH) {
            OnPCPOleh(atom);
            Logger.Debug("Handshake Finished: {0}", PeerCast.GlobalAddress);
            return State.Receiving;
          }
          if (atom.Name==Atom.PCP_QUIT) {
            OnPCPQuit(atom);
            return State.Disconnected;
          }
          else {
            //Ignore packet
          }
          atom = RecvAtom();
        }
        return State.WaitingHandshakeResponse;
      }
      catch (IOException) {
        Stop(StopReason.ConnectionError);
        return State.Disconnected;
      }
    }

    System.Diagnostics.Stopwatch hostInfoUpdateTimer = new System.Diagnostics.Stopwatch();
    private State ReceiveBody()
    {
      if (!hostInfoUpdateTimer.IsRunning) {
        hostInfoUpdateTimer.Reset();
        hostInfoUpdateTimer.Start();
      }
      if (hostInfoUpdateTimer.ElapsedMilliseconds>=120000) {
        BroadcastHostInfo();
      }
      try {
        var atom = RecvAtom();
        while (atom!=null) {
          if (!ProcessAtom(atom)) break;
          atom = RecvAtom();
        }
        return State.Receiving;
      }
      catch (IOException) {
        Stop(StopReason.ConnectionError);
        return State.Disconnected;
      }
    }

    private bool SendPCPHelo()
    {
      var helo = new AtomCollection();
      helo.SetHeloAgent(PeerCast.AgentName);
      helo.SetHeloSessionID(PeerCast.SessionID);
      if (PeerCast.IsFirewalled.HasValue) {
        if (PeerCast.IsFirewalled.Value) {
          //Do nothing
        }
        else {
          var listener = PeerCast.FindListener(
            RemoteEndPoint.Address,
            OutputStreamType.Relay | OutputStreamType.Metadata);
          helo.SetHeloPort(listener.LocalEndPoint.Port);
        }
      }
      else {
        var listener = PeerCast.FindListener(
          RemoteEndPoint.Address,
          OutputStreamType.Relay | OutputStreamType.Metadata);
        if (listener!=null) {
          helo.SetHeloPing(listener.LocalEndPoint.Port);
        }
      }
      PCPVersion.SetHeloVersion(helo);
      try {
        connection.Send(stream => {
          AtomWriter.Write(stream, new Atom(Atom.PCP_HELO, helo));
        });
      }
      catch (IOException e) {
        Logger.Info(e);
        Stop(StopReason.ConnectionError);
        return false;
      }
      return true;
    }

    /// <summary>
    /// 現在のチャンネルとPeerCastの状態からHostパケットを作ります
    /// </summary>
    /// <returns>作ったPCP_HOSTパケット</returns>
    private Atom CreateHostPacket()
    {
      var host = new AtomCollection();
      host.SetHostChannelID(Channel.ChannelID);
      host.SetHostSessionID(PeerCast.SessionID);
      var globalendpoint = 
        PeerCast.GetGlobalEndPoint(
          RemoteEndPoint.AddressFamily,
          OutputStreamType.Relay);
      if (globalendpoint!=null) {
        host.AddHostIP(globalendpoint.Address);
        host.AddHostPort(globalendpoint.Port);
      }
      var localendpoint = 
        PeerCast.GetLocalEndPoint(
          RemoteEndPoint.AddressFamily,
          OutputStreamType.Relay);
      if (localendpoint!=null) {
        host.AddHostIP(localendpoint.Address);
        host.AddHostPort(localendpoint.Port);
      }
      host.SetHostNumListeners(Channel.LocalDirects);
      host.SetHostNumRelays(Channel.LocalRelays);
      host.SetHostUptime(Channel.Uptime);
      if (Channel.Contents.Count > 0) {
        host.SetHostOldPos((uint)(Channel.Contents.Oldest.Position & 0xFFFFFFFFU));
        host.SetHostNewPos((uint)(Channel.Contents.Newest.Position & 0xFFFFFFFFU));
      }
      PCPVersion.SetHostVersion(host);
      host.SetHostFlags1(
        (PeerCast.AccessController.IsChannelRelayable(Channel) ? PCPHostFlags1.Relay : 0) |
        (PeerCast.AccessController.IsChannelPlayable(Channel) ? PCPHostFlags1.Direct : 0) |
        ((!PeerCast.IsFirewalled.HasValue || PeerCast.IsFirewalled.Value) ? PCPHostFlags1.Firewalled : 0) |
        (RecvRate>0 ? PCPHostFlags1.Receiving : 0));
      host.SetHostUphostIP(RemoteEndPoint.Address);
      host.SetHostUphostPort(RemoteEndPoint.Port);
      return new Atom(Atom.PCP_HOST, host);
    }

    /// <summary>
    /// 指定したパケットを含むブロードキャストパケットを作成します
    /// </summary>
    /// <param name="group">配送先グループ</param>
    /// <param name="packet">配送するパケット</param>
    /// <returns>作成したPCP_BCSTパケット</returns>
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

    private void BroadcastHostInfo()
    {
      SyncContext.Post(dummy => {
        Logger.Debug("Broadcasting host info");
        Channel.Broadcast(null, CreateBroadcastPacket(BroadcastGroup.Trackers, CreateHostPacket()), BroadcastGroup.Trackers);
      }, null);
      hostInfoUpdateTimer.Reset();
      hostInfoUpdateTimer.Start();
    }

    private Atom RecvAtom()
    {
      Atom res = null;
      if (connection.Recv(s => { res = AtomReader.Read(s); })) {
        return res;
      }
      else {
        return null;
      }
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

    protected void OnPCPOleh(Atom atom)
    {
      var rip = atom.Children.GetHeloRemoteIP();
      if (rip!=null) {
        switch (rip.AddressFamily) {
        case AddressFamily.InterNetwork:
          if (PeerCast.GlobalAddress==null ||
              Utils.GetAddressLocality(PeerCast.GlobalAddress)<=Utils.GetAddressLocality(rip)) {
            PeerCast.GlobalAddress = rip;
          }
          break;
        case AddressFamily.InterNetworkV6:
          if (PeerCast.GlobalAddress6==null ||
              Utils.GetAddressLocality(PeerCast.GlobalAddress6)<=Utils.GetAddressLocality(rip)) {
            PeerCast.GlobalAddress6 = rip;
          }
          break;
        }
      }
      var port = atom.Children.GetHeloPort();
      if (port.HasValue) {
        PeerCast.IsFirewalled = port.Value==0;
      }
      var sid = atom.Children.GetHeloSessionID();
      if (sid.HasValue) {
        var host = new HostBuilder();
        host.SessionID      = sid.Value;
        host.GlobalEndPoint = RemoteEndPoint;
        uphost = host.ToHost();
      }
    }

    protected void OnPCPOk(Atom atom)
    {
    }

    protected void OnPCPChan(Atom atom)
    {
      foreach (var c in atom.Children) {
        ProcessAtom(c);
      }
    }

    private int      streamIndex = -1;
    private DateTime streamOrigin;
    private long     lastPosition = 0;
    protected void OnPCPChanPkt(Atom atom)
    {
      var pkt_type = atom.Children.GetChanPktType();
      var pkt_data = atom.Children.GetChanPktData();
      if (pkt_type!=null && pkt_data!=null) {
        if (pkt_type==Atom.PCP_CHAN_PKT_TYPE_HEAD) {
          long pkt_pos = atom.Children.GetChanPktPos() ?? 0;
          streamIndex += 1;
          streamOrigin = DateTime.Now;
          Channel.ContentHeader = new Content(streamIndex, TimeSpan.Zero, pkt_pos, pkt_data);
          lastPosition = pkt_pos;
        }
        else if (pkt_type==Atom.PCP_CHAN_PKT_TYPE_DATA) {
          if (atom.Children.GetChanPktPos()!=null) {
            long pkt_pos = atom.Children.GetChanPktPos().Value;
            Channel.Contents.Add(new Content(streamIndex, DateTime.Now-streamOrigin, pkt_pos, pkt_data));
            lastPosition = pkt_pos;
          }
        }
        else if (pkt_type==Atom.PCP_CHAN_PKT_TYPE_META) {
        }
      }
    }

    protected void OnPCPChanInfo(Atom atom)
    {
      Channel.ChannelInfo = new ChannelInfo(atom.Children);
      BroadcastHostInfo();
    }

    protected void OnPCPChanTrack(Atom atom)
    {
      Channel.ChannelTrack = new ChannelTrack(atom.Children);
    }

    protected void OnPCPBcst(Atom atom)
    {
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
      var session_id = atom.Children.GetHostSessionID();
      if (session_id!=null) {
        var node = Channel.Nodes.FirstOrDefault(x => x.SessionID.Equals(session_id));
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
        Channel.AddNode(host.ToHost());
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

    public override ConnectionInfo GetConnectionInfo()
    {
      ConnectionStatus status;
      switch (state) {
      case State.SendingRelayRequest:      status = ConnectionStatus.Connecting; break;
      case State.WaitingRelayResponse:     status = ConnectionStatus.Connecting; break;
      case State.SendingHandshakeRequest:  status = ConnectionStatus.Connecting; break;
      case State.WaitingHandshakeResponse: status = ConnectionStatus.Connecting; break;
      case State.Receiving:                status = ConnectionStatus.Connected; break;
      case State.Disconnected:             status = ConnectionStatus.Error; break;
      default:                             status = ConnectionStatus.Idle; break;
      }
      var server_name = "";
      if (relayResponse!=null && relayResponse.Server!=null) {
        server_name = relayResponse.Server;
      }
      var remote = remoteType;
      if (RemoteEndPoint!=null && Utils.IsSiteLocal(RemoteEndPoint.Address)) remote |= RemoteHostStatus.Local;
      var remote_name = String.Format(
        "{0}:{1}",
        SourceUri.Host,
        SourceUri.IsDefaultPort ? PCPVersion.DefaultPort : SourceUri.Port);
      return new ConnectionInfo(
        "PCP Source",
        ConnectionType.Source,
        status,
        remote_name,
        RemoteEndPoint,
        remote,
        lastPosition,
        RecvRate,
        SendRate,
        null,
        null,
        server_name);
    }
  }

  public class PCPSourceStream : SourceStreamBase
  {
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
        return Channel.Nodes
          .Where(h => !ignoredNodes.Contains(CreateHostUri(h)));
      }
    }

    /// <summary>
    /// 指定したノードが接続先として選択されないように保持します。
    /// 一度無視されたノードは一定時間経過した後、再度選択されるようになります
    /// </summary>
    /// <param name="uri">接続先として選択されないようにするノードのURI</param>
    private void IgnoreNode(Uri uri)
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

    private Uri trackerUri;
    public PCPSourceStream(PeerCast peercast, Channel channel, Uri source_uri)
      : base(peercast, channel, source_uri)
    {
      Logger.Debug("Initialized: Channel {0}, Source {1}",
        channel!=null ? channel.ChannelID.ToString("N") : "(null)",
        source_uri);
      trackerUri = source_uri;
    }

    private Uri CreateHostUri(Host host)
    {
      EndPoint endpoint = IsSiteLocal(host) ? host.LocalEndPoint : host.GlobalEndPoint;
      return new Uri(
        String.Format(
          "pcp://{0}/channel/{1}",
          endpoint.ToString(),
          Channel.ChannelID.ToString("N").ToUpperInvariant()));
    }

    private bool IsSiteLocal(Host node)
    {
      if (node.GlobalEndPoint!=null) {
        IPAddress global;
        switch (node.GlobalEndPoint.AddressFamily) {
        case AddressFamily.InterNetwork:
          global = PeerCast.GlobalAddress;
          break;
        case AddressFamily.InterNetworkV6:
          global = PeerCast.GlobalAddress6;
          break;
        default:
          throw new ArgumentException("Unsupported AddressFamily", "addr");
        }
        return node.GlobalEndPoint.Equals(global);
      }
      else {
        return true;
      }
    }

    private Uri SelectSourceHost()
    {
      var rnd = new Random();
      var res = GetConnectableNodes().OrderByDescending(n =>
        (IsSiteLocal(n) ? 8000 : 0) +
        ( n.IsReceiving ? 4000 : 0) +
        (!n.IsRelayFull ? 2000 : 0) +
        (Math.Max(10-n.Hops, 0)*100) +
        (n.RelayCount*10) +
        rnd.NextDouble()
      ).DefaultIfEmpty().First();
      if (res!=null) {
        return CreateHostUri(res);
      }
      else if (!IsIgnored(trackerUri)) {
        return trackerUri;
      }
      else {
        return null;
      }
    }

    public override ConnectionInfo GetConnectionInfo()
    {
      if (sourceConnection!=null && !sourceConnection.IsStopped) {
        return sourceConnection.GetConnectionInfo();
      }
      ConnectionStatus status = ConnectionStatus.Idle;
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
      return new ConnectionInfo(
        "PCP Source",
        ConnectionType.Source,
        status,
        null,
        null,
        RemoteHostStatus.None,
        null,
        null,
        null,
        null,
        null,
        null);
    }

    protected override SourceConnectionBase CreateConnection(Uri source_uri)
    {
      if (source_uri==trackerUri) {
        return new PCPSourceConnection(PeerCast, Channel, source_uri, RemoteHostStatus.Tracker);
      }
      else {
        return new PCPSourceConnection(PeerCast, Channel, source_uri, RemoteHostStatus.None);
      }
    }

    protected override void OnConnectionStopped(SourceStreamBase.ConnectionStoppedEvent msg)
    {
      switch (msg.StopReason) {
      case StopReason.UnavailableError:
        IgnoreNode(msg.Connection.SourceUri);
        Reconnect(SelectSourceHost());
        break;
      case StopReason.ConnectionError:
      case StopReason.OffAir:
        if (msg.Connection.SourceUri==trackerUri) {
          Stop(msg.StopReason);
        }
        else {
          IgnoreNode(msg.Connection.SourceUri);
          Reconnect(SelectSourceHost());
        }
        break;
      case StopReason.UserReconnect:
        break;
      case StopReason.UserShutdown:
      default:
        Stop(msg.StopReason);
        break;
      }
    }
  }

  [Plugin]
  class PCPSourceStreamPlugin
    : PluginBase
  {
    override public string Name { get { return "PCP Source"; } }

    private PCPSourceStreamFactory factory;
    override protected void OnAttach()
    {
      if (factory==null) factory = new PCPSourceStreamFactory(Application.PeerCast);
      Application.PeerCast.SourceStreamFactories.Add(factory);
    }

    override protected void OnDetach()
    {
      Application.PeerCast.SourceStreamFactories.Remove(factory);
    }
  }
}
