﻿// PeerCastStation, a P2P streaming servent.
// Copyright (C) 2011 Ryuichi Sakamoto (kumaryu@kumaryu.net)
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections;
using System.Net;

namespace PeerCastStation.Core
{

  /// <summary>
  /// Broadcastの送信先を指定します
  /// </summary>
  [Flags]
  public enum BroadcastGroup : byte {
    /// <summary>
    /// YellowPage
    /// </summary>
    Root = 1,
    /// <summary>
    /// SourceStreamを含むストリーム
    /// </summary>
    Trackers = 2,
    /// <summary>
    /// SourceStreamを含まないストリーム
    /// </summary>
    Relays = 4,
  }

  public enum NetworkType : int {
    IPv4,
    IPv6,
  }

  public record ChannelStatus(
    Guid ChannelID,
    NetworkType Network,
    SourceStreamStatus SourceStatus,
    TimeSpan Uptime,
    bool IsBroadCasting,
    int LocalRelays,
    int LocalDirects,
    int TotalRelays,
    int TotalDirects,
    ChannelInfo? ChannelInfo,
    ChannelTrack? ChannelTrack,
    long? OldestContentPosision,
    long? NewestContentPosision,
    bool IsRelayable,
    bool IsPlayable
  );

  public interface IChannel
  {
    Guid ChannelID { get; }
    NetworkType Network { get; }
    ChannelStatus GetChannelStatus();
    IDisposable AddMonitor(IChannelMonitor monitor);
    void Broadcast(Host? from, Atom packet, BroadcastGroup group);
  }

  /// <summary>
  /// チャンネル接続を管理するクラスです
  /// </summary>
  public abstract class Channel
    : IChannel
  {
    protected static Logger logger = new Logger(typeof(Channel));
    private const int NodeLimit = 180000; //ms
    private ChannelSourceSubscription? sourceStream = null;
    private IChannelMonitor[] monitors = Array.Empty<IChannelMonitor>();
    private IChannelSink[] sinks = Array.Empty<IChannelSink>();
    private Host[] sourceNodes = new Host[0];
    private Host[] nodes = new Host[0];
    private Content? contentHeader = null;
    private ContentCollection contents;
    private System.Diagnostics.Stopwatch uptimeTimer = new System.Diagnostics.Stopwatch();
    private int streamID = 0;

    /// <summary>
    /// 所属するPeerCastオブジェクトを取得します
    /// </summary>
    public IPeerCast PeerCast { get; private set; }
    /// <summary>
    /// チャンネルの状態を取得します
    /// </summary>
    public virtual SourceStreamStatus Status {
      get {
        var source = sourceStream?.ChannelSource;
        return source!=null ? source.Status : SourceStreamStatus.Idle;
      }
    }

    public AddressFamily NetworkAddressFamily {
      get {
        switch (this.Network) {
        case NetworkType.IPv6:
          return AddressFamily.InterNetworkV6;
        case NetworkType.IPv4:
        default:
          return AddressFamily.InterNetwork;
        }
      }
    }

    public NetworkType Network { get; private set; }
    public Guid ChannelID   { get; private set; }
    private record ChannelSourceKey(ISourceStreamFactory SourceStreamFactory, Uri Uri);
    private ChannelSourceKey? sourceKey;
    public Uri? SourceUri {
      get {  return sourceKey?.Uri; }
    }
    public abstract bool IsBroadcasting { get; }

    public virtual EndPoint? TrackerEndPoint {
      get {
        var source = SourceUri;
        if (source==null) {
          return null;
        }
        switch (source.HostNameType) {
        case UriHostNameType.IPv4:
        case UriHostNameType.IPv6:
          if (IPAddress.TryParse(source.Host, out var addr)) {
            return new IPEndPoint(addr, source.Port<0 ? 7144 : source.Port);
          }
          else {
            return null;
          }
        case UriHostNameType.Dns:
          return new DnsEndPoint(source.IdnHost, source.Port<0 ? 7144 : source.Port);
        case UriHostNameType.Basic:
        case UriHostNameType.Unknown:
        default:
          return null;
        }
      }
    }

    /// <summary>
    /// ソースストリームを取得します
    /// </summary>
    public ISourceStream? SourceStream {
      get {
        return sourceStream?.ChannelSource;
      }
    }

    /// <summary>
    /// 自分のノードに直接つながっているリレー接続数を取得します
    /// </summary>
    public int LocalRelays {
      get { return sinks.Select(x => x.GetConnectionInfo()).Count(x => x.Type.HasFlag(ConnectionType.Relay)); }
    }

    /// <summary>
    /// 自分のノードに直接つながっている視聴接続数を取得します
    /// </summary>
    public int LocalDirects {
      get { return sinks.Select(x => x.GetConnectionInfo()).Count(x => x.Type.HasFlag(ConnectionType.Direct)); }
    }

    /// <summary>
    /// 保持している全てのノードと自分ノードのリレー合計を取得します
    /// </summary>
    public int TotalRelays {
      get { return LocalRelays + Nodes.Sum(n => n.RelayCount); }
    }

    /// <summary>
    /// 保持している全てのノードと自分ノードの視聴数合計を取得します
    /// </summary>
    public int TotalDirects {
      get { return LocalDirects + Nodes.Sum(n => n.DirectCount); }
    }

    /// <summary>
    /// 出力ストリームの読み取り専用リストを取得します
    /// </summary>
    public IReadOnlyCollection<IChannelSink> OutputStreams {
      get { return sinks; }
    }

    private void ReplaceCollection<T>(ref T[] collection, Func<T[], T[]> newcollection_func) where T : class
    {
      bool replaced = false;
      do {
        var orig = collection;
        var new_collection = newcollection_func(orig);
        replaced = Interlocked.CompareExchange(ref collection, new_collection, orig)==orig;
      } while (!replaced);
    }

    private class ChannelSinkSubscription
      : IDisposable
    {
      public readonly Channel Channel;
      public readonly IChannelSink Sink;
      private bool disposed = false;

      public ChannelSinkSubscription(Channel channel, IChannelSink sink)
      {
        Channel = channel;
        Sink = sink;
      }

      public void Dispose()
      {
        if (!disposed) {
          disposed = true;
          Channel.RemoveOutputStream(Sink);
        }
      }
    }


    /// <summary>
    /// 指定した出力ストリームを出力ストリームリストに追加します
    /// </summary>
    /// <param name="stream">追加する出力ストリーム</param>
    public IDisposable AddOutputStream(IChannelSink stream)
    {
      ReplaceCollection(ref sinks, old => old.Add(stream));
      return new ChannelSinkSubscription(this, stream);
    }

    /// <summary>
    /// 指定した出力ストリームを出力ストリームリストから削除します
    /// </summary>
    /// <param name="stream">削除する出力ストリーム</param>
    public void RemoveOutputStream(IChannelSink stream)
    {
      ReplaceCollection(ref sinks, old => old.Remove(stream));
    }

    public int GetUpstreamRate()
    {
      var connections = sinks
        .Select(s => s.GetConnectionInfo())
        .Where(i => !i.RemoteHostStatus.HasFlag(RemoteHostStatus.Local))
        .Where(i => i.Type.HasFlag(ConnectionType.Direct) || i.Type.HasFlag(ConnectionType.Relay))
        .Count();
      return (ChannelInfo?.Bitrate ?? 0) * connections;
    }

    public int GenerateStreamID()
    {
      return Interlocked.Increment(ref streamID);
    }

    private ChannelInfo channelInfo = new ChannelInfo(new AtomCollection());
    /// <summary>
    /// チャンネル情報を取得および設定します
    /// </summary>
    public ChannelInfo ChannelInfo {
      get {
        return channelInfo;
      }
      set {
        var old = Interlocked.Exchange(ref channelInfo, value);
        if (old!=value) {
          OnChannelInfoChanged(value);
        }
      }
    }

    private ChannelTrack channelTrack = new ChannelTrack(new AtomCollection());
    /// <summary>
    /// トラック情報を取得および設定します
    /// </summary>
    public ChannelTrack ChannelTrack {
      get {
        return channelTrack;
      }
      set {
        var old = Interlocked.Exchange(ref channelTrack, value);
        if (old!=value) {
          OnChannelTrackChanged(value);
        }
      }
    }

    /// <summary>
    /// リレー接続がいっぱいかどうかを取得します
    /// </summary>
    public bool IsRelayFull {
      get { return !IsRelayable(false); }
    }

    private Dictionary<string, DateTimeOffset> banList = new Dictionary<string, DateTimeOffset>();

    public bool HasBanned(string key)
    {
      lock (banList) {
        if (banList.TryGetValue(key, out var until)) {
          if (DateTimeOffset.Now<until) {
            return true;
          }
          else {
            banList.Remove(key);
            return false;
          }
        }
        else {
          return false;
        }
      }
    }

    public void Ban(string key, DateTimeOffset until)
    {
      lock (banList) {
        banList[key] = until;
      }
    }

    public virtual bool IsRelayable(bool local)
    {
      return this.PeerCast.IsChannelRelayable(this, local);
    }

    public bool MakeRelayable(string key, bool local)
    {
      return !HasBanned(key) && MakeRelayable(local);
    }

    public virtual bool MakeRelayable(bool local)
    {
      if (IsRelayable(local)) return true;
      var disconnects =
        sinks
        .Where(os => {
          var info = os.GetConnectionInfo();
          return 
            info.Type.HasFlag(ConnectionType.Relay) &&
            !info.RemoteHostStatus.HasFlag(RemoteHostStatus.Local) &&
            (
              info.RemoteHostStatus.HasFlag(RemoteHostStatus.Firewalled) ||
              (info.RemoteHostStatus.HasFlag(RemoteHostStatus.RelayFull) && (info.LocalRelays ?? 0)<1)
            );
        });
      foreach (var os in disconnects) {
        os.OnStopped(StopReason.UnavailableError);
        RemoveOutputStream(os);
        if (IsRelayable(local)) {
          return true;
        }
      }
      return false;
    }

    /// <summary>
    /// 視聴接続がいっぱいかどうかを取得します
    /// </summary>
    public bool IsDirectFull {
      get { return !IsPlayable(false); }
    }

    public virtual bool IsPlayable(bool local)
    {
      return this.PeerCast.IsChannelPlayable(this, local);
    }

    /// <summary>
    /// チャンネルの連続接続時間を取得します
    /// </summary>
    public TimeSpan Uptime {
      get { return uptimeTimer.Elapsed; }
    }

    /// <summary>
    /// ヘッダコンテントを取得および設定します
    /// </summary>
    public Content? ContentHeader
    {
      get {
        return contentHeader;
      }
      set {
        if (value==null) {
          throw new ArgumentNullException(nameof(value));
        }
        var old = Interlocked.Exchange(ref contentHeader, value);
        if (old!=value) {
          OnContentHeaderChanged(value);
        }
      }
    }

    /// <summary>
    /// ヘッダを除く保持しているコンテントのリストを取得します
    /// </summary>
    public ContentCollection Contents { get { return contents; } }

    private IContentSink[] contentSinks = Array.Empty<IContentSink>();

    private class ContentSinkSubscription
      : IDisposable
    {
      private Channel channel;
      private IContentSink sink;

      public ContentSinkSubscription(Channel channel, IContentSink sink)
      {
        this.channel = channel;
        this.sink = sink;
      }

      public void Dispose()
      {
        channel.RemoveContentSink(sink);
      }
    }

    public IDisposable AddContentSink(IContentSink sink)
    {
      return AddContentSink(sink, -1);
    }

    public IDisposable AddContentSink(IContentSink sink, long requestPos)
    {
      ReplaceCollection(ref contentSinks, orig => orig.Add(sink));
      var header = contentHeader;
      if (header!=null) {
        var channel_info = ChannelInfo;
        if (channel_info!=null) {
          sink.OnChannelInfo(channel_info);
        }
        var channel_track = ChannelTrack;
        if (channel_track!=null) {
          sink.OnChannelTrack(channel_track);
        }
        sink.OnContentHeader(header);
        var contents = Contents.GetFirstContents(header.Stream, header.Timestamp, header.Position);
        foreach (var content in contents) {
          if (header.Position>=requestPos || content.Position>=requestPos) {
            sink.OnContent(content);
          }
        }
      }
      return new ContentSinkSubscription(this, sink);
    }

    public bool RemoveContentSink(IContentSink sink)
    {
      var sinks = contentSinks;
      ReplaceCollection(ref contentSinks, orig => orig.Remove(sink));
      return sinks.Length!=contentSinks.Length;
    }

    private Task lastTask = Task.Delay(0);
    private void DispatchSinkEvent(Action<IContentSink> action)
    {
      var sinks = contentSinks;
      lastTask = lastTask.ContinueWith(prev => {
        sinks.AsParallel().ForAll(action);
      });
    }

    private void DispatchMonitorEvent(Action<IChannelMonitor> action)
    {
      var monitors = this.monitors;
      lastTask = lastTask.ContinueWith(prev => {
        monitors.AsParallel().ForAll(action);
      });
    }

    private void OnChannelInfoChanged(ChannelInfo channel_info)
    {
      DispatchMonitorEvent(mon => mon.OnContentChanged(ChannelContentType.ChannelInfo));
      DispatchSinkEvent(sink => {
        sink.OnChannelInfo(channel_info);
      });
    }

    private void OnChannelTrackChanged(ChannelTrack channel_track)
    {
      DispatchMonitorEvent(mon => mon.OnContentChanged(ChannelContentType.ChannelTrack));
      DispatchSinkEvent(sink => {
        sink.OnChannelTrack(channel_track);
      });
    }

    private void OnContentHeaderChanged(Content header)
    {
      DispatchMonitorEvent(mon => mon.OnContentChanged(ChannelContentType.ContentHeader));
      DispatchSinkEvent(sink => {
        sink.OnContentHeader(header);
      });
    }

    internal void OnContentAdded(Content content)
    {
      DispatchSinkEvent(sink => {
        sink.OnContent(content);
      });
    }

    /// <summary>
    /// 保持している最後のコンテントの次のバイト位置を取得します
    /// </summary>
    public long ContentPosition {
      get {
        var header  = contentHeader;
        var content = contents.Newest;
        if (header==null) {
          return 0;
        }
        else {
          if (content==null || header.Position>content.Position) {
            return header.Position + header.Data.Length;
          }
          else {
            return content.Position + content.Data.Length;
          }
        }
      }
    }

    private void OnClosed(StopReason reason)
    {
      var sinks = contentSinks;
      foreach (var sink in sinks) {
        sink.OnStop(reason);
      }
      DispatchMonitorEvent(mon => mon.OnStopped(reason));
    }

    private class HostComparer
      : IEqualityComparer<Host>
    {
      public bool Equals(Host? x, Host? y)
      {
        if (x==y) return true;
        if (x==null) {
          return y==null;
        }
        if (y==null) {
          return false;
        }
        return x.SessionID.Equals(y.SessionID);
      }

      public int GetHashCode(Host obj)
      {
        if (obj==null) return 0;
        return obj.SessionID.GetHashCode();
      }
    }

    /// <summary>
    /// 接続先として選択できるノードの読み取り専用リストを取得します
    /// </summary>
    public HostsView SourceNodes {
      get { return new HostsView(sourceNodes); }
    }

    public class HostsView
      : IEnumerable<Host>
    {
      private IEnumerable<Host> validNodes;
      internal HostsView(Host[] hosts)
      {
        var cur_time = Environment.TickCount;
        validNodes = hosts.Where(n => cur_time-n.LastUpdated<=NodeLimit);
      }

      public int Count {
        get { return validNodes.Count(); }
      }

      public IEnumerator<Host> GetEnumerator()
      {
        return validNodes.GetEnumerator();
      }

      IEnumerator IEnumerable.GetEnumerator()
      {
        return validNodes.GetEnumerator();
      }
    }

    /// <summary>
    /// このチャンネルに関連付けられたノードの読み取り専用リストを取得します
    /// </summary>
    public HostsView Nodes {
      get { return new HostsView(nodes); }
    }

    public void AddNode(Host host)
    {
      ReplaceCollection(ref nodes, orig => {
        var cur_time = Environment.TickCount;
        return 
          orig
            .Where(n => cur_time-n.LastUpdated<=NodeLimit)
            .Where(n => n.SessionID!=host.SessionID)
            .Concat(Enumerable.Repeat(host, 1))
            .ToArray();
      });
      DispatchMonitorEvent(mon => mon.OnNodeChanged(ChannelNodeAction.Updated, host));
    }

    public void RemoveNode(Host host)
    {
      bool removed = false;
      ReplaceCollection(ref nodes, orig => {
        var new_collection = orig.Where(n => n!=host).ToArray();
        removed = new_collection.Length!=orig.Length;
        return new_collection;
      });
      if (removed) {
        DispatchMonitorEvent(mon => mon.OnNodeChanged(ChannelNodeAction.Removed, host));
      }
    }

    public void AddSourceNode(Host host)
    {
      ReplaceCollection(ref sourceNodes, orig => {
        var cur_time = Environment.TickCount;
        return 
          orig
            .Where(n => cur_time-n.LastUpdated<=NodeLimit)
            .Where(n => n.SessionID!=host.SessionID)
            .Concat(Enumerable.Repeat(host, 1))
            .ToArray();
      });
    }

    public void RemoveSourceNode(Host host)
    {
      ReplaceCollection(ref sourceNodes, orig => {
        return orig.Where(n => n!=host).ToArray();
      });
    }

    public struct HLSSegment {
      public readonly int Index;
      public readonly byte[] Data;
      public readonly double Duration;
      public HLSSegment(int index, byte[] data, double duration)
      {
        Index = index;
        Data = data;
        Duration = duration;
      }
    }

    class ChannelSourceSubscription
      : IDisposable
    {
      public Channel Owner { get; }
      public ISourceStream ChannelSource { get; }
      private bool disposed = false;
      private CancellationTokenSource cancellationTokenSource = new ();
      private Task runningTask = Task.CompletedTask;
      public ChannelSourceSubscription(Channel owner, ISourceStream channelSource)
      {
        Owner = owner;
        ChannelSource = channelSource;
      }

      private async Task Run(ISourceStream channelSource, CancellationToken cancellationToken)
      {
        try {
          var result = await channelSource.Run(cancellationToken).ConfigureAwait(false);
          Owner.OnChannelSourceStopped(this, result);
        }
        catch (Exception) {
          Owner.OnChannelSourceStopped(this, StopReason.NotIdentifiedError);
        }
      }

      public void Start()
      {
        if (disposed) {
          throw new ObjectDisposedException(GetType().Name);
        }
        runningTask = Run(ChannelSource, cancellationTokenSource.Token);
      }

      public void Dispose()
      {
        if (disposed) {
          return;
        }
        cancellationTokenSource.Cancel();
        runningTask.Wait();
        cancellationTokenSource.Dispose();
        disposed = true;
      }
    }

    private void SetChannelSource(ISourceStreamFactory factory, ISourceStream source_stream)
    {
      var old = Interlocked.Exchange(ref sourceStream, new ChannelSourceSubscription(this, source_stream));
      old?.Dispose();
      this.contentHeader = null;
      this.contents.Clear();
      uptimeTimer.Restart();
      sourceStream.Start();
    }

    private void OnChannelSourceStopped(ChannelSourceSubscription channelSourceSubscription, StopReason reason)
    {
      var old = Interlocked.CompareExchange(ref sourceStream, null, channelSourceSubscription);
      if (old!=channelSourceSubscription) return;
      var ostreams = Interlocked.Exchange(ref sinks, Array.Empty<IChannelSink>());
      foreach (var os in ostreams) {
        os.OnStopped(reason);
      }
      uptimeTimer.Stop();
      OnClosed(reason);
    }

    private class MonitorSubscription
      : IDisposable
    {
      private Channel channel;
      private IChannelMonitor monitor;

      public MonitorSubscription(Channel channel, IChannelMonitor monitor)
      {
        this.channel = channel;
        this.monitor = monitor;
      }

      public void Dispose()
      {
        channel.RemoveMonitor(monitor);
      }
    }

    public IDisposable AddMonitor(IChannelMonitor monitor)
    {
      ReplaceCollection(ref monitors, orig => orig.Add(monitor));
      if (contentHeader!=null) {
        if (ChannelInfo!=null) {
          monitor.OnContentChanged(ChannelContentType.ChannelInfo);
        }
        if (ChannelTrack!=null) {
          monitor.OnContentChanged(ChannelContentType.ChannelTrack);
        }
        monitor.OnContentChanged(ChannelContentType.ContentHeader);
      }
      return new MonitorSubscription(this, monitor);
    }

    public void RemoveMonitor(IChannelMonitor monitor)
    {
      ReplaceCollection(ref monitors, orig => orig.Remove(monitor));
    }

    public void Start(ISourceStreamFactory source_stream_factory, Uri source_uri)
    {
      this.sourceKey = new ChannelSourceKey(source_stream_factory, source_uri);
      SetChannelSource(source_stream_factory, CreateSourceStream(source_stream_factory, source_uri));
    }

    protected abstract ISourceStream CreateSourceStream(ISourceStreamFactory source_stream_factory, Uri source_uri);

    public void Reconnect()
    {
      var source = sourceStream?.ChannelSource;
      var status = source?.Status ?? SourceStreamStatus.Idle;
      switch (status) {
      case SourceStreamStatus.Idle:
      case SourceStreamStatus.Error:
        var key = sourceKey;
        if (key!=null) {
          Start(key.SourceStreamFactory, key.Uri);
        }
        break;
      default:
        source!.Reconnect();
        break;
      }
    }

    public void Reconnect(ISourceStreamFactory source_stream_factory, Uri source_uri)
    {
      Start(source_stream_factory, source_uri);
    }

    /// <summary>
    /// 接続されている各ストリームへパケットを送信します
    /// </summary>
    /// <param name="from">送信元のホスト</param>
    /// <param name="packet">送信するデータ</param>
    /// <param name="group">送信先グループ</param>
    public virtual void Broadcast(Host? from, Atom packet, BroadcastGroup group)
    {
      if (group.HasFlag(BroadcastGroup.Trackers) || group.HasFlag(BroadcastGroup.Relays)) {
        var source = sourceStream;
        if (source!=null) {
          source.ChannelSource.Post(from, packet);
        }
      }
      if (group.HasFlag(BroadcastGroup.Relays)) {
        foreach (var os in sinks) {
          os.OnBroadcast(from, packet);
        }
      }
    }

    class ChannelInfoMonitor
      : IChannelMonitor
    {
      Channel Channel { get; }
      TaskCompletionSource<bool> task = new TaskCompletionSource<bool>();

      public ChannelInfoMonitor(Channel channel)
      {
        Channel = channel;
      }

      public async Task WaitForReadyAsync(CancellationToken cancellationToken)
      {
        using (cancellationToken.Register(() => task.TrySetCanceled())) {
          await task.Task.ConfigureAwait(false);
        }
      }

      public void OnContentChanged(ChannelContentType channelContentType)
      {
        if (channelContentType!=ChannelContentType.ChannelInfo) return;
        if (!String.IsNullOrEmpty(Channel.ChannelInfo?.ContentType)) {
          task.TrySetResult(true);
        }
      }

      public void OnNodeChanged(ChannelNodeAction action, Host node)
      {
      }

      public void OnStopped(StopReason reason)
      {
      }
    }

    public async Task WaitForReadyContentTypeAsync(CancellationToken cancel_token)
    {
      var monitor = new ChannelInfoMonitor(this);
      using (AddMonitor(monitor)) {
        if (!String.IsNullOrEmpty(ChannelInfo?.ContentType)) return;
        await monitor.WaitForReadyAsync(cancel_token).ConfigureAwait(false);
      }
    }

    public Task WaitForReadyContentTypeAsync()
    {
      return WaitForReadyContentTypeAsync(CancellationToken.None);
    }

    /// <summary>
    /// チャンネル接続を終了します。ソースストリームと接続している出力ストリームを全て閉じます
    /// </summary>
    public void Close()
    {
      var source = sourceStream;
      if (source!=null) {
        source.Dispose();
      }
      var ostreams = Interlocked.Exchange(ref sinks, Array.Empty<IChannelSink>());
      foreach (var os in ostreams) {
        os.OnStopped(StopReason.OffAir);
      }
    }

    public ChannelStatus GetChannelStatus()
    {
      return new ChannelStatus(
        ChannelID,
        Network,
        Status,
        Uptime,
        IsBroadcasting,
        LocalRelays,
        LocalDirects,
        TotalRelays,
        TotalDirects,
        ChannelInfo,
        ChannelTrack,
        Contents.Oldest?.Position,
        Contents.Newest?.Position,
        IsRelayable(false),
        IsPlayable(false)
      );
    }

    /// <summary>
    /// チャンネルIDを指定してチャンネルを初期化します
    /// </summary>
    /// <param name="peercast">所属するPeerCastオブジェクト</param>
    /// <param name="channel_id">チャンネルID</param>
    protected Channel(IPeerCast peercast, NetworkType network, Guid channel_id)
    {
      this.PeerCast    = peercast;
      this.Network     = network;
      this.ChannelID   = channel_id;
      this.contents    = new ContentCollection(this);
    }
  }

}
