// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Philmist
using PeerCastStation.Core;
using PeerCastStation.FLV.AMF;
using PeerCastStation.FLV.RTMP;
using System;
using System.Collections.Generic;
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

    /// <summary>映像コーデック固有処理のシーム。AV1/HEVC は実装を足すだけで対応できる。</summary>
    public interface IVideoCodecHandler
    {
      /// <summary>コーデックのシーケンスヘッダ(FLVの xVCSequenceHeader)を取り込む。</summary>
      bool TryConsumeSequenceHeader(RTMPMessage msg);
      /// <summary>Matroska の CodecID。</summary>
      string CodecId { get; }
      /// <summary>Matroska の CodecPrivate。</summary>
      byte[] CodecPrivate { get; }
      /// <summary>1フレームの SimpleBlock ペイロード(コンテナ無加工のコーデックデータ)。</summary>
      byte[] BuildBlockPayload(RTMPMessage msg);
      /// <summary>表示時刻補正(CTS, ミリ秒)。PTS = DTS + これ。</summary>
      int CompositionTimeOffset(RTMPMessage msg);
    }

    /// <summary>
    /// H.264 ハンドラ。FLV の AVC データは avcC 形式(長さ付きNAL)なので、
    /// CodecPrivate(avcC)・Blockペイロードともに FLV body の5バイト目以降を無加工で使える。
    /// </summary>
    public class H264VideoCodecHandler
      : IVideoCodecHandler
    {
      private byte[] avcC = Array.Empty<byte>();

      public string CodecId { get { return "V_MPEG4/ISO/AVC"; } }
      public byte[] CodecPrivate { get { return avcC; } }

      public bool TryConsumeSequenceHeader(RTMPMessage msg)
      {
        // body: [0]=frametype|codecid, [1]=AVCPacketType(0), [2..4]=cts, [5..]=AVCDecoderConfigurationRecord(avcC)
        if (msg.Body.Length<=5) return false;
        avcC = new byte[msg.Body.Length-5];
        Array.Copy(msg.Body, 5, avcC, 0, avcC.Length);
        return true;
      }

      public byte[] BuildBlockPayload(RTMPMessage msg)
      {
        var len = msg.Body.Length-5;
        if (len<=0) return Array.Empty<byte>();
        var payload = new byte[len];
        Array.Copy(msg.Body, 5, payload, 0, len);
        return payload;
      }

      public int CompositionTimeOffset(RTMPMessage msg)
      {
        if (msg.Body.Length<5) return 0;
        int cts = (msg.Body[2]<<16) | (msg.Body[3]<<8) | msg.Body[4];
        if (cts>=0x800000) cts -= 0x1000000; // 符号付き24bit
        return cts;
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

      private static readonly int[] SamplingFrequencies = {
        96000, 88200, 64000, 48000, 44100, 32000,
        24000, 22050, 16000, 12000, 11025, 8000, 7350,
      };

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
        if (!AMFValue.IsNull(wv) && !AMFValue.IsNull(hv)) {
          var w = (int)wv;
          var h = (int)hv;
          if (w>0 && h>0) {
            videoWidth = w;
            videoHeight = h;
          }
        }
      }

      public void OnAudio(RTMPMessage msg)
      {
        OnContent(msg);
      }

      public void OnVideo(RTMPMessage msg)
      {
        OnContent(msg);
      }

      private void OnContent(RTMPMessage msg)
      {
        switch (msg.GetPacketType()) {
        case FLVPacketType.AACSequenceHeader:
          OnAACHeader(msg);
          break;
        case FLVPacketType.AACRawData:
          OnAACBody(msg);
          break;
        case FLVPacketType.AVCSequenceHeader:
          OnVideoHeader(msg);
          break;
        case FLVPacketType.AVCNALUnitKeyFrame:
        case FLVPacketType.AVCNALUnitInterFrame:
          OnVideoBody(msg);
          break;
        default:
          break;
        }
      }

      private void OnAACHeader(RTMPMessage msg)
      {
        if (msg.Body.Length<=2) return;
        audioConfig = new byte[msg.Body.Length-2];
        Array.Copy(msg.Body, 2, audioConfig, 0, audioConfig.Length);
        using (var s=new MemoryStream(audioConfig, false))
        using (var bs=new BitReader(s)) {
          var type = bs.ReadBits(5);
          if (type==31) type = bs.ReadBits(6)+32;
          var freqIdx = bs.ReadBits(4);
          audioSampleRate = freqIdx==0x0F
            ? bs.ReadBits(24)
            : (freqIdx<SamplingFrequencies.Length ? SamplingFrequencies[freqIdx] : 0);
          audioChannels = bs.ReadBits(4);
        }
        hasAudio = true;
      }

      private void OnVideoHeader(RTMPMessage msg)
      {
        // 現状はレガシーFLV(codecid=7=AVC)のみ。E-RTMP合流時はここで FourCC からハンドラを選択する。
        var handler = new H264VideoCodecHandler();
        if (handler.TryConsumeSequenceHeader(msg)) {
          videoHandler = handler;
          hasVideo = true;
        }
      }

      private void OnAACBody(RTMPMessage msg)
      {
        WriteHeaderIfNeeded();
        if (!audioEnabled) return;
        if (msg.Body.Length<=2) return;
        // 最初のメディアフレーム(ts=0を含む)を基準に正規化する。
        // 2番目のフレームで確定すると先頭フレームとPTSが衝突・逆行し、
        // PTSのみを持つMKVではH.264のPOC再構成が壊れる。
        if (ptsBase<0) ptsBase = msg.Timestamp;
        var pts = msg.Timestamp - ptsBase;
        EnsureCluster(pts, false);
        var payload = new byte[msg.Body.Length-2];
        Array.Copy(msg.Body, 2, payload, 0, payload.Length);
        WriteSimpleBlock(AudioTrackNumber, pts, true, payload);
      }

      private void OnVideoBody(RTMPMessage msg)
      {
        WriteHeaderIfNeeded();
        if (!videoEnabled || videoHandler==null) return;
        // OnAACBody と同様、最初のメディアフレームを基準に正規化する。
        if (ptsBase<0) ptsBase = msg.Timestamp;
        var dts = msg.Timestamp - ptsBase;
        var pts = dts + videoHandler.CompositionTimeOffset(msg);
        var keyframe = msg.IsKeyFrame();
        EnsureCluster(pts, keyframe);
        WriteSimpleBlock(VideoTrackNumber, pts, keyframe, videoHandler.BuildBlockPayload(msg));
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
        if (audioSampleRate>0) {
          EBMLWriter.WriteElement(audio, EBMLWriter.SamplingFrequency, EBMLWriter.EncodeFloat(audioSampleRate));
        }
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
        }
        if (need) OpenCluster(ptsMs);
      }

      private void OpenCluster(long baseMs)
      {
        clusterBaseMs = baseMs;
        clusterOpen = true;
        var ms = new MemoryStream();
        EBMLWriter.WriteMasterUnknown(ms, EBMLWriter.Cluster);
        EBMLWriter.WriteElement(ms, EBMLWriter.Timecode, EBMLWriter.EncodeUInt((ulong)Math.Max(0, baseMs)));
        sink.OnCluster(ms.ToArray());
      }

      private void WriteSimpleBlock(int trackNumber, long ptsMs, bool keyframe, ReadOnlySpan<byte> payload)
      {
        var rel = ptsMs - clusterBaseMs;
        if (rel>32767) rel = 32767;
        if (rel<-32768) rel = -32768;
        var track = EBMLWriter.EncodeVInt((ulong)trackNumber);
        var content = new byte[track.Length + 2 + 1 + payload.Length];
        var pos = 0;
        Array.Copy(track, 0, content, pos, track.Length);
        pos += track.Length;
        content[pos++] = (byte)((rel>>8) & 0xFF);
        content[pos++] = (byte)(rel & 0xFF);
        content[pos++] = (byte)(keyframe ? 0x80 : 0x00);
        payload.CopyTo(new Span<byte>(content, pos, payload.Length));
        var ms = new MemoryStream();
        EBMLWriter.WriteElement(ms, EBMLWriter.SimpleBlock, content);
        sink.OnBlock(ms.ToArray());
      }

      private class BitReader
        : IDisposable
      {
        private readonly Stream baseStream;
        private int buffer = 0;
        private int bufferLen = 0;

        public BitReader(Stream baseStream)
        {
          this.baseStream = baseStream;
        }

        public void Dispose()
        {
        }

        public int ReadBits(int bits)
        {
          while (bufferLen<bits) {
            var b = baseStream.ReadByte();
            if (b<0) throw new EndOfStreamException();
            buffer = (buffer<<8) | b;
            bufferLen += 8;
          }
          int result = (buffer >> (bufferLen-bits)) & ((1<<bits)-1);
          bufferLen -= bits;
          buffer = buffer & ((1<<bufferLen)-1);
          return result;
        }
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
      : IContentSink
    {
      private Task processorTask;
      struct ContentMessage
      {
        public enum MessageType {
          ChannelInfo,
          ChannelTrack,
          ContentHeader,
          ContentBody,
          Stop,
        }
        public MessageType  Type;
        public StopReason   StopReason;
        public Content      Content;
        public ChannelInfo  ChannelInfo;
        public ChannelTrack ChannelTrack;
      }
      private WaitableQueue<ContentMessage> msgQueue = new WaitableQueue<ContentMessage>();

      public FLVToMKVContentFilterSink(IContentSink sink)
      {
        processorTask = ProcessMessagesAsync(sink, CancellationToken.None);
      }

      class MKVSink
        : FLVToMKV.IMKVContentSink
      {
        public IContentSink TargetSink { get; }
        // 上流のContentは ProcessMessagesAsync が設定するが、出力Contentの位置採番には用いない。
        // MKVは1フレームから複数Content(Cluster + SimpleBlock)を出すため、上流の位置をそのまま流用すると
        // (Stream,Timestamp,Position) が衝突し ContentCollection の重複排除でキーフレームごとドロップされる。
        // 従ってネイティブの MKVContentReader と同様、出力側で独自に連番Positionを採番する。
        public Content? HeaderContent { get; set; } = null;
        public Content? RecentContent { get; set; } = null;

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

      private async Task ProcessMessagesAsync(IContentSink targetSink, CancellationToken cancellationToken)
      {
        var mkvSink = new MKVSink(targetSink);
        var context = new FLVToMKV.Context(mkvSink);
        var contentBuffer = new MemoryStream();
        var fileParser = new FLVFileParser();
        var msg = await msgQueue.DequeueAsync(cancellationToken).ConfigureAwait(false);
        while (msg.Type!=ContentMessage.MessageType.Stop) {
          switch (msg.Type) {
          case ContentMessage.MessageType.ChannelInfo:
            targetSink.OnChannelInfo(msg.ChannelInfo);
            break;
          case ContentMessage.MessageType.ChannelTrack:
            targetSink.OnChannelTrack(msg.ChannelTrack);
            break;
          case ContentMessage.MessageType.ContentHeader:
            {
              mkvSink.HeaderContent = msg.Content;
              var buffer = contentBuffer;
              var pos = buffer.Position;
              buffer.Seek(0, SeekOrigin.End);
              buffer.Write(msg.Content.Data.Span);
              buffer.Position = pos;
              fileParser.Read(buffer, context);
              if (buffer.Position!=0) {
                var new_buf = new MemoryStream();
                var trim_pos = buffer.Position;
                buffer.Close();
                var buf = buffer.ToArray();
                new_buf.Write(buf, (int)trim_pos, (int)(buf.Length-trim_pos));
                new_buf.Position = 0;
                contentBuffer = new_buf;
              }
            }
            break;
          case ContentMessage.MessageType.ContentBody:
            {
              mkvSink.RecentContent = msg.Content;
              var buffer = contentBuffer;
              var pos = buffer.Position;
              buffer.Seek(0, SeekOrigin.End);
              buffer.Write(msg.Content.Data.Span);
              buffer.Position = pos;
              fileParser.Read(buffer, context);
              if (buffer.Position!=0) {
                var new_buf = new MemoryStream();
                var trim_pos = buffer.Position;
                buffer.Close();
                var buf = buffer.ToArray();
                new_buf.Write(buf, (int)trim_pos, (int)(buf.Length-trim_pos));
                new_buf.Position = 0;
                contentBuffer = new_buf;
              }
            }
            break;
          }
          msg = await msgQueue.DequeueAsync(cancellationToken).ConfigureAwait(false);
        }
        targetSink.OnStop(msg.StopReason);
      }

      public void OnChannelInfo(ChannelInfo channel_info)
      {
        var info = new AtomCollection(channel_info.Extra);
        info.SetChanInfoType("MKV");
        info.SetChanInfoStreamType("video/x-matroska");
        info.SetChanInfoStreamExt(".mkv");
        msgQueue.Enqueue(new ContentMessage { Type=ContentMessage.MessageType.ChannelInfo, ChannelInfo=new ChannelInfo(info) });
      }

      public void OnChannelTrack(ChannelTrack channel_track)
      {
        msgQueue.Enqueue(new ContentMessage { Type=ContentMessage.MessageType.ChannelTrack, ChannelTrack=channel_track });
      }

      public void OnContent(Content content)
      {
        msgQueue.Enqueue(new ContentMessage { Type=ContentMessage.MessageType.ContentBody, Content=content });
      }

      public void OnContentHeader(Content content_header)
      {
        msgQueue.Enqueue(new ContentMessage { Type=ContentMessage.MessageType.ContentHeader, Content=content_header });
      }

      public void OnStop(StopReason reason)
      {
        msgQueue.Enqueue(new ContentMessage { Type=ContentMessage.MessageType.Stop, StopReason=reason });
        processorTask.Wait();
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
