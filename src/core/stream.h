
#ifndef PECA_CORE_STREAM_H
#define PECA_CORE_STREAM_H

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

typedef struct PECAIOStream
{
  /**
   * ストリームを閉じます。
   *
   * @param [in]  s 閉じるストリーム
   */
  void PECAAPI (*Close)(PECAIOStream* s);

  /**
   * ストリームからデータを読み取ります。
   *
   * @param [in]  s ストリーム
   * @param [out] dest 読み取ったデータの書き込み先
   * @param [in]  size 読み取るデータの最長バイト数
   * @retval <0 エラー
   * @retval 0  EOF
   * @retval >0 destに格納したバイト数
   */
  int PECAAPI (*Read)(PECAIOStream* s, void* dest, int size);

  /**
   * ストリームにデータを書き込みます。
   *
   * @param [in]  s ストリーム
   * @param [out] data 書き込むデータの書き込み先
   * @param [in]  size 書き込むデータの最長バイト数
   * @retval <0 エラー
   * @retval 0  EOF
   * @retval >0 書き込めたバイト数
   */
  int PECAAPI (*Write)(PECAIOStream* sock, const void* data, int size);
} PECAIOStream;

#ifdef __cplusplus
} //extern "C"
#endif


#endif // PECA_CORE_STREAM_H

