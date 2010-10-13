
#ifndef PECAST_CORE_CORE_H
#define PECAST_CORE_CORE_H

#include <stddef.h>

#ifdef WIN32
#define PECAAPI __stdcall
#else
#define PECAAPI
#endif

#ifdef __cplusplus
extern "C" {
#endif

struct PeCaStCore;
struct PeCaStChannelData;
struct PeCaStChannel;
typedef size_t PeCaStStreamPos;

/**
 * PeerCastStationコアを初期化します。
 * @return PeerCastStationコアのハンドル
 */
PeCaStCore* PECAAPI PeCaStCoreInitialize();

/**
 * PeerCastStationコアを解放します。
 * @param [inout] core PeerCastStationコアのハンドル
 */
void PECAAPI PeCaStCoreTerminate(PeCaStCore* core);

/**
 * YPのアドレスを設定します。
 * @param [in] core PeerCastStationコアのハンドル
 * @param [in] addr YPのアドレス
 * @param [in] port YPのポート番号
 */
void PECAAPI PeCaStCoreSetYP(PeCaStCore* core, const char* addr, short port);

/**
 * YPのアドレスを取得します。
 * @param [in] core PeerCastStationコアのハンドル
 * @return YPのアドレス
 */
const char* PECAAPI PeCaStCoreGetYP(PeCaStCore* core);

/**
 * YPのポート番号を取得します。
 * @param [in] core PeerCastStationコアのハンドル
 * @return YPのポート番号
 */
short PECAAPI PeCaStCoreGetYPPort(PeCaStCore* core);

/**
 * リレーを開始します。
 * 既にチャンネルを保持していた場合はそれを返します。
 *
 * @param [in] core PeerCastStationコアのハンドル
 * @param [in] channel_id リレーするチャンネルID
 * @param [in] tracker    チャンネルのトラッカーアドレス。不明な場合はNULL
 * @return チャンネルハンドル
 */
PeCaStChannel* PECAAPI PeCaStCoreRelay(
    PeCaStCore* core,
    const char* channel_id,
    const char* tracker);

/**
 * チャンネルを閉じます。
 *
 * @param [in] channel チャンネルハンドル
 */
void PECAAPI PeCaStChannelClose(PeCaStChannel* channel);

typedef enum {
  CHANNEL_STATUS_NOCHANNEL = -2, ///< チャンネルが無い
  CHANNEL_STATUS_ERROR     = -1, ///< 接続エラー
  CHANNEL_STATUS_IDLE      =  0, ///< 接続していない
  CHANNEL_STATUS_SEACHING,       ///< 接続先検索中
  CHANNEL_STATUS_CONNECTING,     ///< 接続中
  CHANNEL_STATUS_RELAYING,       ///< リレー中
  CHANNEL_STATUS_BROADCASTING,   ///< 配信中
} PeCaStChannelStatus;

/**
 * チャンネルの状態を取得します。
 *
 * @param [in] channel チャンネルハンドル
 * @return チャンネルの状態。@ref PeCaStChannelStatusを参照のこと。 
 */
PeCaStChannelStatus PECAAPI PeCaStChannelGetStatus(PeCaStChannel* channel);

/**
 * チャンネルデータが保持しているストリーム位置を取得します。
 *
 * @param [in] channel_data チャンネルデータハンドル
 * @param [out] oldest チャンネルデータが保持している一番古いストリーム位置
 * @param [out] newest チャンネルデータが保持している一番新しいストリーム位置
 */
void PECAAPI PeCaStChannelDataGetStreamPosition(
    const PeCaStChannelData* channel_data,
    PeCaStStreamPos* oldest,
    PeCaStStreamPos* newest);

/**
 * 指定したストリーム位置のパケットデータ長を取得します。
 *
 * @param [in] channel_data チャンネルデータハンドル
 * @param [in] pos          ストリーム位置
 * @return
 * パケットデータ長(バイト)。
 * 指定したストリーム位置のパケットを保持していなかった場合は0
 */
size_t PECAAPI PeCaStChannelDataGetDataSize(
    const PeCaStChannelData* channel_data,
    PeCaStStreamPos pos);

/**
 * 指定したストリーム位置のパケットデータを取得します。
 *
 * @param [in]  channel_data チャンネルデータハンドル
 * @param [in]  pos          ストリーム位置
 * @param [out] dest         パケットデータの書き込み先
 * @param [in]  length       destの長さ
 * @return destに実際に書き込んだバイト数
 */
size_t PECAAPI PeCaStChannelDataGetData(
    const PeCaStChannelData* channel_data,
    PeCaStStreamPos pos,
    unsigned char* dest,
    size_t length);

struct PeCaStChannelOutputStream
{
  /**
   * チャンネルデータが更新された時に呼ばれるコールバック関数です。
   *
   * @param [in] self この構造体自体のポインタ
   * @param [in] channel_data チャンネルデータ
   */
  void PECAAPI (*Output)(
      PeCaStChannelOutputStream* self,
      const PeCaStChannelData* channel_data);
  /**
   * チャンネルが閉じられた時やストリームが外された時に呼ばれるコールバック関数です。
   *
   * @param [in] self この構造体自体のポインタ
   */
  void PECAAPI (*Close)(PeCaStChannelOutputStream* self);
};

/**
 * チャンネルに出力ストリームを追加します。
 *
 * @param [in] channel チャンネルハンドル
 * @param [in] output  出力ストリームハンドル
 */
void PECAAPI PeCaStChannelAddOutputStream(
    PeCaStChannel* channel,
    PeCaStChannelOutputStream* output);

/**
 * チャンネルから出力ストリームを外します。
 *
 * @param [in] channel チャンネルハンドル
 * @param [in] output  出力ストリームハンドル
 */
void PECAAPI PeCaStChannelRemoveOutputStream(
    PeCaStChannel* channel,
    PeCaStChannelOutputStream* output);

#ifdef __cplusplus
} //extern "C"
#endif


#endif // PECAST_CORE_CORE_H


