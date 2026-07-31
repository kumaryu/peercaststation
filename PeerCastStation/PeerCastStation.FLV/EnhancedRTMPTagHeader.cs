// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Philmist
using System;
using System.Text;

namespace PeerCastStation.FLV
{
  internal enum VideoPacketType
  {
    SequenceStart        = 0,
    CodedFrames          = 1,
    SequenceEnd          = 2,
    CodedFramesX         = 3,
    Metadata             = 4,
    MPEG2TSSequenceStart = 5,
    Multitrack           = 6,
    ModEx                = 7,
  }

  internal enum AudioPacketType
  {
    SequenceStart     = 0,
    CodedFrames       = 1,
    SequenceEnd       = 2,
    MultichannelConfig = 4,
    Multitrack        = 5,
    ModEx             = 7,
  }

  internal enum AvMultitrackType
  {
    OneTrack             = 0,
    ManyTracks           = 1,
    ManyTracksManyCodecs = 2,
  }

  /// <summary>
  /// Ex タグヘッダ(映像/音声)に共通する読み出し処理。
  /// ModEx プレフィックスと FourCC の形式は E-RTMP 仕様上どちらもコーデック非依存で
  /// 完全に同一なので、音声側・映像側で実装を分けず一箇所にまとめる。
  /// </summary>
  internal static class ExTagHeaderReader
  {
    /// <summary>
    /// ModEx プレフィックスを1つ読み飛ばし、後続バイトの packetType を返す。
    /// バイトが不足する場合は false を返す(例外は投げない)。
    /// </summary>
    public static bool SkipModEx(byte[] body, ref int pos, out int packet_type)
    {
      packet_type = 0;
      if (pos>=body.Length) return false;
      var size = body[pos] + 1;
      pos++;
      if (size==256) {
        if (pos+1>=body.Length) return false;
        size = ((body[pos]<<8) | body[pos+1]) + 1;
        pos += 2;
      }
      pos += size; // modExData を読み飛ばす
      if (pos>=body.Length) return false;
      // 次バイト: 上位ニブル=packetModExType, 下位ニブル=packetType
      packet_type = body[pos] & 0x0F;
      pos++;
      return true;
    }

    /// <summary>FourCC を4バイト読む。バイトが不足する場合は null を返す。</summary>
    public static string? ReadFourCc(byte[] body, ref int pos)
    {
      if (pos+4>body.Length) return null;
      var s = Encoding.ASCII.GetString(body, pos, 4);
      pos += 4;
      return s;
    }
  }

  /// <summary>
  /// Enhanced RTMP(E-RTMP) v2 の ExVideoTagHeader を解析した結果。
  /// ModEx/Multitrack を剥がした後の packetType と共通 FourCC を保持する。
  /// </summary>
  internal readonly struct ExVideoTagHeader
  {
    public bool            IsExHeader   { get; }
    public int             FrameType    { get; }
    public VideoPacketType PacketType   { get; }
    public string?         FourCc       { get; }
    public bool            IsMultitrack { get; }
    /// <summary>コーデックデータの先頭オフセット。解析不能/Multitrack/レガシーは -1。</summary>
    public int             PayloadOffset   { get; }
    /// <summary>CompositionTime(CTS, ミリ秒)。CodedFrames のみ有効、その他は 0。</summary>
    public int             CompositionTime { get; }

    private ExVideoTagHeader(bool is_ex, int frame_type, VideoPacketType packet_type, string? fourcc, bool is_multitrack, int payload_offset, int composition_time)
    {
      IsExHeader      = is_ex;
      FrameType       = frame_type;
      PacketType      = packet_type;
      FourCc          = fourcc;
      IsMultitrack    = is_multitrack;
      PayloadOffset   = payload_offset;
      CompositionTime = composition_time;
    }

    /// <summary>
    /// 映像メッセージの body から ExVideoTagHeader を解析する。
    /// バイト不足など解析できない場合は false を返す（例外は投げない）。
    /// 非 enhanced(レガシー)タグの場合は IsExHeader=false で true を返す。
    /// </summary>
    public static bool TryParse(byte[] body, out ExVideoTagHeader result)
    {
      result = default;
      if (body.Length<1) return false;
      var b0 = body[0];
      if ((b0 & 0x80)==0) {
        // レガシー(非 enhanced)タグ
        result = new ExVideoTagHeader(false, (b0>>4) & 0x07, default, null, false, -1, 0);
        return true;
      }
      var frame_type  = (b0>>4) & 0x07;
      var packet_type = b0 & 0x0F;
      var pos = 1;

      // ModEx: 実 packetType が現れるまでプレフィックスを読み飛ばす
      while (packet_type==(int)VideoPacketType.ModEx) {
        if (!ExTagHeaderReader.SkipModEx(body, ref pos, out packet_type)) return false;
      }

      var is_multitrack = false;
      string? fourcc = null;
      int payload_offset = -1;
      int composition_time = 0;
      if (packet_type==(int)VideoPacketType.Multitrack) {
        is_multitrack = true;
        if (pos>=body.Length) return false;
        var multitrack_type = (body[pos]>>4) & 0x0F;
        packet_type = body[pos] & 0x0F;
        pos++;
        if (multitrack_type!=(int)AvMultitrackType.ManyTracksManyCodecs) {
          fourcc = ExTagHeaderReader.ReadFourCc(body, ref pos);
          if (fourcc==null) return false;
        }
        // Multitrack の per-track フレーミングは未対応。payload_offset は無効(-1)のまま。
      }
      else {
        fourcc = ExTagHeaderReader.ReadFourCc(body, ref pos);
        // 非 Multitrack の Ex タグは仕様上必ず FourCC を持つ。読めない = 切り詰められた
        // 不正タグなので、FourCc=null・PayloadOffset=-1 の半端な結果を成功として返さない。
        // (呼び出し側がこれをコーデック設定付きのタグと誤認するのを防ぐ)
        if (fourcc==null) return false;
        // E-RTMP v2 仕様: CodedFrames で 24bit compositionTimeOffset を持つのは
        // AVC/HEVC/VVC のみ。AV1/VP8/VP9 は body 先頭が即コーデックデータ(AV1なら
        // temporal unit の OBU 列)なので、ここで読むとフレーム先頭3バイトを欠落させる。
        // VVC(vvc1)は未対応のため対象外。CodedFramesX は CTS=0(常に持たない)。
        if (packet_type==(int)VideoPacketType.CodedFrames &&
            (fourcc=="avc1" || fourcc=="hvc1" || fourcc=="hev1")) {
          if (pos+3>body.Length) return false;
          composition_time = (body[pos]<<16) | (body[pos+1]<<8) | body[pos+2];
          if (composition_time>=0x800000) composition_time -= 0x1000000;
          pos += 3;
        }
        payload_offset = pos;
      }

      result = new ExVideoTagHeader(true, frame_type, (VideoPacketType)packet_type, fourcc, is_multitrack, payload_offset, composition_time);
      return true;
    }
  }

  /// <summary>
  /// Enhanced RTMP(E-RTMP) v2 の ExAudioTagHeader を解析した結果。
  /// </summary>
  internal readonly struct ExAudioTagHeader
  {
    public bool            IsExHeader   { get; }
    public AudioPacketType PacketType   { get; }
    public string?         FourCc       { get; }
    public bool            IsMultitrack { get; }
    /// <summary>コーデックデータの先頭オフセット。解析不能/Multitrack/レガシーは -1。</summary>
    public int             PayloadOffset { get; }

    private ExAudioTagHeader(bool is_ex, AudioPacketType packet_type, string? fourcc, bool is_multitrack, int payload_offset)
    {
      IsExHeader    = is_ex;
      PacketType    = packet_type;
      FourCc        = fourcc;
      IsMultitrack  = is_multitrack;
      PayloadOffset = payload_offset;
    }

    /// <summary>
    /// 音声メッセージの body から ExAudioTagHeader を解析する。
    /// 非 enhanced(レガシー)音声(soundFormat!=9)の場合は IsExHeader=false で true を返す。
    /// </summary>
    public static bool TryParse(byte[] body, out ExAudioTagHeader result)
    {
      result = default;
      if (body.Length<1) return false;
      var b0 = body[0];
      var sound_format = (b0>>4) & 0x0F;
      if (sound_format!=9) {
        // レガシー(非 enhanced)音声
        result = new ExAudioTagHeader(false, default, null, false, -1);
        return true;
      }
      var packet_type = b0 & 0x0F;
      var pos = 1;

      while (packet_type==(int)AudioPacketType.ModEx) {
        if (!ExTagHeaderReader.SkipModEx(body, ref pos, out packet_type)) return false;
      }

      var is_multitrack = false;
      string? fourcc = null;
      int payload_offset = -1;
      if (packet_type==(int)AudioPacketType.Multitrack) {
        is_multitrack = true;
        if (pos>=body.Length) return false;
        var multitrack_type = (body[pos]>>4) & 0x0F;
        packet_type = body[pos] & 0x0F;
        pos++;
        if (multitrack_type!=(int)AvMultitrackType.ManyTracksManyCodecs) {
          fourcc = ExTagHeaderReader.ReadFourCc(body, ref pos);
          if (fourcc==null) return false;
        }
        // Multitrack の per-track フレーミングは未対応。payload_offset は無効(-1)のまま。
      }
      else {
        // 映像側と同じく、FourCC が読めない切り詰めタグは解析失敗として扱う。
        fourcc = ExTagHeaderReader.ReadFourCc(body, ref pos);
        if (fourcc==null) return false;
        payload_offset = pos;
      }

      result = new ExAudioTagHeader(true, (AudioPacketType)packet_type, fourcc, is_multitrack, payload_offset);
      return true;
    }
  }

}
