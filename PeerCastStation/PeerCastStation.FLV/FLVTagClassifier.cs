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
    /// <summary>構造は解釈できたが本実装が扱わない。Multitrack や非対応コーデック。</summary>
    Unsupported,
    /// <summary>
    /// 対応コーデックの正常な制御パケットで、多重化には使わないもの
    /// (E-RTMP の映像 Metadata=HDR colorInfo 等、音声 MultichannelConfig)。
    /// </summary>
    /// <remarks>
    /// 健全な配信で普通に流れてくるため Unsupported と分けて黙って捨てる。
    /// 一緒くたにすると「未対応コーデック」警告が正常な配信で出るうえ、
    /// 1回だけの警告枠をこれらが使い切って本当の非対応コーデックが無警告になる。
    /// </remarks>
    Control,
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
    /// <summary>
    /// 既知コーデックの記述子。レガシータグも E-RTMP 相当の記述子に正規化する
    /// (AVC は FourCcRegistry.Avc、AAC は FourCcRegistry.Aac)。
    /// 未知の FourCC・特定不能の場合は null。
    /// </summary>
    public FourCcCodec? Codec { get; }
    /// <summary>
    /// 表示用のコーデック識別子。既知なら記述子の名前、未知でも読めた4文字は警告用に返す。
    /// 特定できない場合は null。
    /// </summary>
    public string? FourCc { get; }
    /// <summary>コーデックデータの先頭オフセット。取得できない場合は -1。</summary>
    public int PayloadOffset { get; }
    /// <summary>CompositionTime(CTS, ミリ秒)。映像フレーム以外は 0。</summary>
    public int CompositionTime { get; }
    /// <summary>
    /// タグの frameType がキーフレーム(1 または 4)として通知されていたか。
    /// </summary>
    /// <remarks>
    /// 多重化にはキーフレームか否かを表す <see cref="FLVTagKind"/> を使えば足りるが、
    /// シーケンスヘッダについては「キーフレームとして送られてきたか」を別途知りたい
    /// 利用者(チャンネルヘッダへの昇格可否を決める FLVContentBuffer)がいるため公開する。
    /// </remarks>
    public bool IsKeyFrameSignaled { get; }

    // レガシー/Ex の区別は下流のどこも見ない(見る必要が出ないよう Kind と Codec に
    // 正規化するのがこの型の役目)ため保持しない。読み手のいない位置指定 bool を
    // 引数に残すと、引数の入れ替わりをコンパイラもテストも検出できなくなる。
    public FLVTagInfo(
      FLVTagKind kind,
      FourCcCodec? codec,
      int payload_offset,
      int composition_time,
      bool key_frame_signaled = false,
      string? raw_fourcc = null)
    {
      Kind               = kind;
      Codec              = codec;
      FourCc             = codec?.Name ?? raw_fourcc;
      PayloadOffset      = payload_offset;
      CompositionTime    = composition_time;
      IsKeyFrameSignaled = key_frame_signaled;
    }

    /// <summary>コーデック設定/フレームの実体が body 内に存在するか。</summary>
    public bool HasPayload(byte[] body)
    {
      return PayloadOffset>=0 && PayloadOffset<body.Length;
    }

    /// <summary>
    /// タグ本体からコーデックデータ部を切り出す。オフセットが無効(Multitrack の -1 など)か
    /// 本体が尽きている場合は空配列を返す。
    /// </summary>
    /// <remarks>
    /// 同じ境界判定を利用側ごとに書くと、オフセットの取り決めを変えたときに
    /// 追従漏れが出るのでここへ集約する。
    /// </remarks>
    public static byte[] SlicePayload(byte[] body, int offset)
    {
      if (offset<0 || body.Length<=offset) return System.Array.Empty<byte>();
      var result = new byte[body.Length-offset];
      System.Array.Copy(body, offset, result, 0, result.Length);
      return result;
    }
  }

  /// <summary>
  /// FLV/RTMP のメディアタグ分類器。
  /// レガシータグと E-RTMP の Ex タグを同じ <see cref="FLVTagInfo"/> に正規化する。
  /// </summary>
  /// <remarks>
  /// FLVContentBuffer(チャンネルヘッダ判定)・FLVToMKV・FLVToMPEG2TS が
  /// 個別に body[0] を解釈していると、E-RTMP チャンネルが成立した際に
  /// 一方だけが Ex タグを理解し、他方が packetType を codecId と誤読する
  /// (例: Ex の ModEx(7) を AVC と誤認する)。分類はここに一本化する。
  /// </remarks>
  internal static class FLVTagClassifier
  {
    private static readonly FLVTagInfo Unknown =
      new FLVTagInfo(FLVTagKind.Unknown, null, -1, 0);

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
        return new FLVTagInfo(FLVTagKind.Unsupported, header.Codec, -1, 0, raw_fourcc: header.FourCc);
      }
      switch (header.PacketType) {
      case AudioPacketType.SequenceStart:
        return new FLVTagInfo(FLVTagKind.AudioSequenceHeader, header.Codec, header.PayloadOffset, 0, raw_fourcc: header.FourCc);
      case AudioPacketType.CodedFrames:
        return new FLVTagInfo(FLVTagKind.AudioFrame, header.Codec, header.PayloadOffset, 0, raw_fourcc: header.FourCc);
      case AudioPacketType.SequenceEnd:
        return new FLVTagInfo(FLVTagKind.AudioSequenceEnd, header.Codec, header.PayloadOffset, 0, raw_fourcc: header.FourCc);
      case AudioPacketType.MultichannelConfig:
        return new FLVTagInfo(FLVTagKind.Control, header.Codec, header.PayloadOffset, 0, raw_fourcc: header.FourCc);
      default:
        return new FLVTagInfo(FLVTagKind.Unsupported, header.Codec, header.PayloadOffset, 0, raw_fourcc: header.FourCc);
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
        return new FLVTagInfo(FLVTagKind.Unsupported, null, -1, 0);
      }
      var kind = body[1]==0 ? FLVTagKind.AudioSequenceHeader : FLVTagKind.AudioFrame;
      return new FLVTagInfo(kind, FourCcRegistry.Aac, 2, 0);
    }

    private static FLVTagInfo ClassifyVideo(byte[] body)
    {
      if (!ExVideoTagHeader.TryParse(body, out var header)) return Unknown;
      if (!header.IsExHeader) return ClassifyLegacyVideo(body);
      if (header.IsMultitrack) {
        return new FLVTagInfo(FLVTagKind.Unsupported, header.Codec, -1, 0, raw_fourcc: header.FourCc);
      }
      var keyframe = IsKeyFrameType(header.FrameType);
      switch (header.PacketType) {
      case VideoPacketType.SequenceStart:
        return new FLVTagInfo(
          FLVTagKind.VideoSequenceHeader, header.Codec, header.PayloadOffset, 0,
          key_frame_signaled: keyframe, raw_fourcc: header.FourCc);
      case VideoPacketType.MPEG2TSSequenceStart:
        return new FLVTagInfo(FLVTagKind.VideoMpeg2TsSequenceHeader, header.Codec, header.PayloadOffset, 0, raw_fourcc: header.FourCc);
      case VideoPacketType.CodedFrames:
      case VideoPacketType.CodedFramesX:
        return new FLVTagInfo(
          keyframe ? FLVTagKind.VideoKeyFrame : FLVTagKind.VideoInterFrame,
          header.Codec, header.PayloadOffset, header.CompositionTime,
          key_frame_signaled: keyframe, raw_fourcc: header.FourCc);
      case VideoPacketType.SequenceEnd:
        return new FLVTagInfo(FLVTagKind.VideoSequenceEnd, header.Codec, header.PayloadOffset, 0, raw_fourcc: header.FourCc);
      case VideoPacketType.Metadata:
        return new FLVTagInfo(FLVTagKind.Control, header.Codec, header.PayloadOffset, 0, raw_fourcc: header.FourCc);
      default:
        return new FLVTagInfo(FLVTagKind.Unsupported, header.Codec, header.PayloadOffset, 0, raw_fourcc: header.FourCc);
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
        return new FLVTagInfo(FLVTagKind.Unsupported, null, -1, 0);
      }
      // frameType=5 は video info/command frame で、body[1] は AVCPacketType ではなく
      // コマンド番号(0=StartOfClientSeek)。AVCPacketType として解釈すると
      // コマンド 0 をコーデック設定と誤認するので、多重化しない制御パケットとして扱う。
      if (frame_type==5) {
        return new FLVTagInfo(FLVTagKind.Control, FourCcRegistry.Avc, -1, 0);
      }
      var keyframe = IsKeyFrameType(frame_type);
      switch (body[1]) {
      case 0:
        // AVC シーケンスヘッダは通常キーフレームとして送られるが、frameType=2(inter)を
        // 立てて送るエンコーダ/中継実装が実在する。ここで frameType を条件にすると
        // avcC を取り逃し、nalSizeLen が決まらないまま以後の全フレームが捨てられて
        // 映像が一切出なくなる(TS/MKV 双方)ため、分類はキーフレームか否かで絞らない。
        // 一方でこれをチャンネルヘッダへ昇格させるかは別の判断で、壊れたインターフレーム
        // (0x27 0x00 ...)を昇格させると GenerateStreamID() が呼ばれ、タグ1つで全視聴者の
        // 再初期化を繰り返し起こせる。判断材料として IsKeyFrameSignaled だけを渡し、
        // 昇格の可否は FLVContentBuffer 側で決める。
        return new FLVTagInfo(
          FLVTagKind.VideoSequenceHeader, FourCcRegistry.Avc, 5, 0, key_frame_signaled: keyframe);
      case 1:
        return new FLVTagInfo(
          keyframe ? FLVTagKind.VideoKeyFrame : FLVTagKind.VideoInterFrame,
          FourCcRegistry.Avc, 5, LegacyCompositionTime(body), key_frame_signaled: keyframe);
      case 2:
        return new FLVTagInfo(FLVTagKind.VideoSequenceEnd, FourCcRegistry.Avc, -1, 0);
      default:
        return new FLVTagInfo(FLVTagKind.Unsupported, FourCcRegistry.Avc, -1, 0);
      }
    }

    /// <remarks>
    /// FrameType==1(key)と ==4(generated key)をキーフレームとする。
    /// レガシー/Ex の両経路が同じ規則を使うよう一箇所にまとめる。
    /// </remarks>
    private static bool IsKeyFrameType(int frame_type)
    {
      return frame_type==1 || frame_type==4;
    }

    /// <summary>レガシー AVC タグの符号付き24bit CompositionTime。</summary>
    private static int LegacyCompositionTime(byte[] body)
    {
      if (body.Length<5) return 0;
      return ExTagHeaderReader.ReadInt24Signed(body, 2);
    }
  }

}
