// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Philmist
using System;

namespace PeerCastStation.FLV
{
  /// <summary>
  /// H.264 の SPS(Sequence Parameter Set)から解像度を取り出す。
  /// </summary>
  /// <remarks>
  /// Matroska は Video 要素に PixelWidth/PixelHeight を要求するため、解像度が判らないと
  /// 映像トラックを作れない。FLV の解像度は本来 onMetaData が運ぶが、これを送らない
  /// (あるいは width/height を欠く)配信は実在するので、その場合の拠り所として使う。
  /// 解析は失敗しうる前提で、例外は投げず false を返す。
  /// </remarks>
  internal static class H264Sps
  {
    /// <summary>SPS を示す nal_unit_type。</summary>
    private const int NalUnitTypeSps = 7;

    /// <summary>
    /// マクロブロック数の上限。
    /// </summary>
    /// <remarks>
    /// level 6.2 の 4096x2304 でも 256x144 マクロブロックなので、
    /// これを超える値は壊れた入力。(x+1)*16 の掛け算をオーバーフローさせないためにも要る。
    /// </remarks>
    private const int MaxMacroblocks = 4096;

    /// <summary>
    /// avcC(AVCDecoderConfigurationRecord)に含まれる最初の解析可能な SPS から解像度を得る。
    /// </summary>
    public static bool TryGetResolutionFromAvcC(byte[] avcc, out int width, out int height)
    {
      width  = 0;
      height = 0;
      // avcC の走査は AvcDecoderConfig に集約してある。TS 出力と同じ判定を通すことで、
      // 同じ avcC を一方のフィルタだけが受け入れるという食い違いを避ける。
      if (!AvcDecoderConfig.TryParse(avcc, out var config)) return false;
      foreach (var sps in config.SequenceParameterSets) {
        if (TryGetResolution(sps, out width, out height)) return true;
      }
      return false;
    }

    /// <summary>SPS の NAL ユニット(1バイトの NAL ヘッダを含む)から解像度を得る。</summary>
    public static bool TryGetResolution(ReadOnlySpan<byte> nal_unit, out int width, out int height)
    {
      width  = 0;
      height = 0;
      if (nal_unit.Length<2) return false;
      if ((nal_unit[0] & 0x1F)!=NalUnitTypeSps) return false;
      var reader = new BitReader(ToRbsp(nal_unit.Slice(1)));
      if (!reader.TryReadBits(8, out var profile_idc)) return false;
      if (!reader.TryReadBits(8, out _)) return false; // constraint_set flags + reserved
      if (!reader.TryReadBits(8, out _)) return false; // level_idc
      if (!reader.TryReadUnsignedExpGolomb(out _)) return false; // seq_parameter_set_id

      // 既定は 4:2:0。以下のプロファイルだけが chroma_format_idc 以降を明示的に持つ。
      var chroma_format_idc     = 1;
      var separate_colour_plane = false;
      if (HasChromaFormat(profile_idc)) {
        if (!reader.TryReadUnsignedExpGolomb(out chroma_format_idc)) return false;
        if (chroma_format_idc<0 || chroma_format_idc>3) return false;
        if (chroma_format_idc==3) {
          if (!reader.TryReadBits(1, out var scp)) return false;
          separate_colour_plane = scp!=0;
        }
        if (!reader.TryReadUnsignedExpGolomb(out _)) return false; // bit_depth_luma_minus8
        if (!reader.TryReadUnsignedExpGolomb(out _)) return false; // bit_depth_chroma_minus8
        if (!reader.TryReadBits(1, out _)) return false;           // qpprime_y_zero_transform_bypass_flag
        if (!reader.TryReadBits(1, out var scaling_matrix_present)) return false;
        if (scaling_matrix_present!=0) {
          var count = chroma_format_idc!=3 ? 8 : 12;
          for (var i=0; i<count; i++) {
            if (!reader.TryReadBits(1, out var present)) return false;
            if (present==0) continue;
            if (!TrySkipScalingList(reader, i<6 ? 16 : 64)) return false;
          }
        }
      }

      if (!reader.TryReadUnsignedExpGolomb(out _)) return false; // log2_max_frame_num_minus4
      if (!reader.TryReadUnsignedExpGolomb(out var poc_type)) return false;
      if (poc_type==0) {
        if (!reader.TryReadUnsignedExpGolomb(out _)) return false; // log2_max_pic_order_cnt_lsb_minus4
      }
      else if (poc_type==1) {
        if (!reader.TryReadBits(1, out _)) return false;           // delta_pic_order_always_zero_flag
        if (!reader.TryReadSignedExpGolomb(out _)) return false;   // offset_for_non_ref_pic
        if (!reader.TryReadSignedExpGolomb(out _)) return false;   // offset_for_top_to_bottom_field
        if (!reader.TryReadUnsignedExpGolomb(out var num_ref_frames_in_cycle)) return false;
        if (num_ref_frames_in_cycle<0 || num_ref_frames_in_cycle>255) return false;
        for (var i=0; i<num_ref_frames_in_cycle; i++) {
          if (!reader.TryReadSignedExpGolomb(out _)) return false;
        }
      }
      else if (poc_type!=2) {
        return false;
      }
      if (!reader.TryReadUnsignedExpGolomb(out _)) return false; // max_num_ref_frames
      if (!reader.TryReadBits(1, out _)) return false;           // gaps_in_frame_num_value_allowed_flag
      if (!reader.TryReadUnsignedExpGolomb(out var width_mbs_minus1)) return false;
      if (!reader.TryReadUnsignedExpGolomb(out var height_map_units_minus1)) return false;
      if (width_mbs_minus1<0 || width_mbs_minus1>=MaxMacroblocks) return false;
      if (height_map_units_minus1<0 || height_map_units_minus1>=MaxMacroblocks) return false;
      if (!reader.TryReadBits(1, out var frame_mbs_only)) return false;
      if (frame_mbs_only==0) {
        if (!reader.TryReadBits(1, out _)) return false; // mb_adaptive_frame_field_flag
      }
      if (!reader.TryReadBits(1, out _)) return false;   // direct_8x8_inference_flag
      if (!reader.TryReadBits(1, out var cropping)) return false;
      var crop_left   = 0;
      var crop_right  = 0;
      var crop_top    = 0;
      var crop_bottom = 0;
      if (cropping!=0) {
        if (!reader.TryReadUnsignedExpGolomb(out crop_left)) return false;
        if (!reader.TryReadUnsignedExpGolomb(out crop_right)) return false;
        if (!reader.TryReadUnsignedExpGolomb(out crop_top)) return false;
        if (!reader.TryReadUnsignedExpGolomb(out crop_bottom)) return false;
      }

      // クロッピングの単位はクロマのサブサンプリングと、フレーム/フィールドの別で決まる。
      var chroma_array_type = separate_colour_plane ? 0 : chroma_format_idc;
      var sub_width_c  = (chroma_array_type==1 || chroma_array_type==2) ? 2 : 1;
      var sub_height_c = chroma_array_type==1 ? 2 : 1;
      var crop_unit_x = chroma_array_type==0 ? 1 : sub_width_c;
      var crop_unit_y = (chroma_array_type==0 ? 1 : sub_height_c) * (2 - frame_mbs_only);
      // crop は ue(v) なのでマクロブロック数と違って上限が無く、1フィールドで int の
      // ほぼ全域(最大 2^31-2)を名乗れる。int のまま足すと既定の unchecked 演算で
      // 桁があふれ、ラップした結果が下の w<1/h<1 検査をすり抜けて不正な解像度が
      // トラックヘッダに書かれる。long で計算してから範囲を確かめる。
      var crop_x = (long)crop_unit_x*((long)crop_left + crop_right);
      var crop_y = (long)crop_unit_y*((long)crop_top + crop_bottom);
      var w = (long)(width_mbs_minus1+1)*16 - crop_x;
      var h = (long)(2-frame_mbs_only)*(height_map_units_minus1+1)*16 - crop_y;
      if (w<1 || h<1) return false;
      width  = (int)w;
      height = (int)h;
      return true;
    }

    /// <summary>chroma_format_idc 以降のフィールドを持つプロファイルか。</summary>
    private static bool HasChromaFormat(int profile_idc)
    {
      switch (profile_idc) {
      case 100: case 110: case 122: case 244: case 44:
      case 83:  case 86:  case 118: case 128: case 138:
      case 139: case 134: case 135:
        return true;
      default:
        return false;
      }
    }

    /// <summary>スケーリングリストを読み飛ばす(値そのものは解像度に関係しない)。</summary>
    private static bool TrySkipScalingList(BitReader reader, int size)
    {
      var last_scale = 8;
      var next_scale = 8;
      for (var i=0; i<size; i++) {
        if (next_scale!=0) {
          if (!reader.TryReadSignedExpGolomb(out var delta)) return false;
          next_scale = (last_scale + delta + 256) % 256;
        }
        if (next_scale!=0) last_scale = next_scale;
      }
      return true;
    }

    /// <summary>
    /// NAL ユニットのペイロードから emulation prevention byte を取り除いて RBSP にする。
    /// </summary>
    /// <remarks>
    /// 0x000000/0x000001 がスタートコードと衝突しないよう符号化側が 0x000003 を挿んでいるので、
    /// これを外さないとビット位置がずれて解析結果が狂う。
    /// </remarks>
    private static byte[] ToRbsp(ReadOnlySpan<byte> data)
    {
      var result = new byte[data.Length];
      var len   = 0;
      var zeros = 0;
      for (var i=0; i<data.Length; i++) {
        var b = data[i];
        if (zeros>=2 && b==0x03) {
          zeros = 0;
          continue;
        }
        result[len++] = b;
        zeros = b==0 ? zeros+1 : 0;
      }
      if (len==result.Length) return result;
      var trimmed = new byte[len];
      Array.Copy(result, trimmed, len);
      return trimmed;
    }
  }

}
