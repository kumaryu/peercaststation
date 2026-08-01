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

    /// <summary>
    /// channelConfiguration が示すチャンネル数。インデックスであって個数ではなく、
    /// 7 は 8ch(7.1)を意味する。0 は PCE でレイアウトを運ぶ指定、8-15 は予約値で
    /// いずれも個数を determine できないため 0。
    /// </summary>
    private static readonly int[] ChannelCounts = {
      0, 1, 2, 3, 4, 5, 6, 8,
      0, 0, 0, 0, 0, 0, 0, 0,
    };

    /// <summary>audioObjectType。31 の拡張エスケープは +32 補正済み。</summary>
    public int AudioObjectType { get; }
    /// <summary>
    /// SBR/PS の明示signaling(AOT 5/29)時に後続で通知されるコア audioObjectType。
    /// 明示signalingでない場合は <see cref="AudioObjectType"/> と同じ。
    /// HE-AAC を AAC LC のフレームとして扱う経路(ADTS 等)はこちらを使う。
    /// </summary>
    public int CoreAudioObjectType { get; }
    /// <summary>4bit の samplingFrequencyIndex。0x0F は明示レートのエスケープ。</summary>
    public int SamplingFrequencyIndex { get; }
    /// <summary>コアのサンプリング周波数(Hz)。予約インデックス(13/14)は 0。</summary>
    public int SampleRate { get; }
    /// <summary>SBR 拡張の samplingFrequencyIndex。明示signalingでない場合は -1。</summary>
    public int ExtensionSamplingFrequencyIndex { get; }
    /// <summary>
    /// デコード後の出力サンプリング周波数(Hz)。SBR の明示signaling時はコアの2倍相当の
    /// 拡張レートになる。明示signalingでない場合は <see cref="SampleRate"/> と同じ。
    /// </summary>
    public int OutputSampleRate { get; }
    /// <summary>4bit の channelConfiguration。個数ではなくインデックス。</summary>
    public int ChannelConfiguration { get; }
    /// <summary>
    /// channelConfiguration が示すチャンネル数。determine できない場合(0 および予約値)は 0。
    /// コンテナのチャンネル数フィールドにはインデックスではなくこちらを書く。
    /// </summary>
    public int ChannelCount { get; }

    private AudioSpecificConfig(
      int audio_object_type,
      int core_audio_object_type,
      int sampling_frequency_index,
      int sample_rate,
      int extension_sampling_frequency_index,
      int output_sample_rate,
      int channel_configuration)
    {
      AudioObjectType                = audio_object_type;
      CoreAudioObjectType            = core_audio_object_type;
      SamplingFrequencyIndex         = sampling_frequency_index;
      SampleRate                     = sample_rate;
      ExtensionSamplingFrequencyIndex = extension_sampling_frequency_index;
      OutputSampleRate               = output_sample_rate;
      ChannelConfiguration           = channel_configuration;
      ChannelCount                   = channel_configuration<ChannelCounts.Length ? ChannelCounts[channel_configuration] : 0;
    }

    /// <summary>
    /// サンプリング周波数(Hz)から 4bit の samplingFrequencyIndex を逆引きする。
    /// 明示レートのエスケープ(0x0F)で通知された配信を、インデックスしか表現できない
    /// 形式(ADTS 等)へ載せ替えるために使う。表にない周波数は false。
    /// </summary>
    public static bool TryGetSamplingFrequencyIndex(int sample_rate, out int index)
    {
      for (var i=0; i<SamplingFrequencies.Length; i++) {
        if (SamplingFrequencies[i]==sample_rate) {
          index = i;
          return true;
        }
      }
      index = -1;
      return false;
    }

    private static bool TryReadAudioObjectType(BitReader reader, out int type)
    {
      if (!reader.TryReadBits(5, out type)) return false;
      if (type==31) {
        if (!reader.TryReadBits(6, out var ext)) return false;
        type = ext+32;
      }
      return true;
    }

    private static bool TryReadSampleRate(BitReader reader, int freq_idx, out int sample_rate)
    {
      if (freq_idx==0x0F) {
        return reader.TryReadBits(24, out sample_rate);
      }
      sample_rate = freq_idx<SamplingFrequencies.Length ? SamplingFrequencies[freq_idx] : 0;
      return true;
    }

    /// <summary>ビットが不足する場合は false を返す(例外は投げない)。</summary>
    public static bool TryParse(byte[] config, out AudioSpecificConfig result)
    {
      result = default;
      var reader = new BitReader(config);
      if (!TryReadAudioObjectType(reader, out var type)) return false;
      if (!reader.TryReadBits(4, out var freq_idx)) return false;
      if (!TryReadSampleRate(reader, freq_idx, out var sample_rate)) return false;
      if (!reader.TryReadBits(4, out var channels)) return false;
      var core_type          = type;
      var ext_freq_idx       = -1;
      var output_sample_rate = sample_rate;
      // AOT 5(SBR)/29(PS)は SBR/PS の明示signaling。この場合 samplingFrequencyIndex は
      // コア(出力の半分)のレートで、続けて拡張レートと本来のコア audioObjectType が並ぶ。
      // ここを読まないと「44.1kHz の HE-AAC」を 22.05kHz の AOT=5 と誤って扱うことになる。
      if (type==5 || type==29) {
        if (!reader.TryReadBits(4, out ext_freq_idx)) return false;
        if (!TryReadSampleRate(reader, ext_freq_idx, out output_sample_rate)) return false;
        if (!TryReadAudioObjectType(reader, out core_type)) return false;
      }
      result = new AudioSpecificConfig(
        type, core_type, freq_idx, sample_rate, ext_freq_idx, output_sample_rate, channels);
      return true;
    }
  }

}
