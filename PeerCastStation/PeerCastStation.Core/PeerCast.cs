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
using System.Threading.Tasks;
using System.Linq;
using PeerCastStation.Core.Http;

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

  public enum PortStatus {
    Unavailable = 0,
    Unknown     = 1,
    Firewalled  = 2,
    Open        = 3,
  }

  /// <summary>
  /// PeerCastStationの主要な動作を行ない、管理するクラスです
  /// </summary>
  public class PeerCast
    : IDisposable
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

    private void ReplaceCollection<T>(ref T[] collection, Func<T[], T[]> newcollection_func)
    {
      bool replaced;
      do {
        var orig = collection;
        replaced = Interlocked.CompareExchange(ref collection, newcollection_func(orig), orig)==orig;
      } while (!replaced);
    }

    /// <summary>
    /// 登録されているYellowPageリストを取得および設定します
    /// 取得は読み取り専用のリストを、設定は指定したリストのコピーを設定します
    /// </summary>
    public IReadOnlyList<IYellowPageClient> YellowPages { get { return yellowPages; } }
    private IYellowPageClient[] yellowPages = Array.Empty<IYellowPageClient>();

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
    public IList<Action<IAppBuilder>> HttpApplicationFactories { get; private set; } = new List<Action<IAppBuilder>>();
    public IList<IContentFilter> ContentFilters { get; private set; }
    /// <summary>
    /// 接続しているチャンネルの読み取り専用リストを取得します
    /// </summary>
    public IReadOnlyList<Channel> Channels { get { return channels; } }
    private Channel[] channels = Array.Empty<Channel>();

    /// <summary>
    /// 監視オブジェクトのリストを取得します
    /// </summary>
    public IReadOnlyList<IPeerCastMonitor> Monitors { get { return monitors; } }
    private IPeerCastMonitor[] monitors = Array.Empty<IPeerCastMonitor>();
    private readonly Timer monitorTimer;

    private CancellationTokenSource cancelSource = new CancellationTokenSource();
    private Task monitorTask = Task.Delay(0);

    /// <summary>
    /// チャンネルへのアクセス制御を行なうクラスの取得および設定をします
    /// </summary>
    public AccessController AccessController { get; set; }

    private OutputListener[] outputListeners = Array.Empty<OutputListener>();
    /// <summary>
    /// 接続待ち受けスレッドのコレクションを取得します
    /// </summary>
    public IReadOnlyList<OutputListener> OutputListeners { get { return outputListeners; } }

    /// <summary>
    /// チャンネルIDを指定してチャンネルのリレーを開始します。
    /// 接続先はYellowPageに問い合わせ取得します。
    /// </summary>
    /// <param name="channel_id">リレーを開始するチャンネルID</param>
    /// <returns>接続先が見付かった場合はChannelのインスタンス、それ以外はnull</returns>
    public Channel? RelayChannel(Guid channel_id)
    {
      Channel? result = null;
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

    private NetworkType GetNetworkTypeFromUri(Uri uri)
    {
      switch (uri.HostNameType) {
      case UriHostNameType.IPv4:
        return NetworkType.IPv4;
      case UriHostNameType.IPv6:
        return NetworkType.IPv6;
      default:
        throw new ArgumentException("Address must be IPv4 or IPv6 host", "uri");
      }
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
      var channel = new RelayChannel(this, GetNetworkTypeFromUri(tracker), channel_id);
      channel.Start(tracker);
      ReplaceCollection(ref channels, orig => orig.Add(channel));
      DispatchMonitorEvent(mon => mon.OnChannelChanged(PeerCastChannelAction.Added, channel));
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
    public virtual Channel? RequestChannel(Guid channel_id, Uri tracker, bool request_relay)
    {
      Channel? channel = channels.FirstOrDefault(c => c.ChannelID==channel_id);
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
      NetworkType           network,
      IYellowPageClient     yp,
      Guid                  channel_id,
      ChannelInfo           channel_info,
      Uri                   source,
      ISourceStreamFactory  source_stream_factory,
      IContentReaderFactory content_reader_factory)
    {
      logger.Debug("Broadcasting channel {0} from {1}", channel_id.ToString("N"), source);
      var channel = new BroadcastChannel(this, network, channel_id, channel_info, source_stream_factory, content_reader_factory);
      channel.Start(source);
      ReplaceCollection(ref channels, orig => orig.Add(channel));
      DispatchMonitorEvent(mon => mon.OnChannelChanged(PeerCastChannelAction.Added, channel));
      if (yp!=null) yp.Announce(channel);
      return channel;
    }

    /// <summary>
    /// 指定されたチャンネルをチャンネル一覧に追加します
    /// </summary>
    /// <param name="channel">追加するチャンネル</param>
    public void AddChannel(Channel channel)
    {
      ReplaceCollection(ref channels, orig => orig.Add(channel));
    }

    /// <summary>
    /// 指定したチャンネルをチャンネルリストから取り除きます
    /// </summary>
    /// <param name="channel"></param>
    public void CloseChannel(Channel channel)
    {
      channel.Close();
      ReplaceCollection(ref channels, orig => orig.Remove(channel));
      logger.Debug("Channel Removed: {0}", channel.ChannelID.ToString("N"));
      DispatchMonitorEvent(mon => mon.OnChannelChanged(PeerCastChannelAction.Removed, channel));
    }

    /// <summary>
    /// 指定されたプロトコル、名前、URIを使って新しいYPを作成しYPリストに追加します
    /// </summary>
    /// <param name="protocol">YPクライアントのプロトコル名</param>
    /// <param name="name">YPの名前</param>
    /// <param name="announce_uri">YPの掲載先URI</param>
    /// <param name="channels_uri">YPのチャンネル一覧取得先URI</param>
    public IYellowPageClient AddYellowPage(string protocol, string name, Uri announce_uri, Uri channels_uri)
    {
      IYellowPageClient? yp = null;
      foreach (var factory in YellowPageFactories) {
        if (factory.Protocol==protocol) {
          yp = factory.Create(name, announce_uri, channels_uri);
          break;
        }
      }
      if (yp==null) {
        throw new ArgumentException(String.Format("Protocol `{0}' is not found", protocol));
      }
      ReplaceCollection(ref yellowPages, orig => orig.Add(yp));
      logger.Debug("YP Added: {0}", yp.Name);
      return yp;
    }

    /// <summary>
    /// 指定したYPをYPリストから取り除きます
    /// </summary>
    /// <param name="yp">取り除くYP</param>
    public void RemoveYellowPage(IYellowPageClient yp)
    {
      yp.StopAnnounce();
      ReplaceCollection(ref yellowPages, orig => orig.Remove(yp));
      logger.Debug("YP Removed: {0}", yp.Name);
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
      this.YellowPageFactories = new List<IYellowPageClientFactory>();
      this.SourceStreamFactories = new List<ISourceStreamFactory>();
      this.OutputStreamFactories = new List<IOutputStreamFactory>();
      this.ContentReaderFactories = new List<IContentReaderFactory>();
      this.ContentFilters = new List<IContentFilter>();
      this.monitorTimer = new Timer(OnMonitorTimer, cancelSource.Token, 0, 5000);
    }

    private void OnMonitorTimer(object state)
    {
      var cs = (CancellationToken)state;
      if (!cs.IsCancellationRequested) {
        DispatchMonitorEvent(mon => mon.OnTimer(), cs);
      }
    }

    private void DispatchMonitorEvent(Action<IPeerCastMonitor> monitorAction)
    {
      var monitors = this.monitors;
      monitorTask = monitorTask.ContinueWith(prev => {
        monitors.AsParallel().ForAll(monitorAction);
      });
    }

    private void DispatchMonitorEvent(Action<IPeerCastMonitor> monitorAction, CancellationToken cs)
    {
      var monitors = this.monitors;
      monitorTask = monitorTask.ContinueWith(prev => {
        monitors.AsParallel().ForAll(monitorAction);
      }, cs);
    }

		public void AddChannelMonitor(IPeerCastMonitor monitor)
		{
			ReplaceCollection(ref monitors, orig => orig.Add(monitor));
		}

		public void RemoveChannelMonitor(IPeerCastMonitor monitor)
		{
			ReplaceCollection(ref monitors, orig => orig.Remove(monitor));
		}

    private async Task StartMonitor(CancellationToken cancel_token)
    {
      try {
        while (!cancel_token.IsCancellationRequested) {
          foreach (var monitor in monitors) {
            monitor.OnTimer();
          }
          await Task.Delay(5000, cancel_token).ConfigureAwait(false);
        }
      }
      catch (OperationCanceledException) {
      }
    }

    private IPAddress GetInterfaceAddress(AddressFamily addr_family)
    {
      return System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
        .Where(intf => intf.OperationalStatus==System.Net.NetworkInformation.OperationalStatus.Up)
        .Where(intf => intf.NetworkInterfaceType!=System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
        .Select(intf => intf.GetIPProperties())
        .SelectMany(prop => prop.UnicastAddresses)
        .Where(uaddr => uaddr.Address.AddressFamily==addr_family)
        .Select(uaddr => uaddr.Address)
        .FirstOrDefault();
    }

    public Guid SessionID { get; private set; }
    public Guid BroadcastID { get; set; }

    public PortStatus GetPortStatus(NetworkType type)
    {
      return GetPortStatus(type.GetAddressFamily());
    }

    public PortStatus GetPortStatus(AddressFamily family)
    {
      return outputListeners
          .Where(port => port.LocalEndPoint.AddressFamily==family)
          .Where(port => port.GlobalOutputAccepts.HasFlag(OutputStreamType.Relay))
          .Select(port => port.Status)
          .OrderByDescending(status => (int)status)
          .FirstOrDefault();
    }

    private bool IsIPAddressMatch(IPAddress a, IPAddress b)
    {
      if (a==null || b==null) return false;
      if (a.AddressFamily!=b.AddressFamily) return false;
      if (a.Equals(IPAddress.Any) || b.Equals(IPAddress.Any) ||
          a.Equals(IPAddress.IPv6Any) || b.Equals(IPAddress.IPv6Any)) {
        return true;
      }
      return a.Equals(b);
    }

    public void SetPortStatus(IPAddress localAddress, IPAddress globalAddress, PortStatus value)
    {
      foreach (var listener in OutputListeners.Where(port => IsIPAddressMatch(port.LocalEndPoint.Address, localAddress))) {
        if (globalAddress!=null) {
          listener.GlobalAddress = globalAddress;
        }
        listener.Status = value;
      }
    }

    private IPAddress? localAddressV4 = null;
    private IPAddress GetLocalAddressV4()
    {
      if (localAddressV4!=null) return localAddressV4;
      var addr = GetInterfaceAddress(AddressFamily.InterNetwork);
      if (addr!=null) {
        this.localAddressV4 = addr;
      }
      else {
        this.localAddressV4 = IPAddress.Loopback;
      }
      logger.Info("IPv4 LocalAddress: {0}", this.localAddressV4);
      return this.localAddressV4;
    }

    private IPAddress? localAddressV6 = null;
    private IPAddress GetLocalAddressV6()
    {
      if (localAddressV6!=null) return localAddressV6;
      var addr = GetInterfaceAddress(AddressFamily.InterNetworkV6);
      if (addr!=null) {
        this.localAddressV6 = addr;
      }
      else {
        this.localAddressV6 = IPAddress.IPv6Loopback;
      }
      logger.Info("IPv6 LocalAddress: {0}", this.localAddressV6);
      return this.localAddressV6;
    }

    public IPAddress GetLocalAddress(AddressFamily family)
    {
      switch (family) {
      case AddressFamily.InterNetwork:
        return GetLocalAddressV4();
      case AddressFamily.InterNetworkV6:
        return GetLocalAddressV6();
      default:
        throw new NotSupportedException();
      }
    }

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
      OutputListener? res = null;
      logger.Info("starting listen at {0}", ip);
      try {
        res = new OutputListener(this, new ConnectionHandler(this), ip, local_accepts, global_accepts);
        ReplaceCollection(ref outputListeners, orig => orig.Add(res));
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
      ReplaceCollection(ref outputListeners, orig => orig.Remove(listener));
    }

    public IPEndPoint? GetGlobalEndPoint(AddressFamily addr_family, OutputStreamType connection_type)
    {
      var listener = outputListeners.FirstOrDefault(
        x => x.LocalEndPoint.AddressFamily==addr_family &&
             (x.GlobalOutputAccepts & connection_type)!=0);
      var addr = listener.GlobalAddress;
      if (listener!=null && addr!=null) {
        return new IPEndPoint(addr, listener.LocalEndPoint.Port);
      }
      return null;
    }

    public IPEndPoint? GetLocalEndPoint(AddressFamily addr_family, OutputStreamType connection_type)
    {
      var listener = outputListeners.FirstOrDefault(
        x =>  x.LocalEndPoint.AddressFamily==addr_family &&
             (x.LocalOutputAccepts & connection_type)!=0);
      if (listener!=null) {
        return new IPEndPoint(GetLocalAddress(addr_family), listener.LocalEndPoint.Port);
      }
      return null;
    }

    public OutputListener FindListener(IPAddress remote_addr, OutputStreamType connection_type)
    {
      if (remote_addr==null) throw new ArgumentNullException("remote_addr");
      return FindListener(remote_addr.AddressFamily, remote_addr, connection_type);
    }

    public OutputListener FindListener(AddressFamily family, IPAddress remote_addr, OutputStreamType connection_type)
    {
      if (remote_addr==null) throw new ArgumentNullException("remote_addr");
      if (remote_addr.IsSiteLocal()) {
        var listener = outputListeners.FirstOrDefault(
          x =>  x.LocalEndPoint.AddressFamily==family &&
               (x.LocalOutputAccepts & connection_type)!=0);
        return listener;
      }
      else {
        var listener = outputListeners.FirstOrDefault(
          x => x.LocalEndPoint.AddressFamily==family &&
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
      cancelSource.Cancel();
      monitorTimer.Dispose();
      foreach (var listener in outputListeners) {
        listener.Stop();
      }
      foreach (var channel in channels) {
        channel.Close();
        DispatchMonitorEvent(mon => mon.OnChannelChanged(PeerCastChannelAction.Removed, channel));
      }
      foreach (var ypclient in yellowPages) {
        ypclient.StopAnnounce();
      }
      outputListeners = outputListeners.Clear();
      channels = channels.Clear();
      uptime.Stop();
      logger.Info("PeerCast Stopped");
    }

    public void Dispose()
    {
      if (cancelSource.IsCancellationRequested) return;
      Stop();
    }

    private static Logger logger = new Logger(typeof(PeerCast));
  }
}
