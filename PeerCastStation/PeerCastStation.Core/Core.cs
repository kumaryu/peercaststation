using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.ComponentModel;
using System.Collections.ObjectModel;

namespace PeerCastStation.Core
{
  /// <summary>
  /// 指定されたプラグインを読み込むためのインターフェース
  /// </summary>
  public interface IPlugInLoader : IDisposable
  {
    /// <summary>
    /// プラグインローダの名前を取得します
    /// </summary>
    string Name { get; }
    /// <summary>
    /// ファイルからプラグインを読み込みます
    /// </summary>
    /// <param name="uri">読み込むファイルのURI</param>
    /// <returns>読み込めた場合はプラグインのインスタンス、読み込めなかった場合はnull</returns>
    IPlugIn Load(Uri uri);
  }

  /// <summary>
  /// プラグインのインスタンスを表すインターフェースです
  /// </summary>
  public interface IPlugIn : IDisposable
  {
    /// <summary>
    /// プラグインの名前を取得します
    /// </summary>
    string Name { get; }
    /// <summary>
    /// プラグインが提供する拡張名のリストを取得します
    /// </summary>
    ICollection<string> Extensions { get; }
    /// <summary>
    /// プラグインの説明を取得します
    /// </summary>
    string Description { get; }
    /// <summary>
    /// プラグインの取得元URIを取得します
    /// </summary>
    Uri Contact { get; }
    /// <summary>
    /// Coreインスタンスへのプラグインの登録を行ないます
    /// </summary>
    /// <param name="core">登録先のCoreインスタンス</param>
    void Register(Core core);
    /// <summary>
    /// Coreインスタンスへのプラグイン登録を解除します
    /// </summary>
    /// <param name="core">登録解除するCoreインスタンス</param>
    void Unregister(Core core);
  }

  /// <summary>
  /// YellowPageのインターフェースです
  /// </summary>
  public interface IYellowPage : IDisposable
  {
    /// <summary>
    /// YwlloePageに関連付けられた名前を取得します
    /// </summary>
    string Name { get; }
    /// <summary>
    /// YellowPageのURLを取得します
    /// </summary>
    Uri    Uri  { get; }
    /// <summary>
    /// チャンネルIDからトラッカーを検索し取得します
    /// </summary>
    /// <param name="channel_id">検索するチャンネルID</param>
    /// <returns>見付かった場合は接続先URI、見付からなかった場合はnull</returns>
    Uri FindTracker(Guid channel_id);
    /// <summary>
    /// YellowPageの持っているチャンネル一覧を取得します
    /// </summary>
    /// <returns>取得したチャンネル一覧。取得できなければ空のリスト</returns>
    ICollection<ChannelInfo> ListChannels();
    /// <summary>
    /// YellowPageにチャンネルを載せます
    /// </summary>
    /// <param name="channel">載せるチャンネル</param>
    void Announce(Channel channel);
  }

  /// <summary>
  /// YellowPageのインスタンスを作成するためのファクトリインターフェースです
  /// </summary>
  public interface IYellowPageFactory : IDisposable
  {
    /// <summary>
    /// YellowPageインスタンスを作成し返します
    /// </summary>
    /// <param name="name">YellowPageに関連付けられる名前</param>
    /// <param name="uri">YellowPageのURI</param>
    /// <returns>IYellowPageのインスタンス</returns>
    IYellowPage Create(string name, Uri uri);
  }

  /// <summary>
  /// 上流からチャンネルにContentを追加するストリームを表すインターフェースです
  /// </summary>
  public interface ISourceStream : IDisposable
  {
    /// <summary>
    /// 指定したホストを起点にストリームの取得を開始します
    /// </summary>
    /// <param name="tracker">ストリーム取得の起点</param>
    /// <param name="channel">取得ストリームの追加先チャンネル</param>
    void Start(Uri tracker, Channel channel);
    /// <summary>
    /// ストリームの取得を終了します
    /// </summary>
    void Close();
  }

  /// <summary>
  /// SourceStreamのインスタンスを作成するファクトリインターフェースです
  /// </summary>
  public interface ISourceStreamFactory : IDisposable
  {
    /// <summary>
    /// URIからプロトコルを判別しSourceStreamのインスタンスを作成します。
    /// </summary>
    /// <param name="tracker">プロトコル判別用のURI</param>
    /// <returns>プロトコルが適合していればSourceStreamのインスタンス、それ以外はnull</returns>
    ISourceStream Create(Uri tracker);
  }

  /// <summary>
  /// 下流にチャンネルのContentを流すストリームを表わすインターフェースです
  /// </summary>
  public interface IOutputStream : IDisposable
  {
    /// <summary>
    /// 指定されたStreamへChannelのContentを流しはじめます
    /// </summary>
    /// <param name="stream">書き込み先のストリーム</param>
    /// <param name="channel">情報を流す元のチャンネル</param>
    void Start(Stream stream, Channel channel);
    /// <summary>
    /// ストリームへの書き込みを終了します
    /// </summary>
    void Close();
  }

  /// <summary>
  /// OutputStreamのインスタンスを作成するファクトリインターフェースです
  /// </summary>
  public interface IOutputStreamFactory : IDisposable
  {
    /// <summary>
    /// 下流からのリクエストからプロトコルを判別しOutpuStreamのインスタンスを作成します
    /// </summary>
    /// <param name="header">下流から受け取ったリクエスト</param>
    /// <returns>headerからプロトコルに適合するのが判別できた場合はOutputStreamのインスタンス、それ以外はnull</returns>
    IOutputStream Create(byte[] header);
  }

  /// <summary>
  /// 接続情報を保持するクラスです
  /// </summary>
  public class Host
  {
    /// <summary>
    /// ホストが持つアドレス情報のリストを返します
    /// </summary>
    public IList<IPEndPoint> Addresses { get; private set; }
    /// <summary>
    /// ホストのセッションIDを取得および設定します
    /// </summary>
    public Guid SessionID { get; set; }
    /// <summary>
    /// ホストのブロードキャストIDを取得および設定します
    /// </summary>
    public Guid BroadcastID { get; set; }
    /// <summary>
    /// ホストへの接続が可能かどうかを取得および設定します
    /// </summary>
    public bool IsFirewalled { get; set; }
    /// <summary>
    /// ホストの拡張リストを取得します
    /// </summary>
    public IList<string> Extensions { get; private set; }
    /// <summary>
    /// その他のホスト情報リストを取得します
    /// </summary>
    public AtomCollection Extra { get; private set; }

    /// <summary>
    /// ホスト情報を初期化します
    /// </summary>
    public Host()
    {
      Addresses    = new List<IPEndPoint>();
      SessionID    = Guid.Empty;
      BroadcastID  = Guid.Empty;
      IsFirewalled = false;
      Extensions   = new List<string>();
      Extra        = new AtomCollection();
    }
  }

  /// <summary>
  /// PCPプロトコルの基本通信単位を表すクラスです。
  /// 4文字以下の名前と対応すう値を保持します
  /// </summary>
  public class Atom
  {
    /// <summary>
    /// 名前を取得します
    /// </summary>
    public string Name  { get; private set; }
    /// <summary>
    /// 保持している値を取得します
    /// </summary>
    public object Value { get; private set; }

    /// <summary>
    /// 名前と値を指定してAtomを初期化します。
    /// </summary>
    /// <param name="name">4文字以下の名前</param>
    /// <param name="value">値</param>
    public Atom(string name, object value)
    {
      if (name.Length > 4) {
        throw new ArgumentException("Atom Name length must be 4 or less.");
      }
      Name = name;
      Value = value;
    }
  }

  public class AtomCollection : ObservableCollection<Atom>
  {
  }

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
  /// チャンネルリレー中のノードを表わすクラスです
  /// </summary>
  public class Node
    : INotifyPropertyChanged
  {
    private Host host = null;
    private int relayCount = 0;
    private int directCount = 0;
    private bool isRelayFull = false;
    private bool isDirectFull = false;
    private AtomCollection extra = new AtomCollection();
    /// <summary>
    /// 接続情報を取得および設定します
    /// </summary>
    public Host Host {
      get { return host; }
      set
      {
        host = value;
        OnPropertyChanged("Host");
      }
    }
    /// <summary>
    /// リレーしている数を取得および設定します
    /// </summary>
    public int RelayCount {
      get { return relayCount; }
      set
      {
        relayCount = value;
        OnPropertyChanged("RelayCount");
      }
    }
    /// <summary>
    /// 直接視聴している数を取得および設定します
    /// </summary>
    public int DirectCount {
      get { return directCount; }
      set
      {
        directCount = value;
        OnPropertyChanged("DirectCount");
      }
    }
    /// <summary>
    /// リレー数が一杯かどうかを取得および設定します
    /// </summary>
    public bool IsRelayFull {
      get { return isRelayFull; }
      set
      {
        isRelayFull = value;
        OnPropertyChanged("IsRelayFull");
      }
    }
    /// <summary>
    /// 直接視聴数が一杯かどうかを取得および設定します
    /// </summary>
    public bool IsDirectFull {
      get { return isDirectFull; }
      set
      {
        isDirectFull = value;
        OnPropertyChanged("IsDirectFull");
      }
    }
    /// <summary>
    /// その他の情報のリストを取得します
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
    /// 接続情報からノード情報を初期化します
    /// </summary>
    /// <param name="host">ノードの接続情報</param>
    public Node(Host host)
    {
      Host = host;
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
    private ChannelStatus status = ChannelStatus.Idle;
    private ISourceStream sourceStream = null;
    private ObservableCollection<IOutputStream> outputStreams = new ObservableCollection<IOutputStream>();
    private ObservableCollection<Node> nodes = new ObservableCollection<Node>();
    private ChannelInfo channelInfo;
    private Content contentHeader = null;
    private ObservableCollection<Content> contents = new ObservableCollection<Content>();
    /// <summary>
    /// チャンネルの状態を取得および設定します
    /// </summary>
    public ChannelStatus Status {
      get { return status; }
      set
      {
        status = value;
        OnPropertyChanged("Status");
      }
    }
    /// <summary>
    /// ソースストリームを取得および設定します
    /// </summary>
    public ISourceStream SourceStream {
      get { return sourceStream; }
      set
      {
        sourceStream = value;
        OnPropertyChanged("SourceStream");
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
    public Content ContentHeader {
      get { return contentHeader; }
      set
      {
        contentHeader = value;
        OnPropertyChanged("ContentHeader");
        OnContentChanged();
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

    /// <summary>
    /// チャンネル接続を終了します。ソースストリームと接続している出力ストリームを全て閉じます
    /// </summary>
    public void Close()
    {
      sourceStream.Close();
      foreach (var os in outputStreams) {
        os.Close();
      }
      Status = ChannelStatus.Closed;
      OnClosed();
    }

    /// <summary>
    /// チャンネルIDとソースストリームを指定してチャンネルを初期化します
    /// </summary>
    /// <param name="channel_id">チャンネルID</param>
    /// <param name="source">ソースストリーム</param>
    public Channel(Guid channel_id, ISourceStream source)
    {
      sourceStream  = source;
      channelInfo   = new ChannelInfo(channel_id);
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

  /// <summary>
  /// チャンネルのストリーム内容を表わすクラスです
  /// </summary>
  public class Content
  {
    /// <summary>
    /// コンテントの位置を取得します。
    /// 位置はバイト数や時間とか関係なくソースの出力したパケット番号です
    /// </summary>
    public long Position { get; private set; } 
    /// <summary>
    /// コンテントの内容を取得します
    /// </summary>
    public byte[] Data   { get; private set; } 

    /// <summary>
    /// コンテントの位置と内容を指定して初期化します
    /// </summary>
    /// <param name="pos">位置</param>
    /// <param name="data">内容</param>
    public Content(long pos, byte[] data)
    {
      Position = pos;
      Data = data;
    }
  }

  /// <summary>
  /// PeerCastStationの主要な動作を行ない、管理するクラスです
  /// </summary>
  public class Core
  {
    public Host Host { get; set; }
    /// <summary>
    /// 登録されているプラグインローダのリストを取得します
    /// </summary>
    public IList<IPlugInLoader> PlugInLoaders { get; private set; }
    /// <summary>
    /// 読み込まれたプラグインのリストを取得します
    /// </summary>
    public ICollection<IPlugIn> PlugIns       { get; private set; }
    /// <summary>
    /// 登録されているYellowPageのリストを取得します
    /// </summary>
    public IList<IYellowPage>   YellowPages   { get; private set; }
    /// <summary>
    /// 登録されているYellowPageのプロトコルとファクトリの辞書を取得します
    /// </summary>
    public IDictionary<string, IYellowPageFactory>   YellowPageFactories   { get; private set; }
    /// <summary>
    /// 登録されているSourceStreamのプロトコルとファクトリの辞書を取得します
    /// </summary>
    public IDictionary<string, ISourceStreamFactory> SourceStreamFactories { get; private set; }
    /// <summary>
    /// 登録されているOutputStreamのリストを取得します
    /// </summary>
    public IList<IOutputStreamFactory> OutputStreamFactories { get; private set; }
    /// <summary>
    /// 接続しているチャンネルのリストを取得します
    /// </summary>
    public ICollection<Channel> Channels { get { return channels; } }
    private List<Channel> channels = new List<Channel>();
    /// <summary>
    /// 指定したファイルをプラグインとして読み込みます
    /// </summary>
    /// <param name="uri">読み込むファイル</param>
    /// <returns>読み込めた場合はPlugInのインスタンス、それ以外はnull</returns>
    public IPlugIn LoadPlugIn(Uri uri)
    {
      foreach (var loader in PlugInLoaders) {
        var plugin = loader.Load(uri);
        if (plugin!=null) {
          plugin.Register(this);
          return plugin;
        }
      }
      return null;
    }

    /// <summary>
    /// チャンネルIDを指定してチャンネルのリレーを開始します。
    /// 接続先はYellowPageに問い合わせ取得します。
    /// </summary>
    /// <param name="channel_id">リレーを開始するチャンネルID</param>
    /// <returns>接続先が見付かった場合はChannelのインスタンス、それ以外はnull</returns>
    public Channel RelayChannel(Guid channel_id)
    {
      foreach (var yp in YellowPages) {
        var tracker = yp.FindTracker(channel_id);
        if (tracker!=null) {
          return RelayChannel(channel_id, tracker);
        }
      }
      return null;
    }

    /// <summary>
    /// 接続先を指定してチャンネルのリレーを開始します。
    /// URIから接続プロトコルも判別します
    /// </summary>
    /// <param name="channel_id">リレーするチャンネルID</param>
    /// <param name="tracker">接続起点およびプロトコル</param>
    /// <returns>Channelのインスタンス</returns>
    public Channel RelayChannel(Guid channel_id, Uri tracker)
    {
      ISourceStreamFactory source_factory = null;
      if (!SourceStreamFactories.TryGetValue(tracker.Scheme, out source_factory)) {
        throw new ArgumentException(String.Format("Protocol `{0}' is not found", tracker.Scheme));
      }
      var source_stream = source_factory.Create(tracker);
      var channel = new Channel(channel_id, source_stream);
      channels.Add(channel);
      source_stream.Start(tracker, channel);
      return channel;
    }

    /// <summary>
    /// 配信を開始します。
    /// </summary>
    /// <param name="yp">チャンネル情報を載せるYellowPage</param>
    /// <param name="channel_id">チャンネルID</param>
    /// <param name="protocol">出力プロトコル</param>
    /// <param name="source">配信ソース</param>
    /// <returns>Channelのインスタンス</returns>
    public Channel BroadcastChannel(IYellowPage yp, Guid channel_id, string protocol, Uri source) { return null; }

    /// <summary>
    /// 指定したチャンネルをチャンネルリストから取り除きます
    /// </summary>
    /// <param name="channel"></param>
    public void CloseChannel(Channel channel)
    {
      channel.Close();
      channels.Remove(channel);
    }

    /// <summary>
    /// 接続待ち受けアドレスを指定してCoreを初期化します
    /// </summary>
    /// <param name="ip">接続を待ち受けるアドレス</param>
    public Core(IPEndPoint ip)
    {
      Host = new Host();
      Host.Addresses.Add(ip);
      Host.SessionID = Guid.NewGuid();

      PlugInLoaders = new List<IPlugInLoader>();
      PlugIns       = new List<IPlugIn>();
      YellowPages   = new List<IYellowPage>();
      YellowPageFactories = new Dictionary<string, IYellowPageFactory>();
      SourceStreamFactories = new Dictionary<string, ISourceStreamFactory>();
      OutputStreamFactories = new List<IOutputStreamFactory>();
    }
  }
}
