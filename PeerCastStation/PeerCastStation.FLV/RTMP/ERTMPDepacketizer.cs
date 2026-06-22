using System;

namespace PeerCastStation.FLV.RTMP
{
  /// <summary>
  /// 中間構造体が表すトラック種別。
  /// </summary>
  internal enum DepacketizedTrackType
  {
    Video,
    Audio,
  }

  /// <summary>
  /// 1 つの RTMP メッセージ(映像/音声)が運ぶデータの種類。
  /// コーデック非依存で正規化したうえで、Matroska 化(M3/M4)で
  /// CodecPrivate(SequenceHeader)と SimpleBlock(CodedFrame)に振り分ける。
  /// </summary>
  internal enum DepacketizedFrameKind
  {
    /// <summary>コーデック設定(avcC/hvcC/av1C/AudioSpecificConfig)。CodecPrivate の元。</summary>
    SequenceHeader,
    /// <summary>生フレーム(NALU/OBU/AAC raw)。SimpleBlock の元。</summary>
    CodedFrame,
    /// <summary>シーケンス終端。</summary>
    SequenceEnd,
    /// <summary>Enhanced RTMP のフレーム単位メタデータ。</summary>
    Metadata,
    /// <summary>未対応・解釈不能。</summary>
    Unknown,
  }

  /// <summary>
  /// RTMPMessage(映像/音声)を正規化した、コーデック非依存の中間表現。
  /// Enhanced RTMP(ExHeader)も旧 FLV(AVC/AAC)も同じ形に正規化する。
  /// </summary>
  internal struct DepacketizedFrame
  {
    public DepacketizedTrackType TrackType;
    /// <summary>正規化した FourCC("avc1"/"hvc1"/"av01"/"mp4a" 等)。不明なら空文字。</summary>
    public string FourCc;
    public DepacketizedFrameKind Kind;
    public bool IsKeyFrame;
    /// <summary>RTMPMessage.Timestamp(デコードタイムスタンプ DTS, ミリ秒)。</summary>
    public long Timestamp;
    /// <summary>映像の Composition Time オフセット(符号付き, ミリ秒)。音声や CTS 無しは 0。</summary>
    public int CompositionTimeOffset;
    /// <summary>FLV/RTMP のヘッダを剥がしたペイロード(config 本体 or 生フレーム)。</summary>
    public ArraySegment<byte> Payload;
  }

  /// <summary>
  /// RTMPMessage(映像/音声)を <see cref="DepacketizedFrame"/> に正規化する純粋パーサ。
  /// 副作用を持たないため単体テスト可能(internal、テストへは InternalsVisibleTo で公開)。
  /// M2 時点では出力はダンプ/ユニットテストのみで、Matroska 化(CodecPrivate/Cluster)は M3/M4。
  /// </summary>
  internal static class ERTMPDepacketizer
  {
    // 旧 FLV ビデオの CodecID(VideoTagHeader 下位 4bit)。
    private const int FlvVideoCodecAvc = 7;
    // 旧 FLV オーディオの SoundFormat(AudioTagHeader 上位 4bit)。
    private const int FlvAudioFormatAac          = 10;
    private const int FlvAudioFormatExHeader     = 9; // Enhanced RTMP v2 の音声 ExHeader 識別子(将来対応)

    // Enhanced RTMP video packet type(byte0 の下位 4bit)。
    private const int VideoPacketTypeSequenceStart        = 0;
    private const int VideoPacketTypeCodedFrames          = 1;
    private const int VideoPacketTypeSequenceEnd          = 2;
    private const int VideoPacketTypeCodedFramesX         = 3;
    private const int VideoPacketTypeMetadata             = 4;
    private const int VideoPacketTypeMpeg2TsSequenceStart = 5;

    // Enhanced RTMP の ExHeader を示す byte0 の最上位ビット。
    private const int ExHeaderFlag = 0x80;

    /// <summary>
    /// 映像 RTMPMessage を正規化する。解釈不能(短すぎる等)なら false。
    /// </summary>
    public static bool TryParseVideo(RTMPMessage msg, out DepacketizedFrame frame)
    {
      frame = default;
      if (msg==null || msg.MessageType!=RTMPMessageType.Video) return false;
      var body = msg.Body;
      if (body==null || body.Length<1) return false;

      var b0 = body[0];
      frame.TrackType = DepacketizedTrackType.Video;
      frame.Timestamp = msg.Timestamp;

      if ((b0 & ExHeaderFlag)!=0) {
        return TryParseEnhancedVideo(msg, b0, ref frame);
      }
      else {
        return TryParseLegacyVideo(msg, b0, ref frame);
      }
    }

    /// <summary>
    /// 音声 RTMPMessage を正規化する。当面 AAC を主対象とし、Enhanced 音声 ExHeader は枠のみ。
    /// </summary>
    public static bool TryParseAudio(RTMPMessage msg, out DepacketizedFrame frame)
    {
      frame = default;
      if (msg==null || msg.MessageType!=RTMPMessageType.Audio) return false;
      var body = msg.Body;
      if (body==null || body.Length<1) return false;

      var b0 = body[0];
      frame.TrackType = DepacketizedTrackType.Audio;
      frame.Timestamp = msg.Timestamp;
      frame.IsKeyFrame = true; // 音声フレームは常にデコード可能とみなす。

      var soundFormat = (b0>>4) & 0x0F;
      switch (soundFormat) {
      case FlvAudioFormatAac:
        return TryParseLegacyAac(msg, ref frame);
      case FlvAudioFormatExHeader:
        // Enhanced RTMP v2 の音声 ExHeader(Opus/FLAC/AC-3 等)。将来対応。
        frame.Kind    = DepacketizedFrameKind.Unknown;
        frame.FourCc  = "";
        frame.Payload = new ArraySegment<byte>(body, Math.Min(1, body.Length), Math.Max(0, body.Length-1));
        return true;
      default:
        // MP3 等のその他レガシ音声。当面は本体だけ持たせる(remux 対象外)。
        frame.Kind    = DepacketizedFrameKind.Unknown;
        frame.FourCc  = "";
        frame.Payload = new ArraySegment<byte>(body, Math.Min(1, body.Length), Math.Max(0, body.Length-1));
        return true;
      }
    }

    private static bool TryParseEnhancedVideo(RTMPMessage msg, byte b0, ref DepacketizedFrame frame)
    {
      var body = msg.Body;
      var frameType  = (b0>>4) & 0x07; // bits 4-6
      var packetType = b0 & 0x0F;      // bits 0-3
      frame.IsKeyFrame = frameType==1;

      // FourCC(byte1-4)まで必要。
      if (body.Length<5) return false;
      frame.FourCc = System.Text.Encoding.ASCII.GetString(body, 1, 4);

      switch (packetType) {
      case VideoPacketTypeSequenceStart:
      case VideoPacketTypeMpeg2TsSequenceStart:
        frame.Kind    = DepacketizedFrameKind.SequenceHeader;
        frame.Payload = Segment(body, 5);
        return true;
      case VideoPacketTypeCodedFrames:
        // FourCC の後に 3byte の符号付き CompositionTime が続く。
        if (body.Length<8) return false;
        frame.Kind    = DepacketizedFrameKind.CodedFrame;
        frame.CompositionTimeOffset = ReadSInt24(body, 5);
        frame.Payload = Segment(body, 8);
        return true;
      case VideoPacketTypeCodedFramesX:
        // CompositionTime 無し(=0)。
        frame.Kind    = DepacketizedFrameKind.CodedFrame;
        frame.CompositionTimeOffset = 0;
        frame.Payload = Segment(body, 5);
        return true;
      case VideoPacketTypeSequenceEnd:
        frame.Kind    = DepacketizedFrameKind.SequenceEnd;
        frame.Payload = Segment(body, 5);
        return true;
      case VideoPacketTypeMetadata:
        frame.Kind    = DepacketizedFrameKind.Metadata;
        frame.Payload = Segment(body, 5);
        return true;
      default:
        frame.Kind    = DepacketizedFrameKind.Unknown;
        frame.Payload = Segment(body, 5);
        return true;
      }
    }

    private static bool TryParseLegacyVideo(RTMPMessage msg, byte b0, ref DepacketizedFrame frame)
    {
      var body = msg.Body;
      var frameType = (b0>>4) & 0x0F;
      var codecId   = b0 & 0x0F;
      frame.IsKeyFrame = frameType==1;
      frame.FourCc = codecId==FlvVideoCodecAvc ? "avc1" : "";

      if (codecId==FlvVideoCodecAvc) {
        // VideoTagHeader: [b0][AVCPacketType][CTS(3)]
        if (body.Length<2) return false;
        var avcPacketType = body[1];
        switch (avcPacketType) {
        case 0: // AVCDecoderConfigurationRecord(avcC)
          frame.Kind = DepacketizedFrameKind.SequenceHeader;
          frame.CompositionTimeOffset = body.Length>=5 ? ReadSInt24(body, 2) : 0;
          frame.Payload = Segment(body, 5);
          return true;
        case 1: // NALU
          if (body.Length<5) return false;
          frame.Kind = DepacketizedFrameKind.CodedFrame;
          frame.CompositionTimeOffset = ReadSInt24(body, 2);
          frame.Payload = Segment(body, 5);
          return true;
        case 2: // end of sequence
          frame.Kind = DepacketizedFrameKind.SequenceEnd;
          frame.Payload = Segment(body, Math.Min(5, body.Length));
          return true;
        default:
          frame.Kind = DepacketizedFrameKind.Unknown;
          frame.Payload = Segment(body, Math.Min(5, body.Length));
          return true;
        }
      }
      else {
        // AVC 以外のレガシ映像(Sorenson/VP6 等)。当面 remux 対象外、本体だけ持たせる。
        frame.Kind = DepacketizedFrameKind.Unknown;
        frame.Payload = Segment(body, 1);
        return true;
      }
    }

    private static bool TryParseLegacyAac(RTMPMessage msg, ref DepacketizedFrame frame)
    {
      var body = msg.Body;
      // AudioTagHeader: [b0][AACPacketType]
      if (body.Length<2) return false;
      frame.FourCc = "mp4a";
      var aacPacketType = body[1];
      switch (aacPacketType) {
      case 0: // AudioSpecificConfig
        frame.Kind = DepacketizedFrameKind.SequenceHeader;
        break;
      case 1: // AAC raw
        frame.Kind = DepacketizedFrameKind.CodedFrame;
        break;
      default:
        frame.Kind = DepacketizedFrameKind.Unknown;
        break;
      }
      frame.Payload = Segment(body, 2);
      return true;
    }

    /// <summary>
    /// 指定オフセットから末尾までの ArraySegment を作る。offset が範囲外なら空。
    /// </summary>
    private static ArraySegment<byte> Segment(byte[] body, int offset)
    {
      if (offset>=body.Length) return new ArraySegment<byte>(body, body.Length, 0);
      return new ArraySegment<byte>(body, offset, body.Length-offset);
    }

    /// <summary>
    /// 符号付き 24bit big-endian を読む。RTMPBinaryReader.ReadUInt24 は符号拡張しないため別途用意。
    /// </summary>
    private static int ReadSInt24(byte[] body, int offset)
    {
      int v = (body[offset]<<16) | (body[offset+1]<<8) | body[offset+2];
      if ((v & 0x800000)!=0) v -= 0x1000000;
      return v;
    }
  }
}
