using System;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Threading;
using System.Net;

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
  /// チャンネルの状態を表します
  /// </summary>
  public enum ChannelStatus
  {
    /// <summary>
    /// 接続が開始されていません
    /// </summary>
    Idle,
    /// <summary>
    /// 接続先を検索中です
    /// </summary>
    Searching,
    /// <summary>
    /// 接続しています
    /// </summary>
    Connecting,
    /// <summary>
    /// ストリームを受け取っています
    /// </summary>
    Receiving,
    /// <summary>
    /// 接続エラーが起きました
    /// </summary>
    Error,
    /// <summary>
    /// チャンネルが閉じられました
    /// </summary>
    Closed
  }

  /// <summary>
  /// チャンネル接続を管理するクラスです
  /// </summary>
  public class Channel
    : INotifyPropertyChanged
  {
    private Uri sourceUri = null;
    private Host sourceHost = null;
    private ChannelStatus status = ChannelStatus.Idle;
    private ISourceStream sourceStream = null;
    private ObservableCollection<IOutputStream> outputStreams = new ObservableCollection<IOutputStream>();
    private ObservableCollection<Node> nodes = new ObservableCollection<Node>();
    private ChannelInfo channelInfo;
    private Content contentHeader = null;
    private ObservableCollection<Content> contents = new ObservableCollection<Content>();
    private Thread sourceThread = null;
    /// <summary>
    /// チャンネルの状態を取得および設定します
    /// </summary>
    public ChannelStatus Status
    {
      get { return status; }
      set
      {
        if (status != value) {
          status = value;
          OnPropertyChanged("Status");
        }
      }
    }
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
    public IList<IOutputStream> OutputStreams { get { return outputStreams; } }
    /// <summary>
    /// このチャンネルに関連付けられたノードリストを取得します
    /// </summary>
    public IList<Node> Nodes { get { return nodes; } }
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
    /// ヘッダを除く保持しているコンテントのリストを取得します
    /// </summary>
    public IList<Content> Contents { get { return contents; } }
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

    private class IgnoredHosts
    {
      private Dictionary<Host, int> ignoredHosts = new Dictionary<Host, int>();
      private int threshold;
      public IgnoredHosts(int threshold)
      {
        this.threshold = threshold;
      }

      public void Add(Host host)
      {
        ignoredHosts[host] = Environment.TickCount;
      }

      public bool Contains(Host host)
      {
        if (ignoredHosts.ContainsKey(host)) {
          int tick = Environment.TickCount;
          return tick - ignoredHosts[host] <= threshold;
        }
        else {
          return false;
        }
      }

      public void Clear()
      {
        ignoredHosts.Clear();
      }
    }
    private IgnoredHosts ignoredHosts = new IgnoredHosts(30 * 1000); //30sec

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
    /// SourceStreamが次に接続しにいくべき場所を選択して返します。
    /// IgnoreHostで無視されているホストは一定時間選択されません
    /// </summary>
    /// <returns>次に接続すべきホスト。無い場合はnull</returns>
    public virtual Host SelectSourceHost()
    {
      var hosts = new List<Host>();
      foreach (var node in nodes) {
        if (!ignoredHosts.Contains(node.Host)) {
          hosts.Add(node.Host);
        }
      }
      if (hosts.Count > 0) {
        int idx = new Random().Next(hosts.Count);
        return hosts[idx];
      }
      else if (!ignoredHosts.Contains(sourceHost)) {
        return sourceHost;
      }
      else {
        return null;
      }
    }

    public void Start()
    {
      var sync = SynchronizationContext.Current ?? new SynchronizationContext();
      sourceThread = new Thread(SourceThreadFunc);
      sourceThread.Start(sync);
    }

    private void SourceThreadFunc(object arg)
    {
      var sync = (SynchronizationContext)arg;
      try {
        sourceStream.Start(sourceUri, this);
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
          Status = ChannelStatus.Closed;
          OnClosed();
        }, Thread.CurrentThread);
      }
    }

    /// <summary>
    /// チャンネル接続を終了します。ソースストリームと接続している出力ストリームを全て閉じます
    /// </summary>
    public void Close()
    {
      if (Status != ChannelStatus.Closed) {
        sourceStream.Close();
      }
    }

    /// <summary>
    /// チャンネルIDとソースストリームを指定してチャンネルを初期化します
    /// </summary>
    /// <param name="channel_id">チャンネルID</param>
    /// <param name="source">ソースストリーム</param>
    /// <param name="source_uri">ソースURI</param>
    public Channel(Guid channel_id, ISourceStream source, Uri source_uri)
    {
      sourceUri = source_uri;
      sourceStream = source;
      sourceHost = new Host();
      var port = sourceUri.Port < 0 ? 7144 : sourceUri.Port;
      foreach (var addr in Dns.GetHostAddresses(sourceUri.DnsSafeHost)) {
        sourceHost.Addresses.Add(new IPEndPoint(addr, port));
      }
      channelInfo = new ChannelInfo(channel_id);
      channelInfo.PropertyChanged += (sender, e) => {
        OnPropertyChanged("ChannelInfo");
      };
      contents.CollectionChanged += (sender, e) => {
        OnPropertyChanged("Contents");
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
