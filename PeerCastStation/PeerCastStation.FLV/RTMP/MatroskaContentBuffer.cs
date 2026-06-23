// SPDX-License-Identifier: GPL-3.0-or-later
// (C) 2026 Philmist
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
  /// OnContentHeader で送出する。以降は映像キーフレーム、および前 Cluster から一定時間
  /// (<see cref="ClusterTimeLimitMs"/>)が経過した映像フレームで新しい Cluster を開き、
  /// 各フレームを SimpleBlock として OnContent で送出する。
  ///
  /// Cluster を開くパケットを <see cref="PCPChanPacketContinuation.None"/> にすることで、Core の
  /// GetFirstContent が後発参加者を必ず Cluster 境界から開始させる(Cluster 外の裸 SimpleBlock を
  /// 配信しない)。GOP が内容バッファ(3秒)より長くても、時間境界 Cluster により直近の join 点が
  /// バッファ内に残る。init 完了かつ最初の映像キーフレーム到達までは全フレームを破棄する。
  /// CTS→PTS を適用(avc1/hvc1 は B フレームで CTS が乗る)。単一映像 + 単一音声トラック前提。
  /// </summary>
  internal class MatroskaContentBuffer
    : IRTMPContentSink
  {
    private static readonly Logger Logger = new Logger(typeof(MatroskaContentBuffer));

    private const ulong VideoTrackNumber = 1;
    private const ulong AudioTrackNumber = 2;

    // 映像・音声の config が揃ってから、この数の CodedFrame が来ても onMetaData が
    // 得られない場合は、CodecPrivate だけで init を組み立てる(width/height 不明のまま)。
    private const int MetadataWaitFrames = 60;

    // 後発参加者の join 点(Cluster 境界)は内容バッファ(ContentCollection.PacketTimeLimit
    // =3秒)に残っている必要がある。GOP がこれより長い配信ではキーフレームが追い出され
    // join 点が消えてしまうため、キーフレームに加えてこの媒体時間ごとにも新 Cluster を開く。
    // 3秒より十分短く取る。
    private const long ClusterTimeLimitMs = 1000;

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

    // config が揃ってから metadata 無しで経過した CodedFrame 数。
    private int  framesSinceConfigReady = 0;
    private bool metadataFallback       = false;
    // 1 回だけ出す警告のためのフラグ(ログのスパム防止)。
    private bool warnedUnexpectedCts       = false;
    private bool warnedTimecodeSaturation  = false;
    private int  parseFailureCount         = 0;

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

      SendChannelInfo(ReadBitrate(meta));

      TrySendInit();
    }

    // onMetaData から bitrate(kbps)を取り出す。無ければ 0。FLVContentBuffer と同方針で
    // maxBitrate(優先)→videodatarate、それに audiodatarate を加算する。
    private static double ReadBitrate(AMFValue meta)
    {
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
      return bitrate;
    }

    // ChannelInfo(MKV)を送る。bitrate 不明(0)時は FLVContentBuffer 同様に値を捏造せず
    // 未設定のままにする(AccessController のリレー計算に誤った値を与えないため)。
    private void SendChannelInfo(double bitrate)
    {
      var info = new AtomCollection();
      info.SetChanInfoType("MKV");
      info.SetChanInfoStreamType("video/x-matroska");
      info.SetChanInfoStreamExt(".mkv");
      if (bitrate>0) {
        info.SetChanInfoBitrate((int)bitrate);
      }
      else {
        Logger.Debug("Channel bitrate unknown (no maxBitrate/videodatarate in onMetaData)");
      }
      ContentSink.OnChannelInfo(new ChannelInfo(info));
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

    // パース失敗の連続を検知できるよう最初の数回だけログする(スパム防止)。
    private const int MaxParseFailureLogs = 5;

    public void OnVideo(RTMPMessage msg)
    {
      if (!ERTMPDepacketizer.TryParseVideo(msg, out var frame)) {
        LogParseFailure("video", msg);
        return;
      }
      HandleFrame(frame);
    }

    public void OnAudio(RTMPMessage msg)
    {
      if (!ERTMPDepacketizer.TryParseAudio(msg, out var frame)) {
        LogParseFailure("audio", msg);
        return;
      }
      HandleFrame(frame);
    }

    private void LogParseFailure(string kind, RTMPMessage msg)
    {
      if (parseFailureCount>=MaxParseFailureLogs) return;
      parseFailureCount++;
      Logger.Debug("Failed to depacketize {0} message (len={1}){2}",
        kind, msg.Body?.Length ?? 0,
        parseFailureCount==MaxParseFailureLogs ? " [further parse failures suppressed]" : "");
    }

    private void HandleFrame(DepacketizedFrame frame)
    {
      switch (frame.Kind) {
      case DepacketizedFrameKind.SequenceHeader:
        var newConfig = ToArray(frame.Payload);
        if (frame.TrackType==DepacketizedTrackType.Video) {
          WarnIfConfigChanged("video", videoFourCc, videoCodecPrivate, frame.FourCc, newConfig);
          videoFourCc       = frame.FourCc;
          videoCodecPrivate = newConfig;
        }
        else {
          WarnIfConfigChanged("audio", audioFourCc, audioCodecPrivate, frame.FourCc, newConfig);
          audioFourCc       = frame.FourCc;
          audioCodecPrivate = newConfig;
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

    // init 送出後に config(コーデック/CodecPrivate)が変わったら Info で可視化する。
    // init は再送しない(後発参加者 resync の不変条件を壊さないため)。将来対応の足場。
    private void WarnIfConfigChanged(string kind, string? oldFourCc, byte[]? oldConfig, string newFourCc, byte[] newConfig)
    {
      if (!initSent || oldConfig==null) return;
      if (oldFourCc==newFourCc && BytesEqual(oldConfig, newConfig)) return;
      Logger.Info(
        "Mid-stream {0} config change ignored (init not resent): {1} -> {2}",
        kind, oldFourCc ?? "?", newFourCc);
    }

    private static bool BytesEqual(byte[] a, byte[] b)
    {
      if (a.Length!=b.Length) return false;
      for (var i=0; i<a.Length; i++) {
        if (a[i]!=b[i]) return false;
      }
      return true;
    }

    private bool ConfigReady {
      get {
        return videoFourCc!=null && videoCodecPrivate!=null
            && audioFourCc!=null && audioCodecPrivate!=null;
      }
    }

    private void TrySendInit()
    {
      if (initSent) return;
      if (!ConfigReady) return;
      // 通常は onMetaData(解像度)が揃うのを待つが、来ない配信でも
      // metadataFallback が立てば config だけで init を出す(width/height=0)。
      if (!hasMetadata && !metadataFallback) return;

      // metadata が無いまま init する場合は ChannelInfo(MKV)もここで送っておく。
      if (!hasMetadata) {
        SendChannelInfo(0.0);
      }

      var video = new MatroskaVideoTrack {
        FourCc       = videoFourCc!,
        CodecPrivate = videoCodecPrivate!,
        PixelWidth   = pixelWidth,
        PixelHeight  = pixelHeight,
      };
      var audio = new MatroskaAudioTrack {
        FourCc            = audioFourCc!,
        CodecPrivate      = audioCodecPrivate!,
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
      Logger.Info(
        "Matroska init sent: video={0} {1}x{2} (codecPrivate={3}B), audio={4} {5}Hz {6}ch (codecPrivate={7}B){8}",
        videoFourCc!, pixelWidth, pixelHeight, videoCodecPrivate!.Length,
        audioFourCc!, (int)samplingFrequency, channels, audioCodecPrivate!.Length,
        hasMetadata ? "" : " [no onMetaData; dimensions unknown]");
    }

    private void WriteFrame(DepacketizedFrame frame)
    {
      if (!initSent) {
        // config は揃っているが onMetaData 待ちの間にフレームが来続けたら、
        // 一定数を超えたところで config だけの init にフォールバックする。
        if (ConfigReady && !hasMetadata && !metadataFallback) {
          if (++framesSinceConfigReady>=MetadataWaitFrames) {
            metadataFallback = true;
            Logger.Debug("onMetaData not received after {0} frames; falling back to config-only init", framesSinceConfigReady);
            TrySendInit();
          }
        }
        if (!initSent) return;
      }

      var isVideo = frame.TrackType==DepacketizedTrackType.Video;
      // 最初の映像キーフレーム到達までは破棄(Cluster はキーフレームで開く)。
      if (!seenFirstKeyFrame) {
        if (!(isVideo && frame.IsKeyFrame)) return;
        seenFirstKeyFrame = true;
        Logger.Debug("First video keyframe reached; starting Matroska cluster output");
      }

      // 提示時刻(PTS)= DTS + CompositionTimeOffset を使う。avc1/hvc1 は B フレームで
      // CTS が乗る。av01 は CTS を持たない(常に 0)ので実質 DTS=PTS。
      if (isVideo && frame.CompositionTimeOffset!=0 && frame.FourCc=="av01" && !warnedUnexpectedCts) {
        Logger.Warn("Unexpected non-zero CTS ({0}ms) on av01 frame; av01 should not carry CompositionTime", frame.CompositionTimeOffset);
        warnedUnexpectedCts = true;
      }
      var pts = frame.Timestamp + frame.CompositionTimeOffset;
      if (!hasTimestampOrigin) {
        timestampOrigin    = pts;
        hasTimestampOrigin = true;
      }
      var rebased = pts - timestampOrigin;

      // Cluster を開く条件:映像キーフレーム、または前 Cluster から ClusterTimeLimitMs 以上の
      // 媒体時間が経過した映像フレーム。後者により GOP が内容バッファ(3秒)より長い配信でも
      // 直近 Cluster 境界が必ずバッファ内に残り、後発参加者がそこから再生開始できる。
      // 非キーフレーム始まりの Cluster でもコンテナは有効で、次の本物のキーフレームまで映像は
      // 乱れるが音声は出る(自己完結タグの FLV 経路と同等の挙動)。Cluster は映像フレームでのみ
      // 開く(先頭ブロックを映像にし、relative timecode の基準を素直に保つため)。
      var openCluster =
        isVideo && (frame.IsKeyFrame ||
                    rebased - currentClusterTimecode >= ClusterTimeLimitMs);

      using (var ms=new MemoryStream()) {
        if (openCluster) {
          currentClusterTimecode = rebased;
          MatroskaWriter.MakeUnknownSizeHeader(Elements.Cluster).Write(ms);
          MatroskaWriter.MakeUInt(Elements.Timecode, (ulong)Math.Max(0, currentClusterTimecode)).Write(ms);
        }
        // 相対 timecode は符号付き int16(±約32.7秒)。Cluster を最大 ClusterTimeLimitMs ごとに
        // 開くため通常は飽和しないが、安全網として検知時に 1 度だけ警告しクランプする。
        var rawRelative = rebased - currentClusterTimecode;
        if ((rawRelative>short.MaxValue || rawRelative<short.MinValue) && !warnedTimecodeSaturation) {
          Logger.Warn("SimpleBlock relative timecode {0}ms exceeds int16 range; timestamp clamped.", rawRelative);
          warnedTimecodeSaturation = true;
        }
        var relative = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, rawRelative));
        var track    = isVideo ? VideoTrackNumber : AudioTrackNumber;
        MatroskaWriter.MakeSimpleBlock(track, relative, frame.IsKeyFrame, frame.Payload).Write(ms);

        // 後発参加者のリシンク点を Core の GetFirstContent に伝える。GetFirstContent は
        // ContFlag==None のパケットを join 点に選ぶので、Cluster を開くパケット(キーフレーム
        // および時間境界)を None にする。これにより GOP がバッファより長くても直近 Cluster
        // から再生開始でき、Cluster 外の裸 SimpleBlock(厳格な新 mpv が壊れ判定)を防ぐ。
        // 中間映像は InterFrame、音声は AudioFrame。
        var contFlag =
          openCluster ? PCPChanPacketContinuation.None :
          isVideo     ? PCPChanPacketContinuation.InterFrame :
                        PCPChanPacketContinuation.AudioFrame;

        var bytes = ms.ToArray();
        ContentSink.OnContent(
          new Content(streamIndex, DateTime.Now-streamOrigin, position, bytes, contFlag));
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
