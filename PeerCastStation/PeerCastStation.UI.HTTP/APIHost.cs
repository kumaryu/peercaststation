using System;
using System.Linq;
using System.IO;
using System.Net;
using System.Collections.Generic;
using PeerCastStation.Core;
using PeerCastStation.HTTP;
using PeerCastStation.UI.HTTP.JSONRPC;
using PeerCastStation.UI;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.UI.HTTP
{
  [Plugin]
  public class APIHost
    : PluginBase,
      IUserInterfacePlugin
  {
    override public string Name { get { return "HTTP API Host UI"; } }
    public LogWriter LogWriter { get { return logWriter; } }
    private LogWriter logWriter = new LogWriter(1000);
    private Updater updater = new Updater();
    private IEnumerable<VersionDescription> newVersions = Enumerable.Empty<VersionDescription>();

    private ObjectIdRegistry idRegistry = new ObjectIdRegistry();
    private APIHostOutputStreamFactory factory;
    override protected void OnAttach()
    {
      factory = new APIHostOutputStreamFactory(this, Application.PeerCast);
      Application.PeerCast.OutputStreamFactories.Add(factory);
    }

    protected override void OnStart()
    {
      Logger.AddWriter(logWriter);
      updater.NewVersionFound += OnNewVersionFound;
      updater.CheckVersion();
    }

    protected override void OnStop()
    {
      Logger.RemoveWriter(logWriter);
    }

    protected override void OnDetach()
    {
      Application.PeerCast.OutputStreamFactories.Remove(factory);
    }

    void OnNewVersionFound(object sender, NewVersionFoundEventArgs args)
    {
      foreach (var plugin in Application.Plugins
          .Select(p => p as IUserInterfacePlugin)
          .Where(p => p!=null)) {
        plugin.ShowNotificationMessage(
          new NewVersionNotificationMessage(args.VersionDescriptions));
      }
    }

    public int[] OpenedPorts { get; set; }

    public void CheckVersion()
    {
      updater.CheckVersion();
    }

    public IEnumerable<VersionDescription> GetNewVersions()
    {
      return newVersions;
    }

    private List<NotificationMessage> notificationMessages = new List<NotificationMessage>();
    public void ShowNotificationMessage(NotificationMessage msg)
    {
      lock (notificationMessages) {
        notificationMessages.Add(msg);
        if (msg is NewVersionNotificationMessage) {
          newVersions = ((NewVersionNotificationMessage)msg).VersionDescriptions;
        }
      }
    }

    public IEnumerable<NotificationMessage> GetNotificationMessages()
    {
      lock (notificationMessages) {
        var result = notificationMessages.ToArray();
        notificationMessages.Clear();
        return result;
      }
    }

		public IEnumerable<IYellowPageChannel> GetYPChannels()
		{
			var channel_list = Application.Plugins.FirstOrDefault(plugin => plugin is YPChannelList) as YPChannelList;
			if (channel_list==null) return Enumerable.Empty<IYellowPageChannel>();
			return channel_list.Channels;
		}

		public IEnumerable<IYellowPageChannel> UpdateYPChannels()
		{
			var channel_list = Application.Plugins.FirstOrDefault(plugin => plugin is YPChannelList) as YPChannelList;
			if (channel_list==null) return Enumerable.Empty<IYellowPageChannel>();
			return channel_list.Update();
		}

    public class APIHostOutputStream
      : OutputStreamBase
    {
      APIHost owner;
      HTTPRequest request;
      JSONRPCHost rpcHost;
      public APIHostOutputStream(
        APIHost owner,
        PeerCast peercast,
        Stream input_stream,
        Stream output_stream,
        EndPoint remote_endpoint,
        AccessControlInfo access_control,
        HTTPRequest request,
        byte[] header)
        : base(peercast, input_stream, output_stream, remote_endpoint, access_control, null, header)
      {
        this.owner   = owner;
        this.request = request;
        this.rpcHost = new JSONRPCHost(this);
        Logger.Debug("Initialized: Remote {0}", remote_endpoint);
      }

      private int GetObjectId(object obj)
      {
        return owner.idRegistry.GetId(obj);
      }

      public override ConnectionInfo GetConnectionInfo()
      {
        ConnectionStatus status = ConnectionStatus.Connected;
        if (IsStopped) {
          status = HasError ? ConnectionStatus.Error : ConnectionStatus.Idle;
        }
        return new ConnectionInfo(
          "API Host",
          ConnectionType.Interface,
          status,
          RemoteEndPoint.ToString(),
          (IPEndPoint)RemoteEndPoint,
          IsLocal ? RemoteHostStatus.Local : RemoteHostStatus.None,
          null,
          Connection.ReadRate,
          Connection.WriteRate,
          null,
          null,
          request.Headers["USER-AGENT"]);
      }

      [RPCMethod("getVersionInfo")]
      private JObject GetVersionInfo()
      {
        var res = new JObject();
        res["agentName"]  = PeerCast.AgentName;
        res["apiVersion"] = "1.0.0";
        res["jsonrpc"]    = "2.0";
        return res;
      }

      [RPCMethod("getAuthToken")]
      public string GetAuthToken()
      {
        if (AccessControlInfo.AuthenticationKey!=null) {
          return HTTPUtils.CreateAuthorizationToken(AccessControlInfo.AuthenticationKey);
        }
        else {
          return null;
        }
      }

      [RPCMethod("getPlugins")]
      private JArray GetPlugins()
      {
        var res = new JArray(owner.Application.Plugins.Select(plugin => {
          var jplugin = new JObject();
          jplugin["name"]     = plugin.Name;
          jplugin["isUsable"] = plugin.IsUsable;
          var jassembly = new JObject();
          var info = plugin.GetVersionInfo();
          jassembly["name"]      = info.AssemblyName;
          jassembly["path"]      = info.FileName;
          jassembly["version"]   = info.Version;
          jassembly["copyright"] = info.Copyright;
          jplugin["assembly"] = jassembly;
          return jplugin;
        }));
        return res;
      }

      [RPCMethod("getStatus")]
      private JObject GetStatus()
      {
        var res = new JObject();
        res["uptime"]       = (int)PeerCast.Uptime.TotalSeconds;
        res["isFirewalled"] = PeerCast.IsFirewalled;
        var endpoint = 
          PeerCast.GetGlobalEndPoint(
            System.Net.Sockets.AddressFamily.InterNetwork,
            Core.OutputStreamType.Relay);
        res["globalRelayEndPoint"]  = endpoint!=null ? new JArray(endpoint.Address.ToString(), endpoint.Port) : null;
        endpoint = 
          PeerCast.GetGlobalEndPoint(
            System.Net.Sockets.AddressFamily.InterNetwork,
            Core.OutputStreamType.Play);
        res["globalDirectEndPoint"] = endpoint!=null ? new JArray(endpoint.Address.ToString(), endpoint.Port) : null;
        endpoint = 
          PeerCast.GetLocalEndPoint(
            System.Net.Sockets.AddressFamily.InterNetwork,
            Core.OutputStreamType.Relay);
        res["localRelayEndPoint"]   = endpoint!=null ? new JArray(endpoint.Address.ToString(), endpoint.Port) : null;
        endpoint = 
          PeerCast.GetLocalEndPoint(
            System.Net.Sockets.AddressFamily.InterNetwork,
            Core.OutputStreamType.Play);
        res["localDirectEndPoint"]  = endpoint!=null ? new JArray(endpoint.Address.ToString(), endpoint.Port) : null;
        return res;
      }

      [RPCMethod("getExternalIPAddresses")]
      private JArray GetExternalIPAddresses()
      {
        var port_mapper = PeerCastApplication.Current.Plugins.GetPlugin<PeerCastStation.UI.PortMapperPlugin>();
        if (port_mapper!=null) {
          return new JArray(
            port_mapper.GetExternalAddresses().Select(addr => addr.ToString())
          );
        }
        else {
          return new JArray();
        }
      }

      [RPCMethod("getSettings")]
      private JToken GetSettings()
      {
        var res = new JObject();
        res["maxRelays"]                 = PeerCast.AccessController.MaxRelays;
        res["maxRelaysPerChannel"]       = PeerCast.AccessController.MaxRelaysPerChannel;
        res["maxDirects"]                = PeerCast.AccessController.MaxPlays;
        res["maxDirectsPerChannel"]      = PeerCast.AccessController.MaxPlaysPerChannel;
        res["maxUpstreamRate"]           = PeerCast.AccessController.MaxUpstreamRate;
        res["maxUpstreamRatePerChannel"] = PeerCast.AccessController.MaxUpstreamRatePerChannel;
        var channelCleaner = new JObject();
        channelCleaner["mode"]          = (int)ChannelCleaner.Mode;
        channelCleaner["inactiveLimit"] = ChannelCleaner.InactiveLimit;
        res["channelCleaner"] = channelCleaner;
        var port_mapper = PeerCastApplication.Current.Plugins.GetPlugin<PeerCastStation.UI.PortMapperPlugin>();
        if (port_mapper!=null) {
          var portMapper = new JObject();
          portMapper["enabled"] = port_mapper.Enabled;
          res["portMapper"] = portMapper;
        }
        return res;
      }

      [RPCMethod("setSettings")]
      private void SetSettings(JObject settings)
      {
        var acc = PeerCast.AccessController;
        settings.TryGetThen("maxRelays",                 v => acc.MaxRelays = v);
        settings.TryGetThen("maxRelaysPerChannel",       v => acc.MaxRelaysPerChannel = v);
        settings.TryGetThen("maxDirects",                v => acc.MaxPlays = v);
        settings.TryGetThen("maxDirectsPerChannel",      v => acc.MaxPlaysPerChannel = v);
        settings.TryGetThen("maxUpstreamRate",           v => acc.MaxUpstreamRate = v);
        settings.TryGetThen("maxUpstreamRatePerChannel", v => acc.MaxUpstreamRatePerChannel = v);
        settings.TryGetThen("channelCleaner", (JObject channel_cleaner) => {
          channel_cleaner.TryGetThen("inactiveLimit", v => ChannelCleaner.InactiveLimit = v);
          channel_cleaner.TryGetThen("mode", v => ChannelCleaner.Mode = (ChannelCleaner.CleanupMode)v);
        });
        settings.TryGetThen("portMapper", (JObject mapper) => {
          var port_mapper = PeerCastApplication.Current.Plugins.GetPlugin<PeerCastStation.UI.PortMapperPlugin>();
          if (port_mapper!=null) {
            mapper.TryGetThen("enabled", v => port_mapper.Enabled = v);
            port_mapper.DiscoverAsync();
          }
        });
        owner.Application.SaveSettings();
      }

      [RPCMethod("getLogSettings")]
      private JToken GetLogSettings()
      {
        var res = new JObject();
        res["level"] = (int)Logger.Level;
        return res;
      }

      [RPCMethod("setLogSettings")]
      private void setLogSettings(JObject settings)
      {
        settings.TryGetThen("level", v => {
          Logger.Level = (LogLevel)v;
          owner.Application.SaveSettings();
        });
      }

      [RPCMethod("getLog")]
      private JToken GetLog(int? from, int? maxLines)
      {
        var lines = owner.LogWriter.Lines;
        var logs  = lines.Skip(from ?? 0).Take(maxLines ?? lines.Count()).ToArray();
        var res = new JObject();
        res["from"]  = from ?? 0;
        res["lines"] = logs.Length;
        res["log"]   = String.Join("\n", logs);
        return res;
      }

      [RPCMethod("clearLog")]
      private void ClearLog()
      {
        owner.LogWriter.Flush();
        owner.LogWriter.Clear();
      }

      [RPCMethod("getChannels")]
      private JArray GetChannels()
      {
        return new JArray(
          PeerCast.Channels.Select(c => {
            var res = new JObject();
            var cid = c.ChannelID.ToString("N").ToUpper();
            res["channelId"] = cid;
            res["status"] = GetChannelStatus(cid);
            var info = GetChannelInfo(cid);
            res["info"] = info["info"];
            res["track"] = info["track"];
            res["yellowPages"] = info["yellowPages"];
            return res;
          })
        );
      }

      private Channel GetChannel(string channel_id)
      {
        if (channel_id==null) throw new RPCError(RPCErrorCode.InvalidParams);
        Guid cid;
        try {
          cid = new Guid(channel_id);
        }
        catch (Exception) {
          throw new RPCError(RPCErrorCode.InvalidParams, "Invalid channelId");
        }
        var channel = PeerCast.Channels.FirstOrDefault(c => c.ChannelID==cid);
        if (channel==null) {
          throw new RPCError(RPCErrorCode.ChannelNotFound, "Channel not found");
        }
        else {
          return channel;
        }
      }

      [RPCMethod("getChannelStatus")]
      private JObject GetChannelStatus(string channelId)
      {
        var channel = GetChannel(channelId);
        var res = new JObject();
        res["status"]          = channel.Status.ToString();
        res["source"]          = channel.SourceUri!=null ? channel.SourceUri.ToString() : null;
        res["uptime"]          = (int)channel.Uptime.TotalSeconds;
        res["localRelays"]     = channel.LocalRelays;
        res["localDirects"]    = channel.LocalDirects;
        res["totalRelays"]     = channel.TotalRelays;
        res["totalDirects"]    = channel.TotalDirects;
        res["isBroadcasting"]  = channel.IsBroadcasting;
        res["isRelayFull"]     = channel.IsRelayFull;
        res["isDirectFull"]    = channel.IsDirectFull;
        res["isReceiving"]     = channel.SelfNode.IsReceiving;
        return res;
      }

      private JArray GetChannelAnnouncingInfo(string channelId)
      {
        var announcings = Enumerable.Empty<IAnnouncingChannel>();
        var channel = GetChannel(channelId);
        foreach (var yp in PeerCast.YellowPages) {
          announcings = announcings.Concat(yp.AnnouncingChannels.Where(ac => ac.Channel.ChannelID==channel.ChannelID));
        }
        return new JArray(announcings.Select(ac => {
          var acinfo = new JObject();
          acinfo["yellowPageId"] = GetObjectId(ac.YellowPage);
          acinfo["name"]         = ac.YellowPage.Name;
          acinfo["protocol"]     = ac.YellowPage.Protocol;
          acinfo["uri"]          = ac.YellowPage.AnnounceUri==null ? null : ac.YellowPage.AnnounceUri.ToString();
          acinfo["status"]       = ac.Status.ToString();
          return acinfo;
        }));
      }

      [RPCMethod("getChannelInfo")]
      private JObject GetChannelInfo(string channelId)
      {
        var channel = GetChannel(channelId);
        var info = new JObject();
        info["name"]        = channel.ChannelInfo.Name;
        info["url"]         = channel.ChannelInfo.URL;
        info["genre"]       = channel.ChannelInfo.Genre;
        info["desc"]        = channel.ChannelInfo.Desc;
        info["comment"]     = channel.ChannelInfo.Comment;
        info["bitrate"]     = channel.ChannelInfo.Bitrate;
        info["contentType"] = channel.ChannelInfo.ContentType;
        info["mimeType"]    = channel.ChannelInfo.MIMEType;
        var track = new JObject();
        track["name"]    = channel.ChannelTrack.Name;
        track["genre"]   = channel.ChannelTrack.Genre;
        track["album"]   = channel.ChannelTrack.Album;
        track["creator"] = channel.ChannelTrack.Creator;
        track["url"]     = channel.ChannelTrack.URL;
        var announcings = GetChannelAnnouncingInfo(channelId);
        var res = new JObject();
        res["info"] = info;
        res["track"] = track;
        res["yellowPages"] = announcings;
        return res;
      }

      [RPCMethod("setChannelInfo")]
      private void SetChannelInfo(string channelId, JObject info, JObject track)
      {
        var channel = GetChannel(channelId);
        if (channel!=null && channel.IsBroadcasting) {
          if (info!=null) {
            var new_info = new AtomCollection(channel.ChannelInfo.Extra);
            if (info["name"]!=null)    new_info.SetChanInfoName((string)info["name"]);
            if (info["url"]!=null)     new_info.SetChanInfoURL((string)info["url"]);
            if (info["genre"]!=null)   new_info.SetChanInfoGenre((string)info["genre"]);
            if (info["desc"]!=null)    new_info.SetChanInfoDesc((string)info["desc"]);
            if (info["comment"]!=null) new_info.SetChanInfoComment((string)info["comment"]);
            channel.ChannelInfo = new ChannelInfo(new_info);
          }
          if (track!=null) {
            var new_track = new AtomCollection(channel.ChannelTrack.Extra);
            if (track["name"]!=null)    new_track.SetChanTrackTitle((string)track["name"]);
            if (track["genre"]!=null)   new_track.SetChanTrackGenre((string)track["genre"]);
            if (track["album"]!=null)   new_track.SetChanTrackAlbum((string)track["album"]);
            if (track["creator"]!=null) new_track.SetChanTrackCreator((string)track["creator"]);
            if (track["url"]!=null)     new_track.SetChanTrackURL((string)track["url"]);
            channel.ChannelTrack = new ChannelTrack(new_track);
          }
        }
      }

      [RPCMethod("stopChannel")]
      private void StopChannel(string channelId)
      {
        var channel = GetChannel(channelId);
        PeerCast.CloseChannel(channel);
      }

      [RPCMethod("bumpChannel")]
      private void BumpChannel(string channelId)
      {
        var channel = GetChannel(channelId);
        channel.Reconnect();
      }

      [RPCMethod("getChannelOutputs")]
      private JArray GetChannelOutputs(string channelId)
      {
        var channel = GetChannel(channelId);
        return new JArray(channel.OutputStreams.Select(os => {
          var res = new JObject();
          res["outputId"] = GetObjectId(os);
          res["name"]     = os.ToString();
          res["type"]     = (int)os.OutputStreamType;
          return res;
        }));
      }

      [RPCMethod("stopChannelOutput")]
      private void StopChannelOutput(string channelId, int outputId)
      {
        var channel = GetChannel(channelId);
        var output_stream = channel.OutputStreams.FirstOrDefault(os => GetObjectId(os)==outputId);
        if (output_stream!=null) {
          channel.RemoveOutputStream(output_stream);
          output_stream.Stop();
        }
      }

      private JObject GetChannelConnection(ISourceStream ss)
      {
        return GetChannelConnection(ss, ss.GetConnectionInfo());
      }

      private JObject GetChannelConnection(IOutputStream os)
      {
        return GetChannelConnection(os, os.GetConnectionInfo());
      }

      private JObject GetChannelConnection(IAnnouncingChannel ac)
      {
        return GetChannelConnection(ac, ac.YellowPage.GetConnectionInfo());
      }

      private JObject GetChannelConnection(object connection, ConnectionInfo info)
      {
        var res = new JObject();
        res["connectionId"]     = GetObjectId(connection);
        res["type"]             = info.Type.ToString().ToLowerInvariant();
        res["status"]           = info.Status.ToString();
        res["sendRate"]         = info.SendRate;
        res["recvRate"]         = info.RecvRate;
        res["protocolName"]     = info.ProtocolName;
        res["localRelays"]      = info.LocalRelays;
        res["localDirects"]     = info.LocalDirects;
        res["contentPosition"]  = info.ContentPosition;
        res["agentName"]        = info.AgentName;
        if (info.RemoteEndPoint!=null) {
          res["remoteEndPoint"] = info.RemoteEndPoint.ToString();
        }
        var remote_host_status = new JArray();
        if ((info.RemoteHostStatus & RemoteHostStatus.Local)!=0)      remote_host_status.Add("local");
        if ((info.RemoteHostStatus & RemoteHostStatus.Firewalled)!=0) remote_host_status.Add("firewalled");
        if ((info.RemoteHostStatus & RemoteHostStatus.RelayFull)!=0)  remote_host_status.Add("relayFull");
        if ((info.RemoteHostStatus & RemoteHostStatus.Receiving)!=0)  remote_host_status.Add("receiving");
        if ((info.RemoteHostStatus & RemoteHostStatus.Root)!=0)       remote_host_status.Add("root");
        if ((info.RemoteHostStatus & RemoteHostStatus.Tracker)!=0)    remote_host_status.Add("tracker");
        res["remoteHostStatus"] = remote_host_status;
        res["remoteName"]       = info.RemoteName;
        return res;
      }

      [RPCMethod("getChannelConnections")]
      private JArray GetChannelConnections(string channelId)
      {
        var channel = GetChannel(channelId);
        var res =
          channel.SourceStream==null ?
          Enumerable.Empty<JObject>() :
          Enumerable.Repeat(channel.SourceStream, 1).Select(s => GetChannelConnection(s));
        res = res.Concat(channel.OutputStreams.Select(s => GetChannelConnection(s)));
        foreach (var yp in PeerCast.YellowPages) {
          res = res.Concat(yp.AnnouncingChannels
                .Where(ac => ac.Channel.ChannelID==channel.ChannelID)
                .Select(s => GetChannelConnection(s)));
        }
        return new JArray(res);
      }

      [RPCMethod("stopChannelConnection")]
      private bool StopChannelConnection(string channelId, int connectionId)
      {
        var channel = GetChannel(channelId);
        var os = channel.OutputStreams.FirstOrDefault(s => GetObjectId(s)==connectionId);
        if (os!=null) {
          channel.RemoveOutputStream(os);
          os.Stop();
          return true;
        }
        foreach (var yp in PeerCast.YellowPages) {
          var ac = yp.AnnouncingChannels
            .Where(s => GetObjectId(s)==connectionId)
            .Where(s => s.Channel.ChannelID==channel.ChannelID).FirstOrDefault();
          if (ac!=null) {
            yp.StopAnnounce(ac);
            return true;
          }
        }
        return false;
      }

      [RPCMethod("restartChannelConnection")]
      private bool RestartChannelConnection(string channelId, int connectionId)
      {
        var channel = GetChannel(channelId);
        if (channel.SourceStream!=null && GetObjectId(channel.SourceStream)==connectionId) {
          channel.SourceStream.Reconnect();
          return true;
        }
        foreach (var yp in PeerCast.YellowPages) {
          var ac = yp.AnnouncingChannels
            .Where(s => GetObjectId(s)==connectionId)
            .Where(s => s.Channel.ChannelID==channel.ChannelID).FirstOrDefault();
          if (ac!=null) {
            yp.RestartAnnounce(ac);
            return true;
          }
        }
        return false;
      }

      private JObject CreateRelayTreeNode(HostTreeNode node)
      {
        var res = new JObject();
        var host = node.Host;
        var endpoint = (host.GlobalEndPoint!=null && host.GlobalEndPoint.Port!=0) ? host.GlobalEndPoint : host.LocalEndPoint;
        res["sessionId"]    = host.SessionID.ToString("N").ToUpper();
        res["address"]      = endpoint.Address.ToString();
        res["port"]         = endpoint.Port;
        res["isFirewalled"] = host.IsFirewalled;
        res["localRelays"]  = host.RelayCount;
        res["localDirects"] = host.DirectCount;
        res["isTracker"]    = host.IsTracker;
        res["isRelayFull"]  = host.IsRelayFull;
        res["isDirectFull"] = host.IsDirectFull;
        res["isReceiving"]  = host.IsReceiving;
        res["isControlFull"]= host.IsControlFull;
        res["version"]      = host.Extra.GetHostVersion();
        res["versionVP"]    = host.Extra.GetHostVersionVP();
        var ex              = host.Extra.GetHostVersionEXPrefix();
        var exnum           = host.Extra.GetHostVersionEXNumber();
        if (ex!=null && exnum.HasValue) {
          try {
            res["versionEX"] = System.Text.Encoding.UTF8.GetString(ex) + exnum.ToString();
          }
          catch (ArgumentException) {
            //ignore
          }
        }
        res["children"] = new JArray(node.Children.Select(c => CreateRelayTreeNode(c)));
        return res;
      }

      [RPCMethod("getChannelRelayTree")]
      private JArray GetChannelRelayTree(string channelId)
      {
        var channel = GetChannel(channelId);
        return new JArray(new HostTree(channel).Nodes.Select(node => CreateRelayTreeNode(node)));
      }

      [RPCMethod("getContentReaders")]
      private JArray GetContentReaders()
      {
        return new JArray(PeerCast.ContentReaderFactories.Select(reader => {
          var res = new JObject();
          res["name"] = reader.Name;
          res["desc"] = reader.Name;
          return res;
        }).ToArray());
      }

      [RPCMethod("getSourceStreams")]
      private JArray GetSourceStreams()
      {
        return new JArray(PeerCast.SourceStreamFactories.Select(sstream => {
          var res = new JObject();
          res["name"]       = sstream.Name;
          res["desc"]       = sstream.Name;
          res["scheme"]     = sstream.Scheme;
          res["type"]       = (int)sstream.Type;
          res["defaultUri"] = sstream.DefaultUri!=null ? sstream.DefaultUri.ToString() : "";
          res["isContentReaderRequired"] = sstream.IsContentReaderRequired;
          return res;
        }).ToArray());
      }

      [RPCMethod("getYellowPageProtocols")]
      private JArray GetYellowPageProtocols()
      {
        return new JArray(PeerCast.YellowPageFactories.Select(protocol => {
          var res = new JObject();
          res["name"]     = protocol.Name;
          res["protocol"] = protocol.Protocol;
          return res;
        }).ToArray());
      }

      [RPCMethod("getYellowPages")]
      private JArray GetYellowPages()
      {
        return new JArray(PeerCast.YellowPages.Select(yp => {
          var res = new JObject();
          res["yellowPageId"] = GetObjectId(yp);
          res["name"]         = yp.Name;
          res["uri"]          = yp.AnnounceUri==null ? null : yp.AnnounceUri.ToString();
          res["announceUri"]  = yp.AnnounceUri==null ? null : yp.AnnounceUri.ToString();
          res["channelsUri"]  = yp.ChannelsUri==null ? null : yp.ChannelsUri.ToString();
          res["protocol"]     = yp.Protocol;
          res["channels"]     = new JArray(yp.AnnouncingChannels.Select(ac => {
            var announcing = new JObject();
            announcing["channelId"] = ac.Channel.ChannelID.ToString("N").ToUpperInvariant();
            announcing["status"]  = ac.Status.ToString();
            return announcing;
          }));
          return res;
        }));
      }

      [RPCMethod("addYellowPage")]
      private JObject AddYellowPage(string protocol, string name, string uri=null, string announceUri=null, string channelsUri=null)
      {
        var factory = PeerCast.YellowPageFactories.FirstOrDefault(p => protocol==p.Protocol);
        if (factory==null) throw new RPCError(RPCErrorCode.InvalidParams, "protocol Not Found");
        if (name==null) throw new RPCError(RPCErrorCode.InvalidParams, "name must be String");
				Uri announce_uri = null;
				try {
					if (String.IsNullOrEmpty(uri)) uri = announceUri;
					if (!String.IsNullOrEmpty(uri)) {
						announce_uri = new Uri(uri, UriKind.Absolute);
						if (!factory.CheckURI(announce_uri)) {
							throw new RPCError(RPCErrorCode.InvalidParams, String.Format("Not suitable uri for {0}", protocol));
						}
					}
				}
				catch (ArgumentNullException) {
					throw new RPCError(RPCErrorCode.InvalidParams, "uri must be String");
				}
				catch (UriFormatException) {
					throw new RPCError(RPCErrorCode.InvalidParams, "Invalid uri");
				}
				Uri channels_uri = null;
				try {
					if (!String.IsNullOrEmpty(channelsUri)) {
						channels_uri = new Uri(channelsUri, UriKind.Absolute);
					}
				}
				catch (ArgumentNullException) {
					throw new RPCError(RPCErrorCode.InvalidParams, "uri must be String");
				}
				catch (UriFormatException) {
					throw new RPCError(RPCErrorCode.InvalidParams, "Invalid uri");
				}
        var yp = PeerCast.AddYellowPage(factory.Protocol, name, announce_uri, channels_uri);
        owner.Application.SaveSettings();
        var res = new JObject();
        res["yellowPageId"] = GetObjectId(yp);
        res["name"]         = yp.Name;
        res["uri"]          = yp.AnnounceUri==null ? null : yp.AnnounceUri.ToString();
        res["announceUri"]  = yp.AnnounceUri==null ? null : yp.AnnounceUri.ToString();
        res["channelsUri"]  = yp.ChannelsUri==null ? null : yp.ChannelsUri.ToString();
        res["protocol"]     = yp.Protocol;
        return res;
      }

      [RPCMethod("removeYellowPage")]
      private void RemoveYellowPage(int yellowPageId)
      {
        var yp = PeerCast.YellowPages.FirstOrDefault(p => GetObjectId(p)==yellowPageId);
        if (yp!=null) {
          PeerCast.RemoveYellowPage(yp);
          owner.Application.SaveSettings();
        }
      }

      [RPCMethod("stopAnnounce")]
      private void StopAnnounce(int yellowPageId, string channelId=null)
      {
        var yp = PeerCast.YellowPages.FirstOrDefault(p => GetObjectId(p)==yellowPageId);
        if (yp!=null) {
          if (channelId!=null) {
            var channel = GetChannel(channelId);
            var announcing = yp.AnnouncingChannels.FirstOrDefault(ac => ac.Channel.ChannelID==channel.ChannelID);
            if (announcing!=null) {
              yp.StopAnnounce(announcing);
            }
          }
          else {
            yp.StopAnnounce();
          }
        }
      }

      [RPCMethod("restartAnnounce")]
      private void RestartAnnounce(int yellowPageId, string channelId=null)
      {
        var yp = PeerCast.YellowPages.FirstOrDefault(p => GetObjectId(p)==yellowPageId);
        if (yp!=null) {
          if (channelId!=null) {
            var channel = GetChannel(channelId);
            var announcing = yp.AnnouncingChannels.FirstOrDefault(ac => ac.Channel.ChannelID==channel.ChannelID);
            if (announcing!=null) {
              yp.RestartAnnounce(announcing);
            }
          }
          else {
            yp.RestartAnnounce();
          }
        }
      }

      private JObject GetListener(OutputListener listener)
      {
        var res = new JObject();
        res["listenerId"]    = GetObjectId(listener);
        res["address"]       = listener.LocalEndPoint.Address.ToString();
        res["port"]          = listener.LocalEndPoint.Port;
        res["localAccepts"]  = (int)listener.LocalOutputAccepts;
        res["globalAccepts"] = (int)listener.GlobalOutputAccepts;
        res["localAuthorizationRequired"]  = listener.LocalAuthorizationRequired;
        res["globalAuthorizationRequired"] = listener.GlobalAuthorizationRequired;
        res["authenticationId"]       = listener.AuthenticationKey!=null ? listener.AuthenticationKey.Id : null;
        res["authenticationPassword"] = listener.AuthenticationKey!=null ? listener.AuthenticationKey.Password : null;
        res["authToken"]     = listener.AuthenticationKey!=null ? HTTPUtils.CreateAuthorizationToken(listener.AuthenticationKey) : null;
        if ((listener.GlobalOutputAccepts & OutputStreamType.Relay)!=0 && owner.OpenedPorts!=null) {
          res["isOpened"] = (listener.GlobalOutputAccepts & OutputStreamType.Relay)!=0 &&
                            owner.OpenedPorts.Contains(listener.LocalEndPoint.Port);
        }
        else {
          res["isOpened"] = null;
        }
        return res;
      }

      [RPCMethod("getListeners")]
      private JArray GetListeners()
      {
        return new JArray(PeerCast.OutputListeners.Select(ol => GetListener(ol)).ToArray());
      }

      [RPCMethod("addListener")]
      private JObject AddListener(
          string address,
          int port,
          int localAccepts,
          int globalAccepts,
          bool localAuthorizationRequired=false,
          bool globalAuthorizationRequired=true)
      {
        IPAddress addr;
        OutputListener listener;
        IPEndPoint endpoint;
        if (address==null) {
          endpoint = new IPEndPoint(IPAddress.Any, port);
        }
        else if (IPAddress.TryParse(address, out addr)) {
          endpoint = new IPEndPoint(addr, port);
        }
        else {
          throw new RPCError(RPCErrorCode.InvalidParams, "Invalid ip address");
        }
        listener = PeerCast.StartListen(endpoint, (OutputStreamType)localAccepts, (OutputStreamType)globalAccepts);
        listener.LocalAuthorizationRequired  = localAuthorizationRequired;
        listener.GlobalAuthorizationRequired = globalAuthorizationRequired;
        owner.Application.SaveSettings();
        return GetListener(listener);
      }

      [RPCMethod("resetListenerAuthenticationKey")]
      private JObject resetListenerAuthenticationKey(int listenerId)
      {
        var listener = PeerCast.OutputListeners.Where(ol => GetObjectId(ol)==listenerId).FirstOrDefault();
        if (listener!=null) {
          owner.Application.SaveSettings();
          listener.ResetAuthenticationKey();
          return GetListener(listener);
        }
        else {
          return null;
        }
      }

      [RPCMethod("removeListener")]
      private void RemoveListener(int listenerId)
      {
        foreach (var listener in PeerCast.OutputListeners.Where(ol => GetObjectId(ol)==listenerId)) {
          PeerCast.StopListen(listener);
        }
        owner.Application.SaveSettings();
      }

      [RPCMethod("setListenerAccepts")]
      private void setListenerAccepts(int listenerId, int localAccepts, int globalAccepts)
      {
        foreach (var listener in PeerCast.OutputListeners.Where(ol => GetObjectId(ol)==listenerId)) {
          listener.LocalOutputAccepts = (OutputStreamType)localAccepts;
          listener.GlobalOutputAccepts = (OutputStreamType)globalAccepts;
        }
        owner.Application.SaveSettings();
      }

      [RPCMethod("setListenerAuthorizationRequired")]
      private void setListenerAuthorizationRequired(int listenerId, bool localAuthorizationRequired, bool globalAuthorizationRequired)
      {
        foreach (var listener in PeerCast.OutputListeners.Where(ol => GetObjectId(ol)==listenerId)) {
          listener.LocalAuthorizationRequired = localAuthorizationRequired;
          listener.GlobalAuthorizationRequired = globalAuthorizationRequired;
        }
        owner.Application.SaveSettings();
      }

      [RPCMethod("broadcastChannel")]
      private string BroadcastChannel(
        int?    yellowPageId,
        string  sourceUri,
        string  contentReader,
        JObject info,
        JObject track,
        string  sourceStream=null)
      {
        IYellowPageClient yp = null;
        if (yellowPageId.HasValue) {
          yp = PeerCast.YellowPages.FirstOrDefault(y => GetObjectId(y)==yellowPageId.Value);
          if (yp==null) throw new RPCError(RPCErrorCode.InvalidParams, "Yellow page not found");
        }
        if (sourceUri==null) throw new RPCError(RPCErrorCode.InvalidParams, "source uri required");
        Uri source;
        try {
          source = new Uri(sourceUri);
        }
        catch (UriFormatException) {
          throw new RPCError(RPCErrorCode.InvalidParams, "Invalid Uri");
        }
        var content_reader = PeerCast.ContentReaderFactories.FirstOrDefault(reader => reader.Name==contentReader);
        if (content_reader==null) throw new RPCError(RPCErrorCode.InvalidParams, "Content reader not found");
        var source_stream = PeerCast.SourceStreamFactories
          .Where(sstream => (sstream.Type & SourceStreamType.Broadcast)!=0)
          .FirstOrDefault(sstream => sstream.Name==sourceStream);
        if (source_stream==null) {
          source_stream = PeerCast.SourceStreamFactories
            .Where(sstream => (sstream.Type & SourceStreamType.Broadcast)!=0)
            .FirstOrDefault(sstream => sstream.Scheme==source.Scheme);
        }
        if (source_stream==null) throw new RPCError(RPCErrorCode.InvalidParams, "Source stream not found");

        var new_info = new AtomCollection();
        if (info!=null) {
          info.TryGetThen("name",    v => new_info.SetChanInfoName(v));
          info.TryGetThen("url",     v => new_info.SetChanInfoURL(v));
          info.TryGetThen("genre",   v => new_info.SetChanInfoGenre(v));
          info.TryGetThen("desc",    v => new_info.SetChanInfoDesc(v));
          info.TryGetThen("comment", v => new_info.SetChanInfoComment(v));
        }
        var channel_info  = new ChannelInfo(new_info);
        if (channel_info.Name==null || channel_info.Name=="") {
          throw new RPCError(RPCErrorCode.InvalidParams, "Channel name must not be empty");
        }
        var channel_id = PeerCastStation.Core.BroadcastChannel.CreateChannelID(
          PeerCast.BroadcastID,
          channel_info.Name,
          channel_info.Genre ?? "",
          source.ToString());
        var channel = PeerCast.BroadcastChannel(yp, channel_id, channel_info, source, source_stream, content_reader);
        if (track!=null) {
          var new_track = new AtomCollection(channel.ChannelTrack.Extra);
          track.TryGetThen("name",    v => new_track.SetChanTrackTitle(v));
          track.TryGetThen("genre",   v => new_track.SetChanTrackGenre(v));
          track.TryGetThen("album",   v => new_track.SetChanTrackAlbum(v));
          track.TryGetThen("creator", v => new_track.SetChanTrackCreator(v));
          track.TryGetThen("url",     v => new_track.SetChanTrackURL(v));
          channel.ChannelTrack = new ChannelTrack(new_track);
        }
        return channel.ChannelID.ToString("N").ToUpper();
      }

      [RPCMethod("getBroadcastHistory")]
      public JArray GetBroadcastHistory()
      {
        var settings = PeerCastApplication.Current.Settings.Get<UISettings>();
        return new JArray(settings.BroadcastHistory
          .OrderBy(info => info.Favorite ? 0 : 1)
          .Select(info => {
            var obj = new JObject();
            obj["streamType"]  = info.StreamType;
            obj["streamUrl"]   = info.StreamUrl;
            obj["bitrate"]     = info.Bitrate;
            obj["contentType"] = info.ContentType;
            obj["yellowPage"]  = info.YellowPage;
            obj["channelName"] = info.ChannelName;
            obj["genre"]       = info.Genre;
            obj["description"] = info.Description;
            obj["comment"]     = info.Comment;
            obj["contactUrl"]  = info.ContactUrl;
            obj["trackTitle"]  = info.TrackTitle;
            obj["trackAlbum"]  = info.TrackAlbum;
            obj["trackArtist"] = info.TrackArtist;
            obj["trackGenre"]  = info.TrackGenre;
            obj["trackUrl"]    = info.TrackUrl;
            obj["favorite"]    = info.Favorite;
            return obj;
          })
        );
      }

      [RPCMethod("addBroadcastHistory")]
      public void AddBroadcastHistory(JObject info)
      {
        var obj = new PeerCastStation.UI.BroadcastInfo();
        info.TryGetThen("streamType",  v => obj.StreamType  = v);
        info.TryGetThen("streamUrl",   v => obj.StreamUrl   = v);
        info.TryGetThen("bitrate",     v => obj.Bitrate     = v);
        info.TryGetThen("contentType", v => obj.ContentType = v);
        info.TryGetThen("yellowPage",  v => obj.YellowPage  = v);
        info.TryGetThen("channelName", v => obj.ChannelName = v);
        info.TryGetThen("genre",       v => obj.Genre       = v);
        info.TryGetThen("description", v => obj.Description = v);
        info.TryGetThen("comment",     v => obj.Comment     = v);
        info.TryGetThen("contactUrl",  v => obj.ContactUrl  = v);
        info.TryGetThen("trackTitle",  v => obj.TrackTitle  = v);
        info.TryGetThen("trackAlbum",  v => obj.TrackAlbum  = v);
        info.TryGetThen("trackArtist", v => obj.TrackArtist = v);
        info.TryGetThen("trackGenre",  v => obj.TrackGenre  = v);
        info.TryGetThen("trackUrl",    v => obj.TrackUrl    = v);
        info.TryGetThen("favorite",    v => obj.Favorite    = v);
        var settings = PeerCastApplication.Current.Settings.Get<UISettings>();
        var item = settings.FindBroadcastHistroryItem(obj);
        if (item!=null) {
          info.TryGetThen("favorite", v => item.Favorite = v);
        }
        else {
          settings.AddBroadcastHistory(obj);
        }
      }

      [RPCMethod("getNotificationMessages")]
      public JArray GetNotificationMessages()
      {
        return new JArray(
          owner.GetNotificationMessages().Select(msg => {
            var obj = new JObject();
            if (msg is NewVersionNotificationMessage) {
              obj["class"] = "newversion";
            }
            else {
              obj["class"] = msg.GetType().Name.ToLowerInvariant();
            }
            obj["type"]    = msg.Type.ToString().ToLowerInvariant();
            obj["title"]   = msg.Title;
            obj["message"] = msg.Message;
            return obj;
          })
        );
      }

      [RPCMethod("checkBandwidth")]
      public int? CheckBandWidth()
      {
        int? result = null;
        Uri target_uri;
        if (AppSettingsReader.TryGetUri("BandwidthChecker", out target_uri)) {
          var checker = new BandwidthChecker(target_uri);
          checker.BandwidthCheckCompleted += (sender, args) => {
            if (args.Success) {
              result = (int)args.Bitrate/1000;
            }
          };
          checker.Run();
        }
        return result;
      }

      [RPCMethod("checkPorts")]
      public JArray CheckPorts()
      {
        int[] results = null;
        var port_checker = PeerCastApplication.Current.Plugins.GetPlugin<PeerCastStation.UI.PCPPortCheckerPlugin>();
        if (port_checker!=null) {
          var task = port_checker.CheckAsync();
          task.Wait();
          var result = task.Result;
          if (result.Success) {
            PeerCast.IsFirewalled = result.Ports.Length==0;
            owner.OpenedPorts = result.Ports;
            results = result.Ports;
          }
        }
        if (results!=null) {
          return new JArray(results);
        }
        else {
          return null;
        }
      }

      [RPCMethod("checkUpdate")]
      public void CheckUpdate()
      {
        owner.CheckVersion();
      }

			[RPCMethod("getNewVersions")]
			public JArray GetNewVersions()
			{
				return new JArray(owner.GetNewVersions()
					.OrderByDescending(v => v.PublishDate)
					.Select(v => {
						var obj = new JObject();
						obj["title"]       = v.Title;
						obj["publishDate"] = v.PublishDate;
						obj["link"]        = v.Link;
						obj["description"] = v.Description;
						obj["enclosures"] = new JArray(v.Enclosures.Select(e => {
							var enclosure = new JObject();
							enclosure["title"] = e.Title;
							enclosure["url"] = e.Url;
							enclosure["length"] = e.Length;
							enclosure["installerType"] = e.InstallerType.ToString().ToLowerInvariant();
							enclosure["type"] = e.Type;
							return enclosure;
						}));
					return obj;
					})
				);
			}

			private JArray YPChannelsToArray(IEnumerable<IYellowPageChannel> channels)
			{
				return new JArray(channels.Select(v => {
						var obj = new JObject();
						obj["yellowPage"]  = v.Source.Name;
						obj["name"]        = v.Name;
						obj["channelId"]   = v.ChannelId.ToString("N").ToUpperInvariant();
						obj["tracker"]     = v.Tracker;
						obj["contactUrl"]  = v.ContactUrl;
						obj["genre"]       = v.Genre;
						obj["description"] = v.Description;
						obj["comment"]     = v.Comment;
						obj["bitrate"]     = v.Bitrate;
						obj["contentType"] = v.ContentType;
						obj["trackTitle"]  = v.TrackTitle;
						obj["album"]       = v.Album;
						obj["creator"]     = v.Artist;
						obj["trackUrl"]    = v.TrackUrl;
						obj["listeners"]   = v.Listeners;
						obj["relays"]      = v.Relays;
						obj["uptime"]      = v.Uptime;
						return obj;
					})
				);
			}

			[RPCMethod("getYPChannels")]
			public JArray GetYPChannels()
			{
				return YPChannelsToArray(owner.GetYPChannels());
			}

			[RPCMethod("updateYPChannels")]
			public JArray UpdateYPChannels()
			{
				return YPChannelsToArray(owner.UpdateYPChannels());
			}

			[RPCMethod("setUserConfig")]
			public void SetUserConfig(string user, string key, JObject value)
			{
				var settings = owner.Application.Settings.Get<UISettings>();
				Dictionary<string, string> user_config;
				if (!settings.UserConfig.TryGetValue(user, out user_config)) {
					user_config = new Dictionary<string, string>();
					settings.UserConfig[user] = user_config;
				}
				user_config[key] = value.ToString();
				owner.Application.SaveSettings();
			}

			[RPCMethod("getUserConfig")]
			public JToken GetUserConfig(string user, string key)
			{
				var settings = owner.Application.Settings.Get<UISettings>();
				Dictionary<string, string> user_config;
				if (!settings.UserConfig.TryGetValue(user, out user_config)) {
					return null;
				}
				if (!user_config.ContainsKey(key)) {
					return null;
				}
				return JToken.Parse(user_config[key]);
			}

      public static readonly int RequestLimit = 64*1024;
      public static readonly int TimeoutLimit = 5000;
      protected override async Task<StopReason> DoProcess(CancellationToken cancel_token)
      {
        System.Threading.SynchronizationContext.SetSynchronizationContext(new System.Threading.SynchronizationContext());
        try {
          if (!HTTPUtils.CheckAuthorization(this.request, this.AccessControlInfo)) {
            throw new HTTPError(HttpStatusCode.Unauthorized);
          }
          if (this.request.Method=="HEAD" || this.request.Method=="GET") {
            await SendJson(GetVersionInfo(), this.request.Method!="HEAD", cancel_token);
            return StopReason.OffAir;
          }
          else if (this.request.Method=="POST") {
            if (!this.request.Headers.ContainsKey("X-REQUESTED-WITH")) {
              throw new HTTPError(HttpStatusCode.BadRequest);
            }
            if (!this.request.Headers.ContainsKey("CONTENT-LENGTH")) {
              throw new HTTPError(HttpStatusCode.LengthRequired);
            }
            string length = request.Headers["CONTENT-LENGTH"];
            int len;
            if (!Int32.TryParse(length, out len) || len<=0 || RequestLimit<len) {
              throw new HTTPError(HttpStatusCode.BadRequest);
            }

            try {
              var timeout_token = new CancellationTokenSource(TimeoutLimit);
              var buf = await Connection.ReadBytesAsync(len, CancellationTokenSource.CreateLinkedTokenSource(cancel_token, timeout_token.Token).Token);
              var request_str = System.Text.Encoding.UTF8.GetString(buf);
              JToken res = rpcHost.ProcessRequest(request_str);
              if (res!=null) {
                await SendJson(res, true, cancel_token);
              }
              else {
                throw new HTTPError(HttpStatusCode.NoContent);
              }
            }
            catch (OperationCanceledException) {
              throw new HTTPError(HttpStatusCode.RequestTimeout);
            }
            if (this.request.KeepAlive) {
              HandlerResult = HandlerResult.Continue;
            }
            else {
              HandlerResult = HandlerResult.Close;
            }
            return StopReason.OffAir;
          }
          else {
            throw new HTTPError(HttpStatusCode.MethodNotAllowed);
          }
        }
        catch (HTTPError err) {
          HandlerResult = HandlerResult.Error;
          var response = new HTTPResponse(this.request.Protocol, err.StatusCode);
          await Connection.WriteAsync(response.GetBytes(), cancel_token);
          return StopReason.OffAir;
        }

      }

      private async Task SendJson(JToken token, bool send_body, CancellationToken cancel_token)
      {
        var body = System.Text.Encoding.UTF8.GetBytes(token.ToString());
        var parameters = new Dictionary<string, string> {
          {"Content-Type",   "application/json" },
          {"Content-Length", body.Length.ToString() },
        };
        var response = new HTTPResponse(this.request.Protocol, HttpStatusCode.OK, parameters);
        await Connection.WriteAsync(response.GetBytes(), cancel_token);
        if (send_body) {
          await Connection.WriteAsync(body, cancel_token);
        }
      }

      public override OutputStreamType OutputStreamType
      {
        get { return OutputStreamType.Interface; }
      }
    }

    public class APIHostOutputStreamFactory
      : OutputStreamFactoryBase
    {
      public override string Name
      {
        get { return "API Host UI"; }
      }

      public override OutputStreamType OutputStreamType
      {
        get { return OutputStreamType.Interface; }
      }

      public override int Priority
      {
        get { return 10; }
      }

      public override IOutputStream Create(
        Stream input_stream,
        Stream output_stream,
        EndPoint remote_endpoint,
        AccessControlInfo access_control,
        Guid channel_id,
        byte[] header)
      {
        using (var stream = new MemoryStream(header)) {
          var request = HTTPRequestReader.Read(stream);
          var bytes = stream.Position;
          return new APIHostOutputStream(
            owner,
            PeerCast,
            input_stream,
            output_stream,
            remote_endpoint,
            access_control,
            request,
            header.Skip((int)bytes).ToArray());
        }
      }

      public override Guid? ParseChannelID(byte[] header)
      {
        using (var stream=new MemoryStream(header)) {
          var res = HTTPRequestReader.Read(stream);
          if (res!=null && res.Uri.AbsolutePath=="/api/1") {
            return Guid.Empty;
          }
          else {
            return null;
          }
        }
      }

      APIHost owner;
      public APIHostOutputStreamFactory(APIHost owner, PeerCast peercast)
        : base(peercast)
      {
        this.owner = owner;
      }
    }
  }
}
