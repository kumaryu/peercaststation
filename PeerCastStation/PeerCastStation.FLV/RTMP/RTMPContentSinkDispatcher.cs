using System;
using System.Collections.Generic;
using PeerCastStation.Core;

namespace PeerCastStation.FLV.RTMP
{
  /// <summary>
  /// 取り込みコーデックに応じて実 Sink を切り替える <see cref="IRTMPContentSink"/>。
  ///
  /// 最初の映像パケット先頭バイトが 0x80(Enhanced RTMP ExHeader)なら
  /// <see cref="MatroskaContentBuffer"/>(Matroska へ remux)、そうでなければ
  /// 従来の <see cref="FLVContentBuffer"/>(旧 FLV 経路はそのまま温存)を選ぶ。
  /// 判定は connect 交渉ではなく実パケットで行う(OBS は connect fourCcList が空でも
  /// 拡張コーデックを送ってくるため)。
  ///
  /// 映像パケットが来るまでにメタデータや音声が先着しうるので、判定が決まるまで受信
  /// メッセージをバッファし、Sink 確定時に到着順で replay してから直接転送に切り替える。
  /// </summary>
  internal class RTMPContentSinkDispatcher
    : IRTMPContentSink
  {
    private const byte ExHeaderFlag = 0x80;
    // 映像が一切来ないまま(=音声のみ配信)この件数までバッファしたら旧 FLV 経路へ確定する。
    // Enhanced RTMP は配信開始直後に映像を送るため、映像があればこの閾値より前に判定が済む。
    private const int FallbackToFlvAfter = 32;

    private readonly Channel      targetChannel;
    private readonly IContentSink contentSink;
    private readonly List<Action<IRTMPContentSink>> buffered = new List<Action<IRTMPContentSink>>();
    private IRTMPContentSink? sink = null;

    public RTMPContentSinkDispatcher(Channel target_channel, IContentSink content_sink)
    {
      this.targetChannel = target_channel;
      this.contentSink   = content_sink;
    }

    public long Position {
      get {
        switch (sink) {
        case FLVContentBuffer flv:     return flv.Position;
        case MatroskaContentBuffer mkv: return mkv.Position;
        default:                       return 0;
        }
      }
    }

    public void OnFLVHeader(FLVFileHeader header)
    {
      if (sink!=null) { sink.OnFLVHeader(header); return; }
      buffered.Add(s => s.OnFLVHeader(header));
    }

    public void OnData(DataMessage msg)
    {
      if (sink!=null) { sink.OnData(msg); return; }
      buffered.Add(s => s.OnData(msg));
      FallbackIfBufferFull();
    }

    public void OnAudio(RTMPMessage msg)
    {
      if (sink!=null) { sink.OnAudio(msg); return; }
      buffered.Add(s => s.OnAudio(msg));
      FallbackIfBufferFull();
    }

    public void OnVideo(RTMPMessage msg)
    {
      if (sink==null) {
        var enhanced = msg.Body!=null && msg.Body.Length>0 && (msg.Body[0] & ExHeaderFlag)!=0;
        Commit(enhanced
          ? (IRTMPContentSink)new MatroskaContentBuffer(targetChannel, contentSink)
          : new FLVContentBuffer(targetChannel, contentSink));
      }
      sink!.OnVideo(msg);
    }

    // 映像が来ないまま閾値までバッファしたら音声のみ配信とみなし旧 FLV 経路へ確定する
    // (無制限バッファの防止 + 音声のみ配信の出力を温存)。
    private void FallbackIfBufferFull()
    {
      if (sink==null && buffered.Count>=FallbackToFlvAfter) {
        Commit(new FLVContentBuffer(targetChannel, contentSink));
      }
    }

    private void Commit(IRTMPContentSink chosen)
    {
      sink = chosen;
      foreach (var action in buffered) {
        action(sink);
      }
      buffered.Clear();
    }
  }
}
