using System;
using System.IO;
using PeerCastStation.Core;
using PeerCastStation.FLV.AMF;
using PeerCastStation.MKV;

namespace PeerCastStation.FLV.RTMP
{
  /// <summary>
  /// Enhanced RTMP(および旧 FLV)を Matroska へ remux(再エンコード無し)する
  /// <see cref="IRTMPContentSink"/>。<see cref="FLVContentBuffer"/> の Matroska 版。
  ///
  /// onMetaData と 映像+音声の SequenceHeader(config)が揃うと
  /// <see cref="MatroskaInitBuilder.BuildInit"/> で init 領域(EBML/Segment/Info/Tracks)を作り
  /// OnContentHeader で送出する。以降は映像キーフレームごとに新しい Cluster を開き、
  /// 各フレームを SimpleBlock として OnContent で送出する。
  ///
  /// 「全 Cluster はキーフレームで始まる」を不変条件とし、後発参加者は次の Cluster ID から
  /// resync できる(既存 MKVContentReader の挙動と対称)。init 完了かつ最初の映像キーフレーム
  /// 到達までは全フレームを破棄する。低遅延 B フレーム無し運用前提で DTS=PTS とみなす(CTS は M5)。
  /// 単一映像 + 単一音声トラック前提。
  /// </summary>
  internal class MatroskaContentBuffer
    : IRTMPContentSink
  {
    private const ulong VideoTrackNumber = 1;
    private const ulong AudioTrackNumber = 2;

    public Channel      TargetChannel { get; private set; }
    public IContentSink ContentSink   { get; private set; }
    public long         Position      { get { return position; } }

    private long     position    = 0;
    private int      streamIndex = -1;
    private DateTime streamOrigin;

    // config(SequenceHeader)。映像・音声が揃うと init を出せる。
    private string? videoFourCc      = null;
    private byte[]? videoCodecPrivate = null;
    private string? audioFourCc      = null;
    private byte[]? audioCodecPrivate = null;

    // onMetaData 由来のトラック属性。
    private bool   hasMetadata = false;
    private int    pixelWidth        = 0;
    private int    pixelHeight       = 0;
    private double samplingFrequency = 48000.0;
    private int    channels          = 2;

    private bool initSent        = false;
    private bool seenFirstKeyFrame = false;
    private bool hasTimestampOrigin = false;
    private long timestampOrigin = 0;
    private long currentClusterTimecode = 0;

    public MatroskaContentBuffer(Channel target_channel, IContentSink content_sink)
    {
      this.TargetChannel = target_channel;
      // 既存 MKVContentReader と同じく Cluster/SimpleBlock 単位でそのまま流す
      // (FLVContentBuffer のような BufferedContentSink での再分割はしない)。
      this.ContentSink   = content_sink;
    }

    public void OnFLVHeader(FLVFileHeader header)
    {
      // Matroska 経路では FLV ヘッダは使わない。
    }

    public void OnData(DataMessage msg)
    {
      switch (msg.PropertyName) {
      case "@setDataFrame":
        if (msg.Arguments.Count>=2 && (string?)msg.Arguments[0]=="onMetaData") {
          ReadMetaData(msg.Arguments[1]);
        }
        break;
      case "onMetaData":
        if (msg.Arguments.Count>=1) {
          ReadMetaData(msg.Arguments[0]);
        }
        break;
      }
    }

    private void ReadMetaData(AMFValue meta)
    {
      if (meta.Type!=AMFValueType.ECMAArray && meta.Type!=AMFValueType.Object) return;
      pixelWidth        = (int)ReadDouble(meta, "width", pixelWidth);
      pixelHeight       = (int)ReadDouble(meta, "height", pixelHeight);
      samplingFrequency = ReadDouble(meta, "audiosamplerate", samplingFrequency);
      channels          = (int)ReadDouble(meta, "audiochannels", channels);
      hasMetadata = pixelWidth>0 && pixelHeight>0;

      var info = new AtomCollection();
      info.SetChanInfoType("MKV");
      info.SetChanInfoStreamType("video/x-matroska");
      info.SetChanInfoStreamExt(".mkv");
      var bitrate = 0.0;
      var val = meta["maxBitrate"];
      if (!AMFValue.IsNull(val)) {
        double maxBitrate;
        var maxBitrateStr = System.Text.RegularExpressions.Regex.Replace((string?)val ?? "", @"([\d]+)k", "$1");
        if (double.TryParse(maxBitrateStr, out maxBitrate)) {
          bitrate += maxBitrate;
        }
      }
      else if (!AMFValue.IsNull(val = meta["videodatarate"])) {
        bitrate += (double)val;
      }
      if (!AMFValue.IsNull(val = meta["audiodatarate"])) {
        bitrate += (double)val;
      }
      if (bitrate>0) {
        info.SetChanInfoBitrate((int)bitrate);
      }
      ContentSink.OnChannelInfo(new ChannelInfo(info));

      TrySendInit();
    }

    private static double ReadDouble(AMFValue meta, string key, double fallback)
    {
      var val = meta[key];
      if (AMFValue.IsNull(val)) return fallback;
      try {
        return (double)val;
      }
      catch (FormatException) {
        return fallback;
      }
      catch (InvalidCastException) {
        return fallback;
      }
    }

    public void OnVideo(RTMPMessage msg)
    {
      if (!ERTMPDepacketizer.TryParseVideo(msg, out var frame)) return;
      HandleFrame(frame);
    }

    public void OnAudio(RTMPMessage msg)
    {
      if (!ERTMPDepacketizer.TryParseAudio(msg, out var frame)) return;
      HandleFrame(frame);
    }

    private void HandleFrame(DepacketizedFrame frame)
    {
      switch (frame.Kind) {
      case DepacketizedFrameKind.SequenceHeader:
        if (frame.TrackType==DepacketizedTrackType.Video) {
          videoFourCc       = frame.FourCc;
          videoCodecPrivate = ToArray(frame.Payload);
        }
        else {
          audioFourCc       = frame.FourCc;
          audioCodecPrivate = ToArray(frame.Payload);
        }
        TrySendInit();
        break;

      case DepacketizedFrameKind.CodedFrame:
        WriteFrame(frame);
        break;

      default:
        // SequenceEnd/Metadata/Unknown は当面破棄(remux に不要)。
        break;
      }
    }

    private void TrySendInit()
    {
      if (initSent) return;
      if (!hasMetadata) return;
      if (videoFourCc==null || videoCodecPrivate==null) return;
      if (audioFourCc==null || audioCodecPrivate==null) return;

      var video = new MatroskaVideoTrack {
        FourCc       = videoFourCc,
        CodecPrivate = videoCodecPrivate,
        PixelWidth   = pixelWidth,
        PixelHeight  = pixelHeight,
      };
      var audio = new MatroskaAudioTrack {
        FourCc            = audioFourCc,
        CodecPrivate      = audioCodecPrivate,
        SamplingFrequency = samplingFrequency,
        Channels          = channels,
      };
      var init = MatroskaInitBuilder.BuildInit(video, audio);

      streamIndex  = TargetChannel.GenerateStreamID();
      streamOrigin = DateTime.Now;
      position     = 0;
      ContentSink.OnContentHeader(
        new Content(streamIndex, TimeSpan.Zero, position, init, PCPChanPacketContinuation.None));
      position += init.Length;
      initSent = true;
    }

    private void WriteFrame(DepacketizedFrame frame)
    {
      if (!initSent) return;

      var isVideo = frame.TrackType==DepacketizedTrackType.Video;
      // 最初の映像キーフレーム到達までは破棄(Cluster はキーフレームで開く)。
      if (!seenFirstKeyFrame) {
        if (!(isVideo && frame.IsKeyFrame)) return;
        seenFirstKeyFrame = true;
      }
      if (!hasTimestampOrigin) {
        timestampOrigin    = frame.Timestamp;
        hasTimestampOrigin = true;
      }
      var rebased = frame.Timestamp - timestampOrigin;

      using (var ms=new MemoryStream()) {
        // 映像キーフレームごとに新しい Cluster を開く。
        if (isVideo && frame.IsKeyFrame) {
          currentClusterTimecode = rebased;
          MatroskaWriter.MakeUnknownSizeHeader(Elements.Cluster).Write(ms);
          MatroskaWriter.MakeUInt(Elements.Timecode, (ulong)Math.Max(0, currentClusterTimecode)).Write(ms);
        }
        var relative = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, rebased - currentClusterTimecode));
        var track    = isVideo ? VideoTrackNumber : AudioTrackNumber;
        MatroskaWriter.MakeSimpleBlock(track, relative, frame.IsKeyFrame, frame.Payload).Write(ms);

        var bytes = ms.ToArray();
        ContentSink.OnContent(
          new Content(streamIndex, DateTime.Now-streamOrigin, position, bytes, PCPChanPacketContinuation.None));
        position += bytes.Length;
      }
    }

    private static byte[] ToArray(ArraySegment<byte> seg)
    {
      var arr = new byte[seg.Count];
      if (seg.Count>0) {
        Array.Copy(seg.Array!, seg.Offset, arr, 0, seg.Count);
      }
      return arr;
    }
  }
}
