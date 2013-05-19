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
using System.IO;
using System.Net;

namespace PeerCastStation.Core
{
  /// <summary>
  /// YellowPageへの掲載状況を表します
  /// </summary>
  public enum AnnouncingStatus {
    /// <summary>
    /// 未接続
    /// </summary>
    Idle,
    /// <summary>
    /// 接続試行中
    /// </summary>
    Connecting,
    /// <summary>
    /// 接続中
    /// </summary>
    Connected,
    /// <summary>
    /// エラーにより切断された
    /// </summary>
    Error,
  }

  /// <summary>
  /// YellowPageへ掲載中のチャンネル状態を取得するインターフェースです
  /// </summary>
  public interface IAnnouncingChannel
  {
    /// <summary>
    /// 掲載しようとしているチャンネルを取得します
    /// </summary>
    Channel           Channel    { get; }
    /// <summary>
    /// YellowPageへの接続状況を取得します
    /// </summary>
    AnnouncingStatus  Status     { get; }
    /// <summary>
    /// 掲載するYellowPageを取得します
    /// </summary>
    IYellowPageClient YellowPage { get; }
  }

  /// <summary>
  /// YellowPageとやりとりするクライアントのインターフェースです
  /// </summary>
  public interface IYellowPageClient
  {
    /// <summary>
    /// YelloePageに関連付けられた名前を取得します
    /// </summary>
    string Name { get; }
    /// <summary>
    /// YelloePageの通信プロトコルを取得します
    /// </summary>
    string Protocol { get; }
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
    /// YellowPageにチャンネルを載せます
    /// </summary>
    /// <param name="channel">載せるチャンネル</param>
    /// <returns>掲載するチャンネルの状態を保持するオブジェクト</returns>
    IAnnouncingChannel Announce(Channel channel);

    /// <summary>
    /// YellowPageとの接続を終了し、載せているチャンネルを全て削除します
    /// </summary>
    void StopAnnounce();

    /// <summary>
    /// 指定したチャンネルの掲載を停止します
    /// </summary>
    /// <param name="announcing">掲載を停止するチャンネル</param>
    void StopAnnounce(IAnnouncingChannel announcing);

    /// <summary>
    /// 全てのチャンネルの再掲載を試みます
    /// </summary>
    void RestartAnnounce();

    /// <summary>
    /// 指定したチャンネルの再掲載を試みます
    /// </summary>
    /// <param name="announcing">再掲載を行なうチャンネル</param>
    void RestartAnnounce(IAnnouncingChannel announcing);

    /// <summary>
    /// YellowPageに掲載しようとしているチャンネルの一覧を取得します
    /// </summary>
    IList<IAnnouncingChannel> AnnouncingChannels { get; }

    /// <summary>
    /// 現在の接続情報を取得します
    /// </summary>
    /// <returns>呼び出した時点の接続先情報</returns>
    ConnectionInfo GetConnectionInfo();
  }

  /// <summary>
  /// YellowPageクライアントのインスタンスを作成するためのファクトリインターフェースです
  /// </summary>
  public interface IYellowPageClientFactory
  {
    /// <summary>
    /// このYellowPageFactoryが扱うプロトコルの表示名を取得します
    /// </summary>
    string Name { get; }
    /// <summary>
    /// このYellowPageFactoryが扱うプロトコルの識別子を取得します
    /// </summary>
    string Protocol { get; }
    /// <summary>
    /// YellowPageクライアントインスタンスを作成し返します
    /// </summary>
    /// <param name="name">YellowPageに関連付けられる名前</param>
    /// <param name="uri">YellowPageのURI</param>
    /// <returns>IYellowPageClientを実装するオブジェクトのインスタンス</returns>
    IYellowPageClient Create(string name, Uri uri);
    /// <summary>
    /// URIがこのYellowPageFactoryで扱えるかどうかを返します
    /// </summary>
    /// <param name="uri">チェックするURI</param>
    /// <returns>扱えるURIの場合はtrue、それ以外はfalse</returns>
    bool CheckURI(Uri uri);
  }

  [Flags]
  public enum ConnectionType
  {
    /// <summary>
    /// 指定無し
    /// </summary>
    None = 0x00,
    /// <summary>
    /// 視聴出力用接続
    /// </summary>
    Direct = 0x01,
    /// <summary>
    /// リレー出力用接続
    /// </summary>
    Relay = 0x02,
    /// <summary>
    /// メタデータ出力用接続
    /// </summary>
    Metadata = 0x04,
    /// <summary>
    /// ユーザインターフェース出力用接続
    /// </summary>
    Interface = 0x08,
    /// <summary>
    /// YellowPageへの掲載用接続
    /// </summary>
    Announce = 0x10,
    /// <summary>
    /// リレー・コンテンツ受信接続
    /// </summary>
    Source = 0x20,
  }

  public enum ConnectionStatus
  {
    /// <summary>
    /// 接続されていません
    /// </summary>
    Idle,
    /// <summary>
    /// 接続先の探索および接続を試みています
    /// </summary>
    Connecting,
    /// <summary>
    /// 接続しています
    /// </summary>
    Connected,
    /// <summary>
    /// エラー発生のため切断しました
    /// </summary>
    Error,
  }

  [Flags]
  public enum RemoteHostStatus
  {
    None       = 0x00,
    Local      = 0x01,
    Firewalled = 0x02,
    RelayFull  = 0x04,
    Receiving  = 0x08,
    Root       = 0x10,
    Tracker    = 0x20,
  }

  /// <summary>
  /// ストリームが終了された原因を表します
  /// </summary>
  public enum StopReason
  {
    None,
    Any,
    UserShutdown,
    UserReconnect,
    NoHost,
    OffAir,
    ConnectionError,
    NotIdentifiedError,
    BadAgentError,
    UnavailableError,
  }

  /// <summary>
  /// ストリームが終了した時に呼ばれるイベントの引数です
  /// </summary>
  public class StreamStoppedEventArgs
    : EventArgs
  {
    /// <summary>
    /// ストリームが終了された原因を取得します
    /// </summary>
    public StopReason StopReason { get; private set; }
    public StreamStoppedEventArgs(StopReason reason)
    {
      this.StopReason = reason;
    }
  }
  /// <summary>
  /// ストリームが終了した時に呼ばれるイベントを表します
  /// </summary>
  /// <param name="sender">終了したストリーム</param>
  /// <param name="args">終了した原因を含む引数</param>
  public delegate void StreamStoppedEventHandler(object sender, StreamStoppedEventArgs args);

  public class ConnectionInfo
  {
    public string     ProtocolName    { get; private set; }
    public ConnectionType   Type      { get; private set; }
    public ConnectionStatus Status    { get; private set; }
    public IPEndPoint RemoteEndPoint  { get; private set; }
    public RemoteHostStatus RemoteHostStatus { get; private set; }
    public long?      ContentPosition { get; private set; }
    public float?     RecvRate        { get; private set; }
    public float?     SendRate        { get; private set; }
    public int?       LocalRelays     { get; private set; }
    public int?       LocalDirects    { get; private set; }
    public string     AgentName       { get; private set; }
    public string     RemoteName      { get; private set; }
    public ConnectionInfo(
      string           protocol_name,
      ConnectionType   type,
      ConnectionStatus status,
      string           remote_name,
      IPEndPoint       remote_endpoint,
      RemoteHostStatus remote_host_status,
      long?      content_position,
      float?     recv_rate,
      float?     send_rate,
      int?       local_relays,
      int?       local_directs,
      string     agent_name)
    {
      ProtocolName     = protocol_name;
      Type             = type;
      Status           = status;
      RemoteName       = remote_name;
      RemoteEndPoint   = remote_endpoint;
      RemoteHostStatus = remote_host_status;
      ContentPosition  = content_position;
      RecvRate         = recv_rate;
      SendRate         = send_rate;
      LocalRelays      = local_relays;
      LocalDirects     = local_directs;
      AgentName        = agent_name;
    }
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
    Receiving,
    /// <summary>
    /// エラー発生のため切断しました
    /// </summary>
    Error,
  }

  /// <summary>
  /// ISourceStream.StatusChangedイベントに渡される引数のクラスです
  /// </summary>
  [Serializable]
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
    /// 現在の接続を切って新しいソースへの接続を試みます。
    /// </summary>
    void Reconnect();
    /// <summary>
    /// 現在の接続を切って指定した新しいソースへの接続を試みます。
    /// </summary>
    void Reconnect(Uri source_uri);
    /// <summary>
    /// ストリームへパケットを送信します
    /// </summary>
    /// <param name="from">ブロードキャストパケットの送信元。無い場合はnull</param>
    /// <param name="packet">送信するデータ</param>
    void Post(Host from, Atom packet);
    /// <summary>
    /// ストリームの取得を終了します
    /// </summary>
    void Stop();
    /// <summary>
    /// ストリームの現在の状態を取得します
    /// </summary>
    SourceStreamStatus Status { get; }
    /// <summary>
    /// 現在の接続情報を取得します
    /// </summary>
    /// <returns>呼び出した時点の接続先情報</returns>
    ConnectionInfo GetConnectionInfo();

    /// <summary>
    /// ストリームの状態が変更された時に呼ばれるイベントです
    /// </summary>
    event EventHandler<SourceStreamStatusChangedEventArgs> StatusChanged;
    /// <summary>
    /// ストリームの動作が終了した際に呼ばれるイベントです
    /// </summary>
    event StreamStoppedEventHandler Stopped;
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
    /// このSourceStreamFactoryが扱うプロトコルのURIスキームを取得します
    /// </summary>
    string Scheme { get; }
    /// <summary>
    /// URIからプロトコルを判別しSourceStreamのインスタンスを作成します。
    /// </summary>
    /// <param name="channel">所属するチャンネル</param>
    /// <param name="tracker">ストリーム取得起点のURI</param>
    /// <returns>プロトコルが適合していればSourceStreamのインスタンス、それ以外はnull</returns>
    /// <exception cref="NotImplentedException">このプロトコルではContentReaderを指定する必要がある</exception> 
    ISourceStream Create(Channel channel, Uri tracker);
    /// <summary>
    /// URIからプロトコルを判別し指定したIContentReaderでコンテンツを読み取るSourceStreamのインスタンスを作成します
    /// </summary>
    /// <param name="channel">所属するチャンネル</param>
    /// <param name="source">ストリーム取得起点のURI</param>
    /// <param name="reader_factory">ストリームからコンテンツを読み取るためのContentReaderインスタンス</param>
    /// <returns>プロトコルが適合していればSourceStreamのインスタンス、それ以外はnull</returns>
    /// <exception cref="NotImplentedException">このプロトコルではContentReaderを指定した読み取りができない</exception> 
    ISourceStream Create(Channel channel, Uri source, IContentReader reader);
  }

  /// <summary>
  /// OutputStreamの種類を表します
  /// </summary>
  [Flags]
  public enum OutputStreamType
  {
    /// <summary>
    /// 指定無し
    /// </summary>
    None = 0,
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
    /// <summary>
    /// ユーザインターフェース用出力ストリーム
    /// </summary>
    Interface = 8,
    /// <summary>
    /// 全て
    /// </summary>
    All = 0x7FFFFFFF,
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
    void Stop();
    /// <summary>
    /// ストリームへの書き込みを終了します
    /// </summary>
    /// <param name="reason">書き込み終了の理由</param>
    void Stop(StopReason reason);
    /// <summary>
    /// 出力ストリームの種類を取得します
    /// </summary>
    OutputStreamType OutputStreamType { get; }
    /// <summary>
    /// 現在の接続情報を取得します
    /// </summary>
    /// <returns>呼び出した時点の接続先情報</returns>
    ConnectionInfo GetConnectionInfo();
    /// <summary>
    /// 出力ストリームの動作が終了した際に呼ばれるイベントです
    /// </summary>
    event StreamStoppedEventHandler Stopped;
  }

  /// <summary>
  /// OutputStreamのインスタンスを作成するファクトリインターフェースです
  /// </summary>
  public interface IOutputStreamFactory
  {
    /// <summary>
    /// 作成されるOutputStreamが扱うプロトコルの名前を取得します
    /// </summary>
    string Name { get; }
    /// <summary>
    /// クライアントのリクエストをパースできるOutputStreamFactoryを検索する際の優先度を取得します
    /// </summary>
    /// <remarks>
    /// 値が小さいOutputStreamFactoryが先にリクエストのパースを試行します
    /// </remarks>
    int Priority { get; }
    /// <summary>
    /// 作成される出力ストリームの種類を取得します
    /// </summary>
    OutputStreamType OutputStreamType { get; }
    /// <summary>
    /// OutpuStreamのインスタンスを作成します
    /// </summary>
    /// <param name="input_stream">接続先の受信ストリーム</param>
    /// <param name="output_stream">接続先の送信ストリーム</param>
    /// <param name="remote_endpoint">接続先。無ければnull</param>
    /// <param name="channel_id">所属するチャンネルのチャンネルID</param>
    /// <param name="header">クライアントから受け取ったリクエスト</param>
    /// <returns>OutputStream</returns>
    IOutputStream Create(
      Stream input_stream,
      Stream output_stream,
      EndPoint remote_endpoint,
      Guid channel_id,
      byte[] header);
    /// <summary>
    /// クライアントのリクエストからチャンネルIDを取得し返します
    /// </summary>
    /// <param name="header">クライアントから受け取ったリクエスト</param>
    /// <returns>headerからチャンネルIDを取得できた場合はチャンネルID、できなかった場合はnull</returns>
    Guid? ParseChannelID(byte[] header);
  }

  /// <summary>
  /// IContentReaderで読み取られた結果を格納する構造体です
  /// </summary>
  public struct ParsedContent
  {
    /// <summary>
    /// チャンネル情報として設定するChannelInfoのインスタンスを格納します。設定する値が無い場合はnull
    /// </summary>
    public ChannelInfo ChannelInfo;
    /// <summary>
    /// トラック情報として設定するChannelTrackのインスタンスを格納します。設定する値が無い場合はnull
    /// </summary>
    public ChannelTrack ChannelTrack;
    /// <summary>
    /// コンテントヘッダとして設定するContentを格納します。設定する値が無い場合はnull
    /// </summary>
    public Content ContentHeader;
    /// <summary>
    /// コンテントボディとして追加するContentのリストを格納します。Contentが無い場合はnullまたは空のリスト
    /// </summary>
    public IList<Content> Contents;
  }

  /// <summary>
  /// ストリームからのコンテントデータの読み取りを行なうインターフェースです
  /// </summary>
  public interface IContentReader
  {
    /// <summary>
    /// 指定したストリームからデータを読み取ります
    /// </summary>
    /// <param name="stream">読み取り元のストリーム</param>
    /// <returns>読み取ったデータを保持するParsedContent</returns>
    /// <exception cref="EndOfStreamException">
    /// 必要なデータを読み取る前にストリームが終端に到達した
    /// </exception>
    /// <remarks>
    /// 戻り値にChannelInfoが存在する場合には、次のパケットが設定されていることが期待されます。
    /// <list type="bullet">
    ///   <item><description>Atom.PCP_CHAN_INFO_TYPE</description></item>
    ///   <item><description>Atom.PCP_CHAN_INFO_MIME</description></item>
    ///   <item><description>Atom.PCP_CHAN_INFO_PLS</description></item>
    ///   <item><description>Atom.PCP_CHAN_INFO_EXT</description></item>
    /// </list>
    /// </remarks>
    ParsedContent Read(Stream stream);

    /// <summary>
    /// コンテント解析器の名称を取得します
    /// </summary>
    string Name { get; } 

    /// <summary>
    /// コンテント追加対象のチャンネルを取得します
    /// </summary>
    Channel Channel { get; }
  }

  /// <summary>
  /// IContentReaderのインスタンスを作成するファクトリインターフェースです
  /// </summary>
  public interface IContentReaderFactory
  {
    /// <summary>
    /// 作成するコンテント解析器の名称を取得します
    /// </summary>
    string Name { get; } 

    /// <summary>
    /// 指定したチャンネルにコンテントデータを追加するIContentReaderを作成します
    /// </summary>
    /// <param name="channel">データ追加先となるチャンネル</param>
    /// <returns>IContentReaderを実装するオブジェクト</returns>
    IContentReader Create(Channel channel);
  }

  /// <summary>
  /// チャンネルを監視して管理するためのオブジェクトのインターフェースです
  /// </summary>
  public interface IChannelMonitor
  {
    /// <summary>
    /// 定期的に呼び出されるメソッドです
    /// </summary>
    void OnTimer();
  }

}

