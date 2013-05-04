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
  /// チャンネルのメタデータを保持するクラスです
  /// </summary>
  public class ChannelInfo
  {
    /// <summary>
    /// チャンネル名を取得します
    /// </summary>
    public string Name {
      get {
        return extra.GetChanInfoName();
      }
    }

    /// <summary>
    /// チャンネルストリームの内容種類を取得します
    /// </summary>
    public string ContentType {
      get {
        return extra.GetChanInfoType();
      }
    }

    /// <summary>
    /// ジャンルを取得します
    /// </summary>
    public string Genre {
      get {
        return extra.GetChanInfoGenre();
      }
    }

    /// <summary>
    /// チャンネル詳細を取得します
    /// </summary>
    public string Desc {
      get {
        return extra.GetChanInfoDesc();
      }
    }

    /// <summary>
    /// 配信コメントを取得します
    /// </summary>
    public string Comment {
      get {
        return extra.GetChanInfoComment();
      }
    }

    /// <summary>
    /// コンタクトURLを取得します
    /// </summary>
    public string URL {
      get {
        return extra.GetChanInfoURL();
      }
    }

    /// <summary>
    /// 配信ビットレート情報を取得します
    /// </summary>
    public int Bitrate
    {
      get
      {
        return extra.GetChanInfoBitrate() ?? 0;
      }
    }

    /// <summary>
    /// ストリームのMIME Typeを取得します。
    /// </summary>
    public string MIMEType {
      get {
        var stream_type = extra.GetChanInfoStreamType();
        if (!String.IsNullOrEmpty(stream_type)) {
          return stream_type;
        }
        else {
          switch (ContentType) {
          case "MP3": return "audio/mpeg";
          case "OGG": return "audio/ogg";
          case "OGM": return "video/ogg";
          case "RAW": return "application/octet-stream";
          case "NSV": return "video/nsv";
          case "WMA": return "audio/x-ms-wma";
          case "WMV": return "video/x-ms-wmv";
          case "PLS": return "audio/mpegurl";
          case "M3U": return "audio/m3u";
          case "ASX": return "video/x-ms-asf";
          default: return "application/octet-stream";
          }
        }
      }
    }

    /// <summary>
    /// ストリームファイルの拡張子を取得します
    /// </summary>
    public string ContentExtension
    {
      get {
        var stream_ext = extra.GetChanInfoStreamExt();
        if (!String.IsNullOrEmpty(stream_ext)) {
          return stream_ext;
        }
        else {
          switch (ContentType) {
          case "MP3": return ".mp3";
          case "OGG": return ".ogg";
          case "OGM": return ".ogv";
          case "RAW": return "";
          case "NSV": return ".nsv";
          case "WMA": return ".wma";
          case "WMV": return ".wmv";
          case "PLS": return ".pls";
          case "M3U": return ".m3u";
          case "ASX": return ".asx";
          default: return "";
          }
        }
      }
    }

    private ReadOnlyAtomCollection extra;
    /// <summary>
    /// その他のチャンネル情報を保持するリストを取得します
    /// </summary>
    public IAtomCollection Extra { get { return extra; } }

    /// <summary>
    /// チャンネル情報を保持するAtomCollectionから新しいチャンネル情報を初期化します
    /// </summary>
    /// <param name="chaninfo">チャンネル情報を保持するAtomCollection</param>
    public ChannelInfo(IAtomCollection chaninfo)
    {
      extra = new ReadOnlyAtomCollection(new AtomCollection(chaninfo));
    }
  }

  /// <summary>
  /// チャンネルのトラック情報を保持するクラスです
  /// </summary>
  public class ChannelTrack
  {
    /// <summary>
    /// タイトルを取得します
    /// </summary>
    public string Name {
      get {
        return extra.GetChanTrackTitle();
      }
    }

    /// <summary>
    /// アルバム名を取得します
    /// </summary>
    public string Album {
      get {
        return extra.GetChanTrackAlbum();
      }
    }

    /// <summary>
    /// ジャンルを取得します
    /// </summary>
    public string Genre {
      get {
        return extra.GetChanTrackGenre();
      }
    }

    /// <summary>
    /// 作者名を取得します
    /// </summary>
    public string Creator {
      get {
        return extra.GetChanTrackCreator();
      }
    }

    /// <summary>
    /// トラック情報に関するURLを取得します
    /// </summary>
    public string URL {
      get {
        return extra.GetChanTrackURL();
      }
    }

    private ReadOnlyAtomCollection extra;
    /// <summary>
    /// その他のトラック情報を保持するリストを取得します
    /// </summary>
    public IAtomCollection Extra { get { return extra; } }

    /// <summary>
    /// トラック情報を保持するAtomCollectionから新しいトラック情報を初期化します
    /// </summary>
    /// <param name="chantrack">トラック情報を保持するAtomCollection</param>
    public ChannelTrack(IAtomCollection chantrack)
    {
      extra = new ReadOnlyAtomCollection(new AtomCollection(chantrack));
    }
  }

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
  /// 出力ストリームを保持するコレクションクラスです
  /// </summary>
  [Serializable]
  public class OutputStreamCollection : Collection<IOutputStream>
  {
    /// <summary>
    /// 空のOutputStreamCollectionを初期化します
    /// </summary>
    public OutputStreamCollection()
      : base()
    {
    }

    /// <summary>
    /// 指定したリストから要素をコピーしてOutputStreamCollectionを初期化します
    /// </summary>
    /// <param name="list">コピー元のリスト</param>
    public OutputStreamCollection(IEnumerable<IOutputStream> list)
      : base(new List<IOutputStream>(list))
    {
    }
  }

  /// <summary>
  /// 出力ストリームを保持するコレクションの読み取り専用ラッパクラスです
  /// </summary>
  [Serializable]
  public class ReadOnlyOutputStreamCollection : ReadOnlyCollection<IOutputStream>
  {
    /// <summary>
    /// 元になるコレクションを指定してReadOnlyOutputStreamCollectionを初期化します
    /// </summary>
    /// <param name="collection">元になるコレクション</param>
    public ReadOnlyOutputStreamCollection(OutputStreamCollection collection)
      : base(collection)
    {
    }
  }

  /// <summary>
  /// チャンネル接続を管理するクラスです
  /// </summary>
  public class Channel
  {
    private const int NodeLimit = 180000; //ms
    private ISourceStream sourceStream = null;
    private OutputStreamCollection outputStreams = new OutputStreamCollection();
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
    /// ソースストリームを取得および設定します
    /// </summary>
    public ISourceStream SourceStream
    {
      get { return sourceStream; }
      set
      {
        if (sourceStream != value) {
          sourceStream = value;
        }
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
    public ReadOnlyOutputStreamCollection OutputStreams {
      get { return new ReadOnlyOutputStreamCollection(outputStreams); }
    }

    public event EventHandler OutputStreamsChanged;
    /// <summary>
    /// 指定した出力ストリームを出力ストリームリストに追加します
    /// </summary>
    /// <param name="stream">追加する出力ストリーム</param>
    public void AddOutputStream(IOutputStream stream)
    {
      Utils.ReplaceCollection(ref outputStreams, orig => {
        var new_collection = new OutputStreamCollection(outputStreams);
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
        var new_collection = new OutputStreamCollection(outputStreams);
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
        os.Stop();
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
    public event EventHandler Closed;
    private void OnClosed()
    {
      if (Closed != null) {
        Closed(this, new EventArgs());
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

    public event EventHandler StatusChanged;
    private void SourceStream_StatusChanged(object sender, SourceStreamStatusChangedEventArgs args)
    {
      if (StatusChanged!=null) StatusChanged(this, new EventArgs());
    }

    private void SourceStream_Stopped(object sender, EventArgs args)
    {
      foreach (var os in outputStreams) {
        os.Stop();
      }
      outputStreams = new OutputStreamCollection();
      startTickCount = null;
      IsClosed = true;
      OnClosed();
    }

    public void Start(ISourceStream source_stream)
    {
      IsClosed = false;
      if (sourceStream!=null) {
        sourceStream.StatusChanged -= SourceStream_StatusChanged;
        sourceStream.Stopped -= SourceStream_Stopped;
      }
      sourceStream = source_stream;
      sourceStream.StatusChanged += SourceStream_StatusChanged;
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
        outputStreams = new OutputStreamCollection();
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
