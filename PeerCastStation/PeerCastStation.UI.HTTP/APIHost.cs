using System;
using System.Linq;
using System.IO;
using System.Net;
using System.Collections.Generic;
using PeerCastStation.Core;
using PeerCastStation.HTTP;
using PeerCastStation.UI.HTTP.JSONRPC;
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
    private APIHostOutputStreamFactory factory;
    override protected void OnAttach()
    {
      factory = new APIHostOutputStreamFactory(this, Application.PeerCast);
      Application.PeerCast.OutputStreamFactories.Add(factory);
    }

    protected override void OnStart()
    {
      Logger.AddWriter(logWriter);
    }

    protected override void OnStop()
    {
      Logger.RemoveWriter(logWriter);
    }

    protected override void OnDetach()
    {
      Application.PeerCast.OutputStreamFactories.Remove(factory);
    }

    private List<NotificationMessage> notificationMessages = new List<NotificationMessage>();
    public void ShowNotificationMessage(NotificationMessage msg)
    {
      lock (notificationMessages) {
        notificationMessages.Add(msg);
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
        HTTPRequest request,
        byte[] header)
        : base(peercast, input_stream, output_stream, remote_endpoint, null, header)
      {
        this.owner   = owner;
        this.request = request;
        this.rpcHost = new JSONRPCHost(this);
        Logger.Debug("Initialized: Remote {0}", remote_endpoint);
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
          var asm = plugin.GetType().Assembly;
          jassembly["name"] = asm.FullName;
          jassembly["path"] = asm.Location;
          if (File.Exists(asm.Location)) {
            var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(asm.Location);
            jassembly["version"] = info.FileVersion;
            jassembly["copyright"] = info.LegalCopyright;
          }
          else {
            jassembly["version"] = "";
            jassembly["copyright"] = "";
          }
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
        res["maxRelays"]            = PeerCast.AccessController.MaxRelays;
        res["maxDirects"]           = PeerCast.AccessController.MaxPlays;
        res["maxRelaysPerChannel"]  = PeerCast.AccessController.MaxRelaysPerChannel;
        res["maxDirectsPerChannel"] = PeerCast.AccessController.MaxPlaysPerChannel;
        res["maxUpstreamRate"]      = PeerCast.AccessController.MaxUpstreamRate;
        var channelCleaner = new JObject();
        channelCleaner["inactiveLimit"] = ChannelCleaner.InactiveLimit;
        channelCleaner["noPlayingLimit"] = ChannelCleaner.NoPlayingLimit;
        res["channelCleaner"] = channelCleaner;
        return res;
      }

      [RPCMethod("setSettings")]
      private void SetSettings(JObject settings)
      {
        if (settings["maxRelays"]!=null) {
          PeerCast.AccessController.MaxRelays = (int)settings["maxRelays"];
        }
        if (settings["maxRelaysPerChannel"]!=null) {
          PeerCast.AccessController.MaxRelaysPerChannel = (int)settings["maxRelaysPerChannel"];
        }
        if (settings["maxDirects"]!=null) {
          PeerCast.AccessController.MaxPlays = (int)settings["maxDirects"];
        }
        if (settings["maxDirectsPerChannel"]!=null) {
          PeerCast.AccessController.MaxPlaysPerChannel = (int)settings["maxDirectsPerChannel"];
        }
        if (settings["maxUpstreamRate"]!=null) {
          PeerCast.AccessController.MaxUpstreamRate = (int)settings["maxUpstreamRate"];
        }
        if (settings["channelCleaner"]!=null) {
          var channelCleaner = settings["channelCleaner"];
          if (channelCleaner["inactiveLimit"]!=null) {
            ChannelCleaner.InactiveLimit = (int)channelCleaner["inactiveLimit"];
          }
          if (channelCleaner["noPlayingLimit"]!=null) {
            ChannelCleaner.NoPlayingLimit = (int)channelCleaner["noPlayingLimit"];
          }
        }
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
          Logger.Level = (LogLevel)(int)settings["level"];
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
        res["status"]         = channel.Status.ToString();
        res["uptime"]         = (int)channel.Uptime.TotalSeconds;
        res["localRelays"]    = channel.LocalRelays;
        res["localDirects"]   = channel.LocalDirects;
        res["totalRelays"]    = channel.TotalRelays;
        res["totalDirects"]   = channel.TotalDirects;
        res["isBroadcasting"] = channel.BroadcastID==PeerCast.BroadcastID;
        res["isRelayFull"]    = channel.IsRelayFull;
        res["isDirectFull"]   = channel.IsDirectFull;
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
          acinfo["yellowPageId"] = ac.YellowPage.GetHashCode();
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
        if (channel!=null && channel.BroadcastID==PeerCast.BroadcastID) {
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
          res["outputId"] = os.GetHashCode();
          res["name"]     = os.ToString();
          res["type"]     = (int)os.OutputStreamType;
          return res;
        }));
      }

      [RPCMethod("stopChannelOutput")]
      private void StopChannelOutput(string channelId, int outputId)
      {
        var channel = GetChannel(channelId);
        var output_stream = channel.OutputStreams.FirstOrDefault(os => os.GetHashCode()==outputId);
        if (output_stream!=null) {
          channel.RemoveOutputStream(output_stream);
          output_stream.Stop();
        }
      }

      private JObject GetChannelConnection(ISourceStream ss)
      {
        var res = new JObject();
        res["type"]         = "Source";
        res["connectionId"] = ss.GetHashCode();
        res["name"]         = ss.ToString();
        res["desc"]         = ss.ToString();
        res["status"]       = ss.Status.ToString();
        return res;
      }

      private JObject GetChannelConnection(IOutputStream os)
      {
        var res = new JObject();
        res["type"]         = "Output";
        res["connectionId"] = os.GetHashCode();
        res["name"]         = os.ToString();
        res["desc"]         = os.ToString();
        res["status"]       = "Connected";
        if ((os.OutputStreamType & OutputStreamType.Interface)!=0) res["type"] = "Interface";
        if ((os.OutputStreamType & OutputStreamType.Play)!=0)      res["type"] = "Play";
        if ((os.OutputStreamType & OutputStreamType.Relay)!=0)     res["type"] = "Relay";
        return res;
      }

      private JObject GetChannelConnection(IAnnouncingChannel ac)
      {
        var res = new JObject();
        res["type"]         = "Announce";
        res["connectionId"] = ac.YellowPage.GetHashCode();
        res["name"]         = ac.YellowPage.Name;
        res["desc"]         = ac.YellowPage.Name;
        res["status"]       = ac.Status.ToString();
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
        var os = channel.OutputStreams.FirstOrDefault(s => s.GetHashCode()==connectionId);
        if (os!=null) {
          channel.RemoveOutputStream(os);
          os.Stop();
          return true;
        }
        foreach (var yp in PeerCast.YellowPages) {
          if (connectionId==yp.GetHashCode()) {
            var ac = yp.AnnouncingChannels.Where(s => s.Channel.ChannelID==channel.ChannelID).FirstOrDefault();
            if (ac!=null) {
              yp.StopAnnounce(ac);
              return true;
            }
          }
        }
        return false;
      }

      [RPCMethod("restartChannelConnection")]
      private bool RestartChannelConnection(string channelId, int connectionId)
      {
        var channel = GetChannel(channelId);
        if (channel.SourceStream!=null && channel.SourceStream.GetHashCode()==connectionId) {
          channel.SourceStream.Reconnect();
          return true;
        }
        foreach (var yp in PeerCast.YellowPages) {
          if (connectionId==yp.GetHashCode()) {
            var ac = yp.AnnouncingChannels.Where(s => s.Channel.ChannelID==channel.ChannelID).FirstOrDefault();
            if (ac!=null) {
              yp.RestartAnnounce(ac);
              return true;
            }
          }
        }
        return false;
      }

      private JObject CreateRelayTreeNode(Utils.HostTreeNode node)
      {
        var res = new JObject();
        var host = node.Host;
        res["sessionId"]    = host.SessionID.ToString("N").ToUpper();
        res["address"]      = host.LocalEndPoint.Address.ToString();
        res["port"]         = host.LocalEndPoint.Port;
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
        return new JArray(channel.CreateHostTree().Select(node => CreateRelayTreeNode(node)));
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
          res["yellowPageId"] = yp.GetHashCode();
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
        var res = new JObject();
        res["yellowPageId"] = yp.GetHashCode();
        res["name"]         = yp.Name;
        res["uri"]          = yp.Uri.ToString();
        res["protocol"]     = yp.Protocol;
        return res;
      }

      [RPCMethod("removeYellowPage")]
      private void RemoveYellowPage(int yellowPageId)
      {
        var yp = PeerCast.YellowPages.FirstOrDefault(p => p.GetHashCode()==yellowPageId);
        if (yp!=null) {
          PeerCast.RemoveYellowPage(yp);
        }
      }

      [RPCMethod("stopAnnounce")]
      private void StopAnnounce(int yellowPageId, string channelId=null)
      {
        var yp = PeerCast.YellowPages.FirstOrDefault(p => p.GetHashCode()==yellowPageId);
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
        var yp = PeerCast.YellowPages.FirstOrDefault(p => p.GetHashCode()==yellowPageId);
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

      [RPCMethod("getListeners")]
      private JArray GetListeners()
      {
        return new JArray(PeerCast.OutputListeners.Select(ol => {
          var res = new JObject();
          res["listenerId"]    = ol.GetHashCode();
          res["address"]       = ol.LocalEndPoint.Address.ToString();
          res["port"]          = ol.LocalEndPoint.Port;
          res["localAccepts"]  = (int)ol.LocalOutputAccepts;
          res["globalAccepts"] = (int)ol.GlobalOutputAccepts;
          return res;
        }).ToArray());
      }

      [RPCMethod("addListener")]
      private JObject AddListener(string address, int port, int localAccepts, int globalAccepts)
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
        var res = new JObject();
        res["listenerId"]    = listener.GetHashCode();
        res["address"]       = listener.LocalEndPoint.Address.ToString();
        res["port"]          = listener.LocalEndPoint.Port;
        res["localAccepts"]  = (int)listener.LocalOutputAccepts;
        res["globalAccepts"] = (int)listener.GlobalOutputAccepts;
        return res;
      }

      [RPCMethod("removeListener")]
      private void RemoveListener(int listenerId)
      {
        foreach (var listener in PeerCast.OutputListeners.Where(ol => ol.GetHashCode()==listenerId)) {
          PeerCast.StopListen(listener);
        }
      }

      [RPCMethod("setListenerAccepts")]
      private void setListenerAccepts(int listenerId, int localAccepts, int globalAccepts)
      {
        foreach (var listener in PeerCast.OutputListeners.Where(ol => ol.GetHashCode()==listenerId)) {
          listener.LocalOutputAccepts = (OutputStreamType)localAccepts;
          listener.GlobalOutputAccepts = (OutputStreamType)globalAccepts;
        }
      }

      [RPCMethod("broadcastChannel")]
      private string BroadcastChannel(int? yellowPageId, string sourceUri, string contentReader, JObject info, JObject track)
      {
        IYellowPageClient yp = null;
        if (yellowPageId.HasValue) {
          yp = PeerCast.YellowPages.FirstOrDefault(y => y.GetHashCode()==yellowPageId.Value);
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
        var channel_id = Utils.CreateChannelID(
          PeerCast.BroadcastID,
          channel_info.Name,
          channel_info.Genre ?? "",
          source.ToString());
        var channel = PeerCast.BroadcastChannel(yp, channel_id, channel_info, source, content_reader);
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
            obj["type"]    = msg.Type.ToString().ToLowerInvariant();
            obj["title"]   = msg.Title;
            obj["message"] = msg.Message;
            return obj;
          })
        );
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
        }
        return new APIHostOutputStream(
          owner,
          PeerCast,
          input_stream,
          output_stream,
          remote_endpoint,
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
