
#ifndef PECA_CORE_CORE_H
#define PECA_CORE_CORE_H

#include <stddef.h>

#ifndef PECAAPI
#  ifdef WIN32
#    define PECAAPI __stdcall
#  else
#    define PECAAPI
#  endif
#endif

#ifdef __cplusplus
extern "C" {
#endif

struct PECACore;
struct PECAChannel;

/**
 * PeerCastStationコアを初期化します。
 * @return PeerCastStationコアのハンドル
 */
PECACore* PECAAPI PECACoreInitialize();

/**
 * PeerCastStationコアを解放します。
 * @param [inout] core PeerCastStationコアのハンドル
 */
void PECAAPI PECACoreTerminate(PECACore* core);

/**
 * YPのアドレスを設定します。
 * @param [in] core PeerCastStationコアのハンドル
 * @param [in] addr YPのアドレス
 * @param [in] port YPのポート番号
 */
void PECAAPI PECACoreSetYP(PECACore* core, const char* addr, short port);

/**
 * YPのアドレスを取得します。
 * @param [in] core PeerCastStationコアのハンドル
 * @return YPのアドレス
 */
const char* PECAAPI PECACoreGetYP(PECACore* core);

/**
 * YPのポート番号を取得します。
 * @param [in] core PeerCastStationコアのハンドル
 * @return YPのポート番号
 */
short PECAAPI PECACoreGetYPPort(PECACore* core);

/**
 * リレーを開始します。
 * 既にチャンネルを保持していた場合はそれを返します。
 *
 * @param [in] core PeerCastStationコアのハンドル
 * @param [in] channel_id リレーするチャンネルID
 * @param [in] tracker    チャンネルのトラッカーアドレス。不明な場合はNULL
 * @return チャンネルハンドル
 */
PECAChannel* PECAAPI PECACoreRelay(
    PECACore* core,
    const char* channel_id,
    const char* tracker);

#endif // PECA_CORE_CORE_H


