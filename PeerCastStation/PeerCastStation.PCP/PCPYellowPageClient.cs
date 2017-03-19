using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using PeerCastStation.Core;
using System.Threading;
using System.Net;

namespace PeerCastStation.PCP
{
  public class PCPYellowPageClientFactory
    : IYellowPageClientFactory
  {
    public PeerCast PeerCast { get; private set; }
    public string Name { get { return "PCP"; } }
    public string Protocol { get { return "pcp"; } }

		public IYellowPageClient Create(string name, Uri announce_uri, Uri channels_uri)
		{
			return new PCPYellowPageClient(PeerCast, name, announce_uri, channels_uri);
		}

    public bool CheckURI(Uri uri)
    {
      return PCPYellowPageClient.IsValidUri(uri);
    }

    public PCPYellowPageClientFactory(PeerCast peercast)
    {
      this.PeerCast = peercast;
    }

	}

  public class PCPYellowPageClient
    : IYellowPageClient
  {
    protected Logger Logger { get; private set; }
    public PeerCast PeerCast { get; private set; }
    public string Name { get; private set; }
    public string Protocol { get { return "pcp"; } }
		public Uri AnnounceUri { get; private set; }
		public Uri ChannelsUri { get; private set; }
    public IList<IAnnouncingChannel> AnnouncingChannels {
      get {
        lock (announcingChannels) {
          return announcingChannels.Cast<IAnnouncingChannel>().ToList();
        }
      }
    }
    public static bool IsValidUri(Uri uri)
    {
      return uri!=null && uri.IsAbsoluteUri && uri.Scheme=="pcp";
    }

    private class AnnouncingChannel
      : IAnnouncingChannel
    {
      public PCPYellowPageClient Owner     { get; set; }
      public Channel             Channel   { get; set; }
      public bool                IsStopped { get; set; }
      public AnnouncingStatus Status {
        get {
          return IsStopped ? AnnouncingStatus.Idle : Owner.AnnouncingStatus;
        }
      }
      public IYellowPageClient YellowPage {
        get {
          return Owner;
        }
      }
    }
    private List<AnnouncingChannel> announcingChannels = new List<AnnouncingChannel>();
    private AnnouncingStatus AnnouncingStatus { get; set; }

		public class PCPYellowPageChannel
			: IYellowPageChannel
		{
			public IYellowPageClient Source { get; private set; }
			public string Name        { get; set; }
			public Guid ChannelId     { get; set; }
			public string Tracker     { get; set; }
			public string ContentType { get; set; }
			public int? Listeners     { get; set; }
			public int? Relays        { get; set; }
			public int? Bitrate       { get; set; }
			public int? Uptime        { get; set; }
			public string ContactUrl  { get; set; }
			public string Genre       { get; set; }
			public string Description { get; set; }
			public string Comment     { get; set; }
			public string Artist      { get; set; }
			public string TrackTitle  { get; set; }
			public string Album       { get; set; }
			public string TrackUrl    { get; set; }

			public PCPYellowPageChannel(IYellowPageClient source)
			{
				this.Source = source;
			}
		}

    public PCPYellowPageClient(PeerCast peercast, string name, Uri announce_uri, Uri channels_uri)
    {
      this.PeerCast = peercast;
      this.Name = name;
      this.AnnounceUri = announce_uri;
      this.ChannelsUri = channels_uri;
      this.Logger = new Logger(this.GetType());
      this.AnnouncingStatus = AnnouncingStatus.Idle;
    }

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
      try {
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
      }
      catch (InvalidCastException e) {
        Logger.Error(e);
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
      if (!IsValidUri(AnnounceUri)) return null;
      Logger.Debug("Finding tracker {0} from {1}", channel_id.ToString("N"), AnnounceUri);
      var host = AnnounceUri.DnsSafeHost;
      var port = AnnounceUri.Port;
      Uri res = null;
      if (port<0) port = PCPVersion.DefaultPort;
      try {
        var client = new TcpClient(host, port);
        var stream = client.GetStream();
        var request = System.Text.Encoding.UTF8.GetBytes(
          String.Format("GET /channel/{0} HTTP/1.0\r\n", channel_id.ToString("N")) +
          "x-peercast-pcp:1\r\n" +
          "\r\n");
        stream.Write(request, 0, request.Length);
        var response = ReadResponse(stream);
        if (response!=null) {
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
              res = AnnounceUri;
              break;
            default:
              //エラーだったのでトラッカーのアドレスを貰えず終了
              break;
            }
          }
        }
        AtomWriter.Write(stream, new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT));
        client.Close();
      }
      catch (SocketException)
      {
      }
      catch (IOException)
      {
      }
      if (res!=null) {
        Logger.Debug("Tracker found: {0}", res);
      }
      else {
        Logger.Debug("Tracker no found");
      }
      return res;
    }

    private Thread announceThread;
    public IAnnouncingChannel Announce(Channel channel)
    {
      if (!IsValidUri(AnnounceUri)) return null;
      AnnouncingChannel announcing = null;
      lock (announcingChannels) {
        announcing = announcingChannels.FirstOrDefault(a => a.Channel==channel);
        if (announcing!=null) return announcing;
      }
      Logger.Debug("Start announce channel {0} to {1}", channel.ChannelID.ToString("N"), AnnounceUri);
      channel.ChannelInfoChanged  += OnChannelPropertyChanged;
      channel.ChannelTrackChanged += OnChannelPropertyChanged;
      channel.Closed              += OnChannelClosed;
      announcing = new AnnouncingChannel { Channel=channel, Owner=this, IsStopped=false };
      lock (announcingChannels) {
        announcingChannels.Add(announcing);
        if (announceThread==null || !announceThread.IsAlive) {
          isStopped = false;
          restartEvent.Reset();
          announceThread = new Thread(AnnounceThreadProc);
          announceThread.Name = String.Format("PCPYP {0} Announce", AnnounceUri);
          announceThread.Start();
        }
      }
      return announcing;
    }

    public void StopAnnounce(IAnnouncingChannel announcing)
    {
      var achan = announcing as AnnouncingChannel;
      if (achan!=null) {
        lock (announcingChannels) {
          if (announcingChannels.Remove(achan)) {
            achan.IsStopped = true;
            UpdateChannelInfo(achan.Channel, false);
          }
        }
      }
    }

    public void StopAnnounce()
    {
      lock (announcingChannels) {
        foreach (var announcing in announcingChannels) {
          announcing.IsStopped = true;
          UpdateChannelInfo(announcing.Channel, false);
        }
        announcingChannels.Clear();
      }
      if (announceThread!=null && announceThread.IsAlive) {
        announceThread.Join();
      }
    }

    public void RestartAnnounce(IAnnouncingChannel announcing)
    {
      RestartAnnounce();
    }

    public void RestartAnnounce()
    {
      if (announceThread==null || !announceThread.IsAlive) {
        lock (announcingChannels) {
          if (announcingChannels.Count>0) {
            isStopped = false;
            restartEvent.Reset();
            announceThread = new Thread(AnnounceThreadProc);
            announceThread.Name = String.Format("PCPYP {0} Announce", AnnounceUri);
            announceThread.Start();
          }
        }
      }
      else {
        restartEvent.Set();
      }
    }

    private void OnPCPBcst(Atom atom)
    {
      var channel_id = atom.Children.GetBcstChannelID();
      if (channel_id!=null) {
        Channel channel;
        lock (announcingChannels) {
          channel = announcingChannels.Find(c => c.Channel.ChannelID==channel_id).Channel;
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
      Logger.Debug("Connection aborted by PCP_QUIT ({0})", atom.GetInt32());
      throw new QuitException();
    }

    private void ProcessAtom(Atom atom)
    {
           if (atom.Name==Atom.PCP_BCST) OnPCPBcst(atom);
      else if (atom.Name==Atom.PCP_QUIT) OnPCPQuit(atom);
    }

    private bool isStopped;
    private bool IsStopped {
      get {
        lock (announcingChannels) {
          return isStopped || announcingChannels.Count==0;
        }
      }
    }
    private class UpdatedChannel {
      public Channel Channel { get; private set; }
      public bool    Playing { get; private set; }
      public UpdatedChannel(Channel channel, bool playing)
      {
        this.Channel = channel;
        this.Playing = playing;
      }
    }
    private List<UpdatedChannel> updatedChannels = new List<UpdatedChannel>();
    private AutoResetEvent restartEvent = new AutoResetEvent(false);
    private class RestartException : Exception {}
    private class QuitException : Exception {}
    private class BannedException : Exception {}
    private IPEndPoint remoteEndPoint;
    private void AnnounceThreadProc()
    {
      Logger.Debug("Thread started");
      var host = AnnounceUri.DnsSafeHost;
      var port = AnnounceUri.Port;
      if (port<0) port = PCPVersion.DefaultPort;
      while (!IsStopped) {
        int next_update = Environment.TickCount;
        try {
          Logger.Debug("Connecting to YP");
          AnnouncingStatus = AnnouncingStatus.Connecting;
          using (var client = new TcpClient(host, port)) {
            remoteEndPoint = (IPEndPoint)client.Client.RemoteEndPoint;
            using (var stream = client.GetStream()) {
              AtomWriter.Write(stream, new Atom(new ID4("pcp\n"), (int)1));
              var helo = new AtomCollection();
              Logger.Debug("Sending Handshake");
              helo.SetHeloAgent(PeerCast.AgentName);
              helo.SetHeloVersion(1218);
              helo.SetHeloSessionID(PeerCast.SessionID);
              helo.SetHeloBCID(PeerCast.BroadcastID);
              if (PeerCast.IsFirewalled.HasValue) {
                if (PeerCast.IsFirewalled.Value) {
                  //Do nothing
                }
                else {
                  var listener = PeerCast.FindListener(
                    ((IPEndPoint)client.Client.RemoteEndPoint).Address,
                    OutputStreamType.Relay | OutputStreamType.Metadata);
                  if (listener!=null) {
                    helo.SetHeloPort(listener.LocalEndPoint.Port);
                  }
                }
              }
              else {
                var listener = PeerCast.FindListener(
                  ((IPEndPoint)client.Client.RemoteEndPoint).Address,
                  OutputStreamType.Relay | OutputStreamType.Metadata);
                if (listener!=null) {
                  helo.SetHeloPing(listener.LocalEndPoint.Port);
                }
              }
              AtomWriter.Write(stream, new Atom(Atom.PCP_HELO, helo));
              while (!IsStopped) {
                var atom = AtomReader.Read(stream);
                if (atom.Name==Atom.PCP_OLEH) {
                  OnPCPOleh(atom);
                  break;
                }
                else if (atom.Name==Atom.PCP_QUIT) {
                  Logger.Debug("Handshake aborted by PCP_QUIT ({0})", atom.GetInt32());
                  throw new QuitException();
                }
                if (restartEvent.WaitOne(10)) throw new RestartException();
              }
              Logger.Debug("Handshake succeeded");
              AnnouncingStatus = AnnouncingStatus.Connected;
              while (!IsStopped) {
                if (next_update-Environment.TickCount<=0) {
                  Logger.Debug("Sending channel info");
                  lock (announcingChannels) {
                    foreach (var announcing in announcingChannels) {
                      UpdateChannelInfo(announcing.Channel, true);
                    }
                  }
                  next_update = Environment.TickCount+30000;
                }
                if (stream.DataAvailable) {
                  Atom atom = AtomReader.Read(stream);
                  ProcessAtom(atom);
                }
                lock (updatedChannels) {
                  foreach (var updated in updatedChannels) {
                    AtomWriter.Write(stream, CreateChannelBcst(updated.Channel, updated.Playing));
                  }
                  updatedChannels.Clear();
                }
                if (restartEvent.WaitOne(10)) throw new RestartException();
              }
              lock (updatedChannels) {
                foreach (var updated in updatedChannels) {
                  AtomWriter.Write(stream, CreateChannelBcst(updated.Channel, updated.Playing));
                }
                updatedChannels.Clear();
              }
              Logger.Debug("Closing connection");
              AtomWriter.Write(stream, new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT));
            }
          }
        }
        catch (RestartException) {
          Logger.Debug("Connection retrying");
          AnnouncingStatus = AnnouncingStatus.Connecting;
        }
        catch (BannedException) {
          AnnouncingStatus = AnnouncingStatus.Error;
          Logger.Error("Your BCID is banned");
          break;
        }
        catch (QuitException) {
          AnnouncingStatus = AnnouncingStatus.Error;
        }
        catch (InvalidDataException e) {
          AnnouncingStatus = AnnouncingStatus.Error;
          Logger.Error(e);
          break;
        }
        catch (SocketException e) {
          AnnouncingStatus = AnnouncingStatus.Error;
          Logger.Info(e);
        }
        catch (IOException e) {
          AnnouncingStatus = AnnouncingStatus.Error;
          Logger.Info(e);
        }
        finally {
          remoteEndPoint = null;
        }
        Logger.Debug("Connection closed");
        if (!IsStopped) {
          restartEvent.WaitOne(10000);
        }
        else {
          AnnouncingStatus = AnnouncingStatus.Idle;
        }
      }
      Logger.Debug("Thread finished");
    }

    private void OnPCPOleh(Atom atom)
    {
      var dis = atom.Children.GetHeloDisable();
      if (dis!=null && dis.Value!=0) {
      }
      var rip = atom.Children.GetHeloRemoteIP();
      if (rip!=null) {
        switch (rip.AddressFamily) {
        case AddressFamily.InterNetwork:
          if (PeerCast.GlobalAddress==null ||
              PeerCast.GlobalAddress.GetAddressLocality()<=rip.GetAddressLocality()) {
            PeerCast.GlobalAddress = rip;
          }
          break;
        case AddressFamily.InterNetworkV6:
          if (PeerCast.GlobalAddress6==null ||
              PeerCast.GlobalAddress6.GetAddressLocality()<=rip.GetAddressLocality()) {
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
      var hostinfo = new AtomCollection();
      hostinfo.SetHostChannelID(channel.ChannelID);
      hostinfo.SetHostSessionID(PeerCast.SessionID);
      var globalendpoint = PeerCast.GetGlobalEndPoint(AddressFamily.InterNetwork, OutputStreamType.Relay);
      if (globalendpoint!=null) {
        hostinfo.AddHostIP(globalendpoint.Address);
        hostinfo.AddHostPort(globalendpoint.Port);
      }
      var localendpoint = PeerCast.GetLocalEndPoint(AddressFamily.InterNetwork, OutputStreamType.Relay);
      if (localendpoint!=null) {
        hostinfo.AddHostIP(localendpoint.Address);
        hostinfo.AddHostPort(localendpoint.Port);
      }
      hostinfo.SetHostNumListeners(channel.TotalDirects);
      hostinfo.SetHostNumRelays(channel.TotalRelays);
      hostinfo.SetHostUptime(channel.Uptime);
      if (channel.Contents.Count > 0) {
        hostinfo.SetHostOldPos((uint)(channel.Contents.Oldest.Position & 0xFFFFFFFFU));
        hostinfo.SetHostNewPos((uint)(channel.Contents.Newest.Position & 0xFFFFFFFFU));
      }
      PCPVersion.SetHostVersion(hostinfo);
      var relayable = PeerCast.AccessController.IsChannelRelayable(channel);
      var playable  = PeerCast.AccessController.IsChannelPlayable(channel) && PeerCast.FindListener(remoteEndPoint.Address, OutputStreamType.Play)!=null;
      var firewalled = !PeerCast.IsFirewalled.HasValue || PeerCast.IsFirewalled.Value || PeerCast.FindListener(remoteEndPoint.Address, OutputStreamType.Relay)==null;
      var receiving = playing && channel.Status==SourceStreamStatus.Receiving;
      hostinfo.SetHostFlags1(
        (relayable  ? PCPHostFlags1.Relay      : 0) |
        (playable   ? PCPHostFlags1.Direct     : 0) |
        (firewalled ? PCPHostFlags1.Firewalled : 0) |
        PCPHostFlags1.Tracker |
        (receiving ? PCPHostFlags1.Receiving : PCPHostFlags1.None));
      parent.SetHost(hostinfo);
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

    private Atom CreateChannelBcst(Channel channel, bool playing)
    {
      var bcst = new AtomCollection();
      bcst.SetBcstTTL(1);
      bcst.SetBcstHops(0);
      bcst.SetBcstFrom(PeerCast.SessionID);
      PCPVersion.SetBcstVersion(bcst);
      bcst.SetBcstChannelID(channel.ChannelID);
      bcst.SetBcstGroup(BroadcastGroup.Root);
      PostChannelInfo(bcst, channel);
      PostHostInfo(bcst, channel, playing);
      return new Atom(Atom.PCP_BCST, bcst);
    }

    private void UpdateChannelInfo(Channel channel, bool playing)
    {
      lock (updatedChannels) {
        updatedChannels.Add(new UpdatedChannel(channel, playing));
      }
    }

    private void OnChannelPropertyChanged(object sender, EventArgs e)
    {
      var channel = sender as Channel;
      if (channel!=null) {
        UpdateChannelInfo(channel, true);
      }
    }

    private void OnChannelClosed(object sender, EventArgs e)
    {
      var channel = sender as Channel;
      if (channel==null) return;
      channel.Closed              -= OnChannelClosed;
      channel.ChannelInfoChanged  -= OnChannelPropertyChanged;
      channel.ChannelTrackChanged -= OnChannelPropertyChanged;
      lock (announcingChannels) {
        var announcing = announcingChannels.FirstOrDefault(a => a.Channel==channel);
        if (announcing!=null) {
          announcing.IsStopped = true;
          announcingChannels.Remove(announcing);
        }
        UpdateChannelInfo(channel, false);
      }
    }

    public ConnectionInfo GetConnectionInfo()
    {
      ConnectionStatus status = ConnectionStatus.Idle;
      switch (AnnouncingStatus) {
      case Core.AnnouncingStatus.Connected:  status = ConnectionStatus.Connected; break;
      case Core.AnnouncingStatus.Connecting: status = ConnectionStatus.Connecting; break;
      case Core.AnnouncingStatus.Error:      status = ConnectionStatus.Error; break;
      case Core.AnnouncingStatus.Idle:       status = ConnectionStatus.Idle; break;
      }
      var host_status = RemoteHostStatus.None;
      var rhost = remoteEndPoint;
      if (rhost!=null) {
        host_status |= RemoteHostStatus.Root;
        if (rhost.Address.IsSiteLocal()) host_status |= RemoteHostStatus.Local;
      }
      return new ConnectionInfoBuilder {
        ProtocolName     = "PCP COUT",
        Type             = ConnectionType.Announce,
        Status           = status,
        RemoteName       = Name,
        RemoteEndPoint   = rhost,
        RemoteHostStatus = host_status,
      }.Build();
    }

		public async System.Threading.Tasks.Task<IEnumerable<IYellowPageChannel>> GetChannelsAsync(CancellationToken cancel_token)
		{
			if (ChannelsUri==null) return Enumerable.Empty<IYellowPageChannel>();
			var client = new WebClient();
			client.Encoding = System.Text.Encoding.UTF8;
			cancel_token.Register(() => client.CancelAsync());
			try {
				using (var reader=new StringReader(await client.DownloadStringTaskAsync(this.ChannelsUri))) {
					var results = new List<IYellowPageChannel>();
					var line = reader.ReadLine();
					while (line!=null) {
						var tokens = line.Split(new string[] { "<>" }, StringSplitOptions.None);
						var channel = new PCPYellowPageChannel(this);
						if (tokens.Length> 0) channel.Name        = ParseStr(tokens[0]);  //1 CHANNEL_NAME チャンネル名
						if (tokens.Length> 1) channel.ChannelId   = ParseGuid(tokens[1]);  //2 ID ID ユニーク値16進数32桁、制限チャンネルは全て0埋め
						if (tokens.Length> 2) channel.Tracker     = ParseStr(tokens[2]);  //3 TIP TIP ポートも含む。Push配信時はブランク、制限チャンネルは127.0.0.1
						if (tokens.Length> 3) channel.ContactUrl  = ParseStr(tokens[3]);  //4 CONTACT_URL コンタクトURL 基本的にURL、任意の文字列も可 CONTACT_URL
						if (tokens.Length> 4) channel.Genre       = ParseStr(tokens[4]);  //5 GENRE ジャンル
						if (tokens.Length> 5) channel.Description = ParseStr(tokens[5]);  //6 DETAIL 詳細
						if (tokens.Length> 6) channel.Listeners   = ParseInt(tokens[6]);  //7 LISTENER_NUM Listener数 -1は非表示、-1未満はサーバのメッセージ。ブランクもあるかも
						if (tokens.Length> 7) channel.Relays      = ParseInt(tokens[7]);  //8 RELAY_NUM Relay数 同上 
						if (tokens.Length> 8) channel.Bitrate     = ParseInt(tokens[8]);  //9 BITRATE Bitrate 単位は kbps 
						if (tokens.Length> 9) channel.ContentType = ParseStr(tokens[9]);  //10 TYPE Type たぶん大文字 
						if (tokens.Length>10) channel.Artist      = ParseStr(tokens[10]); //11 TRACK_ARTIST トラック アーティスト 
						if (tokens.Length>11) channel.Album       = ParseStr(tokens[11]); //12 TRACK_ALBUM トラック アルバム 
						if (tokens.Length>12) channel.TrackTitle  = ParseStr(tokens[12]); //13 TRACK_TITLE トラック タイトル 
						if (tokens.Length>13) channel.TrackUrl    = ParseStr(tokens[13]); //14 TRACK_CONTACT_URL トラック コンタクトURL 基本的にURL、任意の文字列も可 
						if (tokens.Length>15) channel.Uptime      = ParseUptime(tokens[15]); //16 BROADCAST_TIME 配信時間 000〜99999 
						if (tokens.Length>17) channel.Comment     = ParseStr(tokens[17]); //18 COMMENT コメント 
						results.Add(channel);
						line = reader.ReadLine();
					}
					return results;
				}
			}
			catch (Exception e) {
				Logger.Error(e);
				return Enumerable.Empty<IYellowPageChannel>();
			}
		}

		private int? ParseUptime(string token)
		{
			if (String.IsNullOrWhiteSpace(token)) return null;
			var times = token.Split(':');
			if (times.Length<2) return ParseInt(times[0]);
			var hours   = ParseInt(times[0]);
			var minutes = ParseInt(times[1]);
			if (!hours.HasValue || !minutes.HasValue) return null;
			return (hours*60 + minutes)*60;
		}

		private string ParseStr(string token)
		{
			if (String.IsNullOrWhiteSpace(token)) return token;
			return System.Net.WebUtility.HtmlDecode(token);
		}

		private Guid ParseGuid(string token)
		{
			if (String.IsNullOrWhiteSpace(token)) return Guid.Empty;
			Guid result;
			if (Guid.TryParse(token, out result)) {
				return result;
			}
			return Guid.Empty;
		}

		private int? ParseInt(string token)
		{
			int result;
			if (token==null || !Int32.TryParse(token, out result)) return null;
			return result;
		}
	}

  [Plugin]
  class PCPYellowPageClientPlugin
    : PluginBase
  {
    override public string Name { get { return "PCP YellowPage Client"; } }

    private PCPYellowPageClientFactory factory;
    override protected void OnAttach()
    {
      if (factory==null) factory = new PCPYellowPageClientFactory(Application.PeerCast);
      Application.PeerCast.YellowPageFactories.Add(factory);
    }

    override protected void OnDetach()
    {
      Application.PeerCast.YellowPageFactories.Remove(factory);
    }
  }
}
