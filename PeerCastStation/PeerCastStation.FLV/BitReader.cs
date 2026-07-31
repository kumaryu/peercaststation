// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Philmist
namespace PeerCastStation.FLV
{
  /// <summary>
  /// バイト配列からMSB詰めでビットを読む。データ不足は例外ではなく false で返す
  /// (FLVFileParser の「EndOfStreamException=データ待ち」判定と混線させないため)。
  ///
  /// AudioSpecificConfig 等のビット詰めヘッダを FLVToMKV / FLVToMPEG2TS の
  /// 双方が解析するため、実装はここに一本化する。
  /// </summary>
  internal class BitReader
  {
    private readonly byte[] data;
    private int bitPos = 0;

    public BitReader(byte[] data)
    {
      this.data = data;
    }

    public bool TryReadBits(int bits, out int result)
    {
      result = 0;
      if (bits<0 || bits>31) return false;
      if ((long)bitPos+bits > (long)data.Length*8) return false;
      for (var i=0; i<bits; i++) {
        var p = bitPos+i;
        result = (result<<1) | ((data[p>>3] >> (7-(p & 7))) & 1);
      }
      bitPos += bits;
      return true;
    }
  }

}
