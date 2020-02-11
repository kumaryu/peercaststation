// PeerCastStation, a P2P streaming servent.
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
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections;

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

  /// <summary>
  /// チャンネル接続を管理するクラスです
  /// </summary>
  public abstract class Channel
  {
    protected static Logger logger = new Logger(typeof(Channel));
    private const int NodeLimit = 180000; //ms
    private ISourceStream sourceStream = null;
    private ImmutableList<IChannelSink> sinks = ImmutableList<IChannelSink>.Empty;
    private Host[] sourceNodes = new Host[0];
    private Host[] nodes = new Host[0];
    private Content contentHeader = null;
    private ContentCollection contents;
    private System.Diagnostics.Stopwatch uptimeTimer = new System.Diagnostics.Stopwatch();
    private int streamID = 0;

    /// <summary>
    /// 所属するPeerCastオブジェクトを取得します
    /// </summary>
    public PeerCast PeerCast { get; private set; }
    /// <summary>
    /// チャンネルの状態を取得します
    /// </summary>
    public virtual SourceStreamStatus Status {
      get {
        var source = sourceStream;
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
    public Uri  SourceUri   { get; private set; }
    public abstract bool IsBroadcasting { get; }

    /// <summary>
    /// ソースストリームを取得します
    /// </summary>
    public ISourceStream SourceStream {
      get {
        return sourceStream;
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
      get {
        return sinks
          .Select(x => x.GetConnectionInfo())
          .Sum(x => (x.Type.HasFlag(ConnectionType.Relay) ? 1 : 0) + (x.LocalRelays ?? 0));
      }
    }

    /// <summary>
    /// 保持している全てのノードと自分ノードの視聴数合計を取得します
    /// </summary>
    public int TotalDirects {
      get {
        return sinks
          .Select(x => x.GetConnectionInfo())
          .Sum(x => (x.Type.HasFlag(ConnectionType.Direct) ? 1 : 0) + (x.LocalDirects ?? 0));
      }
    }

    /// <summary>
    /// 出力ストリームの読み取り専用リストを取得します
    /// </summary>
    public IReadOnlyCollection<IChannelSink> OutputStreams {
      get { return sinks; }
    }

    public event EventHandler OutputStreamsChanged;

    private void ReplaceCollection<T>(ref T collection, Func<T,T> newcollection_func) where T : class
    {
      bool replaced = false;
      do {
        var orig = collection;
        var new_collection = newcollection_func(orig);
        replaced = Object.ReferenceEquals(System.Threading.Interlocked.CompareExchange(ref collection, new_collection, orig), orig);
      } while (!replaced);
    }


    private class ChannelSinkSubscription
      : IDisposable
    {
      public Channel Channel;
      public IChannelSink Sink;

      public void Dispose()
      {
        Channel.RemoveOutputStream(Sink);
      }
    }


    /// <summary>
    /// 指定した出力ストリームを出力ストリームリストに追加します
    /// </summary>
    /// <param name="stream">追加する出力ストリーム</param>
    public IDisposable AddOutputStream(IChannelSink stream)
    {
      ReplaceCollection(ref sinks, old => old.Add(stream));
      OutputStreamsChanged?.Invoke(this, new EventArgs());
      return new ChannelSinkSubscription { Channel=this, Sink=stream };
    }

    /// <summary>
    /// 指定した出力ストリームを出力ストリームリストから削除します
    /// </summary>
    /// <param name="stream">削除する出力ストリーム</param>
    public void RemoveOutputStream(IChannelSink stream)
    {
      ReplaceCollection(ref sinks, old => old.Remove(stream));
      OutputStreamsChanged?.Invoke(this, new EventArgs());
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

    public class ChannelInfoEventArgs
      : EventArgs
    {
      public ChannelInfo ChannelInfo { get; private set; }
      public ChannelInfoEventArgs(ChannelInfo channel_info)
      {
        this.ChannelInfo = channel_info;
      }
    }

    public event EventHandler<ChannelInfoEventArgs> ChannelInfoChanged;
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

    public class ChannelTrackEventArgs
      : EventArgs
    {
      public ChannelTrack ChannelTrack { get; private set; }
      public ChannelTrackEventArgs(ChannelTrack channel_track)
      {
        this.ChannelTrack = channel_track;
      }
    }

    public event EventHandler<ChannelTrackEventArgs> ChannelTrackChanged;
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

    public virtual bool IsRelayable(bool local)
    {
      return this.PeerCast.AccessController.IsChannelRelayable(this, local);
    }

    public virtual bool MakeRelayable(bool local)
    {
      if (IsRelayable(local)) return true;
      var disconnects = new List<IChannelSink>();
      foreach (var os in OutputStreams) {
        var info = os.GetConnectionInfo();
        if (!info.Type.HasFlag(ConnectionType.Relay)) continue;
        if (info.RemoteHostStatus.HasFlag(RemoteHostStatus.Local)) continue;
        var disconnect = false;
        if (info.RemoteHostStatus.HasFlag(RemoteHostStatus.Firewalled)) disconnect = true;
        if (info.RemoteHostStatus.HasFlag(RemoteHostStatus.RelayFull) &&
            (info.LocalRelays ?? 0)<1) disconnect = true;
        if (disconnect) disconnects.Add(os);
      }
      foreach (var os in disconnects) {
        os.OnStopped(StopReason.UnavailableError);
        RemoveOutputStream(os);
      }
      return IsRelayable(local);
    }

    /// <summary>
    /// 視聴接続がいっぱいかどうかを取得します
    /// </summary>
    public bool IsDirectFull {
      get { return !IsPlayable(false); }
    }

    public virtual bool IsPlayable(bool local)
    {
      return this.PeerCast.AccessController.IsChannelPlayable(this, local);
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
    public Content ContentHeader
    {
      get {
        return contentHeader;
      }
      set {
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

    private List<IContentSink> contentSinks = new List<IContentSink>();

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
      ReplaceCollection(ref contentSinks, orig => {
        var new_collection = new List<IContentSink>(orig);
        new_collection.Add(sink);
        return new_collection;
      });
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
      bool removed = false;
      ReplaceCollection(ref contentSinks, orig => {
        var new_collection = new List<IContentSink>(orig);
        removed = new_collection.Remove(sink);
        return new_collection;
      });
      return removed;
    }

    private Task lastTask = Task.Delay(0);
    private void DispatchSinkEvent(Action<IContentSink> action)
    {
      var sinks = contentSinks;
      lastTask = lastTask.ContinueWith(prev => {
        sinks.AsParallel().ForAll(action);
      });
    }

    private void OnChannelInfoChanged(ChannelInfo channel_info)
    {
      DispatchSinkEvent(sink => {
        sink.OnChannelInfo(channel_info);
      });
    }

    private void OnChannelTrackChanged(ChannelTrack channel_track)
    {
      DispatchSinkEvent(sink => {
        sink.OnChannelTrack(channel_track);
      });
    }

    private void OnContentHeaderChanged(Content header)
    {
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

    class ChannelEventInvoker
      : IContentSink
    {
      private Channel owner;
      public ChannelEventInvoker(Channel owner)
      {
        this.owner = owner;
      }

      public void OnChannelInfo(ChannelInfo channel_info)
      {
        owner.ChannelInfoChanged?.Invoke(owner, new ChannelInfoEventArgs(channel_info));
      }

      public void OnChannelTrack(ChannelTrack channel_track)
      {
        owner.ChannelTrackChanged?.Invoke(owner, new ChannelTrackEventArgs(channel_track));
      }

      public void OnContent(Content content)
      {
        owner.ContentChanged?.Invoke(owner, new EventArgs());
      }

      public void OnContentHeader(Content content_header)
      {
        owner.ContentChanged?.Invoke(owner, new EventArgs());
      }

      public void OnStop(StopReason reason)
      {
        owner.Closed?.Invoke(owner, new StreamStoppedEventArgs(reason));
      }
    }

    /// <summary>
    /// コンテントが追加および削除された時に発生するイベントです
    /// </summary>
    public event EventHandler ContentChanged;

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

    /// <summary>
    /// チャンネル接続が終了する時に発生するイベントです
    /// </summary>
    public event StreamStoppedEventHandler Closed;
    private void OnClosed(StopReason reason)
    {
      var sinks = contentSinks;
      foreach (var sink in sinks) {
        sink.OnStop(reason);
      }
    }

    private class HostComparer
      : IEqualityComparer<Host>
    {
      public bool Equals(Host x, Host y)
      {
        if (x==y) return true;
        if (x!=null && y==null) return false;
        if (x==null && y!=null) return false;
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

    public event EventHandler NodesChanged;
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
      if (NodesChanged!=null) NodesChanged(this, new EventArgs());
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
        if (NodesChanged!=null) NodesChanged(this, new EventArgs());
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

    public Host SelfNode {
      get {
        var source = this.SourceStream;
        var host = new HostBuilder();
        host.SessionID      = this.PeerCast.SessionID;
        host.LocalEndPoint  = this.PeerCast.GetLocalEndPoint(this.NetworkAddressFamily, OutputStreamType.Relay);
        host.GlobalEndPoint = this.PeerCast.GetGlobalEndPoint(this.NetworkAddressFamily, OutputStreamType.Relay);
        host.IsFirewalled   = this.PeerCast.GetPortStatus(this.NetworkAddressFamily)!=PortStatus.Open;
        host.DirectCount    = this.LocalDirects;
        host.RelayCount     = this.LocalRelays;
        host.IsDirectFull   = this.IsDirectFull;
        host.IsRelayFull    = this.IsRelayFull;
        host.IsReceiving    = source!=null && (source.GetConnectionInfo().RecvRate ?? 0.0f)>0;
        return host.ToHost();
      }
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

    protected void AddSourceStream(ISourceStream source_stream)
    {
      var old = Interlocked.Exchange(ref sourceStream, source_stream);
      if (old==null) {
        this.contentHeader = null;
        this.contents.Clear();
        uptimeTimer.Restart();
      }
      else {
        old.Dispose();
      }
      sourceStream.Run().ContinueWith(prev => {
        RemoveSourceStream(source_stream, prev.IsFaulted ? StopReason.NotIdentifiedError : prev.Result);
      });
    }

    protected void RemoveSourceStream(ISourceStream source_stream, StopReason reason)
    {
      var old = Interlocked.CompareExchange(ref sourceStream, null, source_stream);
      if (old!=source_stream) return;
      old.Dispose();
      var ostreams = Interlocked.Exchange(ref sinks, ImmutableList<IChannelSink>.Empty);
      foreach (var os in ostreams) {
        os.OnStopped(reason);
      }
      uptimeTimer.Stop();
      OnClosed(reason);
    }

    public void Start(Uri source_uri)
    {
      this.SourceUri = source_uri;
      AddSourceStream(CreateSourceStream(source_uri));
    }

    protected abstract ISourceStream CreateSourceStream(Uri source_uri);

    public void Reconnect()
    {
      var source = sourceStream;
      var status = source?.Status ?? SourceStreamStatus.Idle;
      switch (status) {
      case SourceStreamStatus.Idle:
      case SourceStreamStatus.Error:
        var source_uri = this.SourceUri;
        if (source_uri!=null) Start(source_uri);
        break;
      default:
        source.Reconnect();
        break;
      }
    }

    public void Reconnect(Uri source_uri)
    {
      var uri = source_uri ?? this.SourceUri;
      if (uri!=null) {
        Start(uri);
      }
    }

    /// <summary>
    /// 接続されている各ストリームへパケットを送信します
    /// </summary>
    /// <param name="from">送信元のホスト</param>
    /// <param name="packet">送信するデータ</param>
    /// <param name="group">送信先グループ</param>
    public virtual void Broadcast(Host from, Atom packet, BroadcastGroup group)
    {
      if (group.HasFlag(BroadcastGroup.Trackers) || group.HasFlag(BroadcastGroup.Relays)) {
        var source = sourceStream;
        if (source!=null) {
          source.Post(from, packet);
        }
      }
      if (group.HasFlag(BroadcastGroup.Relays)) {
        foreach (var os in sinks) {
          os.OnBroadcast(from, packet);
        }
      }
    }

    public async Task WaitForReadyContentTypeAsync(CancellationToken cancel_token)
    {
      var task = new TaskCompletionSource<bool>();
      using (cancel_token.Register(() => task.TrySetCanceled(), false)) {
        var channel_info_changed = new EventHandler<ChannelInfoEventArgs>((sender, e) => {
          if (e.ChannelInfo!=null && !String.IsNullOrEmpty(e.ChannelInfo.ContentType)) {
            task.TrySetResult(true);
          }
        });
        try {
          this.ChannelInfoChanged += channel_info_changed;
          var channel_info = this.ChannelInfo;
          if (channel_info!=null && !String.IsNullOrEmpty(channel_info.ContentType)) return;
          await task.Task.ConfigureAwait(false);
        }
        finally {
          this.ChannelInfoChanged -= channel_info_changed;
        }
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
      var ostreams = Interlocked.Exchange(ref sinks, ImmutableList<IChannelSink>.Empty);
      foreach (var os in ostreams) {
        os.OnStopped(StopReason.OffAir);
      }
    }

    /// <summary>
    /// チャンネルIDを指定してチャンネルを初期化します
    /// </summary>
    /// <param name="peercast">所属するPeerCastオブジェクト</param>
    /// <param name="channel_id">チャンネルID</param>
    protected Channel(PeerCast peercast, NetworkType network, Guid channel_id)
    {
      this.PeerCast    = peercast;
      this.Network     = network;
      this.ChannelID   = channel_id;
      this.contents    = new ContentCollection(this);
      this.contentSinks.Add(new ChannelEventInvoker(this));
    }
  }

}
