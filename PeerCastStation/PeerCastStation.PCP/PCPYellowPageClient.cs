using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using PeerCastStation.Core;

namespace PeerCastStation.PCP
{
  public class PCPYellowPageClientFactory
    : IYellowPageClientFactory
  {
    public PeerCast PeerCast { get; private set; }
    public string Name { get { return "PCP"; } }

    public IYellowPageClient Create(string name, Uri uri)
    {
      return new PCPYellowPageClient(PeerCast, name, uri);
    }

    public PCPYellowPageClientFactory(PeerCast peercast)
    {
      this.PeerCast = peercast;
    }
  }

  public class PCPYellowPageClient
    : IYellowPageClient
  {
    public const int DefaultPort = 7144;
    public PeerCast PeerCast { get; private set; }
    public string Name { get; private set; }
    public Uri Uri { get; private set; }

    private string ReadResponse(Stream s)
    {
      var res = new List<byte>();
      do {
        int b = s.ReadByte();
        if (b>=0) res.Add((byte)b);
        else {
          return null;
        }
      } while (
        res.Count<4 ||
        res[res.Count-4]!='\r' ||
        res[res.Count-3]!='\n' ||
        res[res.Count-2]!='\r' ||
        res[res.Count-1]!='\n');
      return System.Text.Encoding.UTF8.GetString(res.ToArray());
    }

    private List<Host> ReadHosts(Stream s, Guid channel_id)
    {
      var res = new List<Host>();
      bool quit = false;
      while (!quit) {
        var atom = AtomReader.Read(s);
        if (atom.Name==Atom.PCP_HOST) {
          if (atom.Children.GetHostChannelID()==channel_id) {
            var host = new HostBuilder();
            var endpoints = atom.Children.GetHostEndPoints();
            if (endpoints.Length>0) host.GlobalEndPoint = endpoints[0];
            if (endpoints.Length>1) host.LocalEndPoint = endpoints[1];
            host.DirectCount = atom.Children.GetHostNumListeners() ?? 0;
            host.RelayCount = atom.Children.GetHostNumRelays() ?? 0;
            host.SessionID = atom.Children.GetHostSessionID() ?? Guid.Empty;
            if (atom.Children.GetHostFlags1().HasValue) {
              var flags = atom.Children.GetHostFlags1().Value;
              host.IsControlFull = (flags & PCPHostFlags1.ControlIn)!=0;
              host.IsFirewalled = (flags & PCPHostFlags1.Firewalled)!=0;
              host.IsDirectFull = (flags & PCPHostFlags1.Direct)==0;
              host.IsRelayFull = (flags & PCPHostFlags1.Relay)==0;
              host.IsReceiving = (flags & PCPHostFlags1.Receiving)!=0;
              host.IsTracker = (flags & PCPHostFlags1.Tracker)!=0;
            }
            res.Add(host.ToHost());
          }
        }
        if (atom.Name==Atom.PCP_QUIT) {
          quit = true;
        }
      }
      return res;
    }

    private Uri HostToUri(Host host, Guid channel_id)
    {
      if (host==null) return null;
      if (host.GlobalEndPoint!=null) {
        return new Uri(
          String.Format(
            "pcp://{0}:{1}/channel/{2}",
            host.GlobalEndPoint.Address,
            host.GlobalEndPoint.Port,
            channel_id.ToString("N")));
      }
      else if (host.LocalEndPoint!=null) {
        return new Uri(
          String.Format(
            "pcp://{0}:{1}/channel/{2}",
            host.LocalEndPoint.Address,
            host.LocalEndPoint.Port,
            channel_id.ToString("N")));
      }
      else {
        return null;
      }
    }

    public Uri FindTracker(Guid channel_id)
    {
      var host = Uri.DnsSafeHost;
      var port = Uri.Port;
      Uri res = null;
      if (port<0) port = DefaultPort;
      try {
        var client = new TcpClient(host, port);
        var stream = client.GetStream();
        var request = System.Text.Encoding.UTF8.GetBytes(
          String.Format("GET /channel/{0} HTTP/1.0\r\n", channel_id.ToString("N")) +
          "x-peercast-pcp:1\r\n" +
          "\r\n");
        stream.Write(request, 0, request.Length);
        var response = ReadResponse(stream);
        var md = System.Text.RegularExpressions.Regex.Match(response, @"^HTTP/1.\d (\d+) ");
        if (md.Success) {
          var status = md.Groups[1].Value;
          switch (status) {
          case "503":
            var helo = new AtomCollection();
            helo.SetHeloAgent(PeerCast.AgentName);
            helo.SetHeloVersion(1218);
            helo.SetHeloSessionID(PeerCast.SessionID);
            helo.SetHeloPort(0);
            AtomWriter.Write(stream, new Atom(Atom.PCP_HELO, helo));
            var hosts = ReadHosts(stream, channel_id);
            res = HostToUri(hosts.FirstOrDefault(h => h.IsTracker), channel_id);
            break;
          case "200":
            //なぜかリレー可能だったのでYP自体をトラッカーとみなしてしまうことにする
            AtomWriter.Write(stream, new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT));
            res = Uri;
            break;
          default:
            //エラーだったのでトラッカーのアドレスを貰えず終了
            break;
          }
        }
        client.Close();
      }
      catch (SocketException)
      {
      }
      return res;
    }

    public void Announce(Channel channel)
    {
      throw new System.NotImplementedException();
    }

    public PCPYellowPageClient(PeerCast peercast, string name, Uri uri)
    {
      this.PeerCast = peercast;
      this.Name = name;
      this.Uri = uri;
    }
  }
}
