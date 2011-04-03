using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.ComponentModel;
using System.Threading;
using System.Net.Sockets;
using System.Linq;

namespace PeerCastStation.Core
{
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
  /// SourceStreamの現在の状況を表します
  /// </summary>
  public enum SourceStreamStatus
  {
    /// <summary>
    /// 接続されていません
    /// </summary>
    Idle,
    /// <summary>
    /// 接続先を探しています
    /// </summary>
    Searching,
    /// <summary>
    /// 接続しています
    /// </summary>
    Connecting,
    /// <summary>
    /// 受信中です
    /// </summary>
    Recieving,
    /// <summary>
    /// エラー発生のため切断しました
    /// </summary>
    Error,
  }

  /// <summary>
  /// ISourceStream.StatusChangedイベントに渡される引数のクラスです
  /// </summary>
  public class SourceStreamStatusChangedEventArgs
    : EventArgs
  {
    /// <summary>
    /// 変更された状態を取得します
    /// </summary>
    public SourceStreamStatus Status { get; private set; }
    /// <summary>
    /// 変更された状態を指定してSourceStreamStatusChangedEventArgsオブジェクトを初期化します
    /// </summary>
    /// <param name="status">変更された状態</param>
    public SourceStreamStatusChangedEventArgs(SourceStreamStatus status)
    {
      Status = status;
    }
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
    /// <summary>
    /// ストリームの現在の状態を取得します
    /// </summary>
    SourceStreamStatus Status { get; }
    /// <summary>
    /// ストリームの状態が変更された時に呼ばれるイベントです
    /// </summary>
    event EventHandler<SourceStreamStatusChangedEventArgs> StatusChanged;
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
    /// 送信先がローカルネットワークかどうかを取得します
    /// </summary>
    bool IsLocal { get; }
    /// <summary>
    /// 送信に必要な上り帯域を取得します。
    /// IsLocalがtrueの場合は0を返します。
    /// </summary>
    int UpstreamRate { get; }
    /// <summary>
    /// 元になるストリームへチャンネルのContentを流しはじめます
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
    /// <param name="remote_endpoint">接続先。無ければnull</param>
    /// <param name="channel_id">所属するチャンネルのチャンネルID</param>
    /// <param name="header">クライアントから受け取ったリクエスト</param>
    /// <returns>OutputStream</returns>
    IOutputStream Create(Stream stream, EndPoint remote_endpoint, Guid channel_id, byte[] header);
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
    /// ホストが持つローカルなアドレス情報を取得および設定します
    /// </summary>
    public IPEndPoint LocalEndPoint { get; set; }
    /// <summary>
    /// ホストが持つグローバルなアドレス情報を取得および設定します
    /// </summary>
    public IPEndPoint GlobalEndPoint { get; set; }
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
      LocalEndPoint   = null;
      GlobalEndPoint  = null;
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
    private TimeSpan lastUpdated = TimeSpan.Zero;
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
    /// ノードの最終更新時間を取得します
    /// </summary>
    public TimeSpan LastUpdated {
      get { return lastUpdated; }
    }

    /// <summary>
    /// その他の情報のリストを取得します
    /// </summary>
    public AtomCollection Extra { get { return extra; } }
    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged(string name)
    {
      lastUpdated = new TimeSpan(Environment.TickCount*10000L);
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
      lastUpdated = new TimeSpan(Environment.TickCount*10000L);
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
  /// チャンネルへの接続制御を行なうクラスです
  /// </summary>
  public class AccessController
    : INotifyPropertyChanged
  {
    /// <summary>
    /// 所属するPeerCastオブジェクトを取得します
    /// </summary>
    public PeerCast PeerCast { get; private set; }
    /// <summary>
    /// PeerCast全体での最大リレー数を取得および設定します。
    /// </summary>
    /// <value>0は無制限です。</value>
    public int MaxRelays {
      get { return maxRelays; }
      set { if (maxRelays!=value) { maxRelays = value; DoPropertyChanged("MaxRelays"); } }
    }
    /// <summary>
    /// チャンネル毎の最大リレー数を取得および設定します。
    /// </summary>
    /// <value>0は無制限です。</value>
    public int MaxRelaysPerChannel {
      get { return maxRelaysPerChannel; }
      set { if (maxRelaysPerChannel!=value) { maxRelaysPerChannel = value; DoPropertyChanged("MaxRelaysPerChannel"); } }
    }
    /// <summary>
    /// PeerCast全体での最大視聴数を取得および設定します。
    /// </summary>
    /// <value>0は無制限です。</value>
    public int MaxPlays {
      get { return maxPlays; }
      set { if (maxPlays!=value) { maxPlays = value; DoPropertyChanged("MaxPlays"); }  }
    }
    /// <summary>
    /// チャンネル毎の最大視聴数を取得および設定します。
    /// </summary>
    /// <value>0は無制限です。</value>
    public int MaxPlaysPerChannel {
      get { return maxPlaysPerChannel; }
      set { if (maxPlaysPerChannel!=value) { maxPlaysPerChannel = value; DoPropertyChanged("MaxPlaysPerChannel"); }  }
    }
    /// <summary>
    /// PeerCast全体での最大上り帯域を取得および設定します。
    /// </summary>
    /// <value>0は無制限です。</value>
    public int MaxUpstreamRate {
      get { return maxUpstreamRate; }
      set { if (maxUpstreamRate!=value) { maxUpstreamRate = value; DoPropertyChanged("MaxUpstreamRate"); }  }
    }

    private int maxRelays = 0;
    private int maxRelaysPerChannel = 0;
    private int maxPlays = 0;
    private int maxPlaysPerChannel = 0;
    private int maxUpstreamRate = 0;

    /// <summary>
    /// 指定したチャンネルに新しいリレー接続ができるかどうかを取得します
    /// </summary>
    /// <param name="channel">リレー接続先のチャンネル</param>
    /// <returns>リレー可能な場合はtrue、それ以外はfalse</returns>
    public virtual bool IsChannelRelayable(Channel channel)
    {
      var upstream_rate = PeerCast.Channels.Sum(c => c.OutputStreams.Sum(o => o.IsLocal ? 0 : o.UpstreamRate));
      return
        (this.MaxRelays<=0 || this.MaxRelays>PeerCast.Channels.Sum(c => c.OutputStreams.CountRelaying)) &&
        (this.MaxRelaysPerChannel<=0 || this.MaxRelaysPerChannel>channel.OutputStreams.CountRelaying) &&
        (this.MaxUpstreamRate<=0 || this.MaxUpstreamRate>=upstream_rate+(channel.ChannelInfo.Extra.GetChanInfoBitrate() ?? 0));
    }

    /// <summary>
    /// 指定したチャンネルに新しいリレー接続ができるかどうかを取得します
    /// </summary>
    /// <param name="channel">リレー接続先のチャンネル</param>
    /// <param name="output_stream">接続しようとするOutputStream</param>
    /// <returns>リレー可能な場合はtrue、それ以外はfalse</returns>
    public virtual bool IsChannelRelayable(Channel channel, IOutputStream output_stream)
    {
      var upstream_rate = PeerCast.Channels.Sum(c => c.OutputStreams.Sum(o => o.IsLocal ? 0 : o.UpstreamRate));
      return
        (this.MaxRelays<=0 || this.MaxRelays>PeerCast.Channels.Sum(c => c.OutputStreams.CountRelaying)) &&
        (this.MaxRelaysPerChannel<=0 || this.MaxRelaysPerChannel>channel.OutputStreams.CountRelaying) &&
        (this.MaxUpstreamRate<=0 || this.MaxUpstreamRate>=upstream_rate+(output_stream.IsLocal ? 0 : output_stream.UpstreamRate));
    }

    /// <summary>
    /// 指定したチャンネルに新しい視聴接続ができるかどうかを取得します
    /// </summary>
    /// <param name="channel">視聴接続先のチャンネル</param>
    /// <returns>視聴可能な場合はtrue、それ以外はfalse</returns>
    public virtual bool IsChannelPlayable(Channel channel)
    {
      var upstream_rate = PeerCast.Channels.Sum(c => c.OutputStreams.Sum(o => o.IsLocal ? 0 : o.UpstreamRate));
      return
        (this.MaxPlays<=0 || this.MaxPlays>PeerCast.Channels.Sum(c => c.OutputStreams.CountPlaying)) &&
        (this.MaxPlaysPerChannel<=0 || this.MaxPlaysPerChannel>channel.OutputStreams.CountPlaying) &&
        (this.MaxUpstreamRate<=0 || this.MaxUpstreamRate>=upstream_rate+(channel.ChannelInfo.Extra.GetChanInfoBitrate() ?? 0));
    }

    /// <summary>
    /// 指定したチャンネルに新しい視聴接続ができるかどうかを取得します
    /// </summary>
    /// <param name="channel">視聴接続先のチャンネル</param>
    /// <param name="output_stream">接続しようとするOutputStream</param>
    /// <returns>視聴可能な場合はtrue、それ以外はfalse</returns>
    public virtual bool IsChannelPlayable(Channel channel, IOutputStream output_stream)
    {
      var upstream_rate = PeerCast.Channels.Sum(c => c.OutputStreams.Sum(o => o.IsLocal ? 0 : o.UpstreamRate));
      return
        (this.MaxPlays<=0 || this.MaxPlays>PeerCast.Channels.Sum(c => c.OutputStreams.CountPlaying)) &&
        (this.MaxPlaysPerChannel<=0 || this.MaxPlaysPerChannel>channel.OutputStreams.CountPlaying) &&
        (this.MaxUpstreamRate<=0 || this.MaxUpstreamRate>=upstream_rate+(output_stream.IsLocal ? 0 : output_stream.UpstreamRate));
    }

    /// <summary>
    /// AccessControllerオブジェクトを初期化します
    /// </summary>
    /// <param name="peercast">所属するPeerCastオブジェクト</param>
    public AccessController(PeerCast peercast)
    {
      this.PeerCast = peercast;
    }

    private void DoPropertyChanged(string property_name)
    {
      if (PropertyChanged!=null) {
        PropertyChanged(this, new PropertyChangedEventArgs(property_name));
      }
    }

    public event PropertyChangedEventHandler PropertyChanged;
  }

  /// <summary>
  /// ChannelChangedEventHandlerに渡される引数クラスです
  /// </summary>
  public class ChannelChangedEventArgs
    : EventArgs
  {
    /// <summary>
    /// 変更があったチャンネルを取得します
    /// </summary>
    public Channel Channel { get; private set; }
    /// <summary>
    /// 変更があったチャンネルを指定してChannelChangedEventArgsを初期化します
    /// </summary>
    /// <param name="channel">変更があったチャンネル</param>
    public ChannelChangedEventArgs(Channel channel)
    {
      this.Channel = channel;
    }
  }
  /// <summary>
  /// チャンネルの追加や削除があった時に呼ばれるイベントのデリゲートです
  /// </summary>
  /// <param name="sender">イベント送出元のオブジェクト</param>
  /// <param name="e">イベント引数</param>
  public delegate void ChannelChangedEventHandler(object sender, ChannelChangedEventArgs e);

  /// <summary>
  /// 接続待ち受け処理を扱うクラスです
  /// </summary>
  public class OutputListener
  {
    /// <summary>
    /// 所属しているPeerCastオブジェクトを取得します
    /// </summary>
    public PeerCast PeerCast { get; private set; }
    /// <summary>
    /// 待ち受けが閉じられたかどうかを取得します
    /// </summary>
    public bool IsClosed { get; private set; }
    /// <summary>
    /// 接続待ち受けをしているエンドポイントを取得します
    /// </summary>
    public IPEndPoint LocalEndPoint { get { return (IPEndPoint)server.LocalEndpoint; } }

    private TcpListener server;
    /// <summary>
    /// 指定したエンドポイントで接続待ち受けをするOutputListnerを初期化します
    /// </summary>
    /// <param name="peercast">所属するPeerCastオブジェクト</param>
    /// <param name="ip">待ち受けをするエンドポイント</param>
    internal OutputListener(PeerCast peercast, IPEndPoint ip)
    {
      this.PeerCast = peercast;
      server = new TcpListener(ip);
      server.Start();
      listenerThread = new Thread(ListenerThreadFunc);
      listenerThread.Name = "OutputListenerThread";
      listenerThread.Start(server);
    }

    private Thread listenerThread = null;
    private void ListenerThreadFunc(object arg)
    {
      var server = (TcpListener)arg;
      while (!IsClosed) {
        try {
          var client = server.AcceptTcpClient();
          var output_thread = new Thread(OutputThreadFunc);
          PeerCast.SynchronizationContext.Post(dummy => {
            outputThreads.Add(output_thread);
          }, null);
          output_thread.Name = "OutputThread";
          output_thread.Start(client);
        }
        catch (SocketException) {
        }
      }
    }

    /// <summary>
    /// 接続を待ち受けを終了します
    /// </summary>
    internal void Close()
    {
      IsClosed = true;
      server.Stop();
      listenerThread.Join();
    }

    private static List<Thread> outputThreads = new List<Thread>();
    private void OutputThreadFunc(object arg)
    {
      var client = (TcpClient)arg;
      var stream = client.GetStream();
      stream.WriteTimeout = 3000;
      stream.ReadTimeout = 3000;
      IOutputStream output_stream = null;
      Channel channel = null;
      IOutputStreamFactory[] output_factories = null;
      PeerCast.SynchronizationContext.Send(dummy => {
        output_factories = PeerCast.OutputStreamFactories.ToArray();
      }, null);
      try {
        var header = new List<byte>();
        Guid? channel_id = null;
        bool eos = false;
        while (!eos && output_stream==null && header.Count<=4096) {
          try {
            do {
              var val = stream.ReadByte();
              if (val < 0) {
                eos = true;
              }
              else {
                header.Add((byte)val);
              }
            } while (stream.DataAvailable);
          }
          catch (IOException) {
          }
          var header_ary = header.ToArray();
          foreach (var factory in output_factories) {
            channel_id = factory.ParseChannelID(header_ary);
            if (channel_id != null) {
              output_stream = factory.Create(stream, client.Client.RemoteEndPoint, channel_id.Value, header_ary);
              break;
            }
          }
        }
        if (output_stream != null) {
          PeerCast.SynchronizationContext.Send(dummy => {
            channel = PeerCast.Channels.FirstOrDefault(c => c.ChannelInfo.ChannelID==channel_id);
            if (channel!=null) {
              channel.OutputStreams.Add(output_stream);
            }
          }, null);
          output_stream.Start();
        }
      }
      finally {
        if (output_stream != null) {
          if (channel!=null) {
            PeerCast.SynchronizationContext.Post(dummy => {
              channel.OutputStreams.Remove(output_stream);
            }, null);
          }
          output_stream.Close();
        }
        stream.Close();
        client.Close();
        PeerCast.SynchronizationContext.Post(thread => {
          outputThreads.Remove((Thread)thread);
        }, Thread.CurrentThread);
      }
    }
  }

  /// <summary>
  /// PeerCastStationの主要な動作を行ない、管理するクラスです
  /// </summary>
  public class PeerCast
  {
    /// <summary>
    /// UserAgentやServerとして名乗る名前を取得および設定します。
    /// </summary>
    public string AgentName { get; set; }
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
    public IList<Channel> Channels { get { return channels; } }
    private List<Channel> channels = new List<Channel>();

    /// <summary>
    /// チャンネルが追加された時に呼び出されます。
    /// </summary>
    public event ChannelChangedEventHandler ChannelAdded;
    /// <summary>
    /// チャンネルが削除された時に呼び出されます。
    /// </summary>
    public event ChannelChangedEventHandler ChannelRemoved;

    /// <summary>
    /// 所属するスレッドのSynchronizationContextを取得および設定します
    /// </summary>
    public SynchronizationContext SynchronizationContext { get; set; }

    /// <summary>
    /// 待ち受けが閉じられたかどうかを取得します
    /// </summary>
    public bool IsClosed { get; private set; }

    /// <summary>
    /// チャンネルへのアクセス制御を行なうクラスの取得および設定をします
    /// </summary>
    public AccessController AccessController { get; set; }

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
      var channel = new Channel(this, channel_id, tracker);
      channels.Add(channel);
      var source_stream = source_factory.Create(channel, tracker);
      channel.Start(source_stream);
      if (ChannelAdded!=null) ChannelAdded(this, new ChannelChangedEventArgs(channel));
      return channel;
    }

    /// <summary>
    /// リレーしているチャンネルを取得します。
    /// </summary>
    /// <param name="channel_id">リレーするチャンネルID</param>
    /// <param name="request_uri">接続起点およびプロトコル</param>
    /// <param name="request_relay">チャンネルが無かった場合にRelayChannelを呼び出すかどうか。trueの場合呼び出す</param>
    /// <returns>
    /// channel_idに等しいチャンネルIDを持つChannelのインスタンス。
    /// チャンネルが無かった場合はrequest_relayがtrueならReleyChannelを呼び出した結果、
    /// request_relayがfalseならnull。
    /// </returns>
    public virtual Channel RequestChannel(Guid channel_id, Uri tracker, bool request_relay)
    {
      var res = channels.FirstOrDefault(c => c.ChannelInfo.ChannelID==channel_id);
      if (res!=null) {
        return res;
      }
      else if (request_relay) {
        if (tracker!=null) {
          return RelayChannel(channel_id, tracker);
        }
        else {
          return RelayChannel(channel_id);
        }
      }
      else {
        return null;
      }
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
      if (ChannelRemoved!=null) ChannelRemoved(this, new ChannelChangedEventArgs(channel));
    }

    /// <summary>
    /// PeerCastを初期化します
    /// </summary>
    public PeerCast()
    {
      if (SynchronizationContext.Current == null) {
        SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
      }
      this.SynchronizationContext = SynchronizationContext.Current;
      this.AccessController = new AccessController(this);
      var filever = System.Diagnostics.FileVersionInfo.GetVersionInfo(
        System.Reflection.Assembly.GetExecutingAssembly().Location);
      this.AgentName = String.Format("{0}/{1}", filever.ProductName, filever.ProductVersion);
      IsClosed = false;
      this.SessionID   = Guid.NewGuid();
      this.BroadcastID = Guid.NewGuid();
      foreach (var addr in Dns.GetHostAddresses(Dns.GetHostName())) {
        switch (addr.AddressFamily) {
        case AddressFamily.InterNetwork:
          if (this.LocalAddress==null && 
              !addr.Equals(IPAddress.None) &&
              !addr.Equals(IPAddress.Any) &&
              !addr.Equals(IPAddress.Broadcast) &&
              !IPAddress.IsLoopback(addr)) {
            this.LocalAddress = addr;
          }
          break;
        case AddressFamily.InterNetworkV6:
          if (LocalAddress6==null && 
              !addr.Equals(IPAddress.IPv6Any) &&
              !addr.Equals(IPAddress.IPv6Loopback) &&
              !addr.Equals(IPAddress.IPv6None)) {
            this.LocalAddress6 = addr;
          }
          break;
        default:
          break;
        }
      }
      this.GlobalAddress = null;
      this.GlobalAddress6 = null;
      this.IsFirewalled = null;

      YellowPages   = new List<IYellowPage>();
      YellowPageFactories = new Dictionary<string, IYellowPageFactory>();
      SourceStreamFactories = new Dictionary<string, ISourceStreamFactory>();
      OutputStreamFactories = new List<IOutputStreamFactory>();
    }

    public bool? IsFirewalled { get; set; }
    public Guid SessionID { get; private set; }
    public Guid BroadcastID { get; set; }
    public IPAddress LocalAddress { get; private set; }
    public IPAddress GlobalAddress { get; set; }
    public IPAddress LocalAddress6 { get; private set; }
    public IPAddress GlobalAddress6 { get; set; }

    private List<OutputListener> outputListeners = new List<OutputListener>();
    /// <summary>
    /// 接続待ち受けスレッドのコレクションを取得します
    /// </summary>
    public IList<OutputListener> OutputListeners { get { return outputListeners.AsReadOnly(); } }
    /// <summary>
    /// 指定したエンドポイントで接続待ち受けを開始します
    /// </summary>
    /// <param name="ip">待ち受けを開始するエンドポイント</param>
    /// <returns>接続待ち受け</returns>
    /// <exception cref="System.Net.Sockets.SocketException">待ち受けが開始できませんでした</exception>
    public OutputListener StartListen(IPEndPoint ip)
    {
      var res = new OutputListener(this, ip);
      outputListeners.Add(res);
      return res;
    }

    /// <summary>
    /// 指定した接続待ち受けを終了します。
    /// 既に接続されているクライアント接続には影響ありません
    /// </summary>
    /// <param name="listener">待ち受けを終了するリスナ</param>
    public void StopListen(OutputListener listener)
    {
      if (outputListeners.Remove(listener)) {
        listener.Close();
      }
    }

    public IPEndPoint LocalEndPoint
    {
      get
      {
        var listener = outputListeners.FirstOrDefault(
          x => x.LocalEndPoint.AddressFamily==AddressFamily.InterNetwork);
        if (listener!=null) {
          return new IPEndPoint(LocalAddress, listener.LocalEndPoint.Port);
        }
        else {
          return null;
        }
      }
    }

    public IPEndPoint LocalEndPoint6
    {
      get
      {
        var listener = outputListeners.FirstOrDefault(
          x => x.LocalEndPoint.AddressFamily==AddressFamily.InterNetworkV6);
        if (listener!=null) {
          return new IPEndPoint(LocalAddress6, listener.LocalEndPoint.Port);
        }
        else {
          return null;
        }
      }
    }

    public IPEndPoint GlobalEndPoint
    {
      get
      {
        var listener = outputListeners.FirstOrDefault(
          x => x.LocalEndPoint.AddressFamily==AddressFamily.InterNetwork);
        if (listener!=null && GlobalAddress!=null) {
          return new IPEndPoint(GlobalAddress, listener.LocalEndPoint.Port);
        }
        else {
          return null;
        }
      }
    }

    public IPEndPoint GlobalEndPoint6
    {
      get
      {
        var listener = outputListeners.FirstOrDefault(
          x => x.LocalEndPoint.AddressFamily==AddressFamily.InterNetworkV6);
        if (listener!=null && GlobalAddress6!=null) {
          return new IPEndPoint(GlobalAddress6, listener.LocalEndPoint.Port);
        }
        else {
          return null;
        }
      }
    }

    /// <summary>
    /// 待ち受けと全てのチャンネルを終了します
    /// </summary>
    public void Close()
    {
      IsClosed = true;
      foreach (var listener in outputListeners) {
        listener.Close();
      }
      foreach (var channel in channels) {
        channel.Close();
        if (ChannelRemoved!=null) ChannelRemoved(this, new ChannelChangedEventArgs(channel));
      }
      channels.Clear();
    }
  }
}

