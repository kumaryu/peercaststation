using System;
using System.IO;

namespace PeerCastStation.MKV
{
  /// <summary>映像トラックの中立記述(コーデック非依存)。</summary>
  internal struct MatroskaVideoTrack
  {
    /// <summary>実パケットの FourCc("av01"/"hvc1"/"avc1")。</summary>
    public string FourCc;
    /// <summary>SequenceHeader のペイロード(av1C/hvcC/avcC)をそのまま CodecPrivate に入れる。</summary>
    public byte[] CodecPrivate;
    public int PixelWidth;
    public int PixelHeight;
  }

  /// <summary>音声トラックの中立記述(コーデック非依存)。</summary>
  internal struct MatroskaAudioTrack
  {
    /// <summary>実パケットの FourCc("mp4a")。</summary>
    public string FourCc;
    /// <summary>AudioSpecificConfig をそのまま CodecPrivate に入れる。</summary>
    public byte[] CodecPrivate;
    public double SamplingFrequency;
    public int Channels;
  }

  /// <summary>
  /// Enhanced RTMP / 旧 FLV から取り出したコーデック config とメタ情報から、
  /// Matroska の init 領域(EBML Header → Segment 開始 → Info → Tracks)の
  /// バイト列を組み立てる。Clusterは含めない。
  /// 単一映像 + 単一音声トラック前提(capsEx=0、multitrack 非対応)。
  /// CodecPrivate は中身を解析せず透過コピーする(av1C=20B / ASC=5Bを確認)。
  /// </summary>
  internal static class MatroskaInitBuilder
  {
    public const ulong DefaultTimecodeScale = 1000000; //1ms(RTMP の ms タイムスタンプと素直に対応)
    public const string WriterName = "PeerCastStation";

    private const ulong VideoTrackNumber = 1;
    private const ulong AudioTrackNumber = 2;
    private const ulong TrackTypeVideo = 1;
    private const ulong TrackTypeAudio = 2;

    /// <summary>FourCc を Matroska の CodecID 文字列へ対応付ける。</summary>
    public static string FourCcToCodecId(string fourCc)
    {
      switch (fourCc) {
      case "av01": return "V_AV1";
      case "hvc1": return "V_MPEGH/ISO/HEVC";
      case "avc1": return "V_MPEG4/ISO/AVC";
      case "mp4a": return "A_AAC";
      default:
        throw new ArgumentException($"未対応の FourCc: {fourCc}", nameof(fourCc));
      }
    }

    /// <summary>映像 + 音声トラックから init 領域バイト列を組み立てる。</summary>
    public static byte[] BuildInit(MatroskaVideoTrack video, MatroskaAudioTrack audio)
    {
      var ebml = MatroskaWriter.MakeMaster(Elements.EBML,
        MatroskaWriter.MakeUInt(Elements.EBMLVersion, 1),
        MatroskaWriter.MakeUInt(Elements.EBMLReadVersion, 1),
        MatroskaWriter.MakeUInt(Elements.EBMLMaxIDLength, 4),
        MatroskaWriter.MakeUInt(Elements.EBMLMaxSizeLength, 8),
        MatroskaWriter.MakeString(Elements.DocType, "matroska"),
        MatroskaWriter.MakeUInt(Elements.DocTypeVersion, 4),     //AV1/HEVC を含めるため 4
        MatroskaWriter.MakeUInt(Elements.DocTypeReadVersion, 2));

      var info = MatroskaWriter.MakeMaster(Elements.Info,
        MatroskaWriter.MakeUInt(Elements.TimecodeScale, DefaultTimecodeScale),
        MatroskaWriter.MakeString(Elements.MuxingApp, WriterName),
        MatroskaWriter.MakeString(Elements.WritingApp, WriterName));

      var tracks = MatroskaWriter.MakeMaster(Elements.Tracks,
        MakeVideoTrackEntry(video),
        MakeAudioTrackEntry(audio));

      var segmentHeader = MatroskaWriter.MakeUnknownSizeHeader(Elements.Segment);

      using (var ms=new MemoryStream()) {
        ebml.Write(ms);
        segmentHeader.Write(ms); //Segment は開いたまま(unknown-size)。以降は Segment の子。
        info.Write(ms);
        tracks.Write(ms);
        return ms.ToArray();
      }
    }

    private static Element MakeVideoTrackEntry(MatroskaVideoTrack video)
    {
      return MatroskaWriter.MakeMaster(Elements.TrackEntry,
        MatroskaWriter.MakeUInt(Elements.TrackNumber, VideoTrackNumber),
        MatroskaWriter.MakeUInt(Elements.TrackUID, VideoTrackNumber),
        MatroskaWriter.MakeUInt(Elements.TrackType, TrackTypeVideo),
        MatroskaWriter.MakeString(Elements.CodecID, FourCcToCodecId(video.FourCc)),
        MatroskaWriter.MakeBinary(Elements.CodecPrivate, video.CodecPrivate),
        MatroskaWriter.MakeMaster(Elements.Video,
          MatroskaWriter.MakeUInt(Elements.PixelWidth, (ulong)video.PixelWidth),
          MatroskaWriter.MakeUInt(Elements.PixelHeight, (ulong)video.PixelHeight)));
    }

    private static Element MakeAudioTrackEntry(MatroskaAudioTrack audio)
    {
      return MatroskaWriter.MakeMaster(Elements.TrackEntry,
        MatroskaWriter.MakeUInt(Elements.TrackNumber, AudioTrackNumber),
        MatroskaWriter.MakeUInt(Elements.TrackUID, AudioTrackNumber),
        MatroskaWriter.MakeUInt(Elements.TrackType, TrackTypeAudio),
        MatroskaWriter.MakeString(Elements.CodecID, FourCcToCodecId(audio.FourCc)),
        MatroskaWriter.MakeBinary(Elements.CodecPrivate, audio.CodecPrivate),
        MatroskaWriter.MakeMaster(Elements.Audio,
          MatroskaWriter.MakeFloat(Elements.SamplingFrequency, audio.SamplingFrequency),
          MatroskaWriter.MakeUInt(Elements.Channels, (ulong)audio.Channels)));
    }
  }
}
