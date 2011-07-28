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
  /// YellowPageとやりとりするクライアントのインターフェースです
  /// </summary>
  public interface IYellowPageClient
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
    /// YellowPageにチャンネルを載せます
    /// </summary>
    /// <param name="channel">載せるチャンネル</param>
    void Announce(Channel channel);
  }

  /// <summary>
  /// YellowPageクライアントのインスタンスを作成するためのファクトリインターフェースです
  /// </summary>
  public interface IYellowPageClientFactory
  {
    /// <summary>
    /// このYellowPageFactoryが扱うプロトコルの名前を取得します
    /// </summary>
    string Name { get; }
    /// <summary>
    /// YellowPageクライアントインスタンスを作成し返します
    /// </summary>
    /// <param name="name">YellowPageに関連付けられる名前</param>
    /// <param name="uri">YellowPageのURI</param>
    /// <returns>IYellowPageClientを実装するオブジェクトのインスタンス</returns>
    IYellowPageClient Create(string name, Uri uri);
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
    /// ストリームの状態が変更された時に呼ばれるイベントです
    /// </summary>
    event EventHandler<SourceStreamStatusChangedEventArgs> StatusChanged;
    /// <summary>
    /// ストリームの動作が終了した際に呼ばれるイベントです
    /// </summary>
    event EventHandler Stopped;
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
    /// <exception cref="NotImplentedException">このプロトコルではContentReaderを指定する必要がある</exception> 
    ISourceStream Create(Channel channel, Uri tracker);
    /// <summary>
    /// URIからプロトコルを判別し指定したIContentReaderでコンテンツを読み取るSourceStreamのインスタンスを作成します
    /// </summary>
    /// <param name="channel">所属するチャンネル</param>
    /// <param name="source">ストリーム取得起点のURI</param>
    /// <param name="reader">ストリームからコンテンツを読み取るためのIContentReaderインスタンス</param>
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
    void Stop();
    /// <summary>
    /// 出力ストリームの種類を取得します
    /// </summary>
    OutputStreamType OutputStreamType { get; }
    /// <summary>
    /// 出力ストリームの動作が終了した際に呼ばれるイベントです
    /// </summary>
    event EventHandler Stopped;
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
    /// <param name="channel">チャンネル情報設定先のチャンネル</param>
    /// <param name="stream">読み取り元のストリーム</param>
    /// <returns>読み取ったデータを保持するParsedContent</returns>
    /// <exception cref="EndOfStreamException">
    /// 必要なデータを読み取る前にストリームが終端に到達した
    /// </exception>
    ParsedContent Read(Channel channel, Stream stream);

    /// <summary>
    /// コンテント解析器の名称を取得します
    /// </summary>
    string Name { get; } 
  }
}

