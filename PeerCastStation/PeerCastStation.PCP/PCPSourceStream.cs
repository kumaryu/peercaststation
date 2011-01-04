using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using PeerCastStation.Core;
using System.Text.RegularExpressions;

namespace PeerCastStation.PCP
{
  public class PCPSourceStreamFactory
    : ISourceStreamFactory
  {
    private PeerCastStation.Core.Core core;
    public PCPSourceStreamFactory(PeerCastStation.Core.Core core)
    {
      this.core = core;
    }

    public string Name { get { return "pcp"; } }
    public ISourceStream Create(Channel channel, Uri tracker)
    {
      return new PCPSourceStream(core, channel, tracker);
    }
  }

  public enum CloseReason {
    ConnectionError,
    Unavailable,
    AccessDenied,
    ChannelExit,
    ChannelNotFound,
    RetryLimit,
    NodeNotFound,
    UserShutdown,
  }

  public class RelayRequestResponse
  {
    public int StatusCode     { get; set; }
    public int? PCPVersion    { get; set; }
    public string ContentType { get; set; }
    public long? StreamPos    { get; set; }
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

  public interface IStreamState
  {
    IStreamState Process();
  }

  public class PCPSourceConnectState : IStreamState
  {
    public PCPSourceStream Owner { get; private set; }
    public Host Host { get; private set; }

    public PCPSourceConnectState(PCPSourceStream owner, Host host)
    {
      Owner = owner;
      Host = host;
    }

    public IStreamState Process()
    {
      if (Host!=null) {
        if (Owner.Connect(Host)) {
          return new PCPSourceRelayRequestState(Owner);
        }
        else {
          return new PCPSourceClosedState(Owner, CloseReason.ConnectionError);
        }
      }
      else {
        return new PCPSourceClosedState(Owner, CloseReason.NodeNotFound);
      }
    }
  }

  public class PCPSourceRelayRequestState : IStreamState
  {
    public PCPSourceStream Owner { get; private set; }
    public PCPSourceRelayRequestState(PCPSourceStream owner)
    {
      Owner = owner;
    }

    public IStreamState Process()
    {
      Owner.SendRelayRequest();
      return new PCPSourceRecvRelayResponseState(Owner);
    }
  }

  public class PCPSourceRecvRelayResponseState : IStreamState
  {
    public PCPSourceStream Owner { get; private set; }
    public PCPSourceRecvRelayResponseState(PCPSourceStream owner)
    {
      Owner = owner;
    }

    public IStreamState Process()
    {
      RelayRequestResponse res = Owner.RecvRelayRequestResponse();
      if (res!=null) {
        if (res.StatusCode==200 || res.StatusCode==503) {
          return new PCPSourcePCPHandshakeState(Owner);
        }
        else if (res.StatusCode==404) {
          return new PCPSourceClosedState(Owner, CloseReason.ChannelNotFound);
        }
        else {
          return new PCPSourceClosedState(Owner, CloseReason.AccessDenied);
        }
      }
      else {
        return this;
      }
    }
  }

  public class PCPSourcePCPHandshakeState : IStreamState
  {
    public PCPSourceStream Owner { get; private set; }
    public PCPSourcePCPHandshakeState(PCPSourceStream owner)
    {
      Owner = owner;
    }

    public IStreamState Process()
    {
      Owner.SendPCPHelo();
      return new PCPSourceReceivingState(Owner);
    }
  }

  public class PCPSourceReceivingState : IStreamState
  {
    public PCPSourceStream Owner { get; private set; }
    public int LastHostInfoUpdated { get; set; }
    public PCPSourceReceivingState(PCPSourceStream owner)
    {
      Owner = owner;
      LastHostInfoUpdated = 0;
    }

    public IStreamState Process()
    {
      Atom atom = Owner.RecvAtom();
      if (atom!=null) {
        if (Environment.TickCount - LastHostInfoUpdated > 10000) {
          Owner.BroadcastHostInfo();
        }
        var state = Owner.ProcessAtom(atom);
        if (state!=null) {
          return state;
        }
        else {
          return this;
        }
      }
      else {
        return this;
      }
    }
  }

  public class PCPSourceClosedState : IStreamState
  {
    public PCPSourceStream Owner { get; private set; }
    public CloseReason CloseReason { get; private set; }
    public PCPSourceClosedState(PCPSourceStream owner, CloseReason reason)
    {
      Owner = owner;
      CloseReason = reason;
    }

    public IStreamState Process()
    {
      IStreamState res = null;
      switch (CloseReason) {
      case CloseReason.UserShutdown:
      case CloseReason.NodeNotFound:
        res = null;
        break;
      case CloseReason.Unavailable:
        Owner.IgnoreHost(Owner.Uphost);
        res = new PCPSourceConnectState(Owner, Owner.SelectSourceHost());
        break;
      case CloseReason.ChannelExit:
      case CloseReason.ConnectionError:
      case CloseReason.AccessDenied:
      case CloseReason.ChannelNotFound:
        if (Owner.Uphost.Equals(Owner.Channel.SourceHost)) {
          res = null;
        }
        else {
          Owner.IgnoreHost(Owner.Uphost);
          res = new PCPSourceConnectState(Owner, Owner.SelectSourceHost());
        }
        break;
      }
      Owner.Close(CloseReason);
      return res;
    }
  }

  public class PCPSourceStream : ISourceStream
  {
    private PeerCastStation.Core.Core core;
    private Channel channel;
    private Uri sourceUri;
    private IStreamState state = null;

    private TcpClient connection = null;
    private NetworkStream stream = null;
    private Host uphost = null;
    private PeerCastStation.Core.QueuedSynchronizationContext syncContext;

    public IStreamState State { get { return state; } set { state = value; } }
    public PeerCastStation.Core.Core Core { get { return core; } }
    public Channel Channel { get { return channel; } set { channel = value; } }
    public Host Uphost { get { return uphost; } set { uphost = value; } }
    public bool IsConnected { get { return connection!=null; } }

    MemoryStream recvStream = new MemoryStream();
    byte[] recvBuffer = new byte[8192];
    private void StartReceive()
    {
      if (stream != null) {
        stream.BeginRead(recvBuffer, 0, recvBuffer.Length, (ar) => {
          NetworkStream s = (NetworkStream)ar.AsyncState;
          try {
            int bytes = s.EndRead(ar);
            if (bytes > 0) {
              recvStream.Seek(0, SeekOrigin.End);
              recvStream.Write(recvBuffer, 0, bytes);
              recvStream.Seek(0, SeekOrigin.Begin);
              StartReceive();
            }
          }
          catch (ObjectDisposedException) {
          }
          catch (IOException) {
          }
        }, stream);
      }
    }

    MemoryStream sendStream = new MemoryStream(8192);
    byte[] writeBuffer = null;
    private void ProcessSend()
    {
      if (stream!=null && writeBuffer == null && sendStream.Length > 0) {
        writeBuffer = sendStream.ToArray();
        sendStream.SetLength(0);
        sendStream.Position = 0;
        stream.BeginWrite(writeBuffer, 0, writeBuffer.Length, (ar) => {
          NetworkStream s = (NetworkStream)ar.AsyncState;
          try {
            s.EndWrite(ar);
          }
          catch (ObjectDisposedException) {
          }
          catch (IOException) {
          }
          writeBuffer = null;
        }, stream);
      }
    }

    public virtual void Send(byte[] bytes)
    {
      sendStream.Write(bytes, 0, bytes.Length);
    }

    public virtual void Send(Atom atom)
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

    public void ProcessEvents()
    {
      if (syncContext!=null) {
        syncContext.ProcessAll();
      }
    }

    public virtual Host SelectSourceHost()
    {
      var res = new Host[1];
      core.SynchronizationContext.Send(r => {
        ((Host[])r)[0] = channel.SelectSourceHost();
      }, res);
      if (res[0] != null &&
          res[0].Addresses.Any(x => x.AddressFamily == AddressFamily.InterNetwork)) {
        return res[0];
      }
      else {
        return null;
      }
    }

    public virtual bool Connect(Host host)
    {
      if (host!=null) {
        connection = new TcpClient();
        IPEndPoint point = host.Addresses.First(x => x.AddressFamily == AddressFamily.InterNetwork);
        try {
          connection.Connect(point);
          stream = connection.GetStream();
          sendStream.SetLength(0);
          sendStream.Position = 0;
          recvStream.SetLength(0);
          recvStream.Position = 0;
          StartReceive();
          return true;
        }
        catch (SocketException) {
          connection.Close();
          connection = null;
          if (stream!=null) stream.Close();
          stream = null;
          sendStream.SetLength(0);
          sendStream.Position = 0;
          recvStream.SetLength(0);
          recvStream.Position = 0;
          return false;
        }
      }
      else {
        return false;
      }
    }

    public virtual void IgnoreHost(Host host)
    {
      core.SynchronizationContext.Send(dummy => {
        channel.IgnoreHost(host);
      }, null);
    }

    public virtual void Close(CloseReason reason)
    {
      if (connection != null) {
        stream.Close();
        connection.Close();
        stream = null;
        connection = null;
        sendStream.SetLength(0);
        sendStream.Position = 0;
        recvStream.SetLength(0);
        recvStream.Position = 0;
      }
    }

    public virtual void SendRelayRequest()
    {
      var req = String.Format(
        "GET /channel/{0} HTTP/1.0\r\n" +
        "x-peercast-pcp:1\r\n" +
        "\r\n", channel.ChannelInfo.ChannelID.ToString("N"));
      Send(System.Text.Encoding.UTF8.GetBytes(req));
    }

    public bool Recv(Action<Stream> proc)
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

    public virtual RelayRequestResponse RecvRelayRequestResponse()
    {
      RelayRequestResponse response = null;
      if (Recv(s => { response = RelayRequestResponseReader.Read(s); })) {
        return response;
      }
      else {
        return null;
      }
    }

    public virtual void SendPCPHelo()
    {
      var helo = new Atom(Atom.PCP_HELO, new AtomCollection());
      helo.Children.Add(new Atom(Atom.PCP_HELO_AGENT, "PeerCastStation/1.0"));
      helo.Children.Add(new Atom(Atom.PCP_HELO_SESSIONID, core.Host.SessionID.ToByteArray()));
      helo.Children.Add(new Atom(Atom.PCP_HELO_PORT, core.Host.Addresses[0].Port));
      helo.Children.Add(new Atom(Atom.PCP_HELO_VERSION, 1218));
      Send(helo);
    }

    public virtual Atom RecvAtom()
    {
      Atom res = null;
      if (recvStream.Length>=8 && Recv(s => { res = AtomReader.Read(s); })) {
        return res;
      }
      else {
        return null;
      }
    }

    public virtual void Start()
    {
      if (this.syncContext == null) {
        this.syncContext = new QueuedSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(this.syncContext);
      }
      state = new PCPSourceConnectState(this, SelectSourceHost());
      while (state!=null) {
        ProcessState();
      }
    }

    public virtual void ProcessState()
    {
      if (state!=null) {
        state = state.Process();
      }
      ProcessSend();
      ProcessEvents();
    }

    /// <summary>
    /// 現在のチャンネルとCoreの状態からHostパケットを作ります
    /// </summary>
    /// <returns>作ったPCP_HOSTパケット</returns>
    public virtual Atom CreateHostPacket()
    {
      var host = new AtomCollection();
      host.SetHostChannelID(channel.ChannelInfo.ChannelID);
      host.SetHostSessionID(core.Host.SessionID);
      foreach (var endpoint in core.Host.Addresses) {
        if (endpoint.AddressFamily == AddressFamily.InterNetwork) {
          host.AddHostIP(endpoint.Address);
          host.AddHostPort((short)endpoint.Port);
        }
      }
      host.SetHostNumListeners(channel.OutputStreams.CountPlaying);
      host.SetHostNumRelays(channel.OutputStreams.CountRelaying);
      host.SetHostUptime(channel.Uptime);
      if (channel.Contents.Count > 0) {
        host.SetHostOldPos((int)channel.Contents.Oldest.Position);
        host.SetHostNewPos((int)channel.Contents.Newest.Position);
      }
      host.SetHostVersion(1218);
      host.SetHostVersionVP(27);
      host.SetHostVersionEXPrefix(new byte[] { (byte)'P', (byte)'P' });
      host.SetHostVersionEXNumber(23);
      host.SetHostFlags1(
        (channel.IsRelayFull ? 0 : PCPHostFlags1.Relay) |
        (channel.IsDirectFull ? 0 : PCPHostFlags1.Direct) |
        (core.Host.IsFirewalled ? PCPHostFlags1.Firewalled : 0) |
        PCPHostFlags1.Receiving); //TODO:受信中かどうかちゃんと判別する
      if (uphost != null) {
        var endpoint = uphost.Addresses.FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork);
        if (endpoint != null) {
          host.SetHostUphostIP(endpoint.Address);
          host.SetHostUphostPort(endpoint.Port);
        }
      }
      return new Atom(Atom.PCP_HOST, host);
    }

    /// <summary>
    /// 指定したパケットを含むブロードキャストパケットを作成します
    /// </summary>
    /// <param name="group">配送先グループ</param>
    /// <param name="packet">配送するパケット</param>
    /// <returns>作成したPCP_BCSTパケット</returns>
    public virtual Atom CreateBroadcastPacket(BroadcastGroup group, Atom packet)
    {
      var bcst = new AtomCollection();
      bcst.SetBcstFrom(core.Host.SessionID);
      bcst.SetBcstGroup(BroadcastGroup.Relays | BroadcastGroup.Trackers);
      bcst.SetBcstHops(0);
      bcst.SetBcstTTL(11);
      bcst.SetBcstVersion(1218);
      bcst.SetBcstVersionVP(27);
      bcst.SetBcstVersionEXPrefix(new byte[] { (byte)'P', (byte)'P' });
      bcst.SetBcstVersionEXNumber(23);
      bcst.SetBcstChannelID(channel.ChannelInfo.ChannelID);
      bcst.Add(packet);
      return new Atom(Atom.PCP_BCST, bcst);
    }

    public virtual void BroadcastHostInfo()
    {
      channel.Broadcast(core.Host,
        CreateBroadcastPacket(BroadcastGroup.Relays | BroadcastGroup.Trackers, CreateHostPacket()),
        BroadcastGroup.Relays | BroadcastGroup.Trackers);
    }

    public virtual IStreamState ProcessAtom(Atom atom)
    {
           if (atom.Name==Atom.PCP_HELO)       return OnPCPHelo(atom);
      else if (atom.Name==Atom.PCP_OLEH)       return OnPCPOleh(atom);
      else if (atom.Name==Atom.PCP_OK)         return OnPCPOk(atom);
      else if (atom.Name==Atom.PCP_CHAN)       return OnPCPChan(atom);
      else if (atom.Name==Atom.PCP_CHAN_PKT)   return OnPCPChanPkt(atom);
      else if (atom.Name==Atom.PCP_CHAN_INFO)  return OnPCPChanInfo(atom);
      else if (atom.Name==Atom.PCP_CHAN_TRACK) return OnPCPChanTrack(atom);
      else if (atom.Name==Atom.PCP_BCST)       return OnPCPBcst(atom);
      else if (atom.Name==Atom.PCP_HOST)       return OnPCPHost(atom);
      else if (atom.Name==Atom.PCP_QUIT)       return OnPCPQuit(atom);
      else                                     return null;
    }

    protected virtual IStreamState OnPCPHelo(Atom atom)
    {
      var res = new Atom(Atom.PCP_OLEH, new AtomCollection());
      if (connection!=null && connection.Client.RemoteEndPoint.AddressFamily==AddressFamily.InterNetwork) {
        res.Children.SetHeloRemoteIP(((IPEndPoint)connection.Client.RemoteEndPoint).Address);
      }
      res.Children.SetHeloAgent("PeerCastStation/1.0");
      res.Children.SetHeloSessionID(core.Host.SessionID);
      res.Children.SetHeloPort((short)core.Host.Addresses[0].Port);
      res.Children.SetHeloVersion(1218);
      Send(res);
      return null;
    }

    protected virtual IStreamState OnPCPOleh(Atom atom)
    {
      core.SynchronizationContext.Post(dummy => {
        var rip = atom.Children.GetHeloRemoteIP();
        if (!core.Host.Addresses.Any(x => x.Address.Equals(rip))) {
          core.Host.Addresses.Add(new IPEndPoint(rip, core.Host.Addresses[0].Port));
        }
      }, null);
      return null;
    }

    protected virtual IStreamState OnPCPOk(Atom atom)
    {
      return null;
    }

    protected virtual IStreamState OnPCPChan(Atom atom)
    {
      IStreamState state = null;
      foreach (var c in atom.Children) {
        state = ProcessAtom(c);
      }
      return state;
    }

    protected virtual IStreamState OnPCPChanPkt(Atom atom)
    {
      var pkt_type = atom.Children.GetChanPktType();
      var pkt_data = atom.Children.GetChanPktData();
      if (pkt_type!=null && pkt_data!=null) {
        if (pkt_type==Atom.PCP_CHAN_PKT_TYPE_HEAD) {
          core.SynchronizationContext.Post(dummy => {
            channel.ContentHeader = new Content(0, pkt_data);
          }, null);
        }
        else if (pkt_type==Atom.PCP_CHAN_PKT_TYPE_DATA) {
          var pkt_pos = atom.Children.GetChanPktPos();
          if (pkt_pos != null) {
            core.SynchronizationContext.Post(dummy => {
              channel.Contents.Add(new Content((long)pkt_pos, pkt_data));
            }, null);
          }
        }
        else if (pkt_type==Atom.PCP_CHAN_PKT_TYPE_META) {
        }
      }
      return null;
    }

    protected virtual IStreamState OnPCPChanInfo(Atom atom)
    {
      core.SynchronizationContext.Post(dummy => {
        var name = atom.Children.GetChanInfoName();
        if (name != null) channel.ChannelInfo.Name = name;
        channel.ChannelInfo.Extra.SetChanInfo(atom.Children);
      }, null);
      return null;
    }

    protected virtual IStreamState OnPCPChanTrack(Atom atom)
    {
      channel.ChannelInfo.Extra.SetChanTrack(atom.Children);
      return null;
    }

    protected virtual IStreamState OnPCPBcst(Atom atom)
    {
      var dest = atom.Children.GetBcstDest();
      if (dest==null || dest==core.Host.SessionID) {
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
          dest != core.Host.SessionID &&
          ttl>1) {
        atom.Children.SetBcstTTL((byte)(ttl - 1));
        atom.Children.SetBcstHops((byte)(hops + 1));
        channel.Broadcast(uphost, atom, group.Value);
      }
      return null;
    }

    protected virtual IStreamState OnPCPHost(Atom atom)
    {
      var session_id = atom.Children.GetHostSessionID();
      if (session_id!=null) {
        //core.SynchronizationContext.Post(dummy => {
          var node = channel.Nodes.FirstOrDefault(x => x.Host.SessionID.Equals(session_id));
          if (node==null) {
            node = new Node(new Host());
            node.Host.SessionID = (Guid)session_id;
            channel.Nodes.Add(node);
          }
          node.Host.Extra.Update(atom.Children);
          node.DirectCount = atom.Children.GetHostNumListeners() ?? 0;
          node.RelayCount = atom.Children.GetHostNumRelays() ?? 0;
          var flags1 = atom.Children.GetHostFlags1();
          if (flags1 != null) {
            node.Host.IsFirewalled = (flags1.Value & PCPHostFlags1.Firewalled) != 0;
            node.IsRelayFull       = (flags1.Value & PCPHostFlags1.Relay) == 0;
            node.IsDirectFull      = (flags1.Value & PCPHostFlags1.Direct) == 0;
            node.IsReceiving       = (flags1.Value & PCPHostFlags1.Receiving) != 0;
            node.IsControlFull     = (flags1.Value & PCPHostFlags1.ControlIn) == 0;
          }

          var ip = new IPEndPoint(IPAddress.Any, 0);
          foreach (var c in atom.Children) {
            if (c.Name==Atom.PCP_HOST_IP) {
              IPAddress addr;
              if (c.TryGetIPv4Address(out addr)) {
                ip.Address = addr;
                if (ip.Port != 0) {
                  if (!node.Host.Addresses.Any(x => x.Equals(ip))) {
                    node.Host.Addresses.Add(ip);
                  }
                  ip = new IPEndPoint(IPAddress.Any, 0);
                }
              }
            }
            else if (c.Name==Atom.PCP_HOST_PORT) {
              short port;
              if (c.TryGetInt16(out port)) {
                ip.Port = port;
                if (ip.Address != IPAddress.Any) {
                  if (!node.Host.Addresses.Any(x => x.Equals(ip))) {
                    node.Host.Addresses.Add(ip);
                  }
                  ip = new IPEndPoint(IPAddress.Any, 0);
                }
              }
            }
          }
        //}, null);
      }
      return null;
    }

    protected virtual IStreamState OnPCPQuit(Atom atom)
    {
      if (atom.GetInt32() == Atom.PCP_ERROR_QUIT + Atom.PCP_ERROR_UNAVAILABLE) {
        return new PCPSourceClosedState(this, CloseReason.Unavailable);
      }
      else {
        return new PCPSourceClosedState(this, CloseReason.ChannelExit);
      }
    }

    public void Close()
    {
      if (syncContext!=null) {
        syncContext.Post((x) => {
          if (IsConnected) {
            state = new PCPSourceClosedState(this, CloseReason.UserShutdown);
          }
        }, null);
      }
      else {
        if (IsConnected) {
          state = new PCPSourceClosedState(this, CloseReason.UserShutdown);
        }
      }
    }

    public void Post(Host from, Atom packet)
    {
      if (syncContext!=null) {
        syncContext.Post(x => {
          if (uphost != from) {
            Send(packet);
          }
        }
        , null);
      }
      else {
        if (uphost != from) {
          Send(packet);
        }
      }
    }

    public PCPSourceStream(PeerCastStation.Core.Core core, Channel channel, Uri source_uri)
    {
      this.core = core;
      this.channel = channel;
      this.sourceUri = source_uri;
    }
  }
}
