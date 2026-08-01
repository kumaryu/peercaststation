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

  /// <summary>
  /// AudioSpecificConfig(ISO/IEC 14496-3)先頭部の解析結果。
  /// FLVToMKV は SampleRate/ChannelConfiguration を、FLVToMPEG2TS は
  /// AudioObjectType/SamplingFrequencyIndex/ChannelConfiguration を使う。
  /// 解析は1実装に集約し、フィルタごとの解釈差(31拡張の+32補正の有無など)が
  /// 生じないようにする。
  /// </summary>
  internal readonly struct AudioSpecificConfig
  {
    private static readonly int[] SamplingFrequencies = {
      96000, 88200, 64000, 48000, 44100, 32000,
      24000, 22050, 16000, 12000, 11025, 8000, 7350,
    };

    /// <summary>audioObjectType。31 の拡張エスケープは +32 補正済み。</summary>
    public int AudioObjectType { get; }
    /// <summary>4bit の samplingFrequencyIndex。0x0F は明示レートのエスケープ。</summary>
    public int SamplingFrequencyIndex { get; }
    /// <summary>サンプリング周波数(Hz)。予約インデックス(13/14)は 0。</summary>
    public int SampleRate { get; }
    public int ChannelConfiguration { get; }

    private AudioSpecificConfig(int audio_object_type, int sampling_frequency_index, int sample_rate, int channel_configuration)
    {
      AudioObjectType        = audio_object_type;
      SamplingFrequencyIndex = sampling_frequency_index;
      SampleRate             = sample_rate;
      ChannelConfiguration   = channel_configuration;
    }

    /// <summary>ビットが不足する場合は false を返す(例外は投げない)。</summary>
    public static bool TryParse(byte[] config, out AudioSpecificConfig result)
    {
      result = default;
      var reader = new BitReader(config);
      if (!reader.TryReadBits(5, out var type)) return false;
      if (type==31) {
        if (!reader.TryReadBits(6, out var ext)) return false;
        type = ext+32;
      }
      if (!reader.TryReadBits(4, out var freq_idx)) return false;
      int sample_rate;
      if (freq_idx==0x0F) {
        if (!reader.TryReadBits(24, out sample_rate)) return false;
      }
      else {
        sample_rate = freq_idx<SamplingFrequencies.Length ? SamplingFrequencies[freq_idx] : 0;
      }
      if (!reader.TryReadBits(4, out var channels)) return false;
      result = new AudioSpecificConfig(type, freq_idx, sample_rate, channels);
      return true;
    }
  }

}
