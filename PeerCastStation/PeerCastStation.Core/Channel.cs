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

  /// <summary>
  /// チャンネル接続を管理するクラスです
  /// </summary>
  public class Channel
  {
    private const int NodeLimit = 180000; //ms
    private ISourceStream sourceStream = null;
    private List<IOutputStream> outputStreams = new List<IOutputStream>();
    private List<Host> nodes = new List<Host>();
    private Content contentHeader = null;
    private ContentCollection contents = new ContentCollection();
    private int? startTickCount = null;
    /// <summary>
    /// 所属するPeerCastオブジェクトを取得します
    /// </summary>
    public PeerCast PeerCast { get; private set; }
    /// <summary>
    /// チャンネルの状態を取得します
    /// </summary>
    public virtual SourceStreamStatus Status
    {
      get {
        if (sourceStream!=null) {
          return sourceStream.Status;
        }
        else {
          return SourceStreamStatus.Idle;
        }
      }
    }
    /// <summary>
    /// チャンネルが閉じられたかどうかを取得します
    /// </summary>
    public bool IsClosed { get; private set; }
    public Guid ChannelID { get; private set; }
    public Guid BroadcastID { get; private set; }

    /// <summary>
    /// コンテント取得元のUriを取得します
    /// </summary>
    public Uri SourceUri { get; private set; }

    /// <summary>
    /// ソースストリームを取得します
    /// </summary>
    public ISourceStream SourceStream
    {
      get { return sourceStream; }
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
    /// <summary>
    /// 指定した出力ストリームを出力ストリームリストに追加します
    /// </summary>
    /// <param name="stream">追加する出力ストリーム</param>
    public void AddOutputStream(IOutputStream stream)
    {
      Utils.ReplaceCollection(ref outputStreams, orig => {
        var new_collection = new List<IOutputStream>(outputStreams);
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
      Utils.ReplaceCollection(ref outputStreams, orig => {
        var new_collection = new List<IOutputStream>(outputStreams);
        removed = new_collection.Remove(stream);
        return new_collection;
      });
      if (removed) {
        if (OutputStreamsChanged!=null) OutputStreamsChanged(this, new EventArgs());
      }
    }

    public event EventHandler ChannelInfoChanged;
    private ChannelInfo channelInfo = new ChannelInfo(new AtomCollection());
    /// <summary>
    /// チャンネル情報を取得および設定します
    /// </summary>
    public ChannelInfo ChannelInfo {
      get {
        return channelInfo;
      }
      set {
        if (channelInfo!=value) {
          channelInfo = value;
          if (ChannelInfoChanged!=null) ChannelInfoChanged(this, new EventArgs());
        }
      }
    }

    public event EventHandler ChannelTrackChanged;
    private ChannelTrack channelTrack = new ChannelTrack(new AtomCollection());
    /// <summary>
    /// トラック情報を取得および設定します
    /// </summary>
    public ChannelTrack ChannelTrack {
      get {
        return channelTrack;
      }
      set {
        if (channelTrack!=value) {
          channelTrack = value;
          if (ChannelTrackChanged!=null) ChannelTrackChanged(this, new EventArgs());
        }
      }
    }

    /// <summary>
    /// リレー接続がいっぱいかどうかを取得します
    /// </summary>
    public virtual bool IsRelayFull
    {
      get
      {
        return !this.PeerCast.AccessController.IsChannelRelayable(this);
      }
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
    public virtual bool IsDirectFull
    {
      get
      {
        return !this.PeerCast.AccessController.IsChannelPlayable(this);
      }
    }

    /// <summary>
    /// チャンネルの連続接続時間を取得します
    /// </summary>
    public TimeSpan Uptime
    {
      get
      {
        if (startTickCount!=null) {
          return new TimeSpan((Environment.TickCount-startTickCount.Value)*10000L);
        }
        else {
          return TimeSpan.Zero;
        }
      }
    }

    /// <summary>
    /// ヘッダコンテントを取得および設定します
    /// </summary>
    public Content ContentHeader
    {
      get { return contentHeader; }
      set
      {
        if (contentHeader != value) {
          contentHeader = value;
          OnContentChanged();
        }
      }
    }

    /// <summary>
    /// ヘッダを除く保持しているコンテントのリストを取得します
    /// </summary>
    public ContentCollection Contents { get { return contents; } }

    private void OnContentChanged()
    {
      if (ContentChanged != null) {
        ContentChanged(this, new EventArgs());
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
        var content = contents.Newest;
        if (contentHeader==null) {
          return 0;
        }
        else if (content==null || contentHeader.Position>content.Position) {
          return contentHeader.Position + contentHeader.Data.Length;
        }
        else {
          return content.Position + content.Data.Length;
        }
      }
    }

    /// <summary>
    /// チャンネル接続が終了する時に発生するイベントです
    /// </summary>
    public event StreamStoppedEventHandler Closed;
    private void OnClosed(StopReason reason)
    {
      if (Closed != null) {
        Closed(this, new StreamStoppedEventArgs(reason));
      }
    }

    /// <summary>
    /// このチャンネルに関連付けられたノードの読み取り専用リストを取得します
    /// </summary>
    public ReadOnlyCollection<Host> Nodes
    {
      get {
        var cur_time = Environment.TickCount;
        Utils.ReplaceCollection(ref nodes, orig => {
          return new List<Host>(orig.Where(n => cur_time-n.LastUpdated<=NodeLimit));
        });
        return new ReadOnlyCollection<Host>(nodes);
      }
    }

    public event EventHandler NodesChanged;
    public void AddNode(Host host)
    {
      Utils.ReplaceCollection(ref nodes, orig => {
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
      Utils.ReplaceCollection(ref nodes, orig => {
        var new_collection = new List<Host>(orig);
        removed = new_collection.Remove(host);
        return new_collection;
      });
      if (removed) {
        if (NodesChanged!=null) NodesChanged(this, new EventArgs());
      }
    }

    public Host SelfNode {
      get {
        var host = new HostBuilder();
        host.SessionID      = this.PeerCast.SessionID;
        host.LocalEndPoint  = this.PeerCast.GetLocalEndPoint(AddressFamily.InterNetwork, OutputStreamType.Relay);
        host.GlobalEndPoint = this.PeerCast.GetGlobalEndPoint(AddressFamily.InterNetwork, OutputStreamType.Relay);
        host.IsFirewalled   = this.PeerCast.IsFirewalled ?? true;
        host.DirectCount    = this.LocalDirects;
        host.RelayCount     = this.LocalRelays;
        host.IsDirectFull   = !this.PeerCast.AccessController.IsChannelPlayable(this);
        host.IsRelayFull    = !this.PeerCast.AccessController.IsChannelRelayable(this);
        host.IsReceiving    = true;
        return host.ToHost();
      }
    }

    private void SourceStream_Stopped(object sender, StreamStoppedEventArgs args)
    {
      foreach (var os in outputStreams) {
        os.Stop();
      }
      outputStreams = new List<IOutputStream>();
      startTickCount = null;
      IsClosed = true;
      OnClosed(args.StopReason);
    }

    public void Start(ISourceStream source_stream)
    {
      IsClosed = false;
      if (sourceStream!=null) {
        sourceStream.Stopped -= SourceStream_Stopped;
      }
      sourceStream = source_stream;
      sourceStream.Stopped += SourceStream_Stopped;
      startTickCount = Environment.TickCount;
      sourceStream.Start();
    }

    public void Reconnect()
    {
      if (sourceStream!=null) {
        sourceStream.Reconnect();
      }
    }

    public void Reconnect(Uri source_uri)
    {
      if (sourceStream!=null) {
        sourceStream.Reconnect(source_uri);
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
        if (sourceStream!=null) {
          sourceStream.Post(from, packet);
        }
      }
      if ((group & (BroadcastGroup.Relays))!=0) {
        foreach (var outputStream in outputStreams) {
          outputStream.Post(from, packet);
        }
      }
    }

    /// <summary>
    /// チャンネル接続を終了します。ソースストリームと接続している出力ストリームを全て閉じます
    /// </summary>
    public void Close()
    {
      if (!IsClosed) {
        if (sourceStream!=null) {
          sourceStream.Stop();
        }
        foreach (var outputStream in outputStreams) {
          outputStream.Stop();
        }
        outputStreams = new List<IOutputStream>();
      }
    }

    /// <summary>
    /// チャンネルIDとソースストリームを指定してチャンネルを初期化します
    /// </summary>
    /// <param name="peercast">所属するPeerCastオブジェクト</param>
    /// <param name="channel_id">チャンネルID</param>
    /// <param name="source_uri">ソースURI</param>
    public Channel(PeerCast peercast, Guid channel_id, Uri source_uri)
      : this(peercast, channel_id, Guid.Empty, source_uri)
    {
    }

    /// <summary>
    /// チャンネルIDとブロードキャストID、ソースストリームを指定してチャンネルを初期化します
    /// </summary>
    /// <param name="peercast">所属するPeerCastオブジェクト</param>
    /// <param name="channel_id">チャンネルID</param>
    /// <param name="broadcast_id">ブロードキャストID</param>
    /// <param name="source_uri">ソースURI</param>
    public Channel(PeerCast peercast, Guid channel_id, Guid broadcast_id, Uri source_uri)
    {
      this.IsClosed = true;
      this.PeerCast = peercast;
      this.SourceUri = source_uri;
      this.ChannelID = channel_id;
      this.BroadcastID = broadcast_id;
      contents.ContentChanged += (sender, e) => {
        OnContentChanged();
      };
    }
  }
}
