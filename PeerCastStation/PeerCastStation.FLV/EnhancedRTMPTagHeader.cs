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

    private ExVideoTagHeader(bool is_ex, int frame_type, VideoPacketType packet_type, string? fourcc, bool is_multitrack)
    {
      IsExHeader   = is_ex;
      FrameType    = frame_type;
      PacketType   = packet_type;
      FourCc       = fourcc;
      IsMultitrack = is_multitrack;
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
        result = new ExVideoTagHeader(false, (b0>>4) & 0x07, default, null, false);
        return true;
      }
      var frame_type  = (b0>>4) & 0x07;
      var packet_type = b0 & 0x0F;
      var pos = 1;

      // ModEx: 実 packetType が現れるまでプレフィックスを読み飛ばす
      while (packet_type==(int)VideoPacketType.ModEx) {
        if (!SkipModEx(body, ref pos, out packet_type)) return false;
      }

      var is_multitrack = false;
      string? fourcc = null;
      if (packet_type==(int)VideoPacketType.Multitrack) {
        is_multitrack = true;
        if (pos>=body.Length) return false;
        var multitrack_type = (body[pos]>>4) & 0x0F;
        packet_type = body[pos] & 0x0F;
        pos++;
        if (multitrack_type!=(int)AvMultitrackType.ManyTracksManyCodecs) {
          fourcc = ReadFourCc(body, ref pos);
        }
      }
      else {
        fourcc = ReadFourCc(body, ref pos);
      }

      result = new ExVideoTagHeader(true, frame_type, (VideoPacketType)packet_type, fourcc, is_multitrack);
      return true;
    }

    private static bool SkipModEx(byte[] body, ref int pos, out int packet_type)
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

    private static string? ReadFourCc(byte[] body, ref int pos)
    {
      if (pos+4>body.Length) return null;
      var s = Encoding.ASCII.GetString(body, pos, 4);
      pos += 4;
      return s;
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

    private ExAudioTagHeader(bool is_ex, AudioPacketType packet_type, string? fourcc, bool is_multitrack)
    {
      IsExHeader   = is_ex;
      PacketType   = packet_type;
      FourCc       = fourcc;
      IsMultitrack = is_multitrack;
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
        result = new ExAudioTagHeader(false, default, null, false);
        return true;
      }
      var packet_type = b0 & 0x0F;
      var pos = 1;

      while (packet_type==(int)AudioPacketType.ModEx) {
        if (!SkipModEx(body, ref pos, out packet_type)) return false;
      }

      var is_multitrack = false;
      string? fourcc = null;
      if (packet_type==(int)AudioPacketType.Multitrack) {
        is_multitrack = true;
        if (pos>=body.Length) return false;
        var multitrack_type = (body[pos]>>4) & 0x0F;
        packet_type = body[pos] & 0x0F;
        pos++;
        if (multitrack_type!=(int)AvMultitrackType.ManyTracksManyCodecs) {
          fourcc = ReadFourCc(body, ref pos);
        }
      }
      else {
        fourcc = ReadFourCc(body, ref pos);
      }

      result = new ExAudioTagHeader(true, (AudioPacketType)packet_type, fourcc, is_multitrack);
      return true;
    }

    private static bool SkipModEx(byte[] body, ref int pos, out int packet_type)
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
      pos += size;
      if (pos>=body.Length) return false;
      packet_type = body[pos] & 0x0F;
      pos++;
      return true;
    }

    private static string? ReadFourCc(byte[] body, ref int pos)
    {
      if (pos+4>body.Length) return null;
      var s = Encoding.ASCII.GetString(body, pos, 4);
      pos += 4;
      return s;
    }
  }

}
