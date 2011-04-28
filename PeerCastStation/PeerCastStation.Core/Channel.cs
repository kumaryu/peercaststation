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
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Threading;
using System.Net;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Sockets;

namespace PeerCastStation.Core
{
  /// <summary>
  /// チャンネルのメタデータを保持するクラスです
  /// </summary>
  public class ChannelInfo
    : INotifyPropertyChanged
  {
    private Guid channelID;
    private Uri tracker = null;
    private string name = "";
    private string contentType = "";

    /// <summary>
    /// 接続起点のURIを取得および設定します
    /// </summary>
    public Uri Tracker {
      get { return tracker; }
      set {
        tracker = value;
        OnPropertyChanged("Tracker");
      }
    }
    /// <summary>
    /// チャンネルIDを取得します
    /// </summary>
    public Guid ChannelID {
      get { return channelID; }
    }
    /// <summary>
    /// チャンネル名を取得および設定します
    /// </summary>
    public string Name {
      get { return name; }
      set {
        name = value;
        OnPropertyChanged("Name");
      }
    }
    /// <summary>
    /// チャンネルストリームの内容種類を取得および設定します
    /// </summary>
    public string ContentType {
      get {
        return contentType;
      }
      set {
        contentType = value;
        OnPropertyChanged("ContentType");
      }
    }

    public string MIMEType {
      get {
        switch (contentType) {
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

    public string ContentExtension
    {
      get {
        switch (contentType) {
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

    private AtomCollection extra = new AtomCollection();
    /// <summary>
    /// その他のチャンネル情報を保持するリストを取得します
    /// </summary>
    public AtomCollection Extra { get { return extra; } }
    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged(string name)
    {
      if (PropertyChanged != null) {
        PropertyChanged(this, new PropertyChangedEventArgs(name));
      }
    }

    /// <summary>
    /// チャンネルIDを指定して新しいチャンネル情報を初期化します
    /// </summary>
    /// <param name="channel_id">チャンネルID</param>
    public ChannelInfo(Guid channel_id)
    {
      channelID = channel_id;
      extra.CollectionChanged += (sender, e) => {
        OnPropertyChanged("Extra");
      };
    }
  }

  /// <summary>
  /// Broadcastの送信先を指定します
  /// </summary>
  [Flags]
  public enum BroadcastGroup : byte {
    /// <summary>
    /// SourceStreamを含むストリーム
    /// </summary>
    Trackers = 2,
    /// <summary>
    /// SourceStreamを含まないストリーム
    /// </summary>
    Relays = 4,
  }

  public class ContentCollection
    : System.Collections.Specialized.INotifyCollectionChanged,
      ICollection<Content>
  {
    private SortedList<long, Content> list = new SortedList<long,Content>();
    public long LimitPackets { get; set; }
    public ContentCollection()
    {
      LimitPackets = 160;
    }

    public event NotifyCollectionChangedEventHandler CollectionChanged;

    public int Count { get { return list.Count; } }
    public bool IsReadOnly { get { return false; } }

    public void Add(Content item)
    {
      if (list.ContainsKey(item.Position)) {
        list[item.Position] = item;
      }
      else {
        list.Add(item.Position, item);
      }
      while (list.Count>LimitPackets && list.Count>1) {
        list.RemoveAt(0);
      }
      if (CollectionChanged!=null) {
        CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
      }
    }

    public void Clear()
    {
      list.Clear();
      if (CollectionChanged!=null) {
        CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
      }
    }

    public bool Contains(Content item)
    {
      return list.ContainsValue(item);
    }

    public void CopyTo(Content[] array, int arrayIndex)
    {
      list.Values.CopyTo(array, arrayIndex);
    }

    public bool Remove(Content item)
    {
      if (list.Remove(item.Position)) {
        if (CollectionChanged!=null) {
          CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
        }
        return true;
      }
      else {
        return false;
      }
    }

    public IEnumerator<Content> GetEnumerator()
    {
      return list.Values.GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
      return list.Values.GetEnumerator();
    }

    public Content Oldest {
      get {
        if (list.Count>0) {
          return list.Values[0];
        }
        else {
          return null;
        }
      }
    }

    public Content Newest {
      get {
        if (list.Count>0) {
          return list.Values[list.Count-1];
        }
        else {
          return null;
        }
      }
    }

    private int GetNewerPacketIndex(long position)
    {
      if (list.Count<1) {
        return 0;
      }
      if (list.Keys[0]>position) {
        return 0;
      }
      if (list.Keys[list.Count-1]<=position) {
        return list.Count;
      }
      var min = 0;
      var max = list.Count-1;
      var idx = (max+min)/2;
      while (true) {
        if (list.Keys[idx]==position) {
          return idx+1;
        }
        else if (list.Keys[idx]>position) {
          if (min>=max) {
            return idx;
          }
          max = idx-1;
          idx = (max+min)/2;
        }
        else if (list.Keys[idx]<position) {
          if (min>=max) {
            return idx+1;
          }
          min = idx+1;
          idx = (max+min)/2;
        }
      }
    }

    public IList<Content> GetNewerContents(long position)
    {
      int idx = GetNewerPacketIndex(position);
      var res = new List<Content>(Math.Max(list.Count-idx, 0));
      for (var i=idx; i<list.Count; i++) {
        res.Add(list.Values[i]);
      }
      return res;
    }

    public Content NextOf(long position)
    {
      int idx = GetNewerPacketIndex(position);
      if (idx>=list.Count) return null;
      else return list.Values[idx];
    }

    public Content NextOf(Content item)
    {
      return NextOf(item.Position);
    }
  }

  /// <summary>
  /// 出力ストリームを保持するコレクションクラスです
  /// </summary>
  public class OutputStreamCollection : ObservableCollection<IOutputStream>
  {
    /// <summary>
    /// 視聴再生中の出力ストリーム数の数を取得します
    /// </summary>
    public int CountPlaying
    {
      get
      {
        return this.Count(x => (x.OutputStreamType & OutputStreamType.Play) != 0);
      }
    }

    /// <summary>
    /// リレー中の出力ストリーム数の数を取得します
    /// </summary>
    public int CountRelaying
    {
      get
      {
        return this.Count(x => (x.OutputStreamType & OutputStreamType.Relay) != 0);
      }
    }
  }

  /// <summary>
  /// チャンネル接続を管理するクラスです
  /// </summary>
  public class Channel
    : INotifyPropertyChanged
  {
    private const int NodeLimit = 180000; //ms
    private static Logger logger = new Logger(typeof(Channel));
    private Uri sourceUri = null;
    private Host sourceHost = null;
    private ISourceStream sourceStream = null;
    private OutputStreamCollection outputStreams = new OutputStreamCollection();
    private ObservableCollection<Node> nodes = new ObservableCollection<Node>();
    private ChannelInfo channelInfo;
    private Content contentHeader = null;
    private ContentCollection contents = new ContentCollection();
    private Thread sourceThread = null;
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
    /// <summary>
    /// コンテント取得元のUriを取得します
    /// </summary>
    public Uri SourceUri
    {
      get { return sourceUri; }
    }

    public Host SourceHost
    {
      get { return sourceHost; }
    }

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
          OnPropertyChanged("SourceStream");
        }
      }
    }
    /// <summary>
    /// 出力ストリームのリストを取得します
    /// </summary>
    public OutputStreamCollection OutputStreams { get { return outputStreams; } }
    /// <summary>
    /// このチャンネルに関連付けられたノードリストを取得します
    /// </summary>
    public IList<Node> Nodes {
      get {
        var limit_time = TimeSpan.FromMilliseconds(Environment.TickCount-NodeLimit);
        foreach (var node in nodes.Where(n => n.LastUpdated<limit_time).ToArray()) {
          nodes.Remove(node);
        }
        return nodes;
      }
    }
    /// <summary>
    /// チャンネル情報を取得および設定します
    /// </summary>
    public ChannelInfo ChannelInfo { get { return channelInfo; } }
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
          OnPropertyChanged("ContentHeader");
          OnContentChanged();
        }
      }
    }

    /// <summary>
    /// リレー接続がいっぱいかどうかを取得します
    /// </summary>
    public bool IsRelayFull
    {
      get
      {
        return !this.PeerCast.AccessController.IsChannelRelayable(this);
      }
    }

    /// <summary>
    /// 視聴接続がいっぱいかどうかを取得します
    /// </summary>
    public bool IsDirectFull
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
    /// ヘッダを除く保持しているコンテントのリストを取得します
    /// </summary>
    public ContentCollection Contents { get { return contents; } }
    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged(string name)
    {
      if (PropertyChanged != null) {
        PropertyChanged(this, new PropertyChangedEventArgs(name));
      }
    }
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
    /// チャンネル接続が終了する時に発生するイベントです
    /// </summary>
    public event EventHandler Closed;
    private void OnClosed()
    {
      if (Closed != null) {
        Closed(this, new EventArgs());
      }
    }

    private class IgnoredHostCollection
    {
      private Dictionary<Host, int> ignoredHosts = new Dictionary<Host, int>();
      private int threshold;
      public IgnoredHostCollection(int threshold)
      {
        this.threshold = threshold;
      }

      public void Add(Host host)
      {
        if (host!=null) {
          ignoredHosts[host] = Environment.TickCount;
        }
      }

      public bool Contains(Host host)
      {
        if (ignoredHosts.ContainsKey(host)) {
          int tick = Environment.TickCount;
          if (tick - ignoredHosts[host] <= threshold) {
            return true;
          }
          else {
            ignoredHosts.Remove(host);
            return false;
          }
        }
        else {
          return false;
        }
      }

      public void Clear()
      {
        ignoredHosts.Clear();
      }

      public ICollection<Host> Hosts { get { return ignoredHosts.Keys; } }
    }
    private IgnoredHostCollection ignoredHosts = new IgnoredHostCollection(NodeLimit);
    public ICollection<Host> IgnoredHosts { get { return ignoredHosts.Hosts; } }

    /// <summary>
    /// 指定したホストが接続先として選択されないように指定します。
    /// 一度無視されたホストは一定時間経過した後、再度選択されるようになります
    /// </summary>
    /// <param name="host">接続先として選択されないようにするホスト</param>
    public void IgnoreHost(Host host)
    {
      ignoredHosts.Add(host);
    }

    /// <summary>
    /// 全てのホストを接続先として選択可能にします
    /// </summary>
    public void ClearIgnored()
    {
      ignoredHosts.Clear();
    }

    /// <summary>
    /// 指定した中から次に接続するノードを選択します
    /// </summary>
    /// <param name="node_list">接続先のリスト</param>
    /// <returns>node_listから選んだ接続先。node_listが空の場合はnull</returns>
    private Node SelectSourceNode(List<Node> node_list)
    {
      if (node_list.Count > 0) {
        //TODO: 接続先をちゃんと選ぶ
        int idx = new Random().Next(node_list.Count);
        return node_list[idx];
      }
      else {
        return null;
      }
    }

    /// <summary>
    /// SourceStreamが次に接続しにいくべき場所を選択して返します。
    /// IgnoreHostで無視されているホストは一定時間選択されません
    /// </summary>
    /// <returns>次に接続すべきホスト。無い場合はnull</returns>
    public virtual Host SelectSourceHost()
    {
      var node_list = Nodes.Where(node => !ignoredHosts.Contains(node.Host)).ToList<Node>();
      var res = SelectSourceNode(node_list);
      if (res!=null) {
        return res.Host;
      }
      else if (!ignoredHosts.Contains(sourceHost)) {
        return sourceHost;
      }
      else {
        return null;
      }
    }

    /// <summary>
    /// SourceStreamが次に接続しにいくべき場所を複数選択して返します。
    /// IgnoreHostで無視されているホストは一定時間選択されません
    /// </summary>
    /// <returns>次に接続すべきホスト。最大8箇所。無い場合は空の配列</returns>
    public virtual Node[] SelectSourceNodes()
    {
      var node_list = Nodes.Where(node => !ignoredHosts.Contains(node.Host)).ToList<Node>();
      var res = new List<Node>();
      for (var i=0; i<8; i++) {
        var node = SelectSourceNode(node_list);
        if (node!=null) {
          res.Add(node);
          node_list.Remove(node);
        }
        else {
          break;
        }
      }
      return res.ToArray();
    }

    private void SourceStream_StatusChanged(object sender, SourceStreamStatusChangedEventArgs args)
    {
      OnPropertyChanged("Status");
    }

    public void Start(ISourceStream source_stream)
    {
      IsClosed = false;
      if (sourceStream!=null) sourceStream.StatusChanged -= SourceStream_StatusChanged;
      sourceStream = source_stream;
      sourceStream.StatusChanged += SourceStream_StatusChanged;
      var sync = SynchronizationContext.Current ?? new SynchronizationContext();
      sourceThread = new Thread(SourceThreadFunc);
      sourceThread.Name = String.Format("SourceThread:{0}", channelInfo.ChannelID.ToString("N"));
      sourceThread.Start(sync);
      startTickCount = Environment.TickCount;
    }

    public void Reconnect()
    {
      if (sourceStream!=null) {
        sourceStream.Reconnect();
      }
    }

    private void SourceThreadFunc(object arg)
    {
      logger.Debug("Source thread started");
      var sync = (SynchronizationContext)arg;
      try {
        sourceStream.Start();
      }
      finally {
        sourceStream.Close();
        sync.Post(thread => {
          if (sourceThread == thread) {
            sourceThread = null;
          }
          foreach (var os in outputStreams) {
            os.Close();
          }
          startTickCount = null;
          IsClosed = true;
          OnClosed();
        }, Thread.CurrentThread);
      }
      logger.Debug("Source thread finished");
    }

    /// <summary>
    /// 接続されている各ストリームへパケットを送信します
    /// </summary>
    /// <param name="from">送信元のホスト</param>
    /// <param name="packet">送信するデータ</param>
    /// <param name="group">送信先グループ</param>
    public void Broadcast(Host from, Atom packet, BroadcastGroup group)
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
          sourceStream.Close();
        }
        foreach (var outputStream in outputStreams) {
          outputStream.Close();
        }
      }
    }

    /// <summary>
    /// チャンネルIDとソースストリームを指定してチャンネルを初期化します
    /// </summary>
    /// <param name="peercast">所属するPeerCastオブジェクト</param>
    /// <param name="channel_id">チャンネルID</param>
    /// <param name="source_uri">ソースURI</param>
    public Channel(PeerCast peercast, Guid channel_id, Uri source_uri)
    {
      this.IsClosed = true;
      this.PeerCast = peercast;
      sourceUri = source_uri;
      sourceHost = new Host();
      var port = sourceUri.Port < 0 ? 7144 : sourceUri.Port;
      var addresses = Dns.GetHostAddresses(sourceUri.DnsSafeHost);
      var addr = addresses.FirstOrDefault(x => x.AddressFamily==AddressFamily.InterNetwork);
      if (addr!=null) {
        sourceHost.GlobalEndPoint = new IPEndPoint(addr, port);
      }
      channelInfo = new ChannelInfo(channel_id);
      channelInfo.PropertyChanged += (sender, e) => {
        OnPropertyChanged("ChannelInfo");
      };
      contents.CollectionChanged += (sender, e) => {
        OnContentChanged();
      };
      outputStreams.CollectionChanged += (sender, e) => {
        OnPropertyChanged("OutputStreams");
      };
      nodes.CollectionChanged += (sender, e) => {
        OnPropertyChanged("Nodes");
      };
    }
  }
}
