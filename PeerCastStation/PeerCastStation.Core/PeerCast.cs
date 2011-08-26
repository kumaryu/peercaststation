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
    /// 登録されているYellowPageリストを取得および設定します
    /// 取得は読み取り専用のリストを、設定は指定したリストのコピーを設定します
    /// </summary>
    public IList<IYellowPageClient> YellowPages {
      get { return yellowPages.AsReadOnly(); }
      set {
        Utils.ReplaceCollection(ref yellowPages, org => {
          return new List<IYellowPageClient>(value);
        });
        YellowPagesChanged(this, new EventArgs());
      }
    }
    private List<IYellowPageClient> yellowPages = new List<IYellowPageClient>();

    /// <summary>
    /// YPリストが変更された時に呼び出されます。
    /// </summary>
    public event EventHandler YellowPagesChanged;

    /// <summary>
    /// 登録されているYellowPageファクトリのリストを取得します
    /// </summary>
    public IList<IYellowPageClientFactory> YellowPageFactories { get; private set; }
    /// <summary>
    /// 登録されているSourceStreamのプロトコルとファクトリの辞書を取得します
    /// </summary>
    public IDictionary<string, ISourceStreamFactory> SourceStreamFactories { get; private set; }
    /// <summary>
    /// 登録されているOutputStreamのリストを取得します
    /// </summary>
    public IList<IOutputStreamFactory> OutputStreamFactories { get; private set; }
    /// <summary>
    /// 接続しているチャンネルの読み取り専用リストを取得します
    /// </summary>
    public ReadOnlyCollection<Channel> Channels { get { return channels.AsReadOnly(); } }
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
      Channel channel = null;
      logger.Debug("Requesting channel {0} from {1}", channel_id.ToString("N"), tracker);
      ISourceStreamFactory source_factory = null;
      if (!SourceStreamFactories.TryGetValue(tracker.Scheme, out source_factory)) {
        logger.Error("Protocol `{0}' is not found", tracker.Scheme);
        throw new ArgumentException(String.Format("Protocol `{0}' is not found", tracker.Scheme));
      }
      channel = new Channel(this, channel_id, tracker);
      Utils.ReplaceCollection(ref channels, orig => {
        var new_collection = new List<Channel>(orig);
        new_collection.Add(channel);
        return new_collection;
      });
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
      Channel res = channels.FirstOrDefault(c => c.ChannelID==channel_id);
      if (res==null && request_relay) {
        if (tracker!=null) {
          res = RelayChannel(channel_id, tracker);
        }
        else {
          res = RelayChannel(channel_id);
        }
      }
      return res;
    }

    /// <summary>
    /// 配信を開始します。
    /// </summary>
    /// <param name="yp">チャンネル情報を載せるYellowPage</param>
    /// <param name="channel_id">チャンネルID</param>
    /// <param name="channel_info">チャンネル情報</param>
    /// <param name="source">配信ソース</param>
    /// <param name="content_reader">配信ソースのコンテンツを解析するContentReader</param>
    /// <returns>Channelのインスタンス</returns>
    public Channel BroadcastChannel(IYellowPageClient yp, Guid channel_id, ChannelInfo channel_info, Uri source, IContentReader content_reader)
    {
      Channel channel = null;
      logger.Debug("Broadcasting channel {0} from {1}", channel_id.ToString("N"), source);
      ISourceStreamFactory source_factory = null;
      if (!SourceStreamFactories.TryGetValue(source.Scheme, out source_factory)) {
        logger.Error("Protocol `{0}' is not found", source.Scheme);
        throw new ArgumentException(String.Format("Protocol `{0}' is not found", source.Scheme));
      }
      channel = new Channel(this, channel_id, source);
      Utils.ReplaceCollection(ref channels, orig => {
        var new_collection = new List<Channel>(orig);
        new_collection.Add(channel);
        return new_collection;
      });
      channel.ChannelInfo = channel_info;
      var source_stream = source_factory.Create(channel, source, content_reader);
      channel.Start(source_stream);
      if (ChannelAdded!=null) ChannelAdded(this, new ChannelChangedEventArgs(channel));
      //TODO: ypにアナウンス開始
      return channel;
    }

    /// <summary>
    /// 指定されたチャンネルをチャンネル一覧に追加します
    /// </summary>
    /// <param name="channel">追加するチャンネル</param>
    public void AddChannel(Channel channel)
    {
      Utils.ReplaceCollection(ref channels, orig => {
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
      Utils.ReplaceCollection(ref channels, orig => {
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
        if (factory.Name==protocol) {
          yp = factory.Create(name, uri);
          break;
        }
      }
      if (yp==null) {
        throw new ArgumentException(String.Format("Protocol `{0}' is not found", protocol));
      }
      Utils.ReplaceCollection(ref yellowPages, orig => {
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
      Utils.ReplaceCollection(ref yellowPages, orig => {
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
      this.SessionID   = Guid.NewGuid();
      this.BroadcastID = Guid.NewGuid();
      logger.Debug("SessionID: {0}",   this.SessionID.ToString("N"));
      logger.Debug("BroadcastID: {0}", this.BroadcastID.ToString("N"));
      this.GlobalAddress = null;
      this.GlobalAddress6 = null;
      this.IsFirewalled = null;
      this.YellowPageFactories = new List<IYellowPageClientFactory>();
      this.SourceStreamFactories = new Dictionary<string, ISourceStreamFactory>();
      this.OutputStreamFactories = new List<IOutputStreamFactory>();
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
      OutputListener res = null;
      logger.Info("starting listen at {0}", ip);
      try {
        res = new OutputListener(this, ip);
        Utils.ReplaceCollection(ref outputListeners, orig => {
          var new_collection = new List<OutputListener>(orig);
          new_collection.Add(res);
          return new_collection;
        });
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
    public void StopListen(OutputListener listener)
    {
      listener.Stop();
      Utils.ReplaceCollection(ref outputListeners, orig => {
        var new_collection = new List<OutputListener>(orig);
        new_collection.Remove(listener);
        return new_collection;
      });
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
    public void Stop()
    {
      logger.Info("Stopping PeerCast");
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
      logger.Info("PeerCast Stopped");
    }

    private static Logger logger = new Logger(typeof(PeerCast));
  }
}
