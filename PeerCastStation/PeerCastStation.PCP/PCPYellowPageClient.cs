using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using PeerCastStation.Core;
using System.Threading;
using System.ComponentModel;
using System.Net;

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
    private const int PCP_VERSION    = 1218;
    private const int PCP_VERSION_VP = 27;
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
        AtomWriter.Write(stream, new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT));
        client.Close();
      }
      catch (SocketException)
      {
      }
      return res;
    }

    private Thread announceThread;
    private List<Channel> channels = new List<Channel>();
    public void Announce(Channel channel)
    {
      if (channels.Contains(channel)) return;
      channel.PropertyChanged += OnChannelPropertyChanged;
      channel.Closed += OnChannelClosed;
      lock (channels) {
        channels.Add(channel);
      }
      if (announceThread==null || !announceThread.IsAlive) {
        isStopped = false;
        restartEvent.Reset();
        announceThread = new Thread(AnnounceThreadProc);
        announceThread.Start();
      }
    }

    private void OnPCPBcst(Atom atom)
    {
      var channel_id = atom.Children.GetBcstChannelID();
      if (channel_id!=null) {
        Channel channel;
        lock (channels) {
          channel = channels.Find(c => c.ChannelID==channel_id);
        }
        var group = atom.Children.GetBcstGroup();
        var from  = atom.Children.GetBcstFrom();
        var ttl   = atom.Children.GetBcstTTL();
        var hops  = atom.Children.GetBcstHops();
        if (channel!=null && group!=null && from!=null && ttl!=null && ttl.Value>0) {
          var bcst = new AtomCollection(atom.Children);
          bcst.SetBcstTTL((byte)(ttl.Value-1));
          bcst.SetBcstHops((byte)((hops ?? 0)+1));
          channel.Broadcast(null, new Atom(Atom.PCP_BCST, bcst), group.Value);
        }
      }
    }

    private void OnPCPQuit(Atom atom)
    {
      isStopped = true;
    }

    private void ProcessAtom(Atom atom)
    {
           if (atom.Name==Atom.PCP_BCST) OnPCPBcst(atom);
      else if (atom.Name==Atom.PCP_QUIT) OnPCPQuit(atom);
    }

    private bool isStopped;
    private List<Atom> posts = new List<Atom>();
    private AutoResetEvent restartEvent = new AutoResetEvent(false);
    private class RestartException : Exception {}
    private void AnnounceThreadProc()
    {
      var host = Uri.DnsSafeHost;
      var port = Uri.Port;
      if (port<0) port = DefaultPort;
      while (!isStopped) {
        int last_updated = 0;
        posts.Clear();
        try {
          using (var client = new TcpClient(host, port)) {
            using (var stream = client.GetStream()) {
              AtomWriter.Write(stream, new Atom(new ID4("pcp\n"), (int)1));
              var helo = new AtomCollection();
              helo.SetHeloAgent(PeerCast.AgentName);
              helo.SetHeloVersion(1218);
              helo.SetHeloSessionID(PeerCast.SessionID);
              helo.SetHeloBCID(PeerCast.BroadcastID);
              if (PeerCast.IsFirewalled.HasValue) {
                if (PeerCast.IsFirewalled.Value) {
                  //Do nothing
                }
                else {
                  helo.SetHeloPort((short)PeerCast.LocalEndPoint.Port);
                }
              }
              else {
                helo.SetHeloPing((short)PeerCast.LocalEndPoint.Port);
              }
              AtomWriter.Write(stream, new Atom(Atom.PCP_HELO, helo));
              while (!isStopped) {
                var atom = AtomReader.Read(stream);
								if (atom.Name==Atom.PCP_OLEH) {
									OnPCPOleh(atom);
									break;
								}
                if (restartEvent.WaitOne(1)) throw new RestartException();
              }
              while (!isStopped) {
                if (Environment.TickCount-last_updated>30000) {
                  lock (channels) {
                    foreach (var channel in channels) {
                      PostChannelBcst(channel, true);
                    }
                  }
                  last_updated = Environment.TickCount;
                }
                if (stream.DataAvailable) {
                  Atom atom = AtomReader.Read(stream);
                  ProcessAtom(atom);
                }
                lock (posts) {
                  foreach (var atom in posts) {
                    AtomWriter.Write(stream, atom);
                  }
                  posts.Clear();
                }
                if (restartEvent.WaitOne(1)) throw new RestartException();
              }
              lock (posts) {
                foreach (var atom in posts) {
                  AtomWriter.Write(stream, atom);
                }
                posts.Clear();
              }
			        AtomWriter.Write(stream, new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT));
            }
          }
        }
        catch (RestartException) {
        }
        catch (SocketException) {
        }
        catch (IOException) {
        }
        if (!isStopped) Thread.Sleep(10000);
      }
    }

    private void OnPCPOleh(Atom atom)
    {
      var rip = atom.Children.GetHeloRemoteIP();
      if (rip!=null) {
        switch (rip.AddressFamily) {
        case AddressFamily.InterNetwork:
          if (PeerCast.GlobalAddress==null || !PeerCast.GlobalAddress.Equals(rip)) {
            PeerCast.GlobalAddress = rip;
          }
          break;
        case AddressFamily.InterNetworkV6:
          if (PeerCast.GlobalAddress6==null || !PeerCast.GlobalAddress6.Equals(rip)) {
            PeerCast.GlobalAddress6 = rip;
          }
          break;
        }
      }
      var port = atom.Children.GetHeloPort();
      if (port.HasValue) {
        PeerCast.IsFirewalled = port.Value==0;
      }
    }

    private void PostHostInfo(AtomCollection parent, Channel channel, bool playing)
    {
      var host = new AtomCollection();
      host.SetHostChannelID(channel.ChannelID);
      host.SetHostSessionID(PeerCast.SessionID);
      var globalendpoint = PeerCast.GlobalEndPoint ?? new IPEndPoint(IPAddress.Loopback, 7144);
      host.AddHostIP(globalendpoint.Address);
      host.AddHostPort((short)globalendpoint.Port);
      var localendpoint = PeerCast.LocalEndPoint ?? new IPEndPoint(IPAddress.Loopback, 7144);
      host.AddHostIP(localendpoint.Address);
      host.AddHostPort((short)localendpoint.Port);
      host.SetHostNumListeners(channel.OutputStreams.CountPlaying);
      host.SetHostNumRelays(channel.OutputStreams.CountRelaying);
      host.SetHostUptime(channel.Uptime);
      if (channel.Contents.Count > 0) {
        host.SetHostOldPos((uint)(channel.Contents.Oldest.Position & 0xFFFFFFFFU));
        host.SetHostNewPos((uint)(channel.Contents.Newest.Position & 0xFFFFFFFFU));
      }
      host.SetHostVersion(PCP_VERSION);
      host.SetHostVersionVP(PCP_VERSION_VP);
      host.SetHostFlags1(
        (PeerCast.AccessController.IsChannelRelayable(channel) ? PCPHostFlags1.Relay : 0) |
        (PeerCast.AccessController.IsChannelPlayable(channel) ? PCPHostFlags1.Direct : 0) |
        ((!PeerCast.IsFirewalled.HasValue || PeerCast.IsFirewalled.Value) ? PCPHostFlags1.Firewalled : 0) |
        PCPHostFlags1.Tracker |
				(playing ? PCPHostFlags1.Receiving : PCPHostFlags1.None));
      parent.SetHost(host);
    }

    private void PostChannelInfo(AtomCollection parent, Channel channel)
    {
      var atom = new AtomCollection();
      atom.SetChanID(channel.ChannelID);
      atom.SetChanBCID(PeerCast.BroadcastID);
      if (channel.ChannelInfo!=null)  atom.SetChanInfo(channel.ChannelInfo.Extra);
      if (channel.ChannelTrack!=null) atom.SetChanTrack(channel.ChannelTrack.Extra);
      parent.SetChan(atom);
    }

    private void PostChannelBcst(Channel channel, bool playing)
    {
      var bcst = new AtomCollection();
      bcst.SetBcstTTL(1);
      bcst.SetBcstHops(0);
      bcst.SetBcstFrom(PeerCast.SessionID);
      bcst.SetBcstVersion(PCP_VERSION);
      bcst.SetBcstVersionVP(PCP_VERSION_VP);
      bcst.SetBcstChannelID(channel.ChannelID);
      bcst.SetBcstGroup(BroadcastGroup.Root);
      PostChannelInfo(bcst, channel);
      PostHostInfo(bcst, channel, playing);
      lock (posts) posts.Add(new Atom(Atom.PCP_BCST, bcst));
    }

    private void OnChannelPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
      var channel = sender as Channel;
      if (channel!=null && e.PropertyName=="ChannelInfo" || e.PropertyName=="ChannelTrack") {
        PostChannelBcst(channel, true);
      }
    }

    private void OnChannelClosed(object sender, EventArgs e)
    {
      var channel = sender as Channel;
      if (channel!=null) {
        channel.Closed -= OnChannelClosed;
        channel.PropertyChanged -= OnChannelPropertyChanged;
        lock (channels) {
          channels.Remove(channel);
        }
				PostChannelBcst(channel, true);
        if (channels.Count==0) isStopped = true;
      }
    }

    public void StopAnnounce()
    {
      isStopped = true;
      if (announceThread!=null) {
        announceThread.Join();
      }
    }

    public void RestartAnnounce()
    {
      restartEvent.Set();
    }

    public PCPYellowPageClient(PeerCast peercast, string name, Uri uri)
    {
      this.PeerCast = peercast;
      this.Name = name;
      this.Uri = uri;
    }
  }
}
