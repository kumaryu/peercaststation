
#ifndef PECAST_CORE_CORE_H
#define PECAST_CORE_CORE_H

#ifdef WIN32
#define PECAAPI __stdcall
#else
#define PECAAPI
#endif

struct PeCaStCore;

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

#endif // PECAST_CORE_CORE_H


