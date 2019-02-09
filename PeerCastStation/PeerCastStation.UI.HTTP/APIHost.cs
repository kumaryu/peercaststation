using System;
using System.Linq;
using System.IO;
using System.Net;
using System.Collections.Generic;
using PeerCastStation.Core;
using PeerCastStation.HTTP;
using PeerCastStation.UI.HTTP.JSONRPC;
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
    private OWINApplication application;
    private object locker = new object();

    private ObjectIdRegistry idRegistry = new ObjectIdRegistry();
    override protected void OnAttach()
    {
    }

    protected override void OnStart()
    {
      Logger.AddWriter(logWriter);
      updater.NewVersionFound += OnNewVersionFound;
      updater.CheckVersion();
      var owinhost =
        Application.PeerCast.OutputStreamFactories.FirstOrDefault(factory => factory is OWINHostOutputStreamFactory) as OWINHostOutputStreamFactory;
      if (owinhost!=null) {
        if (application!=null) {
          owinhost.RemoveApplication(application);
        }
        application = owinhost.AddApplication("/api/1", PathParameters.None, OnProcess);
      }
    }

    protected override void OnStop()
    {
      var owinhost =
        Application.PeerCast.OutputStreamFactories.FirstOrDefault(factory => factory is OWINHostOutputStreamFactory) as OWINHostOutputStreamFactory;
      if (owinhost!=null && application!=null) {
        owinhost.RemoveApplication(application);
      }
      Logger.RemoveWriter(logWriter);
    }

    protected override void OnDetach()
    {
    }

    private Task saveSettingsTask = Task.Delay(0);
    private void SaveSettings()
    {
      lock (locker) {
        if (!saveSettingsTask.IsCompleted) return;
        saveSettingsTask =
          Task.Delay(100).ContinueWith(_ => {
            Application.SaveSettings();
          });
      }
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

    public int[] OpenedPortsV4 { get; set; }
    public int[] OpenedPortsV6 { get; set; }

    public void CheckVersion()
    {
      updater.CheckVersion();
    }

    public IEnumerable<VersionDescription> GetNewVersions()
    {
      return newVersions;
    }

    public class UpdateStatus {
      public Task UpdateTask;
      public float Progress;
      public bool IsCompleted { get { return UpdateTask.IsCompleted; } }
      public bool IsFaulted { get { return UpdateTask.IsFaulted; } }
      public bool IsSucceeded { get { return UpdateTask.IsCompleted && !UpdateTask.IsFaulted && !UpdateTask.IsCanceled; } }
    }
    private UpdateStatus updateStatus = null;

    public UpdateStatus UpdateAsync()
    {
      if (updateStatus!=null) return updateStatus;
      var latest =
        newVersions
          .OrderByDescending(v => v.PublishDate)
          .FirstOrDefault();
      if (latest==null) return null;
      var status = new UpdateStatus { UpdateTask = null, Progress = 0.0f };
      status.UpdateTask = 
        Updater.DownloadAsync(latest, progress => status.Progress = progress, CancellationToken.None)
          .ContinueWith(prev => {
            if (prev.IsFaulted || prev.IsCanceled) return;
            Updater.Install(prev.Result);
          });
      updateStatus = status;
      return updateStatus;
    }

    public UpdateStatus GetUpdateStatus()
    {
      return updateStatus;
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

    public static readonly int RequestLimit = 64*1024;
    public static readonly int TimeoutLimit = 5000;
    private async Task OnProcess(IDictionary<string, object> owinenv)
    {
      var env = new OWINEnv(owinenv);
      var cancel_token = env.CallCanlelled;
      try {
        
        if (!HTTPUtils.CheckAuthorization(env.GetAuthorizationToken(), env.AccessControlInfo)) {
          throw new HTTPError(HttpStatusCode.Unauthorized);
        }
        var ctx = new APIContext(this, this.Application.PeerCast, env.AccessControlInfo);
        var rpc_host = new JSONRPCHost(ctx);
        switch (env.RequestMethod) {
        case "HEAD":
        case "GET":
          await SendJson(env, ctx.GetVersionInfo(), env.RequestMethod!="HEAD", cancel_token).ConfigureAwait(false);
          break;
        case "POST":
          {
            if (!env.RequestHeaders.ContainsKey("X-REQUESTED-WITH")) {
              throw new HTTPError(HttpStatusCode.BadRequest);
            }
            if (!env.RequestHeaders.ContainsKey("CONTENT-LENGTH")) {
              throw new HTTPError(HttpStatusCode.LengthRequired);
            }
            var body = env.RequestBody;
            var len  = body.Length;
            if (len<=0 || RequestLimit<len) {
              throw new HTTPError(HttpStatusCode.BadRequest);
            }

            try {
              var timeout_token = new CancellationTokenSource(TimeoutLimit);
              var buf = await body.ReadBytesAsync((int)len, CancellationTokenSource.CreateLinkedTokenSource(cancel_token, timeout_token.Token).Token).ConfigureAwait(false);
              var request_str = System.Text.Encoding.UTF8.GetString(buf);
              JToken res = rpc_host.ProcessRequest(request_str);
              if (res!=null) {
                await SendJson(env, res, true, cancel_token).ConfigureAwait(false);
              }
              else {
                throw new HTTPError(HttpStatusCode.NoContent);
              }
            }
            catch (OperationCanceledException) {
              throw new HTTPError(HttpStatusCode.RequestTimeout);
            }
          }
          break;
        default:
          throw new HTTPError(HttpStatusCode.MethodNotAllowed);
        }
      }
      catch (HTTPError err) {
        env.ResponseStatusCode = (int)err.StatusCode;
      }
      catch (UnauthorizedAccessException) {
        env.ResponseStatusCode = (int)HttpStatusCode.Forbidden;
      }
    }

    private async Task SendJson(OWINEnv env, JToken token, bool send_body, CancellationToken cancel_token)
    {
      var body = System.Text.Encoding.UTF8.GetBytes(token.ToString());
      env.AddResponseHeader("Content-Type",   "application/json");
      env.AddResponseHeader("Content-Length", body.Length.ToString());
      env.ResponseStatusCode = (int)HttpStatusCode.OK;
      if (send_body) {
        await env.ResponseBody.WriteAsync(body, 0, body.Length, cancel_token).ConfigureAwait(false);
      }
    }

    public class APIContext
    {
      APIHost owner;
      public PeerCast PeerCast { get; private set; }
      public AccessControlInfo AccessControlInfo { get; private set; }
      public APIContext(
        APIHost owner,
        PeerCast peercast,
        AccessControlInfo access_control)
      {
        this.owner = owner;
        this.PeerCast = peercast;
        this.AccessControlInfo = access_control;
      }

      private int GetObjectId(object obj)
      {
        return owner.idRegistry.GetId(obj);
      }

      private NetworkType ParseNetworkType(string value)
      {
        NetworkType result;
        if (Enum.TryParse(value, true, out result)) {
          return result;
        }
        else {
          return NetworkType.IPv4;
        }
      }

      [RPCMethod("getVersionInfo")]
      public JObject GetVersionInfo()
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
        switch (PeerCast.GetPortStatus(System.Net.Sockets.AddressFamily.InterNetwork)) {
        case PortStatus.Unknown:
          res["isFirewalled"] = null;
          break;
        case PortStatus.Open:
          res["isFirewalled"] = false;
          break;
        case PortStatus.Firewalled:
          res["isFirewalled"] = true;
          break;
        }
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
        var addresses = Enumerable.Empty<IPAddress>();
        var port_mapper = PeerCastApplication.Current.Plugins.GetPlugin<PeerCastStation.UI.PortMapperPlugin>();
        if (port_mapper!=null) {
          addresses = addresses.Concat(port_mapper.GetExternalAddresses());
        }
        var listeners =
          PeerCast.OutputListeners
          .Where(p =>
            p.GlobalOutputAccepts!=OutputStreamType.None &&
            p.LocalEndPoint.AddressFamily==System.Net.Sockets.AddressFamily.InterNetworkV6);
        addresses = addresses.Concat(
          listeners
          .Select(p => p.LocalEndPoint.Address)
          .Where(addr =>
            !addr.Equals(IPAddress.IPv6Loopback) &&
            !addr.Equals(IPAddress.IPv6Any) &&
            !addr.Equals(IPAddress.IPv6None) &&
            !addr.IsIPv6Teredo &&
            !addr.IsIPv6LinkLocal &&
            !addr.IsIPv6SiteLocal)
        );
        if (listeners.Any(p => p.LocalEndPoint.Address.Equals(System.Net.IPAddress.IPv6Any))) {
          addresses = addresses.Concat(
            System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
              .Where(intf => !intf.IsReceiveOnly)
              .Where(intf => intf.OperationalStatus==System.Net.NetworkInformation.OperationalStatus.Up)
              .Where(intf => intf.NetworkInterfaceType!=System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
              .Select(intf => intf.GetIPProperties())
              .SelectMany(prop => prop.UnicastAddresses)
              .Where(uaddr => uaddr.Address.AddressFamily==System.Net.Sockets.AddressFamily.InterNetworkV6)
              .Where(uaddr => !uaddr.Address.IsSiteLocal())
              .Select(uaddr => uaddr.Address)
          );
        }
        return new JArray(addresses.Distinct().Select(addr => addr.ToString()));
      }

      [RPCMethod("getSettings")]
      private JToken GetSettings()
      {
        var res = new JObject();
        res["maxRelays"]                     = PeerCast.AccessController.MaxRelays;
        res["maxRelaysPerChannel"]           = PeerCast.AccessController.MaxRelaysPerBroadcastChannel;
        res["maxRelaysPerBroadcastChannel"]  = PeerCast.AccessController.MaxRelaysPerBroadcastChannel;
        res["maxRelaysPerRelayChannel"]      = PeerCast.AccessController.MaxRelaysPerRelayChannel;
        res["maxDirects"]                    = PeerCast.AccessController.MaxPlays;
        res["maxDirectsPerChannel"]          = PeerCast.AccessController.MaxPlaysPerBroadcastChannel;
        res["maxDirectsPerBroadcastChannel"] = PeerCast.AccessController.MaxPlaysPerBroadcastChannel;
        res["maxDirectsPerRelayChannel"]     = PeerCast.AccessController.MaxPlaysPerRelayChannel;
        res["maxUpstreamRate"]               = PeerCast.AccessController.MaxUpstreamRate;
        res["maxUpstreamRateIPv6"]           = PeerCast.AccessController.MaxUpstreamRateIPv6;
        res["maxUpstreamRatePerChannel"]     = PeerCast.AccessController.MaxUpstreamRatePerBroadcastChannel;
        res["maxUpstreamRatePerBroadcastChannel"] = PeerCast.AccessController.MaxUpstreamRatePerBroadcastChannel;
        res["maxUpstreamRatePerRelayChannel"]     = PeerCast.AccessController.MaxUpstreamRatePerRelayChannel;
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
        settings.TryGetThen("maxRelays",           v => acc.MaxRelays = v);
        settings.TryGetThen("maxRelaysPerChannel", v => {
          acc.MaxRelaysPerBroadcastChannel = v;
          acc.MaxRelaysPerRelayChannel     = v;
        });
        settings.TryGetThen("maxRelaysPerBroadcastChannel", v => acc.MaxRelaysPerBroadcastChannel = v);
        settings.TryGetThen("maxRelaysPerRelayChannel",     v => acc.MaxRelaysPerRelayChannel = v);
        settings.TryGetThen("maxDirects",                v => acc.MaxPlays = v);
        settings.TryGetThen("maxDirectsPerChannel", v => {
          acc.MaxPlaysPerBroadcastChannel = v;
          acc.MaxPlaysPerRelayChannel = v;
        });
        settings.TryGetThen("maxDirectsPerBroadcastChannel", v => acc.MaxPlaysPerBroadcastChannel = v);
        settings.TryGetThen("maxDirectsPerRelayChannel",     v => acc.MaxPlaysPerRelayChannel = v);
        settings.TryGetThen("maxUpstreamRate",           v => acc.MaxUpstreamRate = v);
        settings.TryGetThen("maxUpstreamRateIPv6",       v => acc.MaxUpstreamRateIPv6 = v);
        settings.TryGetThen("maxUpstreamRatePerChannel", v => {
          acc.MaxUpstreamRatePerBroadcastChannel = v;
          acc.MaxUpstreamRatePerRelayChannel = v;
        });
        settings.TryGetThen("maxUpstreamRatePerBroadcastChannel", v => acc.MaxUpstreamRatePerBroadcastChannel = v);
        settings.TryGetThen("maxUpstreamRatePerRelayChannel",     v => acc.MaxUpstreamRatePerRelayChannel = v);
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
        owner.SaveSettings();
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
          owner.SaveSettings();
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
        res["network"]         = channel.Network.ToString().ToLowerInvariant();
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
          announcings = announcings.Concat(yp.GetAnnouncingChannels().Where(ac => ac.Channel.ChannelID==channel.ChannelID));
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
        return GetChannelConnection(ac, ac.GetConnectionInfo());
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
        res["remoteSessionId"]  = info.RemoteSessionID?.ToString("N").ToUpperInvariant();
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
          res = res.Concat(yp.GetAnnouncingChannels()
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
          var ac = yp.GetAnnouncingChannels()
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
          var ac = yp.GetAnnouncingChannels()
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
          res["channels"]     = new JArray(yp.GetAnnouncingChannels().Select(ac => {
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
        owner.SaveSettings();
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
          owner.SaveSettings();
        }
      }

      [RPCMethod("stopAnnounce")]
      private void StopAnnounce(int yellowPageId, string channelId=null)
      {
        var yp = PeerCast.YellowPages.FirstOrDefault(p => GetObjectId(p)==yellowPageId);
        if (yp!=null) {
          if (channelId!=null) {
            var channel = GetChannel(channelId);
            var announcing = yp.GetAnnouncingChannels().FirstOrDefault(ac => ac.Channel.ChannelID==channel.ChannelID);
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
            var announcing = yp.GetAnnouncingChannels().FirstOrDefault(ac => ac.Channel.ChannelID==channel.ChannelID);
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
        switch (listener.LocalEndPoint.AddressFamily) {
        case System.Net.Sockets.AddressFamily.InterNetwork:
          if ((listener.GlobalOutputAccepts & OutputStreamType.Relay)!=0 && owner.OpenedPortsV4!=null) {
            res["isOpened"] = (listener.GlobalOutputAccepts & OutputStreamType.Relay)!=0 &&
                              owner.OpenedPortsV4.Contains(listener.LocalEndPoint.Port);
          }
          else {
            res["isOpened"] = null;
          }
          break;
        case System.Net.Sockets.AddressFamily.InterNetworkV6:
          if ((listener.GlobalOutputAccepts & OutputStreamType.Relay)!=0 && owner.OpenedPortsV6!=null) {
            res["isOpened"] = (listener.GlobalOutputAccepts & OutputStreamType.Relay)!=0 &&
                              owner.OpenedPortsV6.Contains(listener.LocalEndPoint.Port);
          }
          else {
            res["isOpened"] = null;
          }
          break;
        default:
          break;
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
        owner.SaveSettings();
        return GetListener(listener);
      }

      [RPCMethod("resetListenerAuthenticationKey")]
      private JObject resetListenerAuthenticationKey(int listenerId)
      {
        var listener = PeerCast.OutputListeners.Where(ol => GetObjectId(ol)==listenerId).FirstOrDefault();
        if (listener!=null) {
          owner.SaveSettings();
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
        owner.SaveSettings();
      }

      [RPCMethod("setListenerAccepts")]
      private void setListenerAccepts(int listenerId, int localAccepts, int globalAccepts)
      {
        foreach (var listener in PeerCast.OutputListeners.Where(ol => GetObjectId(ol)==listenerId)) {
          listener.LocalOutputAccepts = (OutputStreamType)localAccepts;
          listener.GlobalOutputAccepts = (OutputStreamType)globalAccepts;
        }
        owner.SaveSettings();
      }

      [RPCMethod("setListenerAuthorizationRequired")]
      private void setListenerAuthorizationRequired(int listenerId, bool localAuthorizationRequired, bool globalAuthorizationRequired)
      {
        foreach (var listener in PeerCast.OutputListeners.Where(ol => GetObjectId(ol)==listenerId)) {
          listener.LocalAuthorizationRequired = localAuthorizationRequired;
          listener.GlobalAuthorizationRequired = globalAuthorizationRequired;
        }
        owner.SaveSettings();
      }

      [RPCMethod("broadcastChannel")]
      private string BroadcastChannel(
        int?   yellowPageId,
        string networkType,
        string sourceUri,
        string contentReader,
        JObject info,
        JObject track,
        string sourceStream=null)
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

        var network_type = ParseNetworkType(networkType);

        var new_info = new AtomCollection();
        if (info!=null) {
          info.TryGetThen("name",    v => new_info.SetChanInfoName(v));
          info.TryGetThen("url",     v => new_info.SetChanInfoURL(v));
          info.TryGetThen("genre",   v => new_info.SetChanInfoGenre(v));
          info.TryGetThen("desc",    v => new_info.SetChanInfoDesc(v));
          info.TryGetThen("comment", v => new_info.SetChanInfoComment(v));
          info.TryGetThen("bitrate", v => { if (v>=0) new_info.SetChanInfoBitrate(v); });
        }
        var channel_info  = new ChannelInfo(new_info);
        if (channel_info.Name==null || channel_info.Name=="") {
          throw new RPCError(RPCErrorCode.InvalidParams, "Channel name must not be empty");
        }
        var channel_id = PeerCastStation.Core.BroadcastChannel.CreateChannelID(
          PeerCast.BroadcastID,
          network_type,
          channel_info.Name,
          channel_info.Genre ?? "",
          source.ToString());
        var channel = PeerCast.BroadcastChannel(network_type, yp, channel_id, channel_info, source, source_stream, content_reader);
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
            obj["networkType"] = info.NetworkType.ToString().ToLowerInvariant();
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
        info.TryGetThen("networkType", v => obj.NetworkType = ParseNetworkType(v));
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
      public int? CheckBandWidth(string networkType)
      {
        int? result = null;
        string uri_key;
        var network = ParseNetworkType(networkType);
        switch (network) {
        case NetworkType.IPv6:
          uri_key = "BandwidthCheckerV6";
          break;
        case NetworkType.IPv4:
        default:
          uri_key = "BandwidthChecker";
          break;
        }
        Uri target_uri;
        if (AppSettingsReader.TryGetUri(uri_key, out target_uri)) {
          var checker = new BandwidthChecker(target_uri, network);
          var res = checker.Run();
          if (res.Succeeded) {
            result = (int)res.Bitrate/1000;
          }
        }
        return result;
      }

      [RPCMethod("checkPorts")]
      public JArray CheckPorts()
      {
        List<int> results = null;
        var port_checker = PeerCastApplication.Current.Plugins.GetPlugin<PeerCastStation.UI.PCPPortCheckerPlugin>();
        if (port_checker!=null) {
          var task = port_checker.CheckAsync();
          task.Wait();
          foreach (var result in task.Result) {
            if (!result.Success) continue;
            PeerCast.SetPortStatus(result.LocalAddress.AddressFamily, result.IsOpen ? PortStatus.Open : PortStatus.Firewalled);
            switch (result.LocalAddress.AddressFamily) {
            case System.Net.Sockets.AddressFamily.InterNetwork:
              owner.OpenedPortsV4 = result.Ports;
              break;
            case System.Net.Sockets.AddressFamily.InterNetworkV6:
              owner.OpenedPortsV6 = result.Ports;
              break;
            default:
              break;
            }
            if (results==null) {
              results = new List<int>(result.Ports);
            }
            else {
              results.AddRange(result.Ports);
            }
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

      [RPCMethod("updateAndRestart")]
      public JObject UpdateAndRestart()
      {
        var status = owner.UpdateAsync();
        if (status!=null) {
          var obj = new JObject();
          obj["agentName"] = PeerCast.AgentName;
          obj["progress"] = status.Progress;
          if (status.IsCompleted && status.IsFaulted) {
            obj["status"] = "failed";
          }
          if (status.IsCompleted && !status.IsFaulted) {
            obj["status"] = "succeeded";
          }
          else {
            obj["status"] = "progress";
          }
          return obj;
        }
        else {
          return null;
        }
      }

      [RPCMethod("getUpdateStatus")]
      public JObject GetUpdateStatus()
      {
        var status = owner.GetUpdateStatus();
        if (status!=null) {
          var obj = new JObject();
          obj["agentName"] = PeerCast.AgentName;
          obj["progress"] = status.Progress;
          if (status.IsCompleted && status.IsFaulted) {
            obj["status"] = "failed";
          }
          if (status.IsCompleted && !status.IsFaulted) {
            obj["status"] = "succeeded";
          }
          else {
            obj["status"] = "progress";
          }
          return obj;
        }
        else {
          var obj = new JObject();
          obj["agentName"] = PeerCast.AgentName;
          obj["progress"]  = 1.0f;
          obj["status"]    = "succeeded";
          return obj;
        }
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

      private bool TrySetUIConfig(UISettings settings, string key, JObject value)
      {
        switch (key) {
        case "defaultPlayProtocol":
          settings.DefaultPlayProtocols =
            value
            .Properties()
            .ToDictionary(
              prop => prop.Name,
              prop => Enum.TryParse<PlayProtocol>(prop.Value.ToString(), out var v) ? v : PlayProtocol.Unknown
            );
          return true;
        default:
          return false;
        }
      }

      [RPCMethod("setUserConfig")]
      public void SetUserConfig(string user, string key, JObject value)
      {
        var settings = owner.Application.Settings.Get<UISettings>();
        if (!TrySetUIConfig(settings, key, value)) {
          Dictionary<string, string> user_config;
          if (!settings.UserConfig.TryGetValue(user, out user_config)) {
            user_config = new Dictionary<string, string>();
            settings.UserConfig[user] = user_config;
          }
          user_config[key] = value.ToString();
        }
        owner.SaveSettings();
      }

      private bool TryGetUIConfig(UISettings settings, string key, out JToken value)
      {
        switch (key) {
        case "defaultPlayProtocol":
          {
            var obj = new JObject();
            foreach (var kv in settings.DefaultPlayProtocols) {
              obj[kv.Key] = kv.Value.ToString();
            }
            value = obj;
            return true;
          }
        default:
          value = null;
          return false;
        }
      }

      [RPCMethod("getUserConfig")]
      public JToken GetUserConfig(string user, string key)
      {
        var settings = owner.Application.Settings.Get<UISettings>();
        if (TryGetUIConfig(settings, key, out var value)) {
          return value;
        }
        else {
          if (settings.UserConfig.TryGetValue(user, out var user_config) &&
              user_config.TryGetValue(key, out var str)) {
            return JToken.Parse(str);
          }
          else {
            return null;
          }
        }
      }

    }

  }
}
