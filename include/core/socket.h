
#ifndef PECA_CORE_SOCKET_H_
#define PECA_CORE_SOCKET_H_

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
#endif //__cplusplus

struct PECAIOStream; // in stream.h

struct PECASocket;
struct PECAServerSocket;

typedef enum {
  SOCK_PROTO_ANY,   ///< 指定無し
  SOCK_PROTO_INET,  ///< IPv4
  SOCK_PROTO_INET6, ///< IPv6
} PECASockProto;

typedef enum {
  SOCK_E_NOERROR = 0,      ///< エラー無し
  SOCK_E_TIMEOUT,          ///< 送受信がタイムアウトした
  SOCK_E_CONN_ABORT,       ///< 接続が中断された
  SOCK_E_CONN_RESET,       ///< 接続がリセットされた
  SOCK_E_CONN_REFUSE,      ///< 接続が拒否された
  SOCK_E_ADDRESS,          ///< 無効なアドレスを指定した
  SOCK_E_HOST_NOTFOUND,    ///< 指定したホストが見つからなかった
  SOCK_E_INTF_NOTFOUND,    ///< 指定したネットワークインターフェースが見つからなかった
  SOCK_E_NO_ADDRESS,       ///< アドレスが一つも見つからなかった
  SOCK_E_SERVICE_NOTFOUND, ///< 指定したサービス名が見つからなかった
  SOCK_E_DNS,              ///< 名前を引くのに失敗した
  SOCK_E_NET,              ///< その他のネットワークエラーが発生した
} PECASockError;

PECASockError PECAAPI PECASockGetLastError();

/**
 * TCPクライアントソケットを作成し指定したアドレスに接続します。
 *
 * @param [in] protocol 接続プロトコル指定。@ref SOCKET_PROTO_ANYを指定した場合は自動で判別
 * @param [in] addr 接続先のアドレス
 * @param [in] port 接続先のポート番号
 * @return 接続したソケットハンドル。失敗した場合はNULL
 */
PECASocket* PECAAPI PECASockOpen(PECASockProto protocol, const char* addr, unsigned short port);

/**
 * ソケットの接続を閉じます。
 *
 * @param [inout] sock 閉じるソケットへのハンドル。NULLの場合は何もしない
 */
void PECAAPI PECASockClose(PECASocket* sock);

/**
 * ソケットからデータを読み取ります。
 *
 * @param [in]  sock ソケットハンドル
 * @param [out] dest 読み取ったデータの書き込み先
 * @param [in]  size 読み取るデータの最長バイト数
 * @retval <0 エラー
 * @retval 0  EOF
 * @retval >0 destに格納したバイト数
 */
int PECAAPI PECASockRead(PECASocket* sock, void* dest, int size);

/**
 * ソケットにデータを書き込みます。
 *
 * @param [in]  sock ソケットハンドル
 * @param [out] data 書き込むデータの書き込み先
 * @param [in]  size 書き込むデータの最長バイト数
 * @retval <0 エラー
 * @retval 0  EOF
 * @retval >0 書き込めたバイト数
 */
int PECAAPI PECASockWrite(PECASocket* sock, const void* data, int size);

/**
 * ソケットをPECAIOStreamに変換します。
 *
 * 変換後のIOStreamはソケットを共有します。
 * IOStreamが閉じられたらソケットも閉じられ、無効になります。
 *
 * @param [in]  sock ソケットハンドル
 * @return sockをPECAIOStreamに変換したハンドル
 */
PECAIOStream* PECASockToIOStream(PECASocket* sock);

/**
 * サーバソケットにクライアントが接続された時に呼ばれるコールバック関数です。
 *
 * @param [in] sock      クライアントと接続済みのソケットハンドル
 * @param [in] proc_data @ref PECAServerSockOpenに渡した引数
 *
 * @note
 * @ref PECAServerSockOpenを呼び出したスレッドとは別の
 * クライアントソケット毎のスレッドから呼びだされます。
 */
typedef void PECAAPI (*PECASockCallback)(PECASocket* sock, void* proc_data);

/**
 * サーバソケットを作成し、クライアントの接続を待ち受けます。
 *
 * 待ち受けと接続は別スレッドで行なわれます。
 *
 * @param [in] protocol 接続プロトコル指定。@ref SOCKET_PROTO_ANYを指定した場合は自動で判別
 * @param [in] intf 待ち受けるインターフェースのアドレス。NULLで省略可
 * @param [in] port 待ち受けるポート番号
 * @param [in] max_clients 最大同時接続クライアント数
 * @param [in] proc クライアントが接続された時に呼び出されるコールバック関数
 * @param [in] proc_arg procに渡される引数
 * @return 作成したサーバソケットハンドル。失敗した場合はNULL
 */
PECAServerSocket* PECAAPI PECAServerSockOpen(
    PECASockProto    protocol,
    const char*      intf,
    unsigned short   port,
    unsigned int     max_clients,
    PECASockCallback proc,
    void*            proc_arg);

/**
 * サーバソケットの待ち受けを終了します。
 *
 * 既に接続されているクライアントソケットは閉じません。
 *
 * @param [in] sock サーバソケットハンドル
 */
void PECAAPI PECAServerSockClose(PECAServerSocket* sock);

#ifdef __cplusplus
} //extern "C"
#endif //__cplusplus

#endif //PECA_CORE_SOCKET_H_

