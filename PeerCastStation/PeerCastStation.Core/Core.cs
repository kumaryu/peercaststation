using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.ComponentModel;
using System.Threading;
using System.Net.Sockets;

namespace PeerCastStation.Core
{
  /// <summary>
  /// 指定されたプラグインを読み込むためのインターフェース
  /// </summary>
  public interface IPlugInLoader
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
  public interface IPlugIn
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
  public interface IYellowPage
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
  public interface IYellowPageFactory
  {
    /// <summary>
    /// このYellowPageFactoryが扱うプロトコルの名前を取得します
    /// </summary>
    string Name { get; }
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
  public interface ISourceStream
  {
    /// <summary>
    /// ストリームの取得を開始します。
    /// チャンネルと取得元URIはISourceStreamFactory.Createに渡された物を使います
    /// </summary>
    void Start();
    /// <summary>
    /// ストリームへパケットを送信します
    /// </summary>
    /// <param name="from">ブロードキャストパケットの送信元。無い場合はnull</param>
    /// <param name="packet">送信するデータ</param>
    void Post(Host from, Atom packet);
    /// <summary>
    /// ストリームの取得を終了します
    /// </summary>
    void Close();
  }

  /// <summary>
  /// SourceStreamのインスタンスを作成するファクトリインターフェースです
  /// </summary>
  public interface ISourceStreamFactory
  {
    /// <summary>
    /// このSourceStreamFactoryが扱うプロトコルの名前を取得します
    /// </summary>
    string Name { get; }
    /// <summary>
    /// URIからプロトコルを判別しSourceStreamのインスタンスを作成します。
    /// </summary>
    /// <param name="channel">所属するチャンネル</param>
    /// <param name="tracker">ストリーム取得起点のURI</param>
    /// <returns>プロトコルが適合していればSourceStreamのインスタンス、それ以外はnull</returns>
    ISourceStream Create(Channel channel, Uri tracker);
  }

  /// <summary>
  /// OutputStreamの種類を表します
  /// </summary>
  [Flags]
  public enum OutputStreamType
  {
    /// <summary>
    /// 視聴用出力ストリーム
    /// </summary>
    Play = 1,
    /// <summary>
    /// リレー用出力ストリーム
    /// </summary>
    Relay = 2,
    /// <summary>
    /// メタデータ用出力ストリーム
    /// </summary>
    Metadata = 4,
  }

  /// <summary>
  /// 下流にチャンネルのContentを流すストリームを表わすインターフェースです
  /// </summary>
  public interface IOutputStream
  {
    /// <summary>
    /// 指定されたStreamへChannelのContentを流しはじめます
    /// </summary>
    void Start();
    /// <summary>
    /// ストリームへパケットを送信します
    /// </summary>
    /// <param name="from">ブロードキャストパケットの送信元。無い場合はnull</param>
    /// <param name="packet">送信するデータ</param>
    void Post(Host from, Atom packet);
    /// <summary>
    /// ストリームへの書き込みを終了します
    /// </summary>
    void Close();
    /// <summary>
    /// 出力ストリームの種類を取得します
    /// </summary>
    OutputStreamType OutputStreamType { get; }
  }

  /// <summary>
  /// OutputStreamのインスタンスを作成するファクトリインターフェースです
  /// </summary>
  public interface IOutputStreamFactory
  {
    /// <summary>
    /// このOutputStreamが扱うプロトコルの名前を取得します
    /// </summary>
    string Name { get; }
    /// <summary>
    /// OutpuStreamのインスタンスを作成します
    /// </summary>
    /// <param name="stream">接続先のストリーム</param>
    /// <param name="channel">所属するチャンネル。チャンネルIDに対応するチャンネルが無い場合はnull</param>
    /// <param name="header">クライアントから受け取ったリクエスト</param>
    /// <returns>OutputStream</returns>
    IOutputStream Create(Stream stream, Channel channel, byte[] header);
    /// <summary>
    /// クライアントのリクエストからチャンネルIDを取得し返します
    /// </summary>
    /// <param name="header">クライアントから受け取ったリクエスト</param>
    /// <returns>headerからチャンネルIDを取得できた場合はチャンネルID、できなかった場合はnull</returns>
    Guid? ParseChannelID(byte[] header);
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
    private bool isReceiving = false;
    private bool isControlFull = false;
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
    /// コンテントの受信中かどうかを取得および設定します
    /// </summary>
    public bool IsReceiving {
      get { return isReceiving; }
      set
      {
        isReceiving = value;
        OnPropertyChanged("IsReceiving");
      }
    }

    /// <summary>
    /// Control接続数が一杯かどうかを取得および設定します
    /// </summary>
    public bool IsControlFull {
      get { return isControlFull; }
      set
      {
        isControlFull = value;
        OnPropertyChanged("IsControlFull");
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
    /// 所属するスレッドのSynchronizationContextを取得および設定します
    /// </summary>
    public SynchronizationContext SynchronizationContext { get; set; }

    /// <summary>
    /// 待ち受けが閉じられたかどうかを取得します
    /// </summary>
    public bool IsClosed { get; private set; }

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
      var channel = new Channel(channel_id, tracker);
      channels.Add(channel);
      var source_stream = source_factory.Create(channel, tracker);
      channel.Start(source_stream);
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
      if (SynchronizationContext.Current == null) {
        SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
      }
      this.SynchronizationContext = SynchronizationContext.Current;
      IsClosed = false;
      Host = new Host();
      Host.SessionID = Guid.NewGuid();

      PlugInLoaders = new List<IPlugInLoader>();
      PlugIns       = new List<IPlugIn>();
      YellowPages   = new List<IYellowPage>();
      YellowPageFactories = new Dictionary<string, IYellowPageFactory>();
      SourceStreamFactories = new Dictionary<string, ISourceStreamFactory>();
      OutputStreamFactories = new List<IOutputStreamFactory>();

      var server = new TcpListener(ip);
      server.Start();
      Host.Addresses.Add((IPEndPoint)server.LocalEndpoint);
      listenThread = new Thread(ListenThreadFunc);
      listenThread.Start(server);
    }

    /// <summary>
    /// 待ち受けと全てのチャンネルを終了します
    /// </summary>
    public void Close()
    {
      IsClosed = true;
      if (listenThread != null) {
        listenThread.Join();
        listenThread = null;
      }
      foreach (var channel in channels) {
        channel.Close();
      }
    }

    private Thread listenThread = null;
    private void ListenThreadFunc(object arg)
    {
      var server = (TcpListener)arg;
      while (!IsClosed) {
        while (server.Pending()) {
          var client = server.AcceptTcpClient();
          var output_thread = new Thread(OutputThreadFunc);
          output_thread.Start(client);
          outputThreads.Add(output_thread);
        }
        Thread.Sleep(1);
      }
      server.Stop();
    }

    private List<Thread> outputThreads = new List<Thread>();
    private void OutputThreadFunc(object arg)
    {
      var client = (TcpClient)arg;
      var stream = client.GetStream();
      IOutputStream output_stream = null;
      try {
        var header = new List<byte>();
        Guid? channel_id = null;
        while (output_stream == null && header.Count <= 4096) {
          do {
            var val = stream.ReadByte();
            if (val < 0) {
              break;
            }
            else {
              header.Add((byte)val);
            }
          } while (stream.DataAvailable);
          var header_ary = header.ToArray();
          foreach (var factory in OutputStreamFactories) {
            channel_id = factory.ParseChannelID(header_ary);
            if (channel_id != null) {
              var channel = channels.Find(c => c.ChannelInfo.ChannelID==channel_id);
              output_stream = factory.Create(stream, channel, header_ary);
              break;
            }
          }
        }
        if (output_stream != null) {
          output_stream.Start();
        }
      }
      finally {
        if (output_stream != null) {
          output_stream.Close();
        }
        stream.Close();
        client.Close();
        outputThreads.Remove(Thread.CurrentThread);
      }
    }
  }
}

