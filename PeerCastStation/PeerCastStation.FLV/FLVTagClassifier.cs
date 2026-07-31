// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Philmist
using PeerCastStation.FLV.RTMP;

namespace PeerCastStation.FLV
{
  /// <summary>
  /// FLV/RTMP のメディアタグを、レガシー(codecId/soundFormat)と
  /// enhanced RTMP(E-RTMP、Ex タグ)の差を吸収して分類した結果の種別。
  /// </summary>
  internal enum FLVTagKind
  {
    /// <summary>分類できない。切り詰められたタグや未知の構造。</summary>
    Unknown,
    /// <summary>構造は解釈できたが本実装が扱わない。Multitrack や非対応コーデック、制御タグ。</summary>
    Unsupported,
    /// <summary>音声のコーデック設定(AAC の AudioSpecificConfig 等)。</summary>
    AudioSequenceHeader,
    AudioFrame,
    AudioSequenceEnd,
    /// <summary>映像のコーデック設定(avcC/hvcC/av1C 等の生バイト)。</summary>
    VideoSequenceHeader,
    /// <summary>
    /// MPEG-2 TS 形式のシーケンス開始(E-RTMP の MPEG2TSSequenceStart)。
    /// コーデック設定の生バイトではないため VideoSequenceHeader とは別扱いにする。
    /// </summary>
    VideoMpeg2TsSequenceHeader,
    VideoKeyFrame,
    VideoInterFrame,
    VideoSequenceEnd,
  }

  /// <summary>
  /// タグ1つの分類結果。レガシー/Ex いずれのタグも同じ形で表現し、
  /// 下流のフィルタがヘッダ形式を意識せずペイロードを取り出せるようにする。
  /// </summary>
  internal readonly struct FLVTagInfo
  {
    public FLVTagKind Kind { get; }
    /// <summary>enhanced RTMP(Ex ヘッダ)のタグか。</summary>
    public bool IsEnhanced { get; }
    /// <summary>
    /// コーデック識別子。レガシータグも E-RTMP の FourCC に正規化する
    /// (AVC は avc1、AAC は mp4a)。特定できない場合は null。
    /// </summary>
    public string? FourCc { get; }
    /// <summary>コーデックデータの先頭オフセット。取得できない場合は -1。</summary>
    public int PayloadOffset { get; }
    /// <summary>CompositionTime(CTS, ミリ秒)。映像フレーム以外は 0。</summary>
    public int CompositionTime { get; }

    public FLVTagInfo(FLVTagKind kind, bool is_enhanced, string? fourcc, int payload_offset, int composition_time)
    {
      Kind            = kind;
      IsEnhanced      = is_enhanced;
      FourCc          = fourcc;
      PayloadOffset   = payload_offset;
      CompositionTime = composition_time;
    }

    /// <summary>コーデック設定/フレームの実体が body 内に存在するか。</summary>
    public bool HasPayload(byte[] body)
    {
      return PayloadOffset>=0 && PayloadOffset<body.Length;
    }

    /// <summary>映像のシーケンス開始(通常/MPEG-2 TS 形式のいずれか)か。</summary>
    public bool IsVideoSequenceStart {
      get {
        return Kind==FLVTagKind.VideoSequenceHeader ||
               Kind==FLVTagKind.VideoMpeg2TsSequenceHeader;
      }
    }
  }

  /// <summary>
  /// FLV/RTMP のメディアタグ分類器。
  /// レガシータグと E-RTMP の Ex タグを同じ <see cref="FLVTagInfo"/> に正規化する。
  ///
  /// FLVContentBuffer(チャンネルヘッダ判定)・FLVToMKV・FLVToMPEG2TS が
  /// 個別に body[0] を解釈していると、E-RTMP チャンネルが成立した際に
  /// 一方だけが Ex タグを理解し、他方が packetType を codecId と誤読する
  /// (例: Ex の ModEx(7) を AVC と誤認する)。分類はここに一本化する。
  /// </summary>
  internal static class FLVTagClassifier
  {
    /// <summary>H.264。レガシー codecId 7 もこれに正規化する。</summary>
    public const string FourCcAvc = "avc1";
    /// <summary>AAC。レガシー soundFormat 10 もこれに正規化する。</summary>
    public const string FourCcAac = "mp4a";

    private static readonly FLVTagInfo Unknown =
      new FLVTagInfo(FLVTagKind.Unknown, false, null, -1, 0);

    public static FLVTagInfo Classify(RTMPMessage msg)
    {
      switch (msg.MessageType) {
      case RTMPMessageType.Audio:
        return ClassifyAudio(msg.Body);
      case RTMPMessageType.Video:
        return ClassifyVideo(msg.Body);
      default:
        return Unknown;
      }
    }

    private static FLVTagInfo ClassifyAudio(byte[] body)
    {
      if (!ExAudioTagHeader.TryParse(body, out var header)) return Unknown;
      if (!header.IsExHeader) return ClassifyLegacyAudio(body);
      if (header.IsMultitrack) {
        return new FLVTagInfo(FLVTagKind.Unsupported, true, header.FourCc, -1, 0);
      }
      switch (header.PacketType) {
      case AudioPacketType.SequenceStart:
        return new FLVTagInfo(FLVTagKind.AudioSequenceHeader, true, header.FourCc, header.PayloadOffset, 0);
      case AudioPacketType.CodedFrames:
        return new FLVTagInfo(FLVTagKind.AudioFrame, true, header.FourCc, header.PayloadOffset, 0);
      case AudioPacketType.SequenceEnd:
        return new FLVTagInfo(FLVTagKind.AudioSequenceEnd, true, header.FourCc, header.PayloadOffset, 0);
      default:
        return new FLVTagInfo(FLVTagKind.Unsupported, true, header.FourCc, header.PayloadOffset, 0);
      }
    }

    /// <summary>
    /// レガシー音声タグ。body: [0]=soundFormat|rate|size|type, AAC のみ [1]=AACPacketType, [2..]=データ。
    /// </summary>
    private static FLVTagInfo ClassifyLegacyAudio(byte[] body)
    {
      if (body.Length<2) return Unknown;
      var sound_format = (body[0]>>4) & 0x0F;
      if (sound_format!=10) {
        // AAC 以外(MP3/PCM/Speex 等)は本実装のいずれのフィルタも扱わない。
        return new FLVTagInfo(FLVTagKind.Unsupported, false, null, -1, 0);
      }
      var kind = body[1]==0 ? FLVTagKind.AudioSequenceHeader : FLVTagKind.AudioFrame;
      return new FLVTagInfo(kind, false, FourCcAac, 2, 0);
    }

    private static FLVTagInfo ClassifyVideo(byte[] body)
    {
      if (!ExVideoTagHeader.TryParse(body, out var header)) return Unknown;
      if (!header.IsExHeader) return ClassifyLegacyVideo(body);
      if (header.IsMultitrack) {
        return new FLVTagInfo(FLVTagKind.Unsupported, true, header.FourCc, -1, 0);
      }
      // FrameType==1(key)と ==4(generated key)をキーフレームとする。
      var keyframe = header.FrameType==1 || header.FrameType==4;
      switch (header.PacketType) {
      case VideoPacketType.SequenceStart:
        return new FLVTagInfo(FLVTagKind.VideoSequenceHeader, true, header.FourCc, header.PayloadOffset, 0);
      case VideoPacketType.MPEG2TSSequenceStart:
        return new FLVTagInfo(FLVTagKind.VideoMpeg2TsSequenceHeader, true, header.FourCc, header.PayloadOffset, 0);
      case VideoPacketType.CodedFrames:
      case VideoPacketType.CodedFramesX:
        return new FLVTagInfo(
          keyframe ? FLVTagKind.VideoKeyFrame : FLVTagKind.VideoInterFrame,
          true, header.FourCc, header.PayloadOffset, header.CompositionTime);
      case VideoPacketType.SequenceEnd:
        return new FLVTagInfo(FLVTagKind.VideoSequenceEnd, true, header.FourCc, header.PayloadOffset, 0);
      default:
        return new FLVTagInfo(FLVTagKind.Unsupported, true, header.FourCc, header.PayloadOffset, 0);
      }
    }

    /// <summary>
    /// レガシー映像タグ。body: [0]=frameType|codecId, AVC のみ [1]=AVCPacketType, [2..4]=CTS, [5..]=データ。
    /// </summary>
    private static FLVTagInfo ClassifyLegacyVideo(byte[] body)
    {
      if (body.Length<2) return Unknown;
      var frame_type = (body[0]>>4) & 0x0F;
      var codec_id   = body[0] & 0x0F;
      if (codec_id!=7) {
        // H.264 以外のレガシーコーデック(Sorenson/VP6 等)は扱わない。
        return new FLVTagInfo(FLVTagKind.Unsupported, false, null, -1, 0);
      }
      switch (body[1]) {
      case 0:
        return new FLVTagInfo(FLVTagKind.VideoSequenceHeader, false, FourCcAvc, 5, 0);
      case 1: {
        var keyframe = frame_type==1 || frame_type==4;
        return new FLVTagInfo(
          keyframe ? FLVTagKind.VideoKeyFrame : FLVTagKind.VideoInterFrame,
          false, FourCcAvc, 5, LegacyCompositionTime(body));
      }
      case 2:
        return new FLVTagInfo(FLVTagKind.VideoSequenceEnd, false, FourCcAvc, -1, 0);
      default:
        return new FLVTagInfo(FLVTagKind.Unsupported, false, FourCcAvc, -1, 0);
      }
    }

    /// <summary>レガシー AVC タグの符号付き24bit CompositionTime。</summary>
    private static int LegacyCompositionTime(byte[] body)
    {
      if (body.Length<5) return 0;
      var cts = (body[2]<<16) | (body[3]<<8) | body[4];
      if (cts>=0x800000) cts -= 0x1000000;
      return cts;
    }
  }

}
