using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using PeerCastStation.Core;
using PeerCastStation.FLV.RTMP;

namespace PeerCastStation.FLV
{
  internal class FLVContentBuffer
    : IRTMPContentSink
  {
    public Channel       TargetChannel { get; private set; }
    public IContentSink  ContentSink   { get; private set; }
    public long          Position      { get { return position; } }
    private long         position        = 0;
    private int          streamIndex     = -1;
    private DateTime     streamOrigin;
    private long         timestampOrigin = 0;
    private DataMessage? metadata        = null;
    private RTMPMessage? audioHeader     = null;
    private RTMPMessage? videoHeader     = null;
    private MemoryStream bodyBuffer      = new MemoryStream();

    public FLVContentBuffer(
      Channel target_channel,
      IContentSink content_sink)
    {
      this.TargetChannel = target_channel;
      this.ContentSink   = new BufferedContentSink(content_sink);
    }

    // 引数の個数は配信者側のメッセージ次第で、規定数に満たないものが実際に届く。
    // FLVFileParser は AMF の復号だけを保護して sink 内の例外は通すので、
    // ここで足りない引数を弾かないと壊れたメッセージ1つで配信が落ちる。
    private void SetDataFrame(DataMessage msg)
    {
      if (msg.Arguments.Count<2) return;
      var name = (string?)msg.Arguments[0] ?? "";
      var data_msg = new DataAMF0Message(msg.Timestamp, 0, name, new AMF.AMFValue[] { msg.Arguments[1] });
      OnData(data_msg);
    }

    private void ClearDataFrame(DataMessage msg)
    {
      if (msg.Arguments.Count<1) return;
      var name = (string?)msg.Arguments[0];
      switch (name) {
      case "onMetaData":
        metadata = null;
        break;
      }
    }

    private void OnMetaData(DataMessage msg)
    {
      this.metadata = msg;
      var info = new AtomCollection();
      info.SetChanInfoType("FLV");
      info.SetChanInfoStreamType("video/x-flv");
      info.SetChanInfoStreamExt(".flv");
      if (metadata.Arguments.Count>0 &&
          (metadata.Arguments[0].Type==AMF.AMFValueType.ECMAArray || metadata.Arguments[0].Type==AMF.AMFValueType.Object)) {
        var bitrate = 0.0;
        // 値の型も中身も配信者側のエンコーダ次第なので、AMFValue のキャスト演算子ではなく
        // 例外を投げない読み出し(AMFValue.TryGetDouble)を通す。
        var val = metadata.Arguments[0]["maxBitrate"];
        if (!AMF.AMFValue.IsNull(val)) {
          // maxBitrate は "2500k" のような単位付きの文字列で来る前提の項目。
          string maxBitrateStr = System.Text.RegularExpressions.Regex.Replace((string?)val ?? "", @"([\d]+)k", "$1");
          if (Double.TryParse(maxBitrateStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var maxBitrate)) {
            bitrate += maxBitrate;
          }
        }
        else if (AMF.AMFValue.TryGetDouble(metadata.Arguments[0]["videodatarate"], out var videodatarate)) {
          bitrate += videodatarate;
        }
        if (AMF.AMFValue.TryGetDouble(metadata.Arguments[0]["audiodatarate"], out var audiodatarate)) {
          bitrate += audiodatarate;
        }
        info.SetChanInfoBitrate((int)bitrate);
      }
      OnChannelInfoChanged(info);
      OnHeaderChanged(msg);
      OnContentChanged(msg);
    }

    public void OnFLVHeader(FLVFileHeader header)
    {
      var info = new AtomCollection();
      info.SetChanInfoType("FLV");
      info.SetChanInfoStreamType("video/x-flv");
      info.SetChanInfoStreamExt(".flv");
      OnChannelInfoChanged(info);
    }

    public void OnData(DataMessage msg)
    {
      switch (msg.PropertyName) {
      case "@setDataFrame":
        SetDataFrame(msg);
        break;

      case "@clearDataFrame":
        ClearDataFrame(msg);
        break;

      case "onMetaData":
        OnMetaData(msg);
        break;

      default:
        OnContentChanged(msg);
        break;
      }
    }

    // シーケンスヘッダ判定は共有分類器(FLVTagClassifier)に委ね、レガシー/Ex の差は
    // そちらで吸収する。加えて、種別だけでなくコーデック設定の実体があることまで要求する。
    // 切り詰めタグをチャンネルヘッダに昇格させると、設定を持たないゴミが下流に配られる上に
    // OnHeaderChanged がストリームIDを再生成するため、1パケットで繰り返し全視聴者を
    // 再初期化させられる(Multitrack は PayloadOffset=-1 のためここで除外される)。
    // 昇格させるのは avcC 等のコーデック設定を持つ VideoSequenceHeader のみ。
    // VideoMpeg2TsSequenceHeader(E-RTMP の MPEG2TSSequenceStart)はコーデック設定ではなく
    // TS ブートストラップの生バイト列なので、チャンネルヘッダに埋めても下流の初期化に使えず、
    // 昇格させると GenerateStreamID() で無意味に全視聴者を再初期化することになる。
    // さらに、キーフレームとして通知されたシーケンスヘッダのみを昇格させる。分類器は
    // frameType=2(inter)のシーケンスヘッダも取りこぼさず拾う(そうしないと avcC を落とす
    // エンコーダで映像が全く出ない)が、それをそのまま昇格させると AVCPacketType が 0 に
    // 化けた壊れたインターフレーム1つで全視聴者の再初期化を繰り返し起こせてしまう。
    // 再多重化(FLVToMKV/FLVToMPEG2TS)にはこの制限は不要なので、ここだけで絞る。
    public void OnVideo(RTMPMessage msg)
    {
      var info = FLVTagClassifier.Classify(msg);
      if (info.Kind==FLVTagKind.VideoSequenceHeader && info.IsKeyFrameSignaled && info.HasPayload(msg.Body)) {
        videoHeader = msg;
        OnHeaderChanged(msg);
      }
      OnContentChanged(msg);
    }

    public void OnAudio(RTMPMessage msg)
    {
      var info = FLVTagClassifier.Classify(msg);
      if (info.Kind==FLVTagKind.AudioSequenceHeader && info.HasPayload(msg.Body)) {
        audioHeader = msg;
        OnHeaderChanged(msg);
      }
      OnContentChanged(msg);
    }

    private void WriteMessage(Stream stream, RTMPMessage msg, long time_origin)
    {
      var timestamp = Math.Max(0, msg.Timestamp-time_origin);
      using (var writer=new RTMPBinaryWriter(stream, true)) {
        writer.Write((byte)msg.MessageType);
        writer.WriteUInt24(msg.Body.Length);
        writer.WriteUInt24((int)timestamp & 0xFFFFFF);
        writer.Write((byte)((timestamp>>24) & 0xFF));
        writer.WriteUInt24(0);
        writer.Write(msg.Body, 0, msg.Body.Length);
        writer.Write(msg.Body.Length+11);
      }
    }

    private void OnHeaderChanged(RTMPMessage msg)
    {
      var s = new MemoryStream();
      using (s) {
        using (var writer=new RTMPBinaryWriter(s, true)) {
          writer.Write((byte)'F');
          writer.Write((byte)'L');
          writer.Write((byte)'V');
          writer.Write((byte)1);
          writer.Write((byte)5);
          writer.WriteUInt32(9);
          writer.WriteUInt32(0);
        }
        if (metadata!=null)    WriteMessage(s, metadata,    0xFFFFFFFF);
        if (audioHeader!=null) WriteMessage(s, audioHeader, 0xFFFFFFFF);
        if (videoHeader!=null) WriteMessage(s, videoHeader, 0xFFFFFFFF);
      }
      streamIndex     = TargetChannel.GenerateStreamID();
      streamOrigin    = DateTime.Now;
      timestampOrigin = msg.Timestamp;
      var bytes = s.ToArray();
      ContentSink.OnContentHeader(new Content(streamIndex, TimeSpan.Zero, position, bytes, PCPChanPacketContinuation.None));
      position += bytes.Length;
    }

    private void OnContentChanged(RTMPMessage content)
    {
      if (streamIndex<0) {
        OnHeaderChanged(content);
        return;
      }
      WriteMessage(bodyBuffer, content, timestampOrigin);
      if (bodyBuffer.Length>0) {
        ContentSink.OnContent(new Content(streamIndex, DateTime.Now-streamOrigin, position, bodyBuffer.ToArray(), PCPChanPacketContinuation.None));
        position += bodyBuffer.Length;
        bodyBuffer.SetLength(0);
      }
    }

    private void OnChannelInfoChanged(AtomCollection info)
    {
      ContentSink.OnChannelInfo(new ChannelInfo(info));
    }

    private void OnChannelTrackChanged(AtomCollection info)
    {
      ContentSink.OnChannelTrack(new ChannelTrack(info));
    }
  }

}
