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
    /// <summary>
    /// SBR/PS でデコード後のレートがコアのレートと異なる場合に、実際の出力レートを示す。
    /// SamplingFrequency にはコア側を書く決まりなので、これが無いと HE-AAC のトラックが
    /// 実レートの半分として宣言される。
    /// </summary>
    public static readonly byte[] OutputSamplingFrequency = { 0x78, 0xB5 };
    public static readonly byte[] Channels           = { 0x9F };
    // Cluster
    public static readonly byte[] Cluster            = { 0x1F, 0x43, 0xB6, 0x75 };
    public static readonly byte[] Timecode           = { 0xE7 };
    public static readonly byte[] SimpleBlock        = { 0xA3 };

    /// <summary>値を最短長のVINTで表したときのバイト数。</summary>
    public static int GetVIntLength(ulong value)
    {
      int length = 1;
      // 全データビットが1の値は unknown-size に予約されているため value <= 2^(7L)-2 となる最小Lを選ぶ
      while (length<8 && value >= ((1UL<<(7*length))-1)) {
        length++;
      }
      return length;
    }

    /// <summary>
    /// 最短長のVINTを dest の先頭へ書き、書いたバイト数を返す。
    /// フレームごとに呼ばれる経路で使うため、配列を作らずに済む形も用意する。
    /// </summary>
    public static int WriteVInt(Span<byte> dest, ulong value)
    {
      var length = GetVIntLength(value);
      ulong v = value | (1UL<<(7*length)); // 先頭の長さマーカービット
      for (int i=length-1; i>=0; i--) {
        dest[i] = (byte)(v & 0xFF);
        v >>= 8;
      }
      return length;
    }

    /// <summary>値を最短長のVINT(要素サイズ等)としてエンコードする。</summary>
    public static byte[] EncodeVInt(ulong value)
    {
      var bin = new byte[GetVIntLength(value)];
      WriteVInt(bin, value);
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

    public class Context
      : FLVRemuxContextBase
    {
      public int VideoTrackNumber { get; set; } = 1;
      public int AudioTrackNumber { get; set; } = 2;

      // 同一Cluster内の相対timecodeは符号付き16bit。GOP起点に加え、安全側で強制分割する閾値。
      private const long ClusterSignedLimitMs   = 30000;
      private const long AudioClusterDurationMs = 1000;

      private readonly IMKVContentSink sink;
      private static readonly Logger logger = new Logger(typeof(FLVToMKV));

      protected override string FilterName { get { return "FLVToMKV"; } }
      protected override Logger Logger { get { return logger; } }

      // 「設定を受け取ったか」は設定そのものの有無で判る。真偽値を別に持つと
      // 片方だけ更新する経路が生まれ、トラックが黙って落ちるか null 参照になる。
      // 対応コーデック(avc1/hvc1/av01)はいずれも CodecPrivate もフレームデータも
      // コンテナ無加工で流用できるので、コーデック固有の処理は持たない。
      private string? videoCodecId = null;
      private byte[]? videoCodecPrivate = null;
      private byte[]? audioConfig = null; // AudioSpecificConfig
      private int audioChannels = 0;
      private int audioSampleRate = 0;
      private int audioOutputSampleRate = 0;
      private int videoWidth = 0;
      private int videoHeight = 0;
      private bool videoEnabled = false;
      private bool audioEnabled = false;
      private bool headerSent = false;
      private bool clusterOpen = false;
      private long clusterBaseMs = 0;

      public Context(IMKVContentSink sink)
      {
        this.sink = sink;
      }

      private void Clear()
      {
        videoCodecId = null;
        videoCodecPrivate = null;
        audioConfig = null;
        audioChannels = 0;
        audioSampleRate = 0;
        audioOutputSampleRate = 0;
        videoWidth = 0;
        videoHeight = 0;
        videoEnabled = false;
        audioEnabled = false;
        headerSent = false;
        ResetWarnings();
        ResetTimestampBase();
        clusterOpen = false;
        clusterBaseMs = 0;
      }

      public override void OnFLVHeader(FLVFileHeader header)
      {
        Clear();
      }

      public override void OnData(DataMessage msg)
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
      /// onMetaData の解像度フィールドを解釈する。値は配信者由来で型が保証されないため、
      /// 例外を投げない読み出し(AMFValue.TryGetInt32)を通したうえで、
      /// 解像度として使える範囲かどうかを見る。
      /// </summary>
      private static bool TryGetDimension(AMFValue value, out int result)
      {
        if (!AMFValue.TryGetInt32(value, out result)) return false;
        if (result<1) {
          result = 0;
          return false;
        }
        return true;
      }

      // タグ分類は共有分類器(FLVTagClassifier)、種別ごとの振り分けは FLVRemuxContextBase に
      // 一本化してある。ここは種別ごとの処理だけを持つ。
      protected override bool IsSupportedAudioCodec(string? fourcc)
      {
        return fourcc==FLVTagClassifier.FourCcAac;
      }

      protected override bool IsSupportedVideoCodec(string? fourcc)
      {
        return MapVideoCodecId(fourcc)!=null;
      }

      protected override void OnAudioConfig(byte[] body, int offset)
      {
        OnAudioHeader(body, offset);
      }

      protected override void OnAudioFrame(RTMPMessage msg, int offset)
      {
        OnAudioBody(msg, offset);
      }

      protected override void OnVideoConfig(RTMPMessage msg, int offset, string? fourcc)
      {
        // IsSupportedVideoCodec を通っているので、この FourCC には必ず CodecID が対応する。
        var codecId = MapVideoCodecId(fourcc)!;
        if (offset<0 || msg.Body.Length<=offset) return;
        var payload = new ReadOnlySpan<byte>(msg.Body, offset, msg.Body.Length-offset);
        // 多くのエンコーダは GOP ごとにシーケンスヘッダを送り直す。同じ内容なら取り込み直す
        // 意味はなく(ヘッダ送信後は CodecPrivate を差し替えても出力に反映されない)、
        // 数時間の配信では取りこぼしのないコピーがそのまま無駄になる。
        if (videoCodecId==codecId && videoCodecPrivate!=null && payload.SequenceEqual(videoCodecPrivate)) {
          return;
        }
        SetVideoCodec(codecId, payload.ToArray());
      }

      protected override void OnVideoFrame(RTMPMessage msg, int offset, int compositionTime, bool keyframe)
      {
        OnVideoBody(msg, offset, compositionTime, keyframe);
      }

      private const string CodecIdAvc = "V_MPEG4/ISO/AVC";

      private static string? MapVideoCodecId(string? fourcc)
      {
        switch (fourcc) {
        case FLVTagClassifier.FourCcAvc: return CodecIdAvc;
        case "hvc1":
        case "hev1": return "V_MPEGH/ISO/HEVC";
        case "av01": return "V_AV1"; // Matroska の AV1 CodecID(FourCC の av01 とは異なる)
        default:     return null;
        }
      }

      private void SetVideoCodec(string codecId, byte[] codecPrivate)
      {
        videoCodecId      = codecId;
        videoCodecPrivate = codecPrivate;
        // Matroska は Video 要素に PixelWidth/PixelHeight を要求するので、解像度が判らないと
        // 映像トラックを作れない。本来は onMetaData が運ぶが、これを送らない(あるいは
        // width/height を欠く)配信は実在し、その場合 avcC を受け取っていても
        // 音声だけの MKV になってしまう。H.264 は avcC 内の SPS から導出できるので拠り所にする。
        // HEVC(hvcC)/AV1(av1C)は解析していないため、引き続き onMetaData 頼りになる。
        if ((videoWidth<1 || videoHeight<1) && codecId==CodecIdAvc) {
          if (H264Sps.TryGetResolutionFromAvcC(codecPrivate, out var w, out var h)) {
            videoWidth  = w;
            videoHeight = h;
          }
        }
      }

      private void OnAudioHeader(byte[] body, int offset)
      {
        if (offset<0 || body.Length<=offset) return;
        // 映像側と同じく、同じ設定の送り直しではコピーも解析もしない。
        if (audioConfig!=null &&
            new ReadOnlySpan<byte>(body, offset, body.Length-offset).SequenceEqual(audioConfig)) {
          return;
        }
        var config = FLVTagInfo.SlicePayload(body, offset);
        // 切り詰められた AudioSpecificConfig はここで捨てる。ビット読み出しで例外を投げると
        // FLVFileParser.Read の EndOfStreamException catch がタグ先頭まで巻き戻すため
        // (=「データ待ち」と誤認される)、毒タグがバッファ先頭に残って以後の全パースが
        // 再スローし続け、出力が恒久停止したうえで contentBuffer が無限に成長する。
        if (!AudioSpecificConfig.TryParse(config, out var asc)) {
          WarnBrokenAudioConfig("AudioSpecificConfigが不完全です");
          return;
        }
        // 予約インデックス(13/14)や明示レート0はサンプリング周波数が確定しない。
        // SamplingFrequency 要素を省略すると Matroska 既定の 8000Hz と誤宣言され
        // 誤速度・誤ピッチで再生されるため、壊れた設定として破棄する。
        if (asc.SampleRate<=0) {
          WarnBrokenAudioConfig($"サンプリング周波数が確定しません (index={asc.SamplingFrequencyIndex})");
          return;
        }
        // channelConfiguration はチャンネル数ではなくインデックス(7 は 8ch)。個数として
        // そのまま書くと 7.1ch のトラックが 7ch と宣言され、Audio 要素を信じるプレイヤーの
        // チャンネルマスク/ダウンミックスが狂う。0(レイアウトを PCE で運ぶ)と予約値(8-15)は
        // 個数が確定しないので、サンプリング周波数と同じく設定ごと破棄する。
        if (asc.ChannelCount<=0) {
          WarnBrokenAudioConfig($"チャンネル数が確定しません (channelConfiguration={asc.ChannelConfiguration})");
          return;
        }
        audioConfig = config;
        audioSampleRate = asc.SampleRate;
        audioOutputSampleRate = asc.OutputSampleRate;
        audioChannels = asc.ChannelCount;
      }

      private void WarnBrokenAudioConfig(string reason)
      {
        WarnOnce("brokenAudioConfig", "音声シーケンスヘッダを破棄します ({0})", reason);
      }

      private void OnAudioBody(RTMPMessage msg, int offset)
      {
        WriteHeaderIfNeeded();
        if (!audioEnabled) return;
        if (offset<0 || msg.Body.Length<=offset) return;
        // 時刻原点の取り方は音声・映像で共有する規則なので基底に置いてある。
        // ずれると PTS が衝突・逆行し、PTS のみを持つ MKV では H.264 の POC 再構成が壊れる。
        var pts = NormalizeTimestamp(msg.Timestamp);
        EnsureCluster(pts, false);
        var length = msg.Body.Length-offset;
        var block = AllocateSimpleBlock(AudioTrackNumber, pts, true, length, out var dest);
        new ReadOnlySpan<byte>(msg.Body, offset, length).CopyTo(dest);
        sink.OnBlock(block);
      }

      private void OnVideoBody(RTMPMessage msg, int offset, int cts, bool keyframe)
      {
        WriteHeaderIfNeeded();
        if (!videoEnabled) return;
        if (offset<0 || msg.Body.Length<=offset) return;
        var dts = NormalizeTimestamp(msg.Timestamp);
        // 先頭Bフレームの負CTS(符号拡張済み)で pts が負に振れることがある。
        // SimpleBlock のクラスタ相対timecodeは符号付き16bitなので、負値はそのまま
        // 書けてしまい、ブロックを拒否したり順序を誤るプレイヤーがある。
        var pts = Math.Max(dts, dts + cts);
        EnsureCluster(pts, keyframe);
        // 対応コーデックはいずれもフレームデータを無加工で SimpleBlock に載せられる。
        var length = msg.Body.Length-offset;
        var block = AllocateSimpleBlock(VideoTrackNumber, pts, keyframe, length, out var dest);
        new ReadOnlySpan<byte>(msg.Body, offset, length).CopyTo(dest);
        sink.OnBlock(block);
      }

      private void WriteHeaderIfNeeded()
      {
        if (headerSent) return;
        videoEnabled = videoCodecPrivate!=null && videoWidth>0 && videoHeight>0;
        audioEnabled = audioConfig!=null;
        if (videoCodecPrivate!=null && !videoEnabled) {
          WarnOnce("noResolution", "解像度が取得できないため映像トラックを除外します");
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
        EBMLWriter.WriteElement(e, EBMLWriter.CodecID,      EBMLWriter.EncodeString(videoCodecId!));
        EBMLWriter.WriteElement(e, EBMLWriter.CodecPrivate, videoCodecPrivate!);
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
        // HE-AAC/HE-AACv2 は SamplingFrequency にコア(出力の半分)のレートを書く決まりなので、
        // 実際の出力レートは OutputSamplingFrequency で別途宣言する。これが無いと
        // CodecPrivate を読み直さず Audio 要素を信じるプレイヤーがトラックを半分のレートと
        // 解釈し、ms 単位の映像タイムコードに対して A/V がずれていく。
        if (audioOutputSampleRate>0 && audioOutputSampleRate!=audioSampleRate) {
          EBMLWriter.WriteElement(audio, EBMLWriter.OutputSamplingFrequency, EBMLWriter.EncodeFloat(audioOutputSampleRate));
        }
        // OnAudioHeader が ChannelCount<=0 の設定を弾いているので、ここでは必ず正。
        EBMLWriter.WriteElement(audio, EBMLWriter.Channels, EBMLWriter.EncodeUInt((ulong)audioChannels));
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
        // フレームごとに呼ばれるので、VINT は一時配列を作らず block へ直接書く。
        var trackLength   = EBMLWriter.GetVIntLength((ulong)trackNumber);
        var contentLength = trackLength + 2 + 1 + payloadLength;
        var sizeLength    = EBMLWriter.GetVIntLength((ulong)contentLength);
        var block = new byte[EBMLWriter.SimpleBlock.Length + sizeLength + contentLength];
        var pos = 0;
        Array.Copy(EBMLWriter.SimpleBlock, 0, block, pos, EBMLWriter.SimpleBlock.Length);
        pos += EBMLWriter.SimpleBlock.Length;
        pos += EBMLWriter.WriteVInt(new Span<byte>(block, pos, sizeLength), (ulong)contentLength);
        pos += EBMLWriter.WriteVInt(new Span<byte>(block, pos, trackLength), (ulong)trackNumber);
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
      // 変換ループの起動はコンストラクタではなくここで行う(FLVContentFilterSinkBase.Start)。
      var filter_sink = new FLVToMKVContentFilterSink(sink);
      filter_sink.Start();
      return filter_sink;
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
        // 経過時間にしか使わないので、ローカル時刻への変換ぶん重い DateTime.Now は使わない
        // (フレームごとに参照される)。
        private DateTime streamOrigin = DateTime.UtcNow;

        public MKVSink(IContentSink targetSink)
        {
          TargetSink = targetSink;
        }

        public void OnHeader(ReadOnlyMemory<byte> bytes)
        {
          // 新しい EBML/Segment は新しい論理ストリーム。stream idを進め位置を0へ戻す。
          streamId += 1;
          position = 0;
          streamOrigin = DateTime.UtcNow;
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
            new Content(streamId, DateTime.UtcNow-streamOrigin, position, bytes, cont)
          );
          position += bytes.Length;
        }
      }

      // MKVSink は上流Contentを参照しないため、ヘッダも本体も同じくバッファへ流すだけでよい。
      protected override ContentProcessor CreateProcessor(IContentSink targetSink)
      {
        return new ContentProcessor(new FLVToMKV.Context(new MKVSink(targetSink)));
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
