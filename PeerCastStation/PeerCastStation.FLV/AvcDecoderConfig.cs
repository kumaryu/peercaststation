// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Philmist
using System;

namespace PeerCastStation.FLV
{
  /// <summary>
  /// avcC(AVCDecoderConfigurationRecord, ISO/IEC 14496-15)の解析結果。
  /// </summary>
  /// <remarks>
  /// 固定長ヘッダの後ろに「16bit ビッグエンディアンの長さを前置きした NAL 配列」が
  /// SPS/PPS/SPSExt と並ぶという配置は、解像度取得(<see cref="H264Sps"/>)と
  /// TS 出力(FLVToMPEG2TS)の双方が必要とする。別々に歩くと、片方だけ境界検査や
  /// 個数マスクを直したときに、同じ avcC を一方は受け入れ他方は拒否するというずれ方をする。
  /// 実装をここへ集約して、どのフィルタでも同じ avcC が同じように解釈されるようにする。
  ///
  /// バイトが不足する場合や個数が実データと矛盾する場合は例外を投げず false を返す。
  /// numOfSPS/numOfPPS は実データ量と無関係に最大31/255を名乗れるため、
  /// 読み出し前に必ず残バイト数と照合する。
  /// </remarks>
  internal readonly struct AvcDecoderConfig
  {
    /// <summary>NAL ユニット長を表すバイト数(1..4)。</summary>
    public int NalSizeLength { get; }
    /// <summary>SPS の NAL ユニット。いずれも先頭1バイトの NAL ヘッダを含む。</summary>
    public byte[][] SequenceParameterSets { get; }
    /// <summary>PPS の NAL ユニット。</summary>
    public byte[][] PictureParameterSets { get; }
    /// <summary>SPS 拡張の NAL ユニット。High プロファイル系以外では空。</summary>
    public byte[][] SequenceParameterSetExtensions { get; }

    private AvcDecoderConfig(int nal_size_length, byte[][] sps, byte[][] pps, byte[][] sps_ext)
    {
      NalSizeLength                  = nal_size_length;
      SequenceParameterSets          = sps;
      PictureParameterSets           = pps;
      SequenceParameterSetExtensions = sps_ext;
    }

    /// <summary>
    /// 復号初期化に足る SPS/PPS を少なくとも1つずつ含むか。
    /// </summary>
    /// <remarks>
    /// ISO/IEC 14496-15 上は 0 個(パラメータセットを in-band で運ぶ運用)も合法なので、
    /// <see cref="TryParse"/> は構造の妥当性だけを見てこれを成功条件にしない。
    /// 設定として配ってよいかはコンテナごとに事情が違う(CodecPrivate が唯一の拠り所の
    /// Matroska では必須、素通しの TS では in-band で補える)ため、利用側がこれで判断する。
    /// </remarks>
    public bool HasParameterSets {
      get { return SequenceParameterSets.Length>0 && PictureParameterSets.Length>0; }
    }

    public static bool TryParse(ReadOnlySpan<byte> data, out AvcDecoderConfig result)
    {
      result = default;
      if (!TryReadByte(ref data, out _)) return false;                          // configurationVersion
      if (!TryReadByte(ref data, out var profile_indication)) return false;     // AVCProfileIndication
      if (!TryReadByte(ref data, out _)) return false;                          // profile_compatibility
      if (!TryReadByte(ref data, out _)) return false;                          // AVCLevelIndication
      if (!TryReadByte(ref data, out var length_size_minus_one)) return false;
      var nal_size_length = (length_size_minus_one & 0x3) + 1;
      if (!TryReadByte(ref data, out var sps_count)) return false;
      if (!TryReadNalUnits(ref data, sps_count & 0x1F, out var sps)) return false;
      if (!TryReadByte(ref data, out var pps_count)) return false;
      if (!TryReadNalUnits(ref data, pps_count, out var pps)) return false;
      // High プロファイル系の拡張ブロックはベストエフォートで読む。ISO/IEC 14496-15 は
      // 既知の構造より後ろの余剰バイトを無視するよう定めており、拡張を省略する古い muxer や
      // 末尾を切り詰め・パディングした avcC も実在する。ここの不整合で record 全体を
      // 拒否すると、健全な SPS/PPS まで捨てられて以後の全フレームが破棄され、
      // 拡張なしで再生できたはずの配信の映像が丸ごと失われる。
      var sps_ext = Array.Empty<byte[]>();
      if (data.Length>=4 &&
          (profile_indication==100 ||
           profile_indication==110 ||
           profile_indication==122 ||
           profile_indication==144)) {
        // chroma_format / bit_depth_luma / bit_depth_chroma は使わないが位置を進める。
        data = data.Slice(3);
        TryReadByte(ref data, out var sps_ext_count);
        if (!TryReadNalUnits(ref data, sps_ext_count, out sps_ext)) {
          // 拡張の実データが数え上げと矛盾していても SPS/PPS は独立して有効。
          sps_ext = Array.Empty<byte[]>();
        }
      }
      result = new AvcDecoderConfig(nal_size_length, sps, pps, sps_ext);
      return true;
    }

    private static bool TryReadByte(ref ReadOnlySpan<byte> bytes, out byte value)
    {
      if (bytes.Length<1) {
        value = 0;
        return false;
      }
      value = bytes[0];
      bytes = bytes.Slice(1);
      return true;
    }

    /// <summary>16bit ビッグエンディアンの長さを前置きした NAL 配列を1つ読む。</summary>
    private static bool TryReadNalUnits(ref ReadOnlySpan<byte> data, int count, out byte[][] result)
    {
      result = Array.Empty<byte[]>();
      if (count<1) return true;
      var units = new byte[count][];
      for (var i=0; i<count; i++) {
        if (data.Length<2) return false;
        var len = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(data);
        data = data.Slice(2);
        // NAL ヘッダの1バイトが要るので len>=1。残バイト数を超える長さは壊れた入力。
        if (len<1 || data.Length<len) return false;
        units[i] = data.Slice(0, len).ToArray();
        data = data.Slice(len);
      }
      result = units;
      return true;
    }
  }

}
