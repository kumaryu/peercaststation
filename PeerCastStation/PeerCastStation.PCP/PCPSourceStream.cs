using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using PeerCastStation.Core;

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
    public ISourceStream Create(Uri tracker)
    {
      return new PCPSourceStream(core);
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

  public class SourceClosedEventArgs : EventArgs
  {
    public Host Host { get; set; }
    public CloseReason CloseReason { get; set; }
    public SourceClosedEventArgs(Host host, CloseReason reason)
    {
      Host = host;
      CloseReason = reason;
    }
  }
  public delegate void SourceClosedEventHandler(object sender, SourceClosedEventArgs e);

  public class PCPSourceStream : ISourceStream
  {
    private PeerCastStation.Core.Core core;
    private Channel channel;
    private TcpClient connection;
    private NetworkStream stream;
    private Host uphost = null;
    private int connectWait = Environment.TickCount;
    private int lastHostInfoUpdated = 0;
    public enum SourceStreamState {
      ConnectWait,
      Connect,
      RelayRequest,
      Receiving,
      Closed,
    }
    private SourceStreamState state = SourceStreamState.Connect;
    private PeerCastStation.Core.QueuedSynchronizationContext syncContext;
    public event SourceClosedEventHandler SourceClosed;

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
    private void CheckSend()
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

    static private MemoryStream dropStream(MemoryStream s)
    {
      var res = new MemoryStream((int)Math.Max(8192, s.Length - s.Position));
      res.Write(s.GetBuffer(), (int)s.Position, (int)(s.Length - s.Position));
      res.Position = 0;
      return res;
    }

    private void ProcessEvents()
    {
      syncContext.ProcessAll();
    }

    private Host SelectSourceHost()
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

    private void Connect(Host host)
    {
      connection = new TcpClient();
      IPEndPoint point = host.Addresses.First(x => x.AddressFamily == AddressFamily.InterNetwork);
      try {
        connection.Connect(point);
        stream = connection.GetStream();
        StartRelayRequest();
      }
      catch (SocketException) {
        connection.Close();
        connection = null;
        if (stream!=null) stream.Close();
        stream = null;
        OnClose(CloseReason.ConnectionError);
      }
    }

    private void IgnoreHost(Host host)
    {
      core.SynchronizationContext.Send(dummy => {
        channel.IgnoreHost(host);
      }, null);
    }

    private void OnClose(CloseReason reason)
    {
      if (SourceClosed != null) {
        SourceClosed(this, new SourceClosedEventArgs(uphost, reason));
      }
      switch (reason) {
      case CloseReason.UserShutdown:
      case CloseReason.NodeNotFound:
        state = SourceStreamState.Closed;
        break;
      case CloseReason.Unavailable:
        IgnoreHost(uphost);
        StartConnect();
        break;
      case CloseReason.ChannelExit:
      case CloseReason.ConnectionError:
      case CloseReason.AccessDenied:
      case CloseReason.ChannelNotFound:
        if (uphost == channel.SourceHost) {
          state = SourceStreamState.Closed;
        }
        else {
          IgnoreHost(uphost);
          StartConnect();
        }
        break;
      }
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

    private void StartConnect()
    {
      uphost = SelectSourceHost();
      state = SourceStreamState.Connect;
    }

    private void RetryConnect()
    {
      state = SourceStreamState.Connect;
    }

    private void StartRelayRequest()
    {
      state = SourceStreamState.RelayRequest;
      var req = String.Format(
        "GET /channel/{0} HTTP/1.0\r\n" +
        "x-peercast-pcp:1\r\n" +
        "\r\n", channel.ChannelInfo.ChannelID.ToString("N"));
      var reqb = System.Text.Encoding.UTF8.GetBytes(req);
      sendStream.Write(reqb, 0, reqb.Length);
    }

    private void WaitRelayResponse()
    {
      var response = new List<string>();
      var buf = new List<byte>();
      recvStream.Seek(0, SeekOrigin.Begin);
      while (recvStream.Position<recvStream.Length && (response.Count < 1 || response[response.Count - 1] != "")) {
        buf.Add((byte)recvStream.ReadByte());
        if (buf.Count >= 2 && buf[buf.Count - 2] == '\r' && buf[buf.Count - 1] == '\n') {
          response.Add(System.Text.Encoding.UTF8.GetString(buf.ToArray(), 0, buf.Count - 2));
          buf.Clear();
        }
      }
      if (response.Count > 0 && response[response.Count - 1] == "") {
        recvStream = dropStream(recvStream);
        int response_code = 0;
        var match = System.Text.RegularExpressions.Regex.Match(response[0], @"^HTTP/1.\d (\d+) .*$");
        if (match.Success) {
          response_code = Convert.ToInt32(match.Groups[1].Value);
        }
        if (response_code == 200 || response_code == 503) {
          StartHandshake();
        }
        else if (response_code == 404) {
          OnClose(CloseReason.ChannelNotFound);
        }
        else {
          OnClose(CloseReason.AccessDenied);
        }
      }
    }

    private void StartHandshake()
    {
      state = SourceStreamState.Receiving;
      var helo = new Atom(Atom.PCP_HELO, new AtomCollection());
      helo.Children.Add(new Atom(Atom.PCP_HELO_AGENT,     "PeerCastStation/1.0"));
      helo.Children.Add(new Atom(Atom.PCP_HELO_SESSIONID, core.Host.SessionID.ToByteArray()));
      helo.Children.Add(new Atom(Atom.PCP_HELO_PORT,      core.Host.Addresses[0].Port));
      helo.Children.Add(new Atom(Atom.PCP_HELO_VERSION,   1218));
      AtomWriter.Write(sendStream, helo);
    }

    private void ProcessPacket()
    {
      if (recvStream.Length >= 8) {
        try {
          recvStream.Seek(0, SeekOrigin.Begin);
          var atom = AtomReader.Read(recvStream);
          recvStream = dropStream(recvStream);
          ProcessAtom(atom);
        }
        catch (EndOfStreamException) {
        }
      }
    }

    public void Start(Uri tracker_uri, Channel channel)
    {
      if (this.syncContext == null) {
        this.syncContext = new QueuedSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(this.syncContext);
      }
      this.channel = channel;
      StartConnect();
      while (state!=SourceStreamState.Closed) {
        switch (state) {
        case SourceStreamState.ConnectWait:
          if (Environment.TickCount - connectWait > 0) {
            RetryConnect();
          }
          else {
            Thread.Sleep(1);
          }
          break;
        case SourceStreamState.Connect:
          if (uphost != null) {
            Connect(uphost);
            StartReceive();
          }
          else {
            OnClose(CloseReason.NodeNotFound);
          }
          break;
        case SourceStreamState.RelayRequest:
          WaitRelayResponse();
          break;
        case SourceStreamState.Receiving:
          ProcessPacket();
          CheckHostInfo();
          break;
        case SourceStreamState.Closed:
          break;
        }
        CheckSend();
        ProcessEvents();
      }
    }

    private void CheckHostInfo()
    {
      if (Environment.TickCount - lastHostInfoUpdated > 10000) {
        lastHostInfoUpdated = Environment.TickCount;
        var host = new AtomCollection();
        host.SetHostChannelID(channel.ChannelInfo.ChannelID);
        host.SetHostSessionID(core.Host.SessionID);
        foreach (var endpoint in core.Host.Addresses) {
          if (endpoint.AddressFamily == AddressFamily.InterNetwork) {
            host.AddHostIP(endpoint.Address);
            host.AddHostPort((short)endpoint.Port);
          }
        }
        host.SetHostNumListeners(channel.OutputStreams.Count); //TODO: coutn listenrs
        host.SetHostNumRelays(channel.OutputStreams.Count); //TODO: count relays
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
        host.SetHostUphostIP(uphost.Addresses[0].Address);
        host.SetHostUphostPort(uphost.Addresses[0].Port);
      }
    }

    protected virtual void ProcessAtom(Atom atom)
    {
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

    protected virtual void OnPCPHelo(Atom atom)
    {
      var res = new Atom(Atom.PCP_OLEH, new AtomCollection());
      if (connection.Client.RemoteEndPoint.AddressFamily==AddressFamily.InterNetwork) {
        res.Children.SetHeloRemoteIP(((IPEndPoint)connection.Client.RemoteEndPoint).Address);
      }
      res.Children.SetHeloAgent("PeerCastStation/1.0");
      res.Children.SetHeloSessionID(core.Host.SessionID);
      res.Children.SetHeloPort((short)core.Host.Addresses[0].Port);
      res.Children.SetHeloVersion(1218);
      AtomWriter.Write(sendStream, res);
    }

    protected virtual void OnPCPOleh(Atom atom)
    {
      core.SynchronizationContext.Post(dummy => {
        var rip = atom.Children.GetHeloRemoteIP();
        if (!core.Host.Addresses.Any(x => x.Address == rip)) {
          core.Host.Addresses.Add(new IPEndPoint(rip, core.Host.Addresses[0].Port));
        }
      }, null);
    }

    protected virtual void OnPCPOk(Atom atom)
    {
    }

    protected virtual void OnPCPChan(Atom atom)
    {
      foreach (var c in atom.Children) {
        ProcessAtom(c);
      }
    }

    protected virtual void OnPCPChanPkt(Atom atom)
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
    }

    protected virtual void OnPCPChanInfo(Atom atom)
    {
      core.SynchronizationContext.Post(dummy => {
        var name = atom.Children.GetChanInfoName();
        if (name != null) channel.ChannelInfo.Name = name;
        channel.ChannelInfo.Extra.SetChanInfo(atom.Children);
      }, null);
    }

    protected virtual void OnPCPChanTrack(Atom atom)
    {
      channel.ChannelInfo.Extra.SetChanTrack(atom.Children);
    }

    protected virtual void OnPCPBcst(Atom atom)
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
        if ((group & (byte)BroadcastGroup.Trackers)!=0) {
          channel.Broadcast(uphost, atom, BroadcastGroup.Trackers);
        }
        else {
          channel.Broadcast(uphost, atom, BroadcastGroup.Relays);
        }
      }
    }

    protected virtual void OnPCPHost(Atom atom)
    {
      var session_id = atom.Children.GetHostSessionID();
      if (session_id!=null) {
        core.SynchronizationContext.Post(dummy => {
          var node = channel.Nodes.FirstOrDefault(x => x.Host.SessionID == session_id);
          if (node == null) {
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
            if (c.Name == Atom.PCP_HOST_IP) {
              IPAddress addr;
              if (c.TryGetIPv4Address(out addr)) {
                ip.Address = addr;
                if (ip.Port != 0) {
                  if (!node.Host.Addresses.Any(x => x == ip)) {
                    node.Host.Addresses.Add(ip);
                  }
                  ip = new IPEndPoint(IPAddress.Any, 0);
                }
              }
            }
            else if (c.Name == Atom.PCP_HOST_PORT) {
              short port;
              if (c.TryGetInt16(out port)) {
                ip.Port = port;
                if (ip.Address != IPAddress.Any) {
                  if (node.Host.Addresses.Any(x => x == ip)) {
                    node.Host.Addresses.Add(ip);
                  }
                  ip = new IPEndPoint(IPAddress.Any, 0);
                }
              }
            }
          }
        }, null);
      }
    }

    protected virtual void OnPCPQuit(Atom atom)
    {
      if (atom.GetInt32() == Atom.PCP_ERROR_QUIT + Atom.PCP_ERROR_UNAVAILABLE) {
        OnClose(CloseReason.Unavailable);
      }
      else {
        OnClose(CloseReason.ChannelExit);
      }
    }

    public void Close()
    {
      if (state!=SourceStreamState.Closed) {
        syncContext.Post((x) => { OnClose(CloseReason.UserShutdown); }, null);
      }
    }

    public void Post(Host from, Atom packet)
    {
      if (state!=SourceStreamState.Closed) {
        syncContext.Post(x => {
          if (uphost != from) {
            AtomWriter.Write(sendStream, packet);
          }
        }
        , null);
      }
    }

    public PCPSourceStream(PeerCastStation.Core.Core core)
    {
      this.core = core;
    }
  }
}
