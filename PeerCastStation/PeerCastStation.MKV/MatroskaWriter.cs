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

    /// <summary>
    /// unknown-size(全ビット 1)で開く master 要素のヘッダ(ID + サイズ VInt のみ)。
    /// ライブ mux で全長未確定の Segment を開くのに使う。子要素はこのヘッダの後ろに
    /// 連続して書き出す。リーダーは Element.NoData として読む。
    /// </summary>
    public static ElementHeader MakeUnknownSizeHeader(byte[] id)
    {
      return new ElementHeader(IdVInt(id), VInt.Unknown(1));
    }

    /// <summary>符号なし整数要素(big-endian, 最小バイト長)。</summary>
    public static Element MakeUInt(byte[] id, ulong value)
    {
      return MakeElement(id, MinimalUIntBytes(value));
    }

    /// <summary>
    /// SimpleBlock(0xA3)要素を作る。ライブ mux のフレーム 1 つ分。
    /// ボディは [トラック番号 VInt][相対 timecode int16 big-endian][フラグ][payload]。
    /// 相対 timecode は所属 Cluster の Timecode からの差(ミリ秒, 符号付き int16)。
    /// keyframe なら最上位ビット(0x80)を立てる。lacing は使わない。
    /// payload は中身を解析せず透過コピーする(NAL/OBU をそのまま入れる)。
    /// </summary>
    public static Element MakeSimpleBlock(ulong trackNumber, short relativeTimecode, bool isKeyFrame, ArraySegment<byte> payload)
    {
      var track = VInt.FromSize((long)trackNumber).Binary;
      using (var ms=new MemoryStream()) {
        ms.Write(track, 0, track.Length);
        ms.WriteByte((byte)((relativeTimecode>>8) & 0xFF));
        ms.WriteByte((byte)(relativeTimecode & 0xFF));
        ms.WriteByte((byte)(isKeyFrame ? 0x80 : 0x00));
        if (payload.Count>0) {
          ms.Write(payload.Array!, payload.Offset, payload.Count);
        }
        return MakeElement(Elements.SimpleBlock, ms.ToArray());
      }
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
