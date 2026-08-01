// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Philmist
using PeerCastStation.Core;
using PeerCastStation.FLV.AMF;
using PeerCastStation.FLV.RTMP;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.FLV
{
  /// <summary>
  /// EBML(Matroska) を書き出すためのプリミティブと、本実装で使用する要素IDの定数群。
  /// PeerCastStation.MKV の読み取り側とは責務が逆(マルチプレクサ)であり、
  /// internal な読み取り用型を流用できないため、必要分だけ自己完結で持つ。
  /// </summary>
  public static class EBMLWriter
  {
    // EBML header
    public static readonly byte[] EBMLHeader         = { 0x1A, 0x45, 0xDF, 0xA3 };
    public static readonly byte[] EBMLVersion        = { 0x42, 0x86 };
    public static readonly byte[] EBMLReadVersion    = { 0x42, 0xF7 };
    public static readonly byte[] EBMLMaxIDLength    = { 0x42, 0xF2 };
    public static readonly byte[] EBMLMaxSizeLength  = { 0x42, 0xF3 };
    public static readonly byte[] DocType            = { 0x42, 0x82 };
    public static readonly byte[] DocTypeVersion     = { 0x42, 0x87 };
    public static readonly byte[] DocTypeReadVersion = { 0x42, 0x85 };
    // Segment
    public static readonly byte[] Segment            = { 0x18, 0x53, 0x80, 0x67 };
    // Info
    public static readonly byte[] Info               = { 0x15, 0x49, 0xA9, 0x66 };
    public static readonly byte[] TimecodeScale      = { 0x2A, 0xD7, 0xB1 };
    public static readonly byte[] MuxingApp          = { 0x4D, 0x80 };
    public static readonly byte[] WritingApp         = { 0x57, 0x41 };
    // Tracks
    public static readonly byte[] Tracks             = { 0x16, 0x54, 0xAE, 0x6B };
    public static readonly byte[] TrackEntry         = { 0xAE };
    public static readonly byte[] TrackNumber        = { 0xD7 };
    public static readonly byte[] TrackUID           = { 0x73, 0xC5 };
    public static readonly byte[] TrackType          = { 0x83 };
    public static readonly byte[] FlagLacing         = { 0x9C };
    public static readonly byte[] CodecID            = { 0x86 };
    public static readonly byte[] CodecPrivate       = { 0x63, 0xA2 };
    public static readonly byte[] Video              = { 0xE0 };
    public static readonly byte[] PixelWidth         = { 0xB0 };
    public static readonly byte[] PixelHeight        = { 0xBA };
    public static readonly byte[] Audio              = { 0xE1 };
    public static readonly byte[] SamplingFrequency  = { 0xB5 };
    public static readonly byte[] Channels           = { 0x9F };
    // Cluster
    public static readonly byte[] Cluster            = { 0x1F, 0x43, 0xB6, 0x75 };
    public static readonly byte[] Timecode           = { 0xE7 };
    public static readonly byte[] SimpleBlock        = { 0xA3 };

    /// <summary>値を最短長のVINT(要素サイズ等)としてエンコードする。</summary>
    public static byte[] EncodeVInt(ulong value)
    {
      int length = 1;
      // 全データビットが1の値は unknown-size に予約されているため value <= 2^(7L)-2 となる最小Lを選ぶ
      while (length<8 && value >= ((1UL<<(7*length))-1)) {
        length++;
      }
      ulong v = value | (1UL<<(7*length)); // 先頭の長さマーカービット
      var bin = new byte[length];
      for (int i=length-1; i>=0; i--) {
        bin[i] = (byte)(v & 0xFF);
        v >>= 8;
      }
      return bin;
    }

    /// <summary>unknown-size(全データビット1)のVINT。Segment/Cluster のサイズ未定に使う。</summary>
    public static byte[] EncodeUnknownVInt(int length)
    {
      ulong v = (1UL<<(7*length)) | ((1UL<<(7*length))-1);
      var bin = new byte[length];
      for (int i=length-1; i>=0; i--) {
        bin[i] = (byte)(v & 0xFF);
        v >>= 8;
      }
      return bin;
    }

    /// <summary>符号なし整数を最小バイト数のビッグエンディアンでエンコードする。</summary>
    public static byte[] EncodeUInt(ulong value)
    {
      int len = 1;
      for (ulong t=value; t>0xFF; t>>=8) len++;
      var bin = new byte[len];
      for (int i=len-1; i>=0; i--) {
        bin[i] = (byte)(value & 0xFF);
        value >>= 8;
      }
      return bin;
    }

    /// <summary>Matroska float(8バイトIEEE754ビッグエンディアン)をエンコードする。</summary>
    public static byte[] EncodeFloat(double value)
    {
      var bytes = BitConverter.GetBytes(value);
      if (BitConverter.IsLittleEndian) {
        Array.Reverse(bytes);
      }
      return bytes;
    }

    public static byte[] EncodeString(string value)
    {
      return System.Text.Encoding.ASCII.GetBytes(value);
    }

    /// <summary>ID + サイズVINT + payload を書き出す(サイズ確定の要素)。</summary>
    public static void WriteElement(Stream s, byte[] id, ReadOnlySpan<byte> payload)
    {
      s.Write(id, 0, id.Length);
      var sz = EncodeVInt((ulong)payload.Length);
      s.Write(sz, 0, sz.Length);
      s.Write(payload);
    }

    /// <summary>ID + unknown-size を書き出す(Segment/Cluster の開始。子要素は後続)。</summary>
    public static void WriteMasterUnknown(Stream s, byte[] id)
    {
      s.Write(id, 0, id.Length);
      var sz = EncodeUnknownVInt(8);
      s.Write(sz, 0, sz.Length);
    }
  }

  /// <summary>
  /// FLV(H.264/AAC) を Matroska(MKV) にリマックスするマルチプレクサ。
  /// FLVToMPEG2TS の構造に倣い、TS固有部を EBML 出力に置き換えたもの。
  /// 映像コーデック固有処理は <see cref="IVideoCodecHandler"/> に分離し、現状 H.264 のみ実装する
  /// (将来 AV1/HEVC を handler 追加で対応可能にするためのシーム)。
  /// </summary>
  public class FLVToMKV
  {
    public interface IMKVContentSink
    {
      /// <summary>EBML header + Segment(unknown) + Info + Tracks(=ContentHeader相当)。</summary>
      void OnHeader(ReadOnlyMemory<byte> bytes);
      /// <summary>Cluster開始(Cluster ID+unknown-size + Timecode 要素)。</summary>
      void OnCluster(ReadOnlyMemory<byte> bytes);
      /// <summary>1つの SimpleBlock 要素。</summary>
      void OnBlock(ReadOnlyMemory<byte> bytes);
    }

    /// <summary>
    /// 映像コーデック固有処理のシーム。ペイロード切り出し(オフセット適用)は呼び出し側が行い、
    /// ハンドラは CodecID・CodecPrivate と SimpleBlock ペイロードの組み立てのみを担当する。
    /// avc1/hvc1/av01 はいずれも CodecPrivate・Block ともコンテナ無加工で流用できる。
    /// </summary>
    public interface IVideoCodecHandler
    {
      /// <summary>Matroska の CodecID。</summary>
      string CodecId { get; }
      /// <summary>Matroska の CodecPrivate(取り込んだシーケンスヘッダ)。</summary>
      byte[] CodecPrivate { get; }
      /// <summary>シーケンスヘッダ(コンテナ無加工のコーデック設定)を取り込む。</summary>
      void SetSequenceHeader(byte[] payload);
      /// <summary>組み立て後の SimpleBlock ペイロードのバイト数。</summary>
      int GetBlockPayloadLength(ReadOnlySpan<byte> payload);
      /// <summary>
      /// SimpleBlock ペイロードを dest へ直接書き出す。
      /// 呼び出し側が確保済みの出力配列に書かせることで、フレームごとの中間配列を作らない。
      /// dest の長さは <see cref="GetBlockPayloadLength"/> と一致する。
      /// </summary>
      void WriteBlockPayload(ReadOnlySpan<byte> payload, Span<byte> dest);
    }

    /// <summary>
    /// CodecPrivate もフレームデータも無加工で流用できるコーデック(avc1/hvc1/av01)用の共通ハンドラ。
    /// CodecID 文字列だけが異なる。
    /// </summary>
    public class PassthroughVideoCodecHandler
      : IVideoCodecHandler
    {
      private byte[] codecPrivate = Array.Empty<byte>();

      public string CodecId { get; }
      public byte[] CodecPrivate { get { return codecPrivate; } }

      public PassthroughVideoCodecHandler(string codecId)
      {
        CodecId = codecId;
      }

      public void SetSequenceHeader(byte[] payload)
      {
        codecPrivate = payload;
      }

      public int GetBlockPayloadLength(ReadOnlySpan<byte> payload)
      {
        return payload.Length;
      }

      public void WriteBlockPayload(ReadOnlySpan<byte> payload, Span<byte> dest)
      {
        payload.CopyTo(dest);
      }
    }

    public class Context
      : IRTMPContentSink
    {
      public int VideoTrackNumber { get; set; } = 1;
      public int AudioTrackNumber { get; set; } = 2;

      // 同一Cluster内の相対timecodeは符号付き16bit。GOP起点に加え、安全側で強制分割する閾値。
      private const long ClusterSignedLimitMs   = 30000;
      private const long AudioClusterDurationMs = 1000;

      private readonly IMKVContentSink sink;
      private readonly Logger logger = new Logger(typeof(FLVToMKV));

      private IVideoCodecHandler? videoHandler = null;
      private byte[]? audioConfig = null; // AudioSpecificConfig
      private int audioChannels = 0;
      private int audioSampleRate = 0;
      private int videoWidth = 0;
      private int videoHeight = 0;
      private bool hasVideo = false;
      private bool hasAudio = false;
      private bool videoEnabled = false;
      private bool audioEnabled = false;
      private bool headerSent = false;
      private bool warnedNoResolution = false;
      private bool warnedUnsupportedVideo = false;
      private bool warnedUnsupportedAudio = false;
      private bool warnedBrokenAudioConfig = false;
      private long ptsBase = -1;
      private bool clusterOpen = false;
      private long clusterBaseMs = 0;

      public Context(IMKVContentSink sink)
      {
        this.sink = sink;
      }

      private void Clear()
      {
        videoHandler = null;
        audioConfig = null;
        audioChannels = 0;
        audioSampleRate = 0;
        videoWidth = 0;
        videoHeight = 0;
        hasVideo = false;
        hasAudio = false;
        videoEnabled = false;
        audioEnabled = false;
        headerSent = false;
        warnedNoResolution = false;
        warnedUnsupportedVideo = false;
        warnedUnsupportedAudio = false;
        warnedBrokenAudioConfig = false;
        ptsBase = -1;
        clusterOpen = false;
        clusterBaseMs = 0;
      }

      public void OnFLVHeader(FLVFileHeader header)
      {
        Clear();
      }

      public void OnData(DataMessage msg)
      {
        // 解像度は onMetaData の width/height から取得する(コーデック非依存)。
        if (msg.PropertyName!="onMetaData") return;
        if (msg.Arguments.Count<1) return;
        var info = msg.Arguments[0];
        if (info.Type!=AMFValueType.ECMAArray && info.Type!=AMFValueType.Object) return;
        var wv = info.ContainsKey("width")  ? info["width"]  : AMFValue.Null;
        var hv = info.ContainsKey("height") ? info["height"] : AMFValue.Null;
        if (TryGetDimension(wv, out var w) && TryGetDimension(hv, out var h)) {
          videoWidth = w;
          videoHeight = h;
        }
      }

      /// <summary>
      /// onMetaData の解像度フィールドを防御的に解釈する。
      /// 値は配信者由来で型が保証されないため、AMFValue の int キャスト演算子は使わない
      /// (String に Int32.Parse を掛けて FormatException、その他の型で InvalidCastException を
      /// 投げ、FLVFileParser.Read が捕捉しないまま processorTask をフォルトさせる)。
      /// FLVContentBuffer.OnMetaData と同じく型判定+TryParse で受ける。
      /// </summary>
      private static bool TryGetDimension(AMFValue value, out int result)
      {
        result = 0;
        if (AMFValue.IsNull(value)) return false;
        double d;
        switch (value.Value) {
        case int i:
          d = i;
          break;
        case double v:
          d = v;
          break;
        case string s:
          if (!Double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return false;
          break;
        default:
          return false;
        }
        if (Double.IsNaN(d) || d<1 || d>Int32.MaxValue) return false;
        result = (int)d;
        return true;
      }

      // タグ分類は共有分類器(FLVTagClassifier)に一本化し、レガシー/enhanced の差は
      // そちらで吸収する。ここは種別ごとの処理だけを持つ。
      public void OnAudio(RTMPMessage msg)
      {
        var info = FLVTagClassifier.Classify(msg);
        if (info.Kind==FLVTagKind.AudioSequenceEnd || info.Kind==FLVTagKind.Control) return;
        if (info.FourCc!=FLVTagClassifier.FourCcAac) {
          WarnUnsupportedAudio(info.FourCc);
          return;
        }
        switch (info.Kind) {
        case FLVTagKind.AudioSequenceHeader:
          OnAudioHeader(msg.Body, info.PayloadOffset);
          break;
        case FLVTagKind.AudioFrame:
          OnAudioBody(msg, info.PayloadOffset);
          break;
        default:
          WarnUnsupportedAudio(info.FourCc);
          break;
        }
      }

      public void OnVideo(RTMPMessage msg)
      {
        var info = FLVTagClassifier.Classify(msg);
        if (info.Kind==FLVTagKind.VideoSequenceEnd || info.Kind==FLVTagKind.Control) return;
        var codecId = MapVideoCodecId(info.FourCc);
        if (codecId==null) {
          WarnUnsupportedVideo(info.FourCc);
          return;
        }
        switch (info.Kind) {
        case FLVTagKind.VideoSequenceHeader: {
          var cp = SliceFrom(msg.Body, info.PayloadOffset);
          if (cp.Length>0) SetVideoHandler(codecId, cp);
          break;
        }
        case FLVTagKind.VideoKeyFrame:
        case FLVTagKind.VideoInterFrame:
          OnVideoBody(msg, info.PayloadOffset, info.CompositionTime, info.Kind==FLVTagKind.VideoKeyFrame);
          break;
        default:
          // MPEG2TSSequenceStart はコーデック設定の生バイトではないため CodecPrivate に使えない。
          WarnUnsupportedVideo(info.FourCc);
          break;
        }
      }

      private static string? MapVideoCodecId(string? fourcc)
      {
        switch (fourcc) {
        case FLVTagClassifier.FourCcAvc: return "V_MPEG4/ISO/AVC";
        case "hvc1":
        case "hev1": return "V_MPEGH/ISO/HEVC";
        case "av01": return "V_AV1"; // Matroska の AV1 CodecID(FourCC の av01 とは異なる)
        default:     return null;
        }
      }

      private static byte[] SliceFrom(byte[] body, int offset)
      {
        if (offset<0 || body.Length<=offset) return Array.Empty<byte>();
        var r = new byte[body.Length-offset];
        Array.Copy(body, offset, r, 0, r.Length);
        return r;
      }

      private void SetVideoHandler(string codecId, byte[] codecPrivate)
      {
        var handler = new PassthroughVideoCodecHandler(codecId);
        handler.SetSequenceHeader(codecPrivate);
        videoHandler = handler;
        hasVideo = true;
      }

      private void WarnUnsupportedVideo(string? fourcc)
      {
        if (warnedUnsupportedVideo) return;
        logger.Warn("FLVToMKV: 未対応の映像コーデック/構成のため破棄します (FourCC={0})", fourcc ?? "(none)");
        warnedUnsupportedVideo = true;
      }

      private void WarnUnsupportedAudio(string? fourcc)
      {
        if (warnedUnsupportedAudio) return;
        logger.Warn("FLVToMKV: 未対応の音声コーデック/構成のため破棄します (FourCC={0})", fourcc ?? "(none)");
        warnedUnsupportedAudio = true;
      }

      private void OnAudioHeader(byte[] body, int offset)
      {
        if (offset<0 || body.Length<=offset) return;
        var config = new byte[body.Length-offset];
        Array.Copy(body, offset, config, 0, config.Length);
        // 切り詰められた AudioSpecificConfig はここで捨てる。ビット読み出しで例外を投げると
        // FLVFileParser.Read の EndOfStreamException catch がタグ先頭まで巻き戻すため
        // (=「データ待ち」と誤認される)、毒タグがバッファ先頭に残って以後の全パースが
        // 再スローし続け、出力が恒久停止したうえで contentBuffer が無限に成長する。
        if (!AudioSpecificConfig.TryParse(config, out var asc)) {
          WarnBrokenAudioConfig();
          return;
        }
        // 予約インデックス(13/14)や明示レート0はサンプリング周波数が確定しない。
        // SamplingFrequency 要素を省略すると Matroska 既定の 8000Hz と誤宣言され
        // 誤速度・誤ピッチで再生されるため、壊れた設定として破棄する。
        if (asc.SampleRate<=0) {
          WarnBrokenAudioConfig();
          return;
        }
        audioConfig = config;
        audioSampleRate = asc.SampleRate;
        audioChannels = asc.ChannelConfiguration;
        hasAudio = true;
      }

      private void WarnBrokenAudioConfig()
      {
        if (warnedBrokenAudioConfig) return;
        logger.Warn("FLVToMKV: AudioSpecificConfigが不完全なため音声シーケンスヘッダを破棄します");
        warnedBrokenAudioConfig = true;
      }

      private void OnAudioBody(RTMPMessage msg, int offset)
      {
        WriteHeaderIfNeeded();
        if (!audioEnabled) return;
        if (offset<0 || msg.Body.Length<=offset) return;
        // 最初のメディアフレーム(ts=0を含む)を基準に正規化する。
        // 2番目のフレームで確定すると先頭フレームとPTSが衝突・逆行し、
        // PTSのみを持つMKVではH.264のPOC再構成が壊れる。
        if (ptsBase<0) ptsBase = msg.Timestamp;
        var pts = msg.Timestamp - ptsBase;
        EnsureCluster(pts, false);
        var length = msg.Body.Length-offset;
        var block = AllocateSimpleBlock(AudioTrackNumber, pts, true, length, out var dest);
        new ReadOnlySpan<byte>(msg.Body, offset, length).CopyTo(dest);
        sink.OnBlock(block);
      }

      private void OnVideoBody(RTMPMessage msg, int offset, int cts, bool keyframe)
      {
        WriteHeaderIfNeeded();
        if (!videoEnabled || videoHandler==null) return;
        if (offset<0 || msg.Body.Length<=offset) return;
        // OnAudioBody と同様、最初のメディアフレームを基準に正規化する。
        if (ptsBase<0) ptsBase = msg.Timestamp;
        var dts = msg.Timestamp - ptsBase;
        var pts = dts + cts;
        EnsureCluster(pts, keyframe);
        var slice = new ReadOnlySpan<byte>(msg.Body, offset, msg.Body.Length-offset);
        var block = AllocateSimpleBlock(
          VideoTrackNumber, pts, keyframe, videoHandler.GetBlockPayloadLength(slice), out var dest);
        videoHandler.WriteBlockPayload(slice, dest);
        sink.OnBlock(block);
      }

      private void WriteHeaderIfNeeded()
      {
        if (headerSent) return;
        videoEnabled = hasVideo && videoHandler!=null && videoWidth>0 && videoHeight>0;
        audioEnabled = hasAudio && audioConfig!=null;
        if (hasVideo && videoHandler!=null && !videoEnabled && !warnedNoResolution) {
          logger.Warn("FLVToMKV: onMetaDataから解像度が取得できないため映像トラックを除外します");
          warnedNoResolution = true;
        }
        if (!videoEnabled && !audioEnabled) return;

        var ms = new MemoryStream();
        // EBML header
        var ebml = new MemoryStream();
        EBMLWriter.WriteElement(ebml, EBMLWriter.EBMLVersion,        EBMLWriter.EncodeUInt(1));
        EBMLWriter.WriteElement(ebml, EBMLWriter.EBMLReadVersion,    EBMLWriter.EncodeUInt(1));
        EBMLWriter.WriteElement(ebml, EBMLWriter.EBMLMaxIDLength,    EBMLWriter.EncodeUInt(4));
        EBMLWriter.WriteElement(ebml, EBMLWriter.EBMLMaxSizeLength,  EBMLWriter.EncodeUInt(8));
        EBMLWriter.WriteElement(ebml, EBMLWriter.DocType,            EBMLWriter.EncodeString("matroska"));
        EBMLWriter.WriteElement(ebml, EBMLWriter.DocTypeVersion,     EBMLWriter.EncodeUInt(2));
        EBMLWriter.WriteElement(ebml, EBMLWriter.DocTypeReadVersion, EBMLWriter.EncodeUInt(2));
        EBMLWriter.WriteElement(ms, EBMLWriter.EBMLHeader, ebml.ToArray());

        // Segment (unknown size)
        EBMLWriter.WriteMasterUnknown(ms, EBMLWriter.Segment);

        // Info
        var info = new MemoryStream();
        EBMLWriter.WriteElement(info, EBMLWriter.TimecodeScale, EBMLWriter.EncodeUInt(1000000));
        EBMLWriter.WriteElement(info, EBMLWriter.MuxingApp,  EBMLWriter.EncodeString("PeerCastStation"));
        EBMLWriter.WriteElement(info, EBMLWriter.WritingApp, EBMLWriter.EncodeString("PeerCastStation FLVToMKV"));
        EBMLWriter.WriteElement(ms, EBMLWriter.Info, info.ToArray());

        // Tracks
        var tracks = new MemoryStream();
        if (videoEnabled) EBMLWriter.WriteElement(tracks, EBMLWriter.TrackEntry, BuildVideoTrackEntry());
        if (audioEnabled) EBMLWriter.WriteElement(tracks, EBMLWriter.TrackEntry, BuildAudioTrackEntry());
        EBMLWriter.WriteElement(ms, EBMLWriter.Tracks, tracks.ToArray());

        sink.OnHeader(ms.ToArray());
        headerSent = true;
      }

      private byte[] BuildVideoTrackEntry()
      {
        var e = new MemoryStream();
        EBMLWriter.WriteElement(e, EBMLWriter.TrackNumber,  EBMLWriter.EncodeUInt((ulong)VideoTrackNumber));
        EBMLWriter.WriteElement(e, EBMLWriter.TrackUID,     EBMLWriter.EncodeUInt((ulong)VideoTrackNumber));
        EBMLWriter.WriteElement(e, EBMLWriter.TrackType,    EBMLWriter.EncodeUInt(1)); // video
        EBMLWriter.WriteElement(e, EBMLWriter.FlagLacing,   EBMLWriter.EncodeUInt(0));
        EBMLWriter.WriteElement(e, EBMLWriter.CodecID,      EBMLWriter.EncodeString(videoHandler!.CodecId));
        EBMLWriter.WriteElement(e, EBMLWriter.CodecPrivate, videoHandler!.CodecPrivate);
        var video = new MemoryStream();
        EBMLWriter.WriteElement(video, EBMLWriter.PixelWidth,  EBMLWriter.EncodeUInt((ulong)videoWidth));
        EBMLWriter.WriteElement(video, EBMLWriter.PixelHeight, EBMLWriter.EncodeUInt((ulong)videoHeight));
        EBMLWriter.WriteElement(e, EBMLWriter.Video, video.ToArray());
        return e.ToArray();
      }

      private byte[] BuildAudioTrackEntry()
      {
        var e = new MemoryStream();
        EBMLWriter.WriteElement(e, EBMLWriter.TrackNumber,  EBMLWriter.EncodeUInt((ulong)AudioTrackNumber));
        EBMLWriter.WriteElement(e, EBMLWriter.TrackUID,     EBMLWriter.EncodeUInt((ulong)AudioTrackNumber));
        EBMLWriter.WriteElement(e, EBMLWriter.TrackType,    EBMLWriter.EncodeUInt(2)); // audio
        EBMLWriter.WriteElement(e, EBMLWriter.FlagLacing,   EBMLWriter.EncodeUInt(0));
        EBMLWriter.WriteElement(e, EBMLWriter.CodecID,      EBMLWriter.EncodeString("A_AAC"));
        EBMLWriter.WriteElement(e, EBMLWriter.CodecPrivate, audioConfig!);
        var audio = new MemoryStream();
        // SamplingFrequency は必ず書く。省略すると Matroska の既定値 8000Hz と解釈され、
        // 実レートと食い違った音声トラックになる。OnAudioHeader が sampleRate<=0 の設定を
        // 弾いているので、ここに来た時点で audioSampleRate は必ず正。
        EBMLWriter.WriteElement(audio, EBMLWriter.SamplingFrequency, EBMLWriter.EncodeFloat(audioSampleRate));
        EBMLWriter.WriteElement(audio, EBMLWriter.Channels, EBMLWriter.EncodeUInt((ulong)Math.Max(1, audioChannels)));
        EBMLWriter.WriteElement(e, EBMLWriter.Audio, audio.ToArray());
        return e.ToArray();
      }

      private void EnsureCluster(long ptsMs, bool keyframe)
      {
        if (!clusterOpen) {
          OpenCluster(ptsMs);
          return;
        }
        var rel = ptsMs - clusterBaseMs;
        var need = false;
        if (videoEnabled) {
          if (keyframe) need = true; // GOP起点でCluster分割(途中参加をクリーンにする)
          if (rel>=ClusterSignedLimitMs || rel<=-ClusterSignedLimitMs) need = true; // signed16安全弁
        }
        else {
          if (rel>=AudioClusterDurationMs) need = true; // 音声のみは時間ベース
          // タイムスタンプの32bitラップやソース再開で rel が巨大な負値になると、
          // 上の条件だけでは二度と分割されず全ブロックが rel=-32768 に張り付く。
          // 映像パスと対称に負方向の安全弁を置く。
          if (rel<=-ClusterSignedLimitMs) need = true;
        }
        if (need) OpenCluster(ptsMs);
      }

      private void OpenCluster(long baseMs)
      {
        // Cluster Timecode は符号なしなので負値をクランプする。clusterBaseMs を
        // クランプ前のままにすると、以降のブロックの相対timecodeが実際に書いた Timecode と
        // ずれ、そのクラスタ内の全ブロックが |baseMs| ぶんシフトして A/V 同期が飛ぶ。
        var timecode = Math.Max(0, baseMs);
        clusterBaseMs = timecode;
        clusterOpen = true;
        var ms = new MemoryStream();
        EBMLWriter.WriteMasterUnknown(ms, EBMLWriter.Cluster);
        EBMLWriter.WriteElement(ms, EBMLWriter.Timecode, EBMLWriter.EncodeUInt((ulong)timecode));
        sink.OnCluster(ms.ToArray());
      }

      /// <summary>
      /// SimpleBlock 要素(ID+サイズ+ブロックヘッダ+ペイロード)を1つの配列として確保し、
      /// ペイロード領域を <paramref name="payload"/> で返す。
      /// 要素の総サイズは事前に計算できるので、呼び出し側がここへ直接書けば
      /// フレームあたりのペイロードコピーは1回で済む。
      /// </summary>
      private byte[] AllocateSimpleBlock(int trackNumber, long ptsMs, bool keyframe, int payloadLength, out Span<byte> payload)
      {
        var rel = ptsMs - clusterBaseMs;
        if (rel>32767) rel = 32767;
        if (rel<-32768) rel = -32768;
        var track = EBMLWriter.EncodeVInt((ulong)trackNumber);
        var contentLength = track.Length + 2 + 1 + payloadLength;
        var size = EBMLWriter.EncodeVInt((ulong)contentLength);
        var block = new byte[EBMLWriter.SimpleBlock.Length + size.Length + contentLength];
        var pos = 0;
        Array.Copy(EBMLWriter.SimpleBlock, 0, block, pos, EBMLWriter.SimpleBlock.Length);
        pos += EBMLWriter.SimpleBlock.Length;
        Array.Copy(size, 0, block, pos, size.Length);
        pos += size.Length;
        Array.Copy(track, 0, block, pos, track.Length);
        pos += track.Length;
        block[pos++] = (byte)((rel>>8) & 0xFF);
        block[pos++] = (byte)(rel & 0xFF);
        block[pos++] = (byte)(keyframe ? 0x80 : 0x00);
        payload = new Span<byte>(block, pos, payloadLength);
        return block;
      }
    }
  }

  public class FLVToMKVContentFilter
    : IContentFilter
  {
    public string Name { get { return "FLVToMKV"; } }

    public IContentSink Activate(IContentSink sink)
    {
      return new FLVToMKVContentFilterSink(sink);
    }

    public class FLVToMKVContentFilterSink
      : FLVContentFilterSinkBase
    {
      private static readonly Logger logger = new Logger(typeof(FLVToMKVContentFilter));

      public FLVToMKVContentFilterSink(IContentSink sink)
        : base(sink, logger)
      {
      }

      protected override string ContentType      { get { return "MKV"; } }
      protected override string MimeType         { get { return "video/x-matroska"; } }
      protected override string ContentExtension { get { return ".mkv"; } }

      class MKVSink
        : FLVToMKV.IMKVContentSink
      {
        public IContentSink TargetSink { get; }
        // MKVは1フレームから複数Content(Cluster + SimpleBlock)を出すため、上流Contentの位置を
        // そのまま流用すると (Stream,Timestamp,Position) が衝突し、ContentCollection の重複排除で
        // キーフレームごとドロップされる。従ってネイティブの MKVContentReader と同様、
        // 上流Contentは参照せず出力側で独自に連番Positionを採番する。
        private int streamId = -1;
        private long position = 0;
        private DateTime streamOrigin = DateTime.Now;

        public MKVSink(IContentSink targetSink)
        {
          TargetSink = targetSink;
        }

        public void OnHeader(ReadOnlyMemory<byte> bytes)
        {
          // 新しい EBML/Segment は新しい論理ストリーム。stream idを進め位置を0へ戻す。
          streamId += 1;
          position = 0;
          streamOrigin = DateTime.Now;
          TargetSink.OnContentHeader(
            new Content(streamId, TimeSpan.Zero, 0, bytes, PCPChanPacketContinuation.None)
          );
          position += bytes.Length;
        }

        public void OnCluster(ReadOnlyMemory<byte> bytes)
        {
          // Cluster境界 = 途中参加の開始点。None でマークし GetFirstContent に拾わせる。
          EmitContent(bytes, PCPChanPacketContinuation.None);
        }

        public void OnBlock(ReadOnlyMemory<byte> bytes)
        {
          // Cluster内の継続パケット。Fragment でマークし開始点に選ばれないようにする。
          EmitContent(bytes, PCPChanPacketContinuation.Fragment);
        }

        private void EmitContent(ReadOnlyMemory<byte> bytes, PCPChanPacketContinuation cont)
        {
          TargetSink.OnContent(
            new Content(streamId, DateTime.Now-streamOrigin, position, bytes, cont)
          );
          position += bytes.Length;
        }
      }

      protected override async Task ProcessMessagesLoopAsync(IContentSink targetSink, CancellationToken cancellationToken)
      {
        var mkvSink = new MKVSink(targetSink);
        var context = new FLVToMKV.Context(mkvSink);
        var parseBuffer = new FLVParseBuffer();
        var msg = await MessageQueue.DequeueAsync(cancellationToken).ConfigureAwait(false);
        while (msg.Type!=ContentMessage.MessageType.Stop) {
          switch (msg.Type) {
          case ContentMessage.MessageType.ChannelInfo:
            targetSink.OnChannelInfo(msg.ChannelInfo);
            break;
          case ContentMessage.MessageType.ChannelTrack:
            targetSink.OnChannelTrack(msg.ChannelTrack);
            break;
          // MKVSink は上流Contentを参照しないため、ヘッダも本体も同じくバッファへ流すだけでよい。
          case ContentMessage.MessageType.ContentHeader:
          case ContentMessage.MessageType.ContentBody:
            parseBuffer.Feed(msg.Content.Data.Span, context);
            break;
          }
          msg = await MessageQueue.DequeueAsync(cancellationToken).ConfigureAwait(false);
        }
        targetSink.OnStop(msg.StopReason);
      }
    }
  }

  [Plugin]
  public class FLVToMKVContentFilterPlugin
    : PluginBase
  {
    public override string Name {
      get { return "FLVToMKVContentFilter"; }
    }

    private FLVToMKVContentFilter filter = new FLVToMKVContentFilter();
    protected override void OnAttach(PeerCastApplication app)
    {
      app.PeerCast.ContentFilters.Add(filter);
    }

    protected override void OnDetach(PeerCastApplication app)
    {
      app.PeerCast.ContentFilters.Remove(filter);
    }
  }
}
