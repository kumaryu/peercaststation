// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Philmist
using System.Collections.Generic;

namespace PeerCastStation.FLV
{
  /// <summary>
  /// E-RTMP の FourCC 1つぶんのコーデック記述子。プロセスで1インスタンスを共有する。
  ///
  /// コーデックの知識(名前、CTS の有無、コンテナ側の CodecID)は以前、
  /// ExTagHeaderReader の intern テーブル・CTS 判定の文字列比較・FLVTagClassifier の
  /// 既知判定・FLVToMKV の対応表と複数系統に分かれていた。コーデックを1つ足すには
  /// 全部を同期して編集する必要があり、特に CTS 判定を見落とすと、フレーム先頭
  /// 3バイトをコーデックデータと誤読した壊れた出力がパースエラーなしで出る。
  /// 知識は <see cref="FourCcRegistry"/> の1テーブルへ集約し、利用側は属性を参照する。
  /// </summary>
  public sealed class FourCcCodec
  {
    /// <summary>E-RTMP 上の FourCC 文字列。共有インスタンスなので毎タグの割り当てが出ない。</summary>
    public string Name { get; }
    /// <summary>FourCC の4バイトをビッグエンディアンで詰めた値。既知判定はこの比較1回で済む。</summary>
    public uint Value { get; }
    public bool IsAudio { get; }
    /// <summary>CodedFrames が 24bit compositionTimeOffset を運ぶか(映像のみ)。</summary>
    public bool HasCompositionTime { get; }
    /// <summary>Matroska の CodecID。FLVToMKV が対応していないコーデックは null。</summary>
    public string? MkvCodecId { get; }

    private FourCcCodec(string name, bool is_audio, bool has_composition_time, string? mkv_codec_id)
    {
      Name = name;
      Value = ((uint)name[0]<<24) | ((uint)name[1]<<16) | ((uint)name[2]<<8) | name[3];
      IsAudio = is_audio;
      HasCompositionTime = has_composition_time;
      MkvCodecId = mkv_codec_id;
    }

    internal static FourCcCodec Video(string name, bool cts, string? mkv)
    {
      return new FourCcCodec(name, is_audio: false, has_composition_time: cts, mkv_codec_id: mkv);
    }

    internal static FourCcCodec Audio(string name, string? mkv)
    {
      return new FourCcCodec(name, is_audio: true, has_composition_time: false, mkv_codec_id: mkv);
    }
  }

  /// <summary>
  /// 既知コーデックの一覧。コーデックの追加・変更はこのテーブル1箇所で完結させる。
  /// </summary>
  public static class FourCcRegistry
  {
    /// <summary>H.264。レガシー codecId 7 の正規化先でもある。</summary>
    public static readonly FourCcCodec Avc = FourCcCodec.Video("avc1", cts: true, mkv: "V_MPEG4/ISO/AVC");
    /// <summary>AAC。レガシー soundFormat 10 の正規化先でもある。</summary>
    public static readonly FourCcCodec Aac = FourCcCodec.Audio("mp4a", mkv: "A_AAC");

    // E-RTMP v2 が定めるコーデック。CTS(24bit compositionTimeOffset)を持つのは
    // AVC/HEVC/VVC のみで、AV1/VP9 は body 先頭が即コーデックデータ。
    // vvc1 はどのフィルタも未対応だが、ここに無いと CodedFrames の CTS 3バイトが
    // コーデックデータとして誤読されるため、ヘッダ解釈のために登録しておく。
    private static readonly FourCcCodec[] Known = {
      Avc,
      FourCcCodec.Video("hvc1", cts: true,  mkv: "V_MPEGH/ISO/HEVC"),
      FourCcCodec.Video("hev1", cts: true,  mkv: "V_MPEGH/ISO/HEVC"),
      FourCcCodec.Video("vvc1", cts: true,  mkv: null),
      FourCcCodec.Video("av01", cts: false, mkv: "V_AV1"), // Matroska の CodecID は FourCC と綴りが違う
      FourCcCodec.Video("vp09", cts: false, mkv: null),
      Aac,
      FourCcCodec.Audio("Opus", mkv: null),
      FourCcCodec.Audio("ac-3", mkv: null),
      FourCcCodec.Audio("ec-3", mkv: null),
      FourCcCodec.Audio("fLaC", mkv: null),
      FourCcCodec.Audio(".mp3", mkv: null),
    };

    private static readonly Dictionary<uint, FourCcCodec> byValue = BuildIndex();

    private static Dictionary<uint, FourCcCodec> BuildIndex()
    {
      var map = new Dictionary<uint, FourCcCodec>(Known.Length);
      foreach (var codec in Known) {
        map.Add(codec.Value, codec);
      }
      return map;
    }

    public static FourCcCodec? Lookup(uint value)
    {
      return byValue.TryGetValue(value, out var codec) ? codec : null;
    }
  }
}
