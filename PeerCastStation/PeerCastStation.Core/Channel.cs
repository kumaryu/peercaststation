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
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

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
    private List<IOutputStream> outputStreams = new List<IOutputStream>();
    private List<Host> sourceNodes = new List<Host>();
    private List<Host> nodes = new List<Host>();
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
      get { return outputStreams.Count(x => (x.OutputStreamType & OutputStreamType.Relay) != 0); }
    }

    /// <summary>
    /// 自分のノードに直接つながっている視聴接続数を取得します
    /// </summary>
    public int LocalDirects {
      get { return outputStreams.Count(x => (x.OutputStreamType & OutputStreamType.Play) != 0); }
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
    public ReadOnlyCollection<IOutputStream> OutputStreams {
      get { return new ReadOnlyCollection<IOutputStream>(outputStreams); }
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

    /// <summary>
    /// 指定した出力ストリームを出力ストリームリストに追加します
    /// </summary>
    /// <param name="stream">追加する出力ストリーム</param>
    public void AddOutputStream(IOutputStream stream)
    {
      ReplaceCollection(ref outputStreams, orig => {
        var new_collection = new List<IOutputStream>(orig);
        new_collection.Add(stream);
        return new_collection;
      });
      if (OutputStreamsChanged!=null) OutputStreamsChanged(this, new EventArgs());
    }

    /// <summary>
    /// 指定した出力ストリームを出力ストリームリストから削除します
    /// </summary>
    /// <param name="stream">削除する出力ストリーム</param>
    public void RemoveOutputStream(IOutputStream stream)
    {
      bool removed = false;
      ReplaceCollection(ref outputStreams, orig => {
        var new_collection = new List<IOutputStream>(orig);
        removed = new_collection.Remove(stream);
        return new_collection;
      });
      if (removed) {
        if (OutputStreamsChanged!=null) OutputStreamsChanged(this, new EventArgs());
      }
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
    public virtual bool IsRelayFull {
      get { return !this.PeerCast.AccessController.IsChannelRelayable(this); }
    }

    public virtual bool IsRelayable(IOutputStream sink)
    {
      return this.PeerCast.AccessController.IsChannelRelayable(this, sink);
    }

    /// <summary>
    ///  新しいOutputStreamがリレー可能になるようにリレー不可のOutputStreamを切断します
    /// </summary>
    /// <param name="newoutput_stream">新しくリレーしようとするOutputStream</param>
    /// <returns>リレー可能になった場合はtrue、それ以外はfalse</returns>
    public bool MakeRelayable(IOutputStream newoutput_stream)
    {
      if (IsRelayable(newoutput_stream)) return true;
      var disconnects = new List<IOutputStream>();
      foreach (var os in OutputStreams
          .Where(os => os!=newoutput_stream)
          .Where(os => !os.IsLocal)
          .Where(os => (os.OutputStreamType & OutputStreamType.Relay)!=0)) {
        var info = os.GetConnectionInfo();
        var disconnect = false;
        if ((info.RemoteHostStatus & RemoteHostStatus.Firewalled)!=0) disconnect = true;
        if ((info.RemoteHostStatus & RemoteHostStatus.RelayFull)!=0 &&
            (!info.LocalRelays.HasValue || info.LocalRelays.Value<1)) disconnect = true;
        if (disconnect) disconnects.Add(os);
      }
      foreach (var os in disconnects) {
        os.Stop(StopReason.UnavailableError);
        RemoveOutputStream(os);
      }
      return IsRelayable(newoutput_stream);
    }

    /// <summary>
    /// 視聴接続がいっぱいかどうかを取得します
    /// </summary>
    public virtual bool IsDirectFull {
      get { return !this.PeerCast.AccessController.IsChannelPlayable(this); }
    }

    public virtual bool IsPlayable(IOutputStream sink)
    {
      return this.PeerCast.AccessController.IsChannelPlayable(this, sink);
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

    public void AddContentSink(IContentSink sink)
    {
      ReplaceCollection(ref contentSinks, orig => {
        var new_collection = new List<IContentSink>(orig);
        new_collection.Add(sink);
        return new_collection;
      });
      var header = contentHeader;
      if (header!=null) {
        sink.OnContentHeader(header);
        var channel_info = ChannelInfo;
        if (channel_info!=null) {
          sink.OnChannelInfo(channel_info);
        }
        var channel_track = ChannelTrack;
        if (channel_track!=null) {
          sink.OnChannelTrack(channel_track);
        }
        var contents = Contents.GetNewerContents(header.Stream, header.Timestamp, header.Position);
        foreach (var content in contents) {
          sink.OnContent(content);
        }
      }
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

    private void OnContentChanged()
    {
      var header = contentHeader;
      if (header!=null) {
        OnContentHeaderChanged(header);
        var channel_info = ChannelInfo;
        if (channel_info!=null) {
          OnChannelInfoChanged(channel_info);
        }
        var channel_track = ChannelTrack;
        if (channel_track!=null) {
          OnChannelTrackChanged(channel_track);
        }
        var contents = Contents.GetNewerContents(header.Stream, header.Timestamp, header.Position);
        foreach (var content in contents) {
          OnContentAdded(content);
        }
      }
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
    public ReadOnlyCollection<Host> SourceNodes {
      get {
        var cur_time = Environment.TickCount;
        ReplaceCollection(ref sourceNodes, orig => {
          return new List<Host>(orig.Where(n => cur_time-n.LastUpdated<=NodeLimit));
        });
        return new ReadOnlyCollection<Host>(
          sourceNodes.Except(nodes, new HostComparer()).ToArray()
        );
      }
    }

    /// <summary>
    /// このチャンネルに関連付けられたノードの読み取り専用リストを取得します
    /// </summary>
    public ReadOnlyCollection<Host> Nodes
    {
      get {
        var cur_time = Environment.TickCount;
        ReplaceCollection(ref nodes, orig => {
          return new List<Host>(orig.Where(n => cur_time-n.LastUpdated<=NodeLimit));
        });
        return new ReadOnlyCollection<Host>(nodes);
      }
    }

    public event EventHandler NodesChanged;
    public void AddNode(Host host)
    {
      ReplaceCollection(ref nodes, orig => {
        var new_collection = new List<Host>(orig);
        var idx = new_collection.FindIndex(h => h.SessionID==host.SessionID);
        if (idx>=0) {
          new_collection[idx] = host;
        }
        else {
          new_collection.Add(host);
        }
        return new_collection;
      });
      if (NodesChanged!=null) NodesChanged(this, new EventArgs());
    }

    public void RemoveNode(Host host)
    {
      bool removed = false;
      ReplaceCollection(ref nodes, orig => {
        var new_collection = new List<Host>(orig);
        removed = new_collection.Remove(host);
        return new_collection;
      });
      if (removed) {
        if (NodesChanged!=null) NodesChanged(this, new EventArgs());
      }
    }

    public void AddSourceNode(Host host)
    {
      ReplaceCollection(ref sourceNodes, orig => {
        var new_collection = new List<Host>(orig);
        var idx = new_collection.FindIndex(h => h.SessionID==host.SessionID);
        if (idx>=0) {
          new_collection[idx] = host;
        }
        else {
          new_collection.Add(host);
        }
        return new_collection;
      });
    }

    public void RemoveSourceNode(Host host)
    {
      ReplaceCollection(ref sourceNodes, orig => {
        var new_collection = new List<Host>(orig);
        new_collection.Remove(host);
        return new_collection;
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
        host.IsDirectFull   = !this.PeerCast.AccessController.IsChannelPlayable(this);
        host.IsRelayFull    = !this.PeerCast.AccessController.IsChannelRelayable(this);
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

    public interface IHTTPLiveStreaming
    {
      Task<IList<HLSSegment>> GetSegmentsAsync(CancellationToken cancellationToken);
    }

    private IHTTPLiveStreaming hls = null;
    public IHTTPLiveStreaming Hls {
      get { return hls; }
      set {
        if (hls == null) {
          hls = value;
        }
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
      var ostreams = Interlocked.Exchange(ref outputStreams, new List<IOutputStream>());
      foreach (var os in ostreams) {
        os.Stop();
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
      if ((group & (BroadcastGroup.Trackers | BroadcastGroup.Relays))!=0) {
        var source = sourceStream;
        if (source!=null) {
          source.Post(from, packet);
        }
      }
      if ((group & (BroadcastGroup.Relays))!=0) {
        foreach (var outputStream in outputStreams) {
          outputStream.Post(from, packet);
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
      var ostreams = Interlocked.Exchange(ref outputStreams, new List<IOutputStream>());
      foreach (var os in ostreams) {
        os.Stop();
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
