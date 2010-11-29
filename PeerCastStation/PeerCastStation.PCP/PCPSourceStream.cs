using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;
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
    private Channel channel;
    public void Start(Uri tracker, Channel channel)
    {
      this.channel = channel;
      var port = tracker.Port < 0 ? 7144 : tracker.Port;
      connection = new TcpClient(tracker.Host, port);
      var stream = connection.GetStream();
      var encoding = new System.Text.UTF8Encoding(false);
      var reader = new BinaryReader(stream);
      var writer = new BinaryWriter(stream);
      var atom_reader = new AtomReader(stream);
      var atom_writer = new AtomWriter(stream);
      var req = String.Format(
        "GET /channel/{0} HTTP/1.0\r\n" +
        "x-peercast-pcp:1\r\n" +
        "\r\n", channel.ChannelInfo.ChannelID.ToString("N"));
      writer.Write(encoding.GetBytes(req));
      var response = new List<string>();
      var buf = new List<byte>();
      while (response.Count < 1 || response[response.Count - 1] != "") {
        buf.Add(reader.ReadByte());
        if (buf.Count >= 2 && buf[buf.Count - 2] == '\r' && buf[buf.Count - 1] == '\n') {
          response.Add(encoding.GetString(buf.ToArray(), 0, buf.Count-2));
          buf.Clear();
        }
      }
      int response_code = 0;
      var match = System.Text.RegularExpressions.Regex.Match(response[0], @"^HTTP/1.\d (\d+) .*$");
      if (match.Success) {
        response_code = Convert.ToInt32(match.Groups[1].Value);
      }

      if (response_code == 200 || response_code==504) {
        var helo = new Atom(Atom.PCP_HELO, new AtomCollection());
        helo.Children.Add(new Atom(Atom.PCP_HELO_AGENT,     "PeerCastStation/1.0"));
        helo.Children.Add(new Atom(Atom.PCP_HELO_SESSIONID, core.Host.SessionID.ToByteArray()));
        helo.Children.Add(new Atom(Atom.PCP_HELO_PORT,      core.Host.Addresses[0].Port));
        helo.Children.Add(new Atom(Atom.PCP_HELO_VERSION,   1218));
        atom_writer.Write(helo);
        bool closed = false;
        while (!closed) {
          var atom = atom_reader.Read();
          closed = ProcessAtom(atom, atom_writer);
        }
      }

      connection.Close();
      stream.Close();
    }

    protected bool ProcessAtom(Atom atom, AtomWriter writer)
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
        writer.Write(res);
      }
      else if (atom.Name==Atom.PCP_OLEH) {
        var rip = atom.Children.GetHeloRemoteIP();
        if (!core.Host.Addresses.Any(x => x.Address==rip)) {
          core.Host.Addresses.Add(new IPEndPoint(rip, core.Host.Addresses[0].Port));
        }
      }
      else if (atom.Name==Atom.PCP_OK) {
      }
      else if (atom.Name==Atom.PCP_CHAN) {
        foreach (var c in atom.Children) ProcessAtom(c, writer);
      }
      else if (atom.Name==Atom.PCP_CHAN_PKT) {
        var pkt_type = atom.Children.GetChanPktType();
        var pkt_data = atom.Children.GetChanPktData();
        if (pkt_type!=null && pkt_data!=null) {
          if (pkt_type==Atom.PCP_CHAN_PKT_TYPE_HEAD) {
            channel.ContentHeader = new Content(0, pkt_data);
          }
          else if (pkt_type==Atom.PCP_CHAN_PKT_TYPE_DATA) {
            var pkt_pos = atom.Children.GetChanPktPos();
            if (pkt_pos != null) {
              channel.Contents.Add(new Content((long)pkt_pos, pkt_data));
            }
          }
          else if (pkt_type==Atom.PCP_CHAN_PKT_TYPE_META) {
          }
        }
      }
      else if (atom.Name==Atom.PCP_CHAN_INFO) {
        var name = atom.Children.GetChanInfoName();
        if (name != null) channel.ChannelInfo.Name = name;
        channel.ChannelInfo.Extra.SetChanInfo(atom.Children);
      }
      else if (atom.Name==Atom.PCP_CHAN_TRACK) {
        channel.ChannelInfo.Extra.SetChanTrack(atom.Children);
      }
      else if (atom.Name==Atom.PCP_BCST) {
        var dest = atom.Children.GetBcstDest();
        if (dest==null || dest==core.Host.SessionID) {
          foreach (var c in atom.Children) ProcessAtom(c, writer);
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
          var node = channel.Nodes.FirstOrDefault(x => x.Host.SessionID==session_id);
          if (node == null) {
            node = new Node(new Host());
            node.Host.SessionID = (Guid)session_id;
            channel.Nodes.Add(node);
          }
          node.Host.Extra.Update(atom.Children);
          node.DirectCount = atom.Children.GetHostNumListeners() ?? 0;
          node.RelayCount = atom.Children.GetHostNumRelays() ?? 0;
          var flags1 = atom.Children.GetHostFlags1();
          if (flags1!=null) {
            node.Host.IsFirewalled = (flags1 & Atom.PCP_HOST_FLAGS1_PUSH) != 0;
            node.IsRelayFull = (flags1 & Atom.PCP_HOST_FLAGS1_RELAY) == 0;
            node.IsDirectFull = (flags1 & Atom.PCP_HOST_FLAGS1_DIRECT) == 0;
            node.IsReceiving = (flags1 & Atom.PCP_HOST_FLAGS1_RECV) != 0;
            node.IsControlFull = (flags1 & Atom.PCP_HOST_FLAGS1_CIN) == 0;
          }

          var ip = new IPEndPoint(IPAddress.Any, 0);
          foreach (var c in atom.Children) {
            if (c.Name==Atom.PCP_HOST_IP) {
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
            else if (c.Name==Atom.PCP_HOST_PORT) {
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
        }
      }
      else if (atom.Name==Atom.PCP_QUIT) {
        quit = true;
      }
      return quit;
    }

    public void Close()
    {
    }

    public PCPSourceStream(PeerCastStation.Core.Core core)
    {
      this.core = core;
    }
  }
}
