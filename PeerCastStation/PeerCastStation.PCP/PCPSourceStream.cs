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
          var rip = ((IPEndPoint)connection.Client.RemoteEndPoint).Address.GetAddressBytes();
          Array.Reverse(rip);
          res.Children.Add(new Atom(Atom.PCP_HELO_REMOTEIP, rip));
        }
        res.Children.Add(new Atom(Atom.PCP_HELO_AGENT,     "PeerCastStation/1.0"));
        res.Children.Add(new Atom(Atom.PCP_HELO_SESSIONID, core.Host.SessionID.ToByteArray()));
        res.Children.Add(new Atom(Atom.PCP_HELO_PORT,      core.Host.Addresses[0].Port));
        res.Children.Add(new Atom(Atom.PCP_HELO_VERSION,   1218));
        writer.Write(res);
      }
      else if (atom.Name==Atom.PCP_OLEH) {
        var rip = atom.Children.FirstOrDefault(x => x.Name==Atom.PCP_HELO_REMOTEIP);
        var ip_ary = new byte[rip.GetBytes().Length];
        rip.GetBytes().CopyTo(ip_ary, 0);
        Array.Reverse(ip_ary);
        var ip = new IPAddress(ip_ary);
        if (!core.Host.Addresses.Any(x => x.Address==ip)) {
          core.Host.Addresses.Add(new IPEndPoint(ip, core.Host.Addresses[0].Port));
        }
      }
      else if (atom.Name==Atom.PCP_OK) {
      }
      else if (atom.Name==Atom.PCP_CHAN) {
        foreach (var c in atom.Children) ProcessAtom(c, writer);
      }
      else if (atom.Name==Atom.PCP_CHAN_PKT) {
        var pkt_type = atom.Children.FindByName(Atom.PCP_CHAN_PKT_TYPE);
        var pkt_data = atom.Children.FindByName(Atom.PCP_CHAN_PKT_DATA);
        if (pkt_type!=null && pkt_data!=null) {
          var type = new ID4(pkt_type.GetBytes());
          if (type==Atom.PCP_CHAN_PKT_TYPE_HEAD) {
            channel.ContentHeader = new Content(0, pkt_data.GetBytes());
          }
          else if (type==Atom.PCP_CHAN_PKT_TYPE_DATA) {
            var pkt_pos = atom.Children.FindByName(Atom.PCP_CHAN_PKT_POS);
            if (pkt_pos != null) {
              channel.Contents.Add(new Content(pkt_pos.GetInt32(), pkt_data.GetBytes()));
            }
          }
          else if (type==Atom.PCP_CHAN_PKT_TYPE_META) {
          }
        }
      }
      else if (atom.Name==Atom.PCP_CHAN_INFO) {
        var name = atom.Children.FindByName(Atom.PCP_CHAN_INFO_NAME);
        if (name!=null) channel.ChannelInfo.Name = name.GetString();
        channel.ChannelInfo.Extra.Add(atom);
      }
      else if (atom.Name==Atom.PCP_CHAN_TRACK) {
        channel.ChannelInfo.Extra.Add(atom);
      }
      else if (atom.Name==Atom.PCP_BCST) {
        var dest = atom.Children.FindByName(Atom.PCP_BCST_DEST);
        if (dest==null || new Guid(dest.GetBytes())==core.Host.SessionID) {
          foreach (var c in atom.Children) ProcessAtom(c, writer);
        }
        var ttl  = atom.Children.FindByName(Atom.PCP_BCST_TTL);
        var hops = atom.Children.FindByName(Atom.PCP_BCST_HOPS);
        var from = atom.Children.FindByName(Atom.PCP_BCST_FROM);
        var group = atom.Children.FindByName(Atom.PCP_BCST_GROUP);
        if (ttl != null && hops != null && group != null && from != null && ttl.GetByte()<hops.GetByte()) {
          //TODO: HOPSを増やしてまわす
        }
      }
      else if (atom.Name == Atom.PCP_HOST) {
        var id = atom.Children.FindByName(Atom.PCP_HOST_ID);
        if (id!=null) {
          var session_id = new Guid(id.GetBytes());
          var node = channel.Nodes.FirstOrDefault(x => x.Host.SessionID==session_id);
          if (node == null) {
            node = new Node(new Host());
            node.Host.SessionID = session_id;
            channel.Nodes.Add(node);
          }
          var ip = new IPEndPoint(IPAddress.Any, 0);
          foreach (var c in atom.Children) {
            if (c.Name==Atom.PCP_HOST_ID) {
            }
            else if (c.Name==Atom.PCP_HOST_IP) {
              var ary = new byte[c.GetBytes().Length];
              c.GetBytes().CopyTo(ary, 0);
              Array.Reverse(ary);
              ip.Address = new IPAddress(ary);
              if (ip.Port != 0) {
                if (node.Host.Addresses.Any(x => x == ip)) {
                  node.Host.Addresses.Add(ip);
                }
                ip = new IPEndPoint(IPAddress.Any, 0);
              }
            }
            else if (c.Name==Atom.PCP_HOST_PORT) {
              ip.Port = c.GetInt16();
              if (ip.Address != IPAddress.Any) {
                if (node.Host.Addresses.Any(x => x == ip)) {
                  node.Host.Addresses.Add(ip);
                }
                ip = new IPEndPoint(IPAddress.Any, 0);
              }
            }
            else if (c.Name==Atom.PCP_HOST_CHANID) {
            }
            else if (c.Name==Atom.PCP_HOST_NUML) {
              node.DirectCount = c.GetInt32();
            }
            else if (c.Name==Atom.PCP_HOST_NUMR) {
              node.RelayCount = c.GetInt32();
            }
            else if (c.Name==Atom.PCP_HOST_FLAGS1) {
              var flags1 = c.GetByte();
              node.Host.IsFirewalled = (flags1 & Atom.PCP_HOST_FLAGS1_PUSH) != 0;
              node.IsRelayFull = (flags1 & Atom.PCP_HOST_FLAGS1_RELAY) == 0;
              node.IsDirectFull = (flags1 & Atom.PCP_HOST_FLAGS1_DIRECT) == 0;
              node.IsReceiving = (flags1 & Atom.PCP_HOST_FLAGS1_RECV) != 0;
              node.IsControlFull = (flags1 & Atom.PCP_HOST_FLAGS1_CIN) == 0;
            }
            else if (c.Name==Atom.PCP_HOST_UPHOST_IP) {
              node.Extra.Add(c);
            }
            else if (c.Name==Atom.PCP_HOST_UPHOST_PORT) {
              node.Extra.Add(c);
            }
            else if (c.Name==Atom.PCP_HOST_UPHOST_HOPS) {
              node.Extra.Add(c);
            }
            else if (c.Name==Atom.PCP_HOST_OLDPOS) {
              node.Extra.Add(c);
            }
            else if (c.Name==Atom.PCP_HOST_NEWPOS) {
              node.Extra.Add(c);
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
