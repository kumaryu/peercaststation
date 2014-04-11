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
          RecvRate,
          SendRate,
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
        return res;
      }

      private int? ParseInt(JToken token)
      {
        if (token==null) return null;
        switch (token.Type) {
        case JTokenType.Boolean:
        case JTokenType.Float:
        case JTokenType.Integer:
          return (int)token;
        default:
          return null;
        }
      }

      [RPCMethod("setSettings")]
      private void SetSettings(JObject settings)
      {
        var acc = PeerCast.AccessController;
        acc.MaxRelays                 = ParseInt(settings["maxRelays"])                 ?? acc.MaxRelays;
        acc.MaxRelaysPerChannel       = ParseInt(settings["maxRelaysPerChannel"])       ?? acc.MaxRelaysPerChannel;
        acc.MaxPlays                  = ParseInt(settings["maxDirects"])                ?? acc.MaxPlays;
        acc.MaxPlaysPerChannel        = ParseInt(settings["maxDirectsPerChannel"])      ?? acc.MaxPlaysPerChannel;
        acc.MaxUpstreamRate           = ParseInt(settings["maxUpstreamRate"])           ?? acc.MaxUpstreamRate;
        acc.MaxUpstreamRatePerChannel = ParseInt(settings["maxUpstreamRatePerChannel"]) ?? acc.MaxUpstreamRatePerChannel;
        if (settings["channelCleaner"]!=null && settings["channelCleaner"].HasValues) {
          var channelCleaner = settings["channelCleaner"];
          ChannelCleaner.InactiveLimit = ParseInt(channelCleaner["inactiveLimit"]) ?? ChannelCleaner.InactiveLimit;
          ChannelCleaner.Mode = (ChannelCleaner.CleanupMode)(ParseInt(channelCleaner["mode"]) ?? (int)ChannelCleaner.Mode);
        }
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
        if (settings["level"]!=null) {
          Logger.Level = (LogLevel)(ParseInt(settings["level"]) ?? (int)Logger.Level);
          owner.Application.SaveSettings();
        }
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
        res["source"]          = channel.SourceUri.ToString();
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
          announcings.Concat(yp.AnnouncingChannels.Where(ac => ac.Channel.ChannelID==channel.ChannelID));
        }
        return new JArray(announcings.Select(ac => {
          var acinfo = new JObject();
          acinfo["yellowPageId"] = GetObjectId(ac.YellowPage);
          acinfo["name"]         = ac.YellowPage.Name;
          acinfo["protocol"]     = ac.YellowPage.Protocol;
          acinfo["uri"]          = ac.YellowPage.Uri.ToString();
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
          res["uri"]          = yp.Uri.ToString();
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
      private JObject AddYellowPage(string protocol, string name, string uri)
      {
        var factory = PeerCast.YellowPageFactories.FirstOrDefault(p => protocol==p.Protocol);
        if (factory==null) throw new RPCError(RPCErrorCode.InvalidParams, "protocol Not Found");
        if (name==null) throw new RPCError(RPCErrorCode.InvalidParams, "name must be String");
        Uri yp_uri;
        try {
          yp_uri = new Uri(uri);
        }
        catch (ArgumentNullException) {
          throw new RPCError(RPCErrorCode.InvalidParams, "uri must be String");
        }
        catch (UriFormatException) {
          throw new RPCError(RPCErrorCode.InvalidParams, "Invalid uri");
        }
        if (!factory.CheckURI(yp_uri)) {
          throw new RPCError(RPCErrorCode.InvalidParams, String.Format("Not suitable uri for {0}", protocol));
        }
        var yp = PeerCast.AddYellowPage(factory.Protocol, name, yp_uri);
        owner.Application.SaveSettings();
        var res = new JObject();
        res["yellowPageId"] = GetObjectId(yp);
        res["name"]         = yp.Name;
        res["uri"]          = yp.Uri.ToString();
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
          if (info["name"]!=null)    new_info.SetChanInfoName(   (string)info["name"]);
          if (info["url"]!=null)     new_info.SetChanInfoURL(    (string)info["url"]);
          if (info["genre"]!=null)   new_info.SetChanInfoGenre(  (string)info["genre"]);
          if (info["desc"]!=null)    new_info.SetChanInfoDesc(   (string)info["desc"]);
          if (info["comment"]!=null) new_info.SetChanInfoComment((string)info["comment"]);
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
          if (track["name"]!=null)    new_track.SetChanTrackTitle((string)track["name"]);
          if (track["genre"]!=null)   new_track.SetChanTrackGenre((string)track["genre"]);
          if (track["album"]!=null)   new_track.SetChanTrackAlbum((string)track["album"]);
          if (track["creator"]!=null) new_track.SetChanTrackCreator((string)track["creator"]);
          if (track["url"]!=null)     new_track.SetChanTrackURL((string)track["url"]);
          channel.ChannelTrack = new ChannelTrack(new_track);
        }
        return channel.ChannelID.ToString("N").ToUpper();
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

      [RPCMethod("checkUpdate")]
      public void CheckUpdate()
      {
        owner.CheckVersion();
      }

      [RPCMethod("getNewVersions")]
      public JArray GetNewVersions()
      {
        return new JArray(owner.GetNewVersions().Select(v => {
          var obj = new JObject();
          obj["title"]       = v.Title;
          obj["publishDate"] = v.PublishDate;
          obj["link"]        = v.Link;
          obj["description"] = v.Description;
          return obj;
        }));
      }

      public static readonly int RequestLimit = 64*1024;
      public static readonly int TimeoutLimit = 5000;
      private int bodyLength = -1;
      private System.Diagnostics.Stopwatch timeoutWatch = new System.Diagnostics.Stopwatch();
      protected override void OnStarted()
      {
        base.OnStarted();
        Logger.Debug("Started");
        try {
          if (!HTTPUtils.CheckAuthorization(this.request, AccessControl.AuthenticationKey)) {
            throw new HTTPError(HttpStatusCode.Unauthorized);
          }
          if (this.request.Method=="HEAD" || this.request.Method=="GET") {
            var token = GetVersionInfo();
            var body = System.Text.Encoding.UTF8.GetBytes(token.ToString());
            var parameters = new Dictionary<string, string> {
              {"Content-Type",   "application/json" },
              {"Content-Length", body.Length.ToString() },
            };
            Send(HTTPUtils.CreateResponseHeader(HttpStatusCode.OK, parameters));
            if (this.request.Method!="HEAD") {
              Send(body);
            }
            Stop();
          }
          else if (this.request.Method=="POST") {
            string length;
            if (request.Headers.TryGetValue("CONTENT-LENGTH", out length)) {
              int len;
              if (int.TryParse(length, out len) && len>=0 && len<=RequestLimit) {
                bodyLength = len;
                timeoutWatch.Start();
              }
              else {
                throw new HTTPError(HttpStatusCode.BadRequest);
              }
            }
            else {
              throw new HTTPError(HttpStatusCode.LengthRequired);
            }
          }
          else {
            throw new HTTPError(HttpStatusCode.MethodNotAllowed);
          }
        }
        catch (HTTPError err) {
          Send(HTTPUtils.CreateResponseHeader(err.StatusCode, new Dictionary<string, string> { }));
          Stop();
        }
      }

      protected override void OnIdle()
      {
        base.OnIdle();
        if (this.request.Method!="POST" || bodyLength<0) return;
        string request_str = null;
        if (Recv(stream => {
          if (stream.Length-stream.Position<bodyLength) throw new EndOfStreamException();
          var buf = new byte[stream.Length-stream.Position];
          stream.Read(buf, 0, (int)(stream.Length-stream.Position));
          request_str = System.Text.Encoding.UTF8.GetString(buf);
        })) {
          JToken res = rpcHost.ProcessRequest(request_str);
          if (res!=null) {
            SendJson(res);
          }
          else {
            Send(HTTPUtils.CreateResponseHeader(HttpStatusCode.NoContent, new Dictionary<string, string>()));
          }
          Stop();
        }
        else if (timeoutWatch.ElapsedMilliseconds>TimeoutLimit) {
          Send(HTTPUtils.CreateResponseHeader(HttpStatusCode.RequestTimeout, new Dictionary<string, string>()));
          Stop();
        }
      }

      private void Send(string str)
      {
        Send(System.Text.Encoding.UTF8.GetBytes(str));
      }

      private void SendJson(JToken token)
      {
        var body = System.Text.Encoding.UTF8.GetBytes(token.ToString());
        var parameters = new Dictionary<string, string> {
          {"Content-Type",   "application/json" },
          {"Content-Length", body.Length.ToString() },
        };
        Send(HTTPUtils.CreateResponseHeader(HttpStatusCode.OK, parameters));
        Send(body);
      }

      protected override void OnStopped()
      {
        Logger.Debug("Finished");
       	base.OnStopped();
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
        HTTPRequest request = null;
        long bytes = 0;
        using (var stream = new MemoryStream(header)) {
          try {
            request = HTTPRequestReader.Read(stream);
            bytes = stream.Position;
          }
          catch (EndOfStreamException) {
          }
          catch (InvalidDataException) {
          }
        }
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

      public override Guid? ParseChannelID(byte[] header)
      {
        HTTPRequest res = null;
        using (var stream = new MemoryStream(header)) {
          try {
            res = HTTPRequestReader.Read(stream);
          }
          catch (EndOfStreamException) {
          }
          catch (InvalidDataException) {
          }
        }
        if (res!=null && res.Uri.AbsolutePath=="/api/1") {
          return Guid.Empty;
        }
        else {
          return null;
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
