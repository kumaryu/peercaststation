using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Linq;

namespace PeerCastStation.Core
{
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
      logger.Debug("Finding channel {0} from YP", channel_id.ToString("N"));
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
      logger.Debug("Requesting channel {0} from {1}", channel_id.ToString("N"), tracker);
      ISourceStreamFactory source_factory = null;
      if (!SourceStreamFactories.TryGetValue(tracker.Scheme, out source_factory)) {
        logger.Error("Protocol `{0}' is not found", tracker.Scheme);
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
      logger.Debug("Channel Removed: {0}", channel.ChannelInfo.ChannelID.ToString("N"));
      if (ChannelRemoved!=null) ChannelRemoved(this, new ChannelChangedEventArgs(channel));
    }

    /// <summary>
    /// PeerCastを初期化します
    /// </summary>
    public PeerCast()
    {
      logger.Info("Starting PeerCast");
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
      logger.Debug("SessionID: {0}",   this.SessionID.ToString("N"));
      logger.Debug("BroadcastID: {0}", this.BroadcastID.ToString("N"));
      foreach (var addr in Dns.GetHostAddresses(Dns.GetHostName())) {
        switch (addr.AddressFamily) {
        case AddressFamily.InterNetwork:
          if (this.LocalAddress==null && 
              !addr.Equals(IPAddress.None) &&
              !addr.Equals(IPAddress.Any) &&
              !addr.Equals(IPAddress.Broadcast) &&
              !IPAddress.IsLoopback(addr)) {
            this.LocalAddress = addr;
            logger.Info("IPv4 LocalAddress: {0}", this.LocalAddress);
          }
          break;
        case AddressFamily.InterNetworkV6:
          if (LocalAddress6==null && 
              !addr.Equals(IPAddress.IPv6Any) &&
              !addr.Equals(IPAddress.IPv6Loopback) &&
              !addr.Equals(IPAddress.IPv6None)) {
            this.LocalAddress6 = addr;
            logger.Info("IPv6 LocalAddress: {0}", this.LocalAddress6);
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
      logger.Info("starting listen at {0}", ip);
      try {
        var res = new OutputListener(this, ip);
        outputListeners.Add(res);
        return res;
      }
      catch (System.Net.Sockets.SocketException e) {
        logger.Error("Listen failed: {0}", ip);
        logger.Error(e);
        throw;
      }
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
      logger.Info("Closing PeerCast");
      IsClosed = true;
      foreach (var listener in outputListeners) {
        listener.Close();
      }
      foreach (var channel in channels) {
        channel.Close();
        if (ChannelRemoved!=null) ChannelRemoved(this, new ChannelChangedEventArgs(channel));
      }
      channels.Clear();
      logger.Info("PeerCast Closed");
    }

    private static Logger logger = new Logger(typeof(PeerCast));
  }
}
