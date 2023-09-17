﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using PeerCastStation.Core;
using System.Threading;
using System.Net;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Runtime.InteropServices;

namespace PeerCastStation.PCP
{
  public class PCPYellowPageClientFactory
    : IYellowPageClientFactory
  {
    public PeerCast PeerCast { get; private set; }
    public string Name { get { return "PCP"; } }
    public string Protocol { get { return "pcp"; } }

    public IYellowPageClient Create(string name, Uri? announce_uri, Uri? channels_uri)
    {
      return new PCPYellowPageClient(PeerCast, name, announce_uri, channels_uri);
    }

    private static readonly System.Text.RegularExpressions.Regex indexTxtEntryRegex =
      new System.Text.RegularExpressions.Regex(@"\A(^((.*<>)+.*)?$\n?)+\Z", System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.Multiline);

    private enum IndexTxtResult {
      Success,
      ConnectionError,
      DownloadError,
      ParseError,
    }
    private async Task<IndexTxtResult> CheckIndexTxtAsync(Uri uri)
    {
      try {
        using var client = new HttpClient();
        var str = await client.GetStringAsync(uri).ConfigureAwait(false);
        if (indexTxtEntryRegex.IsMatch(str)) {
          return IndexTxtResult.Success;
        }
        else {
          return IndexTxtResult.ParseError;
        }
      }
      catch (HttpRequestException ex) {
        if (ex.StatusCode.HasValue) {
          return IndexTxtResult.DownloadError;
        }
        else {
          return IndexTxtResult.ConnectionError;
        }
      }
    }

    public async Task<YellowPageUriValidationResult> ValidateUriAsync(YellowPageUriType type, Uri uri)
    {
      if (uri==null) throw new ArgumentNullException(nameof(uri));
      switch (type) {
      case YellowPageUriType.Announce:
        if (!uri.IsAbsoluteUri) {
          var builder = new UriBuilder();
          builder.Scheme = "pcp";
          var pathidx = uri.OriginalString.IndexOf('/');
          if (pathidx==0) {
            return new YellowPageUriValidationResult(false, null, "ホスト名を指定してください");
          }
          else if (pathidx>0) {
            builder.Host = uri.OriginalString.Substring(0, pathidx);
            builder.Path = uri.OriginalString.Substring(pathidx);
          }
          else {
            builder.Host = uri.OriginalString;
            builder.Path = "";
          }
          builder.Query = "";
          return new YellowPageUriValidationResult(false, builder.Uri, "pcp:で始まるURIを指定してください");
        }
        else if (uri.Scheme!="pcp") {
          var builder = new UriBuilder(uri);
          builder.Scheme = "pcp";
          builder.Path = "";
          builder.Query = "";
          var portStr = uri.AbsolutePath;
          var pathidx = portStr.IndexOf('/');
          if (pathidx==0) {
            return new YellowPageUriValidationResult(false, null, $"{uri.Scheme}で始まるURIは使用できません");
          }
          else if (pathidx>0) {
            portStr = uri.AbsolutePath.Substring(0, pathidx);
          }
          if (String.IsNullOrEmpty(uri.Host) && Int32.TryParse(portStr, out var port)) {
            builder.Host = uri.Scheme;
            builder.Port = port;
            return new YellowPageUriValidationResult(false, builder.Uri, $"{uri.Scheme}で始まるURIは使用できません");
          }
          else {
            return new YellowPageUriValidationResult(false, null, $"{uri.Scheme}で始まるURIは使用できません");
          }
        }
        else if (String.IsNullOrWhiteSpace(uri.Host)) {
          return new YellowPageUriValidationResult(false, null, "ホスト名を指定してください");
        }
        else if (!String.IsNullOrWhiteSpace(uri.PathAndQuery) || uri.PathAndQuery!="/") {
          var builder = new UriBuilder(uri);
          builder.Path = "";
          builder.Query = "";
          return new YellowPageUriValidationResult(false, builder.Uri, $"パスは指定できません");
        }
        else {
          return new YellowPageUriValidationResult(true, null, null);
        }
      case YellowPageUriType.Channels:
        if (!uri.IsAbsoluteUri) {
          var builder = new UriBuilder();
          builder.Scheme = "http";
          builder.Path = "";
          builder.Query = "";
          if (String.IsNullOrEmpty(builder.Host)) {
            return new YellowPageUriValidationResult(false, null, "ホスト名を指定してください");
          }
          return new YellowPageUriValidationResult(false, builder.Uri, "http:またはhttps:で始まるURIを指定してください");
        }
        else if (uri.Scheme!="http" && uri.Scheme!="https") {
          var builder = new UriBuilder(uri);
          builder.Scheme = "http";
          builder.Path = "";
          builder.Query = "";
          return new YellowPageUriValidationResult(false, builder.Uri, "http:またはhttps:で始まるURIを指定してください");
        }
        else {
          var result = await CheckIndexTxtAsync(uri).ConfigureAwait(false);
          switch (result) {
          case IndexTxtResult.Success:
            return new YellowPageUriValidationResult(true, null, null);
          case IndexTxtResult.DownloadError:
          case IndexTxtResult.ParseError:
            {
              var builder = new UriBuilder(uri);
              if (String.IsNullOrEmpty(builder.Path)) {
                builder.Path = "/index.txt";
              }
              else if (!builder.Path.EndsWith("/")) {
                builder.Path += "/index.txt";
              }
              else {
                builder.Path += "index.txt";
              }
              var result2 = await CheckIndexTxtAsync(builder.Uri).ConfigureAwait(false);
              switch (result2) {
              case IndexTxtResult.Success:
                return new YellowPageUriValidationResult(false, builder.Uri, "index.txtを指定してください");
              case IndexTxtResult.ParseError:
                return new YellowPageUriValidationResult(false, null, "index.txtを指定してください");
              case IndexTxtResult.DownloadError:
              case IndexTxtResult.ConnectionError:
              default:
                return new YellowPageUriValidationResult(false, null, "チャンネル情報が取得できません");
              }
            }
          case IndexTxtResult.ConnectionError:
          default:
            return new YellowPageUriValidationResult(false, null, "チャンネル情報が取得できません");
          }
        }
      default:
        throw new ArgumentOutOfRangeException(nameof(type));
      }
    }

    public PCPYellowPageClientFactory(PeerCast peercast)
    {
      this.PeerCast = peercast;
    }

  }

  internal class BundledYPConnectionPool
    : IDisposable
  {
    private class QuitException : Exception
    {
      public int Code { get; set; }
      public QuitException(int code)
        : base()
      {
        this.Code = code;
      }
    }

    private class PCPYellowPageConnection
      : IDisposable
    {
      private class UpdatedChannel {
        public IChannel Channel { get; private set; }
        public bool     Playing { get; private set; }
        public UpdatedChannel(IChannel channel, bool playing)
        {
          this.Channel = channel;
          this.Playing = playing;
        }
      }

      public PCPYellowPageConnection(IPeerCast hostInfoProvider, YPConnectionPool owner, NetworkType networkType, string name, Uri host)
      {
        this.hostInfoProvider = hostInfoProvider;
        this.logger = new Logger(this.GetType(), $"{name} ({networkType})");
        this.owner = owner;
        this.networkType = networkType;
        this.name = name;
        this.host = host;
      }

      private YPConnectionPool owner;
      private volatile int refCount = 0;
      private CancellationTokenSource disposedCancellation = new CancellationTokenSource();
      private Task connectionTask = Task.Delay(0);
      private WaitableQueue<UpdatedChannel> updateQueue = new WaitableQueue<UpdatedChannel>();
      private Logger logger;
      private IPeerCast hostInfoProvider;
      private NetworkType networkType;
      private Uri host;
      private string name;
      private ConnectionStatus status;

      public ConnectionStatus Status => status;

      public void Dispose()
      {
        disposedCancellation.Cancel();
      }

      private class BannedException : Exception {}
      private IPEndPoint? remoteEndPoint = null;
      private Guid? remoteSessionID;

      private ListenerInfo? GetListenerInfo(IPEndPoint remoteEndPoint)
      {
        return hostInfoProvider.GetListenerInfos()
          .Where(listener =>
            listener.NetworkType==remoteEndPoint.Address.AddressFamily.GetNetworkType() &&
            listener.Locality==remoteEndPoint.Address.GetAddressLocality() &&
            listener.AccessControl.Accepts.HasFlag(OutputStreamType.Relay | OutputStreamType.Metadata)
          ).FirstOrDefault();
      }

      private async Task PCPHandshake(ConnectionStream stream, CancellationToken ct)
      {
        logger.Debug("Sending Handshake");
        await stream.WriteAsync(new Atom(new ID4("pcp\n"), PCPVersion.Default.GetPCPVersionForNetworkType(networkType)), ct).ConfigureAwait(false);
        var helo = new AtomCollection();
        helo.SetHeloAgent(hostInfoProvider.AgentName);
        helo.SetHeloVersion(PCPVersion.Default.ServantVersion);
        helo.SetHeloSessionID(hostInfoProvider.SessionID);
        helo.SetHeloBCID(hostInfoProvider.BroadcastID);
        var listener = GetListenerInfo(stream.RemoteEndPoint!);
        switch (listener?.Status ?? PortStatus.Firewalled) {
        case PortStatus.Open:
          helo.SetHeloPort(listener!.EndPoint.Port);
          break;
        case PortStatus.Firewalled:
          break;
        case PortStatus.Unknown:
          helo.SetHeloPing(listener!.EndPoint.Port);
          break;
        }
        await stream.WriteAsync(new Atom(Atom.PCP_HELO, helo), ct).ConfigureAwait(false);
        while (!ct.IsCancellationRequested) {
          var atom = await stream.ReadAtomAsync(ct).ConfigureAwait(false);
          if (atom.Name==Atom.PCP_OLEH) {
            OnPCPOleh(stream, atom);
            break;
          }
          else if (atom.Name==Atom.PCP_QUIT) {
            logger.Debug("Handshake aborted by PCP_QUIT ({0})", atom.GetInt32());
            throw new QuitException(atom.GetInt32());
          }
        }
      }

      private void OnPCPOleh(ConnectionStream stream, Atom atom)
      {
        if (atom.Children==null) {
          throw new InvalidDataException($"{atom.Name} has no SessionID");
        }
        remoteSessionID = atom.Children.GetHeloSessionID();
        if (remoteSessionID==null) {
          throw new InvalidDataException($"{atom.Name} has no SessionID");
        }
        var dis = atom.Children.GetHeloDisable();
        if (dis!=null && dis.Value!=0) {
          throw new BannedException();
        }
        var rip = atom.Children.GetHeloRemoteIP();
        var port = atom.Children.GetHeloPort();
        hostInfoProvider.NotifyRemoteAddressAndPort(stream.RemoteEndPoint!, stream.LocalEndPoint!, rip, port);
      }

      private async Task ReceiveAndProcessAtomAsync(Stream stream, CancellationToken ct)
      {
        do {
          var atom = await stream.ReadAtomAsync(ct).ConfigureAwait(false);
          owner.ProcessAtom(atom);
        } while (refCount>0 && !ct.IsCancellationRequested);
      }

      private async Task DequeueAndUpdateChannelAsync(Stream stream, CancellationToken ct)
      {
        do {
          var updatedChannel = await updateQueue.DequeueAsync(ct).ConfigureAwait(false);
          await stream.WriteAsync(CreateChannelBcst(updatedChannel.Channel.GetChannelStatus(), updatedChannel.Playing)).ConfigureAwait(false);
        } while (refCount>0 && !ct.IsCancellationRequested);
      }

      private async Task CancelIfCompletedTask(Task task, CancellationTokenSource cts)
      {
        try {
          await task.ConfigureAwait(false);
        }
        finally {
          cts.Cancel();
        }
      }

      private Task TaskWhenAllForAwait(params Task[] tasks)
      {
        var completionSource = new TaskCompletionSource<bool>();
        Task.WhenAll(tasks)
          .ContinueWith(prev => {
            if (prev.IsFaulted) {
              completionSource.SetException(prev.Exception!);
            }
            else if (prev.IsCanceled) {
              completionSource.SetCanceled();
            }
            else {
              completionSource.SetResult(true);
            }
          });
        return completionSource.Task;
      }

      private enum ConnectionResult {
        Stopped,
        ServerQuit,
        Banned,
        Error,
      }

      private async Task<ConnectionResult> ConnectionProc(Uri uri, CancellationToken ct)
      {
        ConnectionResult result;
        ct.ThrowIfCancellationRequested();
        void handleExceptions(AggregateException exception) {
          foreach (var ex in exception.InnerExceptions) {
            handleException(ex);
          }
        }
        void handleException(Exception ex) {
          switch (ex) {
          case TaskCanceledException e:
            result = ConnectionResult.Stopped;
            status = ConnectionStatus.Idle;
            break;
          case BannedException e:
            status = ConnectionStatus.Error;
            logger.Error("Your BCID is banned");
            result = ConnectionResult.Banned;
            break;
          case QuitException e:
            status = ConnectionStatus.Error;
            if (e.Code==Atom.PCP_ERROR_QUIT+Atom.PCP_ERROR_GENERAL) {
              result = ConnectionResult.ServerQuit;
            }
            else {
              result = ConnectionResult.Error;
            }
            break;
          case InvalidDataException e:
            status = ConnectionStatus.Error;
            logger.Error(e);
            result = ConnectionResult.Error;
            break;
          case SocketException e:
            status = ConnectionStatus.Error;
            logger.Info(e);
            result = ConnectionResult.Error;
            break;
          case IOException e:
            status = ConnectionStatus.Error;
            logger.Info(e);
            result = ConnectionResult.Error;
            break;
          case AggregateException e:
            handleExceptions(e);
            break;
          default:
            status = ConnectionStatus.Error;
            logger.Error(ex);
            result = ConnectionResult.Error;
            break;
          }
        }
        var host = uri.DnsSafeHost;
        var port = uri.Port;

        if (port<0) port = PCPVersion.Default.DefaultPort;
        logger.Debug("Connecting to YP");
        status = ConnectionStatus.Connecting;
        remoteSessionID = null;
        try {
          using (var client=new TcpClient()) {
            await client.ConnectAsync(host, port).ConfigureAwait(false);
            using (var stream=new ConnectionStream(client.Client, client.GetStream())) {
              OnStarted(stream);
              await PCPHandshake(stream, ct).ConfigureAwait(false);
              logger.Debug("Handshake succeeded");
              status = ConnectionStatus.Connected;
              remoteEndPoint = stream.RemoteEndPoint;
              using (var subCancellationSource=CancellationTokenSource.CreateLinkedTokenSource(ct)) {
                try {
                  await TaskWhenAllForAwait(
                    CancelIfCompletedTask(ReceiveAndProcessAtomAsync(stream, subCancellationSource.Token), subCancellationSource),
                    CancelIfCompletedTask(DequeueAndUpdateChannelAsync(stream, subCancellationSource.Token), subCancellationSource)).ConfigureAwait(false);
                }
                catch (TaskCanceledException) {
                }
              }
              logger.Debug("Closing connection");
              await stream.WriteAsync(new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT)).ConfigureAwait(false);
              OnStopped(stream);
              result = ConnectionResult.Stopped;
              status = ConnectionStatus.Idle;
            }
          }
        }
        catch (AggregateException aggregateException) {
          result = ConnectionResult.Error;
          handleExceptions(aggregateException);
        }
        catch (Exception e) {
          result = ConnectionResult.Error;
          handleException(e);
        }
        remoteEndPoint = null;
        remoteSessionID = null;
        logger.Debug("Connection closed");
        return result;
      }

      private void OnStarted(ConnectionStream connection)
      {
      }

      private void OnStopped(ConnectionStream connection)
      {
      }

      private void CheckConnection() {
        async Task startConnection(Task prevTask) {
          await prevTask.ConfigureAwait(false);
          int retryWait = 1000;
        retry:
          var result = await ConnectionProc(host, disposedCancellation.Token).ConfigureAwait(false);
          switch (result) {
          case ConnectionResult.Banned:
          case ConnectionResult.ServerQuit:
            disposedCancellation.Cancel();
            break;
          case ConnectionResult.Stopped:
            break;
          case ConnectionResult.Error:
            if (refCount>0 && !disposedCancellation.IsCancellationRequested) {
              await Task.Delay(retryWait, disposedCancellation.Token).ConfigureAwait(false);
              retryWait = Math.Min(retryWait * 3 / 2, 30000);
              goto retry;
            }
            break;
          }
        }
        lock (this) {
          if (connectionTask.IsCompleted && refCount>0 && !disposedCancellation.IsCancellationRequested) {
            connectionTask = startConnection(connectionTask);
          }
        }
      }

      public void UpdateChannel(IChannel channel, bool playing=true)
      {
        updateQueue.Enqueue(new UpdatedChannel(channel, playing));
        CheckConnection();
      }

      public void ChannelAdded(IChannel channel)
      {
        Interlocked.Increment(ref refCount);
        UpdateChannel(channel, true);
      }

      public void ChannelRemoved(IChannel channel)
      {
        Interlocked.Decrement(ref refCount);
        UpdateChannel(channel, false);
      }

      private void PostHostInfo(AtomCollection parent, ChannelStatus channelStatus, bool playing)
      {
        var listeners = hostInfoProvider.GetListenerInfos().Where(listener => listener.NetworkType==channelStatus.Network).ToArray();
        var hostinfo = new AtomCollection();
        hostinfo.SetHostChannelID(channelStatus.ChannelID);
        hostinfo.SetHostSessionID(hostInfoProvider.SessionID);
        var globalEndPoint = listeners.FirstOrDefault(listener => listener.Locality==NetworkLocality.Global && listener.AccessControl.Accepts.HasFlag(OutputStreamType.Relay));
        if (globalEndPoint!=null) {
          hostinfo.AddHostIP(globalEndPoint.EndPoint.Address);
          hostinfo.AddHostPort(globalEndPoint.EndPoint.Port);
        }
        var localEndPoint = listeners.FirstOrDefault(listener => listener.Locality==NetworkLocality.Local && listener.AccessControl.Accepts.HasFlag(OutputStreamType.Relay));
        if (localEndPoint!=null) {
          hostinfo.AddHostIP(localEndPoint.EndPoint.Address);
          hostinfo.AddHostPort(localEndPoint.EndPoint.Port);
        }
        hostinfo.SetHostNumListeners(channelStatus.TotalDirects);
        hostinfo.SetHostNumRelays(channelStatus.TotalRelays);
        hostinfo.SetHostUptime(channelStatus.Uptime);
        var oldest = channelStatus.OldestContentPosision;
        if (oldest!=null) {
          hostinfo.SetHostOldPos((uint)(oldest.Value & 0xFFFFFFFFU));
        }
        var newest = channelStatus.NewestContentPosision;
        if (newest!=null) {
          hostinfo.SetHostNewPos((uint)(newest.Value & 0xFFFFFFFFU));
        }
        PCPVersion.Default.SetHostVersion(hostinfo);
        var relayable = channelStatus.IsRelayable;
        var playEndPoint = listeners.FirstOrDefault(listener => listener.Locality==remoteEndPoint?.Address.GetAddressLocality() && listener.AccessControl.Accepts.HasFlag(OutputStreamType.Play));
        var playable  = channelStatus.IsPlayable && playEndPoint?.Status==PortStatus.Open;
        var relayEndPoint = listeners.FirstOrDefault(listener => listener.Locality==remoteEndPoint?.Address.GetAddressLocality() && listener.AccessControl.Accepts.HasFlag(OutputStreamType.Relay));
        var firewalled = relayEndPoint?.Status!=PortStatus.Open;
        var receiving = playing && channelStatus.SourceStatus==SourceStreamStatus.Receiving;
        hostinfo.SetHostFlags1(
          (relayable  ? PCPHostFlags1.Relay      : 0) |
          (playable   ? PCPHostFlags1.Direct     : 0) |
          (firewalled ? PCPHostFlags1.Firewalled : 0) |
          PCPHostFlags1.Tracker |
          (receiving ? PCPHostFlags1.Receiving : PCPHostFlags1.None));
        parent.SetHost(hostinfo);
      }

      private void PostChannelInfo(AtomCollection parent, ChannelStatus channelStatus)
      {
        var atom = new AtomCollection();
        atom.SetChanID(channelStatus.ChannelID);
        atom.SetChanBCID(hostInfoProvider.BroadcastID);
        if (channelStatus.ChannelInfo!=null)  atom.SetChanInfo(channelStatus.ChannelInfo.Extra);
        if (channelStatus.ChannelTrack!=null) atom.SetChanTrack(channelStatus.ChannelTrack.Extra);
        parent.SetChan(atom);
      }

      private Atom CreateChannelBcst(ChannelStatus channelStatus, bool playing)
      {
        var bcst = new AtomCollection();
        bcst.SetBcstTTL(1);
        bcst.SetBcstHops(0);
        bcst.SetBcstFrom(hostInfoProvider.SessionID);
        PCPVersion.Default.SetBcstVersion(bcst);
        bcst.SetBcstChannelID(channelStatus.ChannelID);
        bcst.SetBcstGroup(BroadcastGroup.Root);
        PostChannelInfo(bcst, channelStatus);
        PostHostInfo(bcst, channelStatus, playing);
        return new Atom(Atom.PCP_BCST, bcst);
      }

      public ConnectionInfo GetConnectionInfo()
      {
        var host_status = RemoteHostStatus.None;
        var rhost = remoteEndPoint;
        if (rhost!=null) {
          host_status |= RemoteHostStatus.Root;
          if (rhost.Address.IsSiteLocal()) host_status |= RemoteHostStatus.Local;
        }
        return new ConnectionInfoBuilder {
          ProtocolName     = $"PCP COUT {networkType}",
          Type             = ConnectionType.Announce,
          Status           = status,
          RemoteName       = name,
          RemoteEndPoint   = rhost,
          RemoteHostStatus = host_status,
          RemoteSessionID  = remoteSessionID,
        }.Build();
      }
    }

    class YPConnectionPool
      : IDisposable
    {
      private class AnnouncingChannel
        : IAnnouncingChannel,
          IChannelMonitor
      {
        private PCPYellowPageClient yellowPageClient;
        private IChannel channel;
        private YPConnectionPool connection;
        private IDisposable subscription;
        private bool removed = false;
        private List<CancellationTokenSource> removedCancellationTokens = new List<CancellationTokenSource>();
        public IChannel Channel => channel;
        public AnnouncingStatus Status => connection.GetChannelStatus(channel);
        public IYellowPageClient YellowPage => yellowPageClient;

        public AnnouncingChannel(PCPYellowPageClient yellowPageClient, IChannel channel, YPConnectionPool connection)
        {
          this.yellowPageClient = yellowPageClient;
          this.channel = channel;
          this.connection = connection;
          this.subscription = channel.AddMonitor(this);
        }

        public ConnectionInfo GetConnectionInfo()
        {
          return connection.GetConnectionInfo();
        }

        public void OnContentChanged(ChannelContentType channelContentType)
        {
          switch (channelContentType) {
          case ChannelContentType.ChannelInfo:
          case ChannelContentType.ChannelTrack:
            connection.UpdateChannel(Channel);
            break;
          }
        }

        public void OnNodeChanged(ChannelNodeAction action, Host node)
        {
        }

        public void OnStopped(StopReason reason)
        {
          connection.RemoveChannel(Channel);
        }

        public void OnRemoved()
        {
          subscription.Dispose();
          lock (removedCancellationTokens) {
            foreach (var cts in removedCancellationTokens) {
              cts.Cancel();
            }
            removedCancellationTokens.Clear();
            removed = true;
          }
        }

        public void CancelOnRemoved(CancellationTokenSource cts)
        {
          lock (removedCancellationTokens) {
            if (removed) {
              cts.Cancel();
            }
            else {
              removedCancellationTokens.Add(cts);
            }
          }
        }

      }

      private PCPYellowPageClient owner;
      private PCPYellowPageConnection connection;
      private readonly Dictionary<IChannel, AnnouncingChannel> channels = new Dictionary<IChannel, AnnouncingChannel>();
      private Task updateTask;
      private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
      private NetworkType networkType;
      private string name;
      private Uri host;
      private Logger logger = new Logger(nameof(YPConnectionPool));
      private static readonly TimeSpan UpdateTimeSpan = TimeSpan.FromSeconds(30.0);

      public YPConnectionPool(PCPYellowPageClient owner, NetworkType networkType, string name, Uri host)
      {
        this.owner = owner;
        this.networkType = networkType;
        this.name = name;
        this.host = host;
        connection = new PCPYellowPageConnection(owner.PeerCast, this, networkType, name, host);
        var ct = cancellationTokenSource.Token;
        updateTask = Task.Run(async () => {
          try {
            while (!ct.IsCancellationRequested) {
              await Task.Delay(UpdateTimeSpan, ct).ConfigureAwait(false);
              UpdateChannels();
            }
          }
          catch (TaskCanceledException) {
          }
        });
      }

      public void Dispose()
      {
        connection.Dispose();
        cancellationTokenSource.Cancel();
        try {
          updateTask.Wait();
        }
        catch (AggregateException) {
        }
      }

      public void Restart()
      {
        var newConnection = new PCPYellowPageConnection(owner.PeerCast, this, networkType, name, host);
        var oldConnection = Interlocked.Exchange(ref connection, newConnection);
        Task.Run(() => oldConnection.Dispose());
        lock (channels) {
          foreach (var channel in channels.Keys) {
            connection.ChannelAdded(channel);
          }
        }
      }

      public void UpdateChannels()
      {
        lock (channels) {
          foreach (var channel in channels.Keys) {
            UpdateChannel(channel);
          }
        }
      }

      public void UpdateChannel(IChannel channel)
      {
        lock (channels) {
          if (channels.ContainsKey(channel)) {
            connection.UpdateChannel(channel);
          }
        }
      }

      public IAnnouncingChannel AddChannel(IChannel channel)
      {
        lock (channels) {
          if (!channels.ContainsKey(channel)) {
            var announcing = new AnnouncingChannel(owner, channel, this);
            channels.Add(channel, announcing);
            connection.ChannelAdded(channel);
            logger.Debug($"Start announce channel {channel.ChannelID.ToString("N")} to {name}");
            return announcing;
          }
          else {
            UpdateChannel(channel);
            return channels[channel];
          }
        }
      }

      public void RemoveChannel(IChannel channel)
      {
        lock (channels) {
          if (!channels.TryGetValue(channel, out var announcing)) return;
          channels.Remove(channel);
          connection.ChannelRemoved(channel);
          announcing.OnRemoved();
          logger.Debug($"Stop announce channel {channel.ChannelID.ToString("N")} from {name}");
        }
      }

      public AnnouncingStatus GetChannelStatus(IChannel channel)
      {
        lock (channels) {
          if (channels.ContainsKey(channel)) return AnnouncingStatus.Idle;
          switch (connection.Status) {
          case ConnectionStatus.Idle:
            return AnnouncingStatus.Idle;
          case ConnectionStatus.Error:
            return AnnouncingStatus.Error;
          case ConnectionStatus.Connected:
            return AnnouncingStatus.Connected;
          case ConnectionStatus.Connecting:
            return AnnouncingStatus.Connecting;
          default:
            return AnnouncingStatus.Error;
          }
        }
      }

      public ConnectionInfo GetConnectionInfo()
      {
        return connection.GetConnectionInfo();
      }

      public IEnumerable<IAnnouncingChannel> GetAnnouncingChannels()
      {
        lock (channels) {
          return channels.Values.ToArray();
        }
      }

      internal void ProcessAtom(Atom atom)
      {
             if (atom.Name==Atom.PCP_BCST) OnPCPBcst(atom);
        else if (atom.Name==Atom.PCP_QUIT) OnPCPQuit(atom);
        else if (atom.Name==Atom.PCP_PUSH) OnPCPPush(atom);
      }

      private void OnPCPQuit(Atom atom)
      {
        logger.Debug("Connection aborted by PCP_QUIT ({0})", atom.GetInt32());
        throw new QuitException(atom.GetInt32());
      }

      private void OnPCPBcst(Atom atom)
      {
        if (atom.Children==null) return;
        var dest = atom.Children.GetBcstDest();
        if (dest==null || dest==owner.PeerCast.SessionID) {
          foreach (var c in atom.Children) {
            ProcessAtom(c);
          }
        }
        var channel_id = atom.Children.GetBcstChannelID();
        var group = atom.Children.GetBcstGroup();
        var from  = atom.Children.GetBcstFrom();
        var ttl   = atom.Children.GetBcstTTL();
        var hops  = atom.Children.GetBcstHops();
        lock (channels) {
          var channel = channels.FirstOrDefault(c => c.Key.ChannelID==channel_id).Key;
          if (channel!=null && group!=null && from!=null && ttl!=null && ttl.Value>0) {
            var bcst = new AtomCollection(atom.Children);
            bcst.SetBcstTTL((byte)(ttl.Value-1));
            bcst.SetBcstHops((byte)((hops ?? 0)+1));
            channel.Broadcast(null, new Atom(Atom.PCP_BCST, bcst), group.Value);
          }
        }
      }

      private void OnPCPPush(Atom atom)
      {
        if (atom.Children==null) return;
        var channel_id = atom.Children.GetPushChannelID();
        var endpoint   = atom.Children.GetPushEndPoint();
        lock (channels) {
          var (channel, annoucing) = channels.FirstOrDefault(c => c.Key.ChannelID==channel_id);
          if (channel_id!=null && channel!=null && endpoint!=null) {
            //GIVする接続を作成する
            Task.Run(async () => {
              using var cts = new CancellationTokenSource();
              using var client = new TcpClient();
              annoucing.CancelOnRemoved(cts);
              await client.ConnectAsync(endpoint, cts.Token).ConfigureAwait(false);
              var stream = client.GetStream();
              await stream.WriteUTF8Async($"GIV /{channel_id.Value.ToString("N")}\r\n\r\n", cts.Token).ConfigureAwait(false);
              var handler = new ConnectionHandler(owner.PeerCast);
              await handler.HandleClient(
                (IPEndPoint)client.Client.LocalEndPoint!,
                client,
                stream,
                new AccessControlInfo(OutputStreamType.Relay, false, AuthenticationKey.Generate()),
                cts.Token
              ).ConfigureAwait(false);
            });
          }
        }
      }

    }

    private YPConnectionPool poolIPv4;
    private YPConnectionPool poolIPv6;

    public BundledYPConnectionPool(PCPYellowPageClient owner, string name, Uri host)
    {
      poolIPv4 = new YPConnectionPool(owner, NetworkType.IPv4, name, host);
      poolIPv6 = new YPConnectionPool(owner, NetworkType.IPv6, name, host);
    }

    public void Dispose()
    {
      poolIPv4.Dispose();
      poolIPv6.Dispose();
    }

    public void Restart()
    {
      poolIPv4.Restart();
      poolIPv6.Restart();
    }

    private YPConnectionPool GetChannelConnectionPool(IChannel channel)
    {
      switch (channel.Network) {
      case NetworkType.IPv4:
        return poolIPv4;
      case NetworkType.IPv6:
        return poolIPv6;
      default:
        throw new ArgumentException("Unexpected Channel Network type", "channel");
      }
    }

    public void UpdateChannel(IChannel channel)
    {
      GetChannelConnectionPool(channel).UpdateChannel(channel);
    }

    public IAnnouncingChannel AddChannel(IChannel channel)
    {
      return GetChannelConnectionPool(channel).AddChannel(channel);
    }

    public void RemoveChannel(IChannel channel)
    {
      GetChannelConnectionPool(channel).RemoveChannel(channel);
    }

    public IEnumerable<IAnnouncingChannel> GetAnnouncingChannels()
    {
      return poolIPv4.GetAnnouncingChannels()
        .Concat(poolIPv6.GetAnnouncingChannels());
    }

  }

  public class PCPYellowPageClient
    : IYellowPageClient
  {
    protected Logger Logger { get; private set; }
    public IPeerCast PeerCast { get; private set; }
    public string Name { get; private set; }
    public string Protocol { get { return "pcp"; } }
    public Uri? AnnounceUri { get; private set; }
    public Uri? ChannelsUri { get; private set; }
    public static bool IsValidUri([NotNullWhen(true)] Uri? uri)
    {
      return uri!=null && uri.IsAbsoluteUri && uri.Scheme=="pcp";
    }

    public class PCPYellowPageChannel
      : IYellowPageChannel
    {
      public IYellowPageClient Source { get; private set; }
      public string Name         { get; set; } = "";
      public Guid ChannelId      { get; set; }
      public string? Tracker     { get; set; }
      public string? ContentType { get; set; }
      public int? Listeners      { get; set; }
      public int? Relays         { get; set; }
      public int? Bitrate        { get; set; }
      public int? Uptime         { get; set; }
      public string? ContactUrl  { get; set; }
      public string? Genre       { get; set; }
      public string? Description { get; set; }
      public string? Comment     { get; set; }
      public string? Artist      { get; set; }
      public string? TrackTitle  { get; set; }
      public string? Album       { get; set; }
      public string? TrackUrl    { get; set; }

      public PCPYellowPageChannel(IYellowPageClient source)
      {
        this.Source = source;
      }
    }

    public PCPYellowPageClient(IPeerCast peca, string name, Uri? announce_uri, Uri? channels_uri)
    {
      this.PeerCast = peca;
      this.Name = name;
      this.AnnounceUri = announce_uri;
      this.ChannelsUri = channels_uri;
      this.Logger = new Logger(this.GetType());
    }

    private string? ReadResponse(Stream s)
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
          if (atom.Name==Atom.PCP_HOST && atom.Children!=null) {
            if (atom.Children.GetHostChannelID()==channel_id) {
              var host = new HostBuilder();
              var endpoints = atom.Children.GetHostEndPoints();
              if (endpoints.Length>0) host.GlobalEndPoint = endpoints[0];
              if (endpoints.Length>1) host.LocalEndPoint = endpoints[1];
              host.DirectCount = atom.Children.GetHostNumListeners() ?? 0;
              host.RelayCount = atom.Children.GetHostNumRelays() ?? 0;
              host.SessionID = atom.Children.GetHostSessionID() ?? Guid.Empty;
              if (atom.Children.GetHostFlags1().HasValue) {
                var flags = atom.Children.GetHostFlags1().GetValueOrDefault();
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

    private Uri? HostToUri(Host? host, Guid channel_id)
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

    public Uri? FindTracker(Guid channel_id)
    {
      if (!IsValidUri(AnnounceUri)) return null;
      Logger.Debug($"Finding tracker {channel_id.ToString("N")} from {AnnounceUri}");
      var host = AnnounceUri.DnsSafeHost;
      var port = AnnounceUri.Port;
      Uri? res = null;
      if (port<0) port = PCPVersion.Default.DefaultPort;
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
              helo.SetHeloVersion(PCPVersion.Default.ServantVersion);
              helo.SetHeloSessionID(PeerCast.SessionID);
              helo.SetHeloPort(0);
              AtomWriter.Write(stream, new Atom(Atom.PCP_HELO, helo));
              var hosts = ReadHosts(stream, channel_id);
              if (hosts.Count>0) {
                res = HostToUri(hosts.FirstOrDefault(h => h.IsTracker), channel_id);
              }
              else {
                // 503 ながらホスト情報が返ってこなかった→GIVを待て
                // なのでYP自体をトラッカーとしてチャンネル側で再度リレーリクエストしてもらう
                res = AnnounceUri;
              }
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

    private BundledYPConnectionPool? connectionPool = null;

    public IEnumerable<IAnnouncingChannel> GetAnnouncingChannels()
    {
      var pool = connectionPool;
      if (pool==null) return Enumerable.Empty<IAnnouncingChannel>();
      return pool.GetAnnouncingChannels();
    }

    public IAnnouncingChannel? Announce(IChannel channel)
    {
    start:
      if (!IsValidUri(AnnounceUri)) return null;
      var pool = connectionPool;
      if (pool==null) {
        var newPool = new BundledYPConnectionPool(this, Name, AnnounceUri);
        if (Interlocked.CompareExchange(ref connectionPool, newPool, pool)!=pool) {
          newPool.Dispose();
          goto start;
        }
      }
      return connectionPool!.AddChannel(channel);
    }

    public void StopAnnounce(IAnnouncingChannel announcing)
    {
      connectionPool?.RemoveChannel(announcing.Channel);
    }

    public void StopAnnounce()
    {
    start:
      var pool = connectionPool;
      if (pool!=null) {
        pool.Dispose();
      }
      if (Interlocked.CompareExchange(ref connectionPool, null, pool)!=pool) {
        goto start;
      }
    }

    public void RestartAnnounce(IAnnouncingChannel announcing)
    {
      RestartAnnounce();
    }

    public void RestartAnnounce()
    {
      connectionPool?.Restart();
    }

    public async Task<IEnumerable<IYellowPageChannel>> GetChannelsAsync(CancellationToken cancel_token)
    {
      if (ChannelsUri==null) return Enumerable.Empty<IYellowPageChannel>();
      using var client = new HttpClient();
      using (var reader=new StringReader(await client.GetStringAsync(ChannelsUri, cancel_token).ConfigureAwait(false))) {
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

    private PCPYellowPageClientFactory? factory = null;
    override protected void OnAttach(PeerCastApplication app)
    {
      if (factory==null) factory = new PCPYellowPageClientFactory(app.PeerCast);
      app.PeerCast.YellowPageFactories.Add(factory);
    }

    override protected void OnDetach(PeerCastApplication app)
    {
      if (factory!=null) {
        app.PeerCast.YellowPageFactories.Remove(factory);
      }
    }
  }
}
