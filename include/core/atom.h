
#ifndef PECA_CORE_ATOM_H
#define PECA_CORE_ATOM_H

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

struct PECAIOStream; //in core/stream.h
struct PECAAtom;

/**
 * 子を持つATOMを新しく作成します。
 *
 * @param name [in] ATOMの名前(4文字以内)
 * @return 作成したATOM。
 * nameがNULLの場合、4文字を越える場合は作成失敗としてNULLを返す
 */
PECAAtom* PECAAPI PECAAtomAllocList(const char* name);

/**
 * ATOMを削除します。
 *
 * @param atom [in] 削除するATOM。NULLを指定した場合は何もしません
 */
void PECAAPI PECAAtomFree(PECAAtom* atom);

/**
 * 値を持つATOMを新しく作成します。
 *
 * @param name   [in] ATOMの名前(4文字以内)
 * @param data   [in] ATOOMに格納する値
 * @param length [in] ATOOMに格納する値のバイト長
 * @return 作成したATOM。
 * nameがNULLの場合、4文字を越える場合は作成失敗としてNULLを返す
 */
PECAAtom* PECAAPI PECAAtomAllocData(const char* name, const void* data, size_t length);

/**
 * 4バイト整数値を持つATOMを新しく作成します。
 *
 * @param name   [in] ATOMの名前(4文字以内)
 * @param data   [in] ATOOMに格納する値
 * @return 作成したATOM。
 * nameがNULLの場合、4文字を越える場合は作成失敗としてNULLを返す
 */
PECAAtom* PECAAPI PECAAtomAllocInt(const char* name, int data);

/**
 * 2バイト整数値を持つATOMを新しく作成します。
 *
 * @param name   [in] ATOMの名前(4文字以内)
 * @param data   [in] ATOOMに格納する値
 * @return 作成したATOM。
 * nameがNULLの場合、4文字を越える場合は作成失敗としてNULLを返す
 */
PECAAtom* PECAAPI PECAAtomAllocShort(const char* name, short data);

/**
 * 1バイト整数値を持つATOMを新しく作成します。
 *
 * @param name   [in] ATOMの名前(4文字以内)
 * @param data   [in] ATOOMに格納する値
 * @return 作成したATOM。
 * nameがNULLの場合、4文字を越える場合は作成失敗としてNULLを返す
 */
PECAAtom* PECAAPI PECAAtomAllocByte(const char* name, unsigned char data);

/**
 * 文字列値を持つATOMを新しく作成します。
 *
 * @param name   [in] ATOMの名前(4文字以内)
 * @param data   [in] ATOOMに格納する値(NULL終端)
 * @return 作成したATOM。
 * nameがNULLの場合、4文字を越える場合は作成失敗としてNULLを返す
 */
PECAAtom* PECAAPI PECAAtomAllocString(const char* name, const char* data);

/**
 * ATOMが子を持つATOMであるかどうかを取得します。
 *
 * @param atom [in] ATOM
 * @return 子持ちの場合非0、値持ちの場合0を返す
 */
int PECAAPI PECAAtomIsList(PECAAtom* atom);

/**
 * ATOMの名前を取得します。
 *
 * @param atom [in]  ATOM
 * @param name [out] 名前の書き込み先(NULL終端含む最大5文字)
 * @return 子持ちの場合非0、値持ちの場合0を返す
 */
void PECAAPI PECAAtomGetName(PECAAtom* atom, char* name);

/**
 * ATOMの値のバイト長を取得します。
 *
 * @param atom [in] ATOM
 * @return 保持している値のバイト長。子持ちATOMの場合は0
 */
size_t PECAAPI PECAAtomGetDataLength(const PECAAtom* atom);

/**
 * ATOMの値を取得します。
 *
 * @param atom [in]  ATOM
 * @param dest [out] 値の書き込み先
 * @param len  [in]  destに書き込める最大バイト長
 * @return 実際にdest書き込んだバイト数。atomが子持ちであった場合は0
 */
size_t PECAAPI PECAAtomGetData(const PECAAtom* atom, void* dest, size_t len);

/**
 * ATOMの値を4バイト整数値として取得します。
 *
 * @param atom [in]  ATOM
 * @param dest [out] 値の書き込み先
 * @return 値のバイト長が4でなかった場合は0、それ以外は非0
 */
size_t PECAAPI PECAAtomGetInt(const PECAAtom* atom, int* dest);

/**
 * ATOMの値を2バイト整数値として取得します。
 *
 * @param atom [in]  ATOM
 * @param dest [out] 値の書き込み先
 * @return 値のバイト長が2でなかった場合は0、それ以外は非0
 */
size_t PECAAPI PECAAtomGetShort(const PECAAtom* atom, short* dest);

/**
 * ATOMの値を1バイト整数値として取得します。
 *
 * @param atom [in]  ATOM
 * @param dest [out] 値の書き込み先
 * @return 値のバイト長が1でなかった場合は0、それ以外は非0
 */
size_t PECAAPI PECAAtomGetByte(const PECAAtom* atom, unsigned char* dest);

/**
 * ATOMの値を文字列として取得します。
 *
 * @param atom [in]  ATOM
 * @param dest [out] 値の書き込み先
 * @param len  [in]  destに書き込める最大バイト長
 * @return destに実際に書き込んだバイト長
 *
 * @note destには必ずNULL終端を書き込みます。
 */
size_t PECAAPI PECAAtomGetString(const PECAAtom* atom, char* dest, size_t len);

/**
 * リストATOMの子の数を取得します。
 *
 * @param atom [in] ATOMハンドル
 * @return ATOMの子の数
 */
size_t PECAAtomGetChildren(PECAAtom* atom);

/**
 * 子持ちATOMの子を取得します。
 *
 * 取得した子のATOMの所有権は親ATOMにあります。
 * PECAAtomFreeで解放しないようにしてください。
 *
 * @param atom  [in] ATOMハンドル
 * @param index [in] 取得する子ATOMのインデックス
 * @return ATOMの子、indexが範囲外や子持ちATOMでない場合はNULL
 */
PECAAtom* PECAAPI PECAAtomGetChild(PECAAtom* atom, size_t index);

/**
 * 子持ちATOMに子ATOMを追加します。
 *
 * 追加した子のATOMの所有権は親ATOMに移ります。
 * PECAAtomFreeで解放しないようにしてください。
 *
 * @param atom  [in] ATOMハンドル
 * @param child [in] 追加する子ATOM
 */
void PECAAPI PECAAtomAddChild(PECAAtom* atom, PECAAtom* child);

/**
 * IOストリームにATOMとその子を書き込みます。
 *
 * @param atom   [in] 書き込むATOMハンドル
 * @param stream [in] 書き込み先のIOストリーム
 * @return ATOMが正常に書き込めた場合は非0、それ以外の場合は0
 */
int PECAAPI PECAAtomWrite(PECAAtom* atom, PECAIOStream* stream);

/**
 * IOストリームからATOMとその子を読み取ります。
 *
 * @param stream [in] 読み込むIOストリーム
 * @return ATOMが正常に読み込めた場合はATOMハンドル、それ以外の場合はNULL
 */
PECAAtom* PECAAPI PECAAtomRead(PECAIOStream* stream);

#ifdef __cplusplus
} //extern "C"
#endif


#endif // PECA_CORE_ATOM_H


