
#ifndef PECAST_CORE_SOCKET_H_
#define PECAST_CORE_SOCKET_H_

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

struct PeCaStSocket;
typedef enum {
  SOCK_PROTO_ANY,   ///< 指定無し
  SOCK_PROTO_INET,  ///< IPv4
  SOCK_PROTO_INET6, ///< IPv6
} PeCaStSockProto;

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
} PeCaStSockError;

PeCaStSockError PECAAPI PeCaStSockGetLastError();

/**
 * TCPクライアントソケットを作成し指定したアドレスに接続します。
 *
 * @param [in] protocol 接続プロトコル指定。@ref SOCKET_PROTO_ANYを指定した場合は自動で判別
 * @param [in] addr 接続先のアドレス
 * @param [in] port 接続先のポート番号
 * @return 接続したソケットハンドル。失敗した場合はNULL
 */
PeCaStSocket* PECAAPI PeCaStSockOpen(PeCaStSockProto protocol, const char* addr, unsigned short port);

/**
 * ソケットの接続を閉じます。
 *
 * @param [inout] sock 閉じるソケットへのハンドル。NULLの場合は何もしない
 */
void PECAAPI PeCaStSockClose(PeCaStSocket* sock);

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
int PECAAPI PeCaStSockRead(PeCaStSocket* sock, void* dest, int size);

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
int PECAAPI PeCaStSockWrite(PeCaStSocket* sock, const void* data, int size);

#ifdef __cplusplus
} //extern "C"
#endif //__cplusplus

#endif //PECAST_CORE_SOCKET_H_

