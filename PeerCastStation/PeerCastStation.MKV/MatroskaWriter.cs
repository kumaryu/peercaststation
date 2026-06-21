using System;
using System.IO;
using System.Text;

namespace PeerCastStation.MKV
{
  /// <summary>
  /// EBML/Matroska 要素を組み立てる writer プリミティブ。
  /// MKVContentReader のリーダー(VInt/ElementHeader/Element)と対称に動作し、
  /// 生成した Element を既存リーダーで読み戻すと元の値・バイト列に一致する。
  /// 要素 ID は Elements の既知バイト列(長さマーカー込み)をそのまま使う。
  /// </summary>
  internal static class MatroskaWriter
  {
    /// <summary>ID バイト列を VInt としてラップする(書き出しでは Binary のみ使用)。</summary>
    private static VInt IdVInt(byte[] id)
    {
      return new VInt(0, id);
    }

    /// <summary>ID + サイズ VInt + payload からなるリーフ/任意要素を作る。</summary>
    public static Element MakeElement(byte[] id, byte[] data)
    {
      return new Element(new ElementHeader(IdVInt(id), VInt.FromSize(data.LongLength)), data);
    }

    /// <summary>子要素を連結し、その合計バイト長をサイズに持つ master 要素を作る。</summary>
    public static Element MakeMaster(byte[] id, params Element[] children)
    {
      using (var ms=new MemoryStream()) {
        foreach (var c in children) {
          c.Write(ms);
        }
        return MakeElement(id, ms.ToArray());
      }
    }

    /// <summary>符号なし整数要素(big-endian, 最小バイト長)。</summary>
    public static Element MakeUInt(byte[] id, ulong value)
    {
      return MakeElement(id, MinimalUIntBytes(value));
    }

    /// <summary>UTF-8 文字列要素。</summary>
    public static Element MakeString(byte[] id, string value)
    {
      return MakeElement(id, Encoding.UTF8.GetBytes(value));
    }

    /// <summary>バイナリ要素(CodecPrivate など)。</summary>
    public static Element MakeBinary(byte[] id, byte[] value)
    {
      return MakeElement(id, value);
    }

    /// <summary>倍精度浮動小数要素(8 バイト IEEE754, big-endian)。</summary>
    public static Element MakeFloat(byte[] id, double value)
    {
      var bin = BitConverter.GetBytes(value);
      if (BitConverter.IsLittleEndian) {
        Array.Reverse(bin);
      }
      return MakeElement(id, bin);
    }

    /// <summary>値を表せる最小バイト数の big-endian 表現(0 は 1 バイト)。</summary>
    private static byte[] MinimalUIntBytes(ulong value)
    {
      int len = 1;
      var t = value >> 8;
      while (t!=0) {
        len++;
        t >>= 8;
      }
      var bin = new byte[len];
      for (int i=len-1; i>=0; i--) {
        bin[i] = (byte)(value & 0xFF);
        value >>= 8;
      }
      return bin;
    }
  }
}
