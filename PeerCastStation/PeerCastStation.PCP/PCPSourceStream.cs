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

  public class PCPSourceStream : ISourceStream
  {
    private PeerCastStation.Core.Core core;
    private TcpClient connection;
    private NetworkStream stream;
    private Channel channel;
    private bool closed = true;
    public enum SourceStreamState {
      Idle,
      Connect,
      RelayRequest,
      Receiving,
      Closed,
    }
    private enum CloseReason {
      ConnectionError,
      AccessDenied,
      ChannelExit,
      RetryLimit,
      NodeNotFound,
      UserShutdown,
    }
    private SourceStreamState state = SourceStreamState.Idle;
    private PeerCastStation.Core.QueuedSynchronizationContext syncContext;

    MemoryStream recvStream = new MemoryStream();
    byte[] recvBuffer = new byte[8192];
    private void StartReceive()
    {
      if (stream != null) {
        stream.BeginRead(recvBuffer, 0, recvBuffer.Length, (ar) => {
          if (stream != null) {
            int bytes = stream.EndRead(ar);
            if (bytes > 0) {
              recvStream.Seek(0, SeekOrigin.End);
              recvStream.Write(recvBuffer, 0, bytes);
              recvStream.Seek(0, SeekOrigin.Begin);
              StartReceive();
            }
          }
        }, null);
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
          if (stream!=null) {
            stream.EndWrite(ar);
          }
          writeBuffer = null;
          CheckSend();
        }, null);
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

    private void Connect(IPEndPoint host)
    {
      connection = new TcpClient();
      connection.Connect(host);
      stream = connection.GetStream();
      StartRelayRequest();
    }

    private void Close(CloseReason reason)
    {
      switch (reason) {
      case CloseReason.ChannelExit:
      case CloseReason.UserShutdown:
      case CloseReason.NodeNotFound:
        state = SourceStreamState.Closed;
        closed = true;
        break;
      case CloseReason.ConnectionError:
      case CloseReason.AccessDenied:
        state = SourceStreamState.Connect;
        break;
      }
      if (connection != null) {
        connection.Close();
        stream.Close();
        connection = null;
        stream = null;
      }
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
        if (response_code == 200 || response_code == 504) {
          StartHandshake();
        }
        else {
          Close(CloseReason.AccessDenied);
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
          var quit = ProcessAtom(atom);
          if (quit) {
            Close(CloseReason.ChannelExit);
          }
        }
        catch (EndOfStreamException) {
        }
      }
    }

    List<IPEndPoint> ignoredHosts = new List<IPEndPoint>();
    private IPEndPoint SelectHost(Uri tracker)
    {
      var hosts = new List<IPEndPoint>();
      if (tracker != null) {
        var port = tracker.Port < 0 ? 7144 : tracker.Port;
        foreach (var addr in Dns.GetHostAddresses(tracker.DnsSafeHost)) {
          var host = new IPEndPoint(addr, port);
          if (host.AddressFamily==AddressFamily.InterNetwork && !ignoredHosts.Exists(x => x == host)) {
            hosts.Add(host);
          }
        }
      }
      foreach (var node in channel.Nodes) {
        foreach (var host in node.Host.Addresses) {
          if (!ignoredHosts.Exists(x => x == host)) {
            hosts.Add(host);
          }
        }
      }
      if (hosts.Count > 0) {
        int idx = new Random().Next(hosts.Count);
        ignoredHosts.Add(hosts[idx]);
        return hosts[idx];
      }
      else {
        ignoredHosts.Clear();
        return null;
      }
    }

    public void Start(Uri tracker, Channel channel)
    {
      if (this.syncContext == null) {
        this.syncContext = new QueuedSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(this.syncContext);
      }
      this.closed = false;
      this.channel = channel;
      state = SourceStreamState.Connect;
      while (!closed) {
        switch (state) {
        case SourceStreamState.Idle:
          break;
        case SourceStreamState.Connect:
          var host = SelectHost(tracker);
          if (host != null) {
            Connect(host);
            StartReceive();
          }
          else {
            Close(CloseReason.NodeNotFound);
          }
          break;
        case SourceStreamState.RelayRequest:
          WaitRelayResponse();
          break;
        case SourceStreamState.Receiving:
          ProcessPacket();
          break;
        case SourceStreamState.Closed:
          break;
        }
        CheckSend();
        ProcessEvents();
      }
    }

    protected bool ProcessAtom(Atom atom)
    {
      bool quit = false;
      if (atom.Name==Atom.PCP_HELO) {
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
      else if (atom.Name==Atom.PCP_OLEH) {
        core.SynchronizationContext.Post(dummy => {
          var rip = atom.Children.GetHeloRemoteIP();
          if (!core.Host.Addresses.Any(x => x.Address == rip)) {
            core.Host.Addresses.Add(new IPEndPoint(rip, core.Host.Addresses[0].Port));
          }
        }, null);
      }
      else if (atom.Name==Atom.PCP_OK) {
      }
      else if (atom.Name==Atom.PCP_CHAN) {
        foreach (var c in atom.Children) ProcessAtom(c);
      }
      else if (atom.Name==Atom.PCP_CHAN_PKT) {
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
      else if (atom.Name==Atom.PCP_CHAN_INFO) {
        core.SynchronizationContext.Post(dummy => {
          var name = atom.Children.GetChanInfoName();
          if (name != null) channel.ChannelInfo.Name = name;
          channel.ChannelInfo.Extra.SetChanInfo(atom.Children);
        }, null);
      }
      else if (atom.Name==Atom.PCP_CHAN_TRACK) {
        channel.ChannelInfo.Extra.SetChanTrack(atom.Children);
      }
      else if (atom.Name==Atom.PCP_BCST) {
        var dest = atom.Children.GetBcstDest();
        if (dest==null || dest==core.Host.SessionID) {
          foreach (var c in atom.Children) ProcessAtom(c);
        }
        var ttl = atom.Children.GetBcstTTL();
        var hops = atom.Children.GetBcstHops();
        var from = atom.Children.GetBcstFrom();
        var group = atom.Children.GetBcstGroup();
        if (ttl != null && hops != null && group != null && from != null && ttl<hops) {
          //TODO: HOPSを増やしてまわす
        }
      }
      else if (atom.Name == Atom.PCP_HOST) {
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
              node.Host.IsFirewalled = (flags1 & Atom.PCP_HOST_FLAGS1_PUSH) != 0;
              node.IsRelayFull = (flags1 & Atom.PCP_HOST_FLAGS1_RELAY) == 0;
              node.IsDirectFull = (flags1 & Atom.PCP_HOST_FLAGS1_DIRECT) == 0;
              node.IsReceiving = (flags1 & Atom.PCP_HOST_FLAGS1_RECV) != 0;
              node.IsControlFull = (flags1 & Atom.PCP_HOST_FLAGS1_CIN) == 0;
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
      else if (atom.Name==Atom.PCP_QUIT) {
        quit = true;
      }
      return quit;
    }

    public void Close()
    {
      if (!closed) {
        syncContext.Post((x) => { Close(CloseReason.UserShutdown); }, null);
      }
    }

    public PCPSourceStream(PeerCastStation.Core.Core core)
    {
      this.core = core;
    }
  }
}
