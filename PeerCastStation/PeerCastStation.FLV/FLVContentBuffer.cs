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
      DataAMF0Message data_msg;
      try {
        data_msg = new DataAMF0Message(msg.Timestamp, 0, name, new AMF.AMFValue[] { msg.Arguments[1] });
      }
      catch (ArgumentException) {
        // AMF0Reader は未対応マーカー(MovieClip/Unsupported/AVM+切替等)を例外にせず
        // NotSupported 値として復号するが、AMF0Writer はそれを直列化できず
        // ArgumentException を投げる(値は入れ子の奥にも入りうるので事前の型検査では
        // 防ぎきれない)。再エンコードできないメッセージとしてここで捨てる。
        return;
      }
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
        // maxBitrate の有無ではなく「数値として読めたか」で videodatarate へ落とす。
        // 存在の有無だけで分岐すると、maxBitrate が空文字や非数値だった場合に
        // videodatarate まで諦めることになり、読める videodatarate を持つ配信が
        // 0kbps として公開される。
        if (TryGetMaxBitrate(metadata.Arguments[0]["maxBitrate"], out var maxBitrate)) {
          bitrate += maxBitrate;
        }
        else if (AMF.AMFValue.TryGetDouble(metadata.Arguments[0]["videodatarate"], out var videodatarate)) {
          bitrate += videodatarate;
        }
        if (AMF.AMFValue.TryGetDouble(metadata.Arguments[0]["audiodatarate"], out var audiodatarate)) {
          bitrate += audiodatarate;
        }
        // TryGetDouble は "1e300" のような指数表記も受理するので、個々の値が読めても
        // 合計が int に収まるとは限らない。未チェックの (int) キャストは int.MinValue に
        // なり、負の巨大ビットレートが ChanInfo として全ネットワークへ公開される。
        // AMFValue.TryGetInt32 と同じ規則で、int で表現できない値は読めなかったもの
        // (0kbps)として扱う。
        if (Double.IsNaN(bitrate) || bitrate<0 || bitrate>Int32.MaxValue) {
          bitrate = 0;
        }
        info.SetChanInfoBitrate((int)bitrate);
      }
      OnChannelInfoChanged(info);
      OnHeaderChanged(msg);
      OnContentChanged(msg);
    }

    /// <summary>
    /// onMetaData の maxBitrate を数値として読む。"2500k" のような単位付きの文字列で
    /// 来る前提の項目だが、型も書式も配信者のエンコーダ次第なので読めないことがある。
    /// 文字列以外の型は TryGetDouble に委ねる。string キャスト経由で文字列化すると
    /// 現在カルチャの ToString と InvariantCulture の解析が食い違い、小数点がカンマの
    /// 環境で数値型の maxBitrate が読めなくなる。
    /// </summary>
    private static bool TryGetMaxBitrate(AMF.AMFValue value, out double result)
    {
      result = 0.0;
      if (AMF.AMFValue.IsNull(value)) return false;
      if (value.Type!=AMF.AMFValueType.String) {
        return AMF.AMFValue.TryGetDouble(value, out result);
      }
      var text = System.Text.RegularExpressions.Regex.Replace((string)value.Value, @"([\d]+)k", "$1");
      return Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
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
    // さらに昇格には IsPromotableVideoConfig の条件(キーフレーム通知、または AVC で
    // ペイロードが avcC として内容検証を通ること)を課す。詳細はそちらの注記を参照。
    // 既知コーデックであること(FourCcRegistry の記述子が引けたこと)も要求する
    // (Ex タグの FourCC フィールドは任意の4バイトが通るため)。
    // 保持中のヘッダと同一内容の再送は IsSameHeader で弾く。
    public void OnVideo(RTMPMessage msg)
    {
      var info = FLVTagClassifier.Classify(msg);
      if (info.Kind==FLVTagKind.VideoSequenceHeader &&
          info.HasPayload(msg.Body) &&
          info.Codec?.IsAudio==false &&
          IsPromotableVideoConfig(info, msg.Body) &&
          !IsSameHeader(videoHeader, msg)) {
        videoHeader = msg;
        OnHeaderChanged(msg);
      }
      OnContentChanged(msg);
    }

    /// <summary>
    /// 昇格してよい映像シーケンスヘッダか。キーフレームとして通知されたものは信用する。
    ///
    /// 分類器は frameType=2(inter)のシーケンスヘッダも取りこぼさず拾い(そうしないと
    /// avcC を inter で送る実在のエンコーダ/中継で映像が全く出ない)、再多重化
    /// (FLVToMKV/FLVToMPEG2TS)はそれを受け入れる。一方ここで無条件に昇格させると、
    /// AVCPacketType が 0 に化けた壊れたインターフレーム1つで全視聴者の再初期化を
    /// 繰り返し起こせてしまう。フラグでは両者を区別できないので、AVC については
    /// ペイロードが avcC として解析でき SPS/PPS を伴うことを内容で確かめて昇格させる
    /// (壊れたフレームの中身が偶然この検証を通る見込みはまず無い)。
    /// AVC 以外は設定を解析できず内容で確かめようがないため、キーフレーム通知を要求する。
    /// </summary>
    private static bool IsPromotableVideoConfig(FLVTagInfo info, byte[] body)
    {
      if (info.IsKeyFrameSignaled) return true;
      if (info.Codec!=FourCcRegistry.Avc) return false;
      return AvcDecoderConfig.TryParse(
               new ReadOnlySpan<byte>(body, info.PayloadOffset, body.Length-info.PayloadOffset),
               out var config) &&
             config.HasParameterSets;
    }

    // 音声には frameType が無いのでキーフレーム相当の絞り込みはできない。代わりに
    // 既知コーデックの記述子(レガシー AAC は FourCcRegistry.Aac に正規化される)を要求する。
    // レガシーの予約 soundFormat 9 は Ex エスケープと同じビットパターンのため、
    // 0x9? で始まる壊れたタグが Ex の SequenceStart に化けて任意の4バイトが FourCC に
    // なる。これを無条件に昇格させると、ゴミタグ1つごとに GenerateStreamID() が走り
    // 全視聴者を繰り返し再初期化させられる(映像側の IsKeyFrameSignaled と対になる制限)。
    public void OnAudio(RTMPMessage msg)
    {
      var info = FLVTagClassifier.Classify(msg);
      if (info.Kind==FLVTagKind.AudioSequenceHeader && info.HasPayload(msg.Body) &&
          info.Codec?.IsAudio==true &&
          !IsSameHeader(audioHeader, msg)) {
        audioHeader = msg;
        OnHeaderChanged(msg);
      }
      OnContentChanged(msg);
    }

    /// <summary>
    /// 保持中のヘッダと同一内容の再送か。多くのエンコーダは GOP ごとにシーケンスヘッダを
    /// 送り直すが、内容が同じならチャンネルヘッダは変わらない。昇格し直すと
    /// OnHeaderChanged が GenerateStreamID() で新しい論理ストリームを始めるため、
    /// 再送のたびに全視聴者の再生が初期化されてしまう。
    /// </summary>
    private static bool IsSameHeader(RTMPMessage? current, RTMPMessage msg)
    {
      return current!=null && current.Body.AsSpan().SequenceEqual(msg.Body);
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
