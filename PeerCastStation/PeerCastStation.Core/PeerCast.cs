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
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Linq;
using System.Collections.ObjectModel;

namespace PeerCastStation.Core
{
  /// <summary>
  /// ChannelChangedEventHandlerに渡される引数クラスです
  /// </summary>
  [Serializable]
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
  /// PeerCastStationの主要な動作を行ない、管理するクラスです
  /// </summary>
  public class PeerCast
  {
    /// <summary>
    /// UserAgentやServerとして名乗る名前を取得および設定します。
    /// </summary>
    public string AgentName { get; set; }

    /// <summary>
    /// PeerCastインスタンスの稼動時間を取得します
    /// </summary>
    public TimeSpan Uptime { get { return uptime.Elapsed; } }
    private System.Diagnostics.Stopwatch uptime = System.Diagnostics.Stopwatch.StartNew();

    /// <summary>
    /// 登録されているYellowPageリストを取得および設定します
    /// 取得は読み取り専用のリストを、設定は指定したリストのコピーを設定します
    /// </summary>
    public IList<IYellowPageClient> YellowPages {
      get { return yellowPages.AsReadOnly(); }
      set {
        ReplaceCollection(ref yellowPages, org => {
          return new List<IYellowPageClient>(value);
        });
        YellowPagesChanged(this, new EventArgs());
      }
    }
    private List<IYellowPageClient> yellowPages = new List<IYellowPageClient>();

    private void ReplaceCollection<T>(ref T collection, Func<T,T> newcollection_func) where T : class
    {
      bool replaced = false;
      while (!replaced) {
        var prev = collection;
        var new_collection = newcollection_func(collection);
        System.Threading.Interlocked.CompareExchange(ref collection, new_collection, prev);
        replaced = Object.ReferenceEquals(collection, new_collection);
      }
    }

    /// <summary>
    /// YPリストが変更された時に呼び出されます。
    /// </summary>
    public event EventHandler YellowPagesChanged;

    /// <summary>
    /// 登録されているYellowPageファクトリのリストを取得します
    /// </summary>
    public IList<IYellowPageClientFactory> YellowPageFactories { get; private set; }
    /// <summary>
    /// 登録されているSourceStreamプロトコルのリストを取得します
    /// </summary>
    public IList<ISourceStreamFactory> SourceStreamFactories { get; private set; }
    /// <summary>
    /// 登録されているOutputStreamのリストを取得します
    /// </summary>
    public IList<IOutputStreamFactory> OutputStreamFactories { get; private set; }
    /// <summary>
    /// 登録されているIContentReaderFactoryのリストを取得します
    /// </summary>
    public IList<IContentReaderFactory> ContentReaderFactories { get; private set; }
    /// <summary>
    /// 接続しているチャンネルの読み取り専用リストを取得します
    /// </summary>
    public ReadOnlyCollection<Channel> Channels { get { return channels.AsReadOnly(); } }
    private List<Channel> channels = new List<Channel>();

    /// <summary>
    /// チャンネル管理オブジェクトのリストを取得します
    /// </summary>
    public IList<IChannelMonitor> ChannelMonitors { get; private set; }

    /// <summary>
    /// チャンネルが追加された時に呼び出されます。
    /// </summary>
    public event ChannelChangedEventHandler ChannelAdded;
    /// <summary>
    /// チャンネルが削除された時に呼び出されます。
    /// </summary>
    public event ChannelChangedEventHandler ChannelRemoved;

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
      Channel result = null;
      logger.Debug("Finding channel {0} from YP", channel_id.ToString("N"));
      foreach (var yp in YellowPages) {
        var tracker = yp.FindTracker(channel_id);
        if (tracker!=null) {
          result = RelayChannel(channel_id, tracker);
          break;
        }
      }
      return result;
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
      var channel = new RelayChannel(this, channel_id);
      ReplaceCollection(ref channels, orig => {
        var new_collection = new List<Channel>(orig);
        new_collection.Add(channel);
        return new_collection;
      });
      channel.Start(tracker);
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
      Channel channel = channels.FirstOrDefault(c => c.ChannelID==channel_id);
      if (request_relay) {
        if (channel!=null) {
          if (!channel.IsBroadcasting &&
              (channel.Status==SourceStreamStatus.Error ||
               channel.Status==SourceStreamStatus.Idle)) {
            channel.Reconnect(tracker);
          }
        }
        else {
          if (tracker!=null) {
            channel = RelayChannel(channel_id, tracker);
          }
          else {
            channel = RelayChannel(channel_id);
          }
        }
      }
      return channel;
    }

    /// <summary>
    /// 配信を開始します。
    /// </summary>
    /// <param name="yp">チャンネル情報を載せるYellowPage</param>
    /// <param name="channel_id">チャンネルID</param>
    /// <param name="channel_info">チャンネル情報</param>
    /// <param name="source">配信ソース</param>
    /// <param name="source_stream_factory">配信ソースからコンテンツを取得するISourceStreamFactory</param>
    /// <param name="content_reader_factory">配信ソースのコンテンツを解析するIContentReaderFactory</param>
    /// <returns>Channelのインスタンス</returns>
    public Channel BroadcastChannel(
      IYellowPageClient     yp,
      Guid                  channel_id,
      ChannelInfo           channel_info,
      Uri                   source,
      ISourceStreamFactory  source_stream_factory,
      IContentReaderFactory content_reader_factory)
    {
      logger.Debug("Broadcasting channel {0} from {1}", channel_id.ToString("N"), source);
      var channel = new BroadcastChannel(this, channel_id, channel_info, source_stream_factory, content_reader_factory);
      ReplaceCollection(ref channels, orig => {
        var new_collection = new List<Channel>(orig);
        new_collection.Add(channel);
        return new_collection;
      });
      channel.Start(source);
      if (ChannelAdded!=null) ChannelAdded(this, new ChannelChangedEventArgs(channel));
      if (yp!=null) yp.Announce(channel);
      return channel;
    }

    /// <summary>
    /// 指定されたチャンネルをチャンネル一覧に追加します
    /// </summary>
    /// <param name="channel">追加するチャンネル</param>
    public void AddChannel(Channel channel)
    {
      ReplaceCollection(ref channels, orig => {
        var new_channels = new List<Channel>(orig);
        new_channels.Add(channel);
        return new_channels;
      });
    }

    /// <summary>
    /// 指定したチャンネルをチャンネルリストから取り除きます
    /// </summary>
    /// <param name="channel"></param>
    public void CloseChannel(Channel channel)
    {
      channel.Close();
      ReplaceCollection(ref channels, orig => {
        var new_channels = new List<Channel>(orig);
        new_channels.Remove(channel);
        return new_channels;
      });
      logger.Debug("Channel Removed: {0}", channel.ChannelID.ToString("N"));
      if (ChannelRemoved!=null) ChannelRemoved(this, new ChannelChangedEventArgs(channel));
    }

    /// <summary>
    /// 指定されたプロトコル、名前、URIを使って新しいYPを作成しYPリストに追加します
    /// </summary>
    /// <param name="protocol">YPクライアントのプロトコル名</param>
    /// <param name="name">YPの名前</param>
    /// <param name="uri">YPのURI</param>
    public IYellowPageClient AddYellowPage(string protocol, string name, Uri uri)
    {
      IYellowPageClient yp = null;
      foreach (var factory in YellowPageFactories) {
        if (factory.Protocol==protocol) {
          yp = factory.Create(name, uri);
          break;
        }
      }
      if (yp==null) {
        throw new ArgumentException(String.Format("Protocol `{0}' is not found", protocol));
      }
      ReplaceCollection(ref yellowPages, orig => {
        var new_yps = new List<IYellowPageClient>(orig);
        new_yps.Add(yp);
        return new_yps;
      });
      logger.Debug("YP Added: {0}", yp.Name);
      if (YellowPagesChanged!=null) YellowPagesChanged(this, new EventArgs());
      return yp;
    }

    /// <summary>
    /// 指定したYPをYPリストから取り除きます
    /// </summary>
    /// <param name="yp">取り除くYP</param>
    public void RemoveYellowPage(IYellowPageClient yp)
    {
      yp.StopAnnounce();
      ReplaceCollection(ref yellowPages, orig => {
        var new_yps = new List<IYellowPageClient>(orig);
        new_yps.Remove(yp);
        return new_yps;
      });
      logger.Debug("YP Removed: {0}", yp.Name);
      if (YellowPagesChanged!=null) YellowPagesChanged(this, new EventArgs());
    }

    /// <summary>
    /// PeerCastを初期化します
    /// </summary>
    public PeerCast()
    {
      logger.Info("Starting PeerCast");
      this.AccessController = new AccessController(this);
      var filever = System.Diagnostics.FileVersionInfo.GetVersionInfo(
        System.Reflection.Assembly.GetExecutingAssembly().Location);
      this.AgentName = String.Format("{0}/{1}", filever.ProductName, filever.ProductVersion);
      this.SessionID = Guid.NewGuid();
      var bcid = AtomCollectionExtensions.IDToByteArray(Guid.NewGuid());
      bcid[0] = 0x00;
      this.BroadcastID = AtomCollectionExtensions.ByteArrayToID(bcid);
      logger.Debug("SessionID: {0}",   this.SessionID.ToString("N"));
      logger.Debug("BroadcastID: {0}", this.BroadcastID.ToString("N"));
      this.GlobalAddress = null;
      this.GlobalAddress6 = null;
      this.IsFirewalled = null;
      this.YellowPageFactories = new List<IYellowPageClientFactory>();
      this.SourceStreamFactories = new List<ISourceStreamFactory>();
      this.OutputStreamFactories = new List<IOutputStreamFactory>();
      this.ContentReaderFactories = new List<IContentReaderFactory>();
      this.ChannelMonitors = new List<IChannelMonitor>();
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
      if (this.LocalAddress==null)  this.LocalAddress  = IPAddress.Loopback;
      if (this.LocalAddress6==null) this.LocalAddress6 = IPAddress.IPv6Loopback;
      StartMonitor();
    }

    private AutoResetEvent stoppedEvent = new AutoResetEvent(false);
    private RegisteredWaitHandle monitorThreadPool = null;
    private void StartMonitor()
    {
      monitorThreadPool = ThreadPool.RegisterWaitForSingleObject(stoppedEvent, (state,timed_out) => {
        if (!timed_out) {
          monitorThreadPool.Unregister(stoppedEvent);
        }
        else {
          lock (ChannelMonitors) {
            foreach (var monitor in ChannelMonitors) {
              monitor.OnTimer();
            }
          }
        }
      }, null, 5000, false);
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
    /// <param name="local_accepts">リンクローカルな接続相手に許可する出力ストリームタイプ</param>
    /// <param name="global_accepts">リンクグローバルな接続相手に許可する出力ストリームタイプ</param>
    /// <returns>接続待ち受け</returns>
    /// <exception cref="System.Net.Sockets.SocketException">待ち受けが開始できませんでした</exception>
    /// <remarks>WANへのリレーが許可されておりIsFirewalledがtrueだった場合にはnullにリセットします</remarks>
    public OutputListener StartListen(IPEndPoint ip, OutputStreamType local_accepts, OutputStreamType global_accepts)
    {
      OutputListener res = null;
      logger.Info("starting listen at {0}", ip);
      try {
        res = new OutputListener(this, ip, local_accepts, global_accepts);
        ReplaceCollection(ref outputListeners, orig => {
          var new_collection = new List<OutputListener>(orig);
          new_collection.Add(res);
          return new_collection;
        });
        if ((global_accepts & OutputStreamType.Relay)!=0) {
          OnListenPortOpened();
        }
      }
      catch (System.Net.Sockets.SocketException e) {
        logger.Error("Listen failed: {0}", ip);
        logger.Error(e);
        throw;
      }
      return res;
    }

    /// <summary>
    /// 指定した接続待ち受けを終了します。
    /// 既に接続されているクライアント接続には影響ありません
    /// </summary>
    /// <param name="listener">待ち受けを終了するリスナ</param>
    /// <remarks>指定したリスナのWANへのリレーが許可されておりIsFirewalledがfalseだった場合にはnullにリセットします</remarks>
    public void StopListen(OutputListener listener)
    {
      listener.Stop();
      ReplaceCollection(ref outputListeners, orig => {
        var new_collection = new List<OutputListener>(orig);
        new_collection.Remove(listener);
        return new_collection;
      });
      if ((listener.GlobalOutputAccepts & OutputStreamType.Relay)!=0) {
        OnListenPortClosed();
      }
    }

    internal void OnListenPortOpened()
    {
      if (IsFirewalled.HasValue && IsFirewalled.Value==true) {
        IsFirewalled = null;
      }
    }

    internal void OnListenPortClosed()
    {
      if (IsFirewalled.HasValue && IsFirewalled.Value==false) {
        IsFirewalled = null;
      }
    }

    public IPEndPoint GetGlobalEndPoint(AddressFamily addr_family, OutputStreamType connection_type)
    {
      var listener = outputListeners.FirstOrDefault(
        x => x.LocalEndPoint.AddressFamily==addr_family &&
             (x.GlobalOutputAccepts & connection_type)!=0);
      var addr = addr_family==AddressFamily.InterNetwork ? GlobalAddress : GlobalAddress6;
      if (listener!=null && addr!=null) {
        return new IPEndPoint(addr, listener.LocalEndPoint.Port);
      }
      return null;
    }

    public IPEndPoint GetLocalEndPoint(AddressFamily addr_family, OutputStreamType connection_type)
    {
      var listener = outputListeners.FirstOrDefault(
        x =>  x.LocalEndPoint.AddressFamily==addr_family &&
             (x.LocalOutputAccepts & connection_type)!=0);
      var addr = addr_family==AddressFamily.InterNetwork ? LocalAddress : LocalAddress6;
      if (listener!=null) {
        return new IPEndPoint(addr, listener.LocalEndPoint.Port);
      }
      return null;
    }

    public IPEndPoint GetEndPoint(IPAddress remote_addr, OutputStreamType connection_type)
    {
      if (remote_addr==null) throw new ArgumentNullException("remote_addr");
      if (remote_addr.IsSiteLocal()) {
        return GetLocalEndPoint(remote_addr.AddressFamily, connection_type);
      }
      else {
        return GetGlobalEndPoint(remote_addr.AddressFamily, connection_type);
      }
    }

    public OutputListener FindListener(IPAddress remote_addr, OutputStreamType connection_type)
    {
      if (remote_addr==null) throw new ArgumentNullException("remote_addr");
      if (remote_addr.IsSiteLocal()) {
        var listener = outputListeners.FirstOrDefault(
          x =>  x.LocalEndPoint.AddressFamily==remote_addr.AddressFamily &&
               (x.LocalOutputAccepts & connection_type)!=0);
        return listener;
      }
      else {
        var listener = outputListeners.FirstOrDefault(
          x => x.LocalEndPoint.AddressFamily==remote_addr.AddressFamily &&
               (x.GlobalOutputAccepts & connection_type)!=0);
        return listener;
      }
    }

    /// <summary>
    /// 待ち受けと全てのチャンネルを終了します
    /// </summary>
    public void Stop()
    {
      logger.Info("Stopping PeerCast");
      stoppedEvent.Set();
      foreach (var listener in outputListeners) {
        listener.Stop();
      }
      foreach (var channel in channels) {
        channel.Close();
        if (ChannelRemoved!=null) ChannelRemoved(this, new ChannelChangedEventArgs(channel));
      }
      foreach (var ypclient in yellowPages) {
        ypclient.StopAnnounce();
      }
      outputListeners = new List<OutputListener>();
      channels = new List<Channel>();
      uptime.Stop();
      logger.Info("PeerCast Stopped");
    }

    private static Logger logger = new Logger(typeof(PeerCast));
  }
}
