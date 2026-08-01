// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Philmist
using System.Collections.Generic;
using PeerCastStation.Core;
using PeerCastStation.FLV.RTMP;

namespace PeerCastStation.FLV
{
  /// <summary>
  /// FLV/E-RTMP のメディアタグを別コンテナへ載せ替える変換器の共通土台。
  ///
  /// 分類そのものは <see cref="FLVTagClassifier"/> に一本化してあるが、分類結果を
  /// 「設定・フレーム・黙って捨てる・警告して捨てる」のどれに割り振るかという方針は
  /// FLVToMKV と FLVToMPEG2TS が各自で書いており、種別を1つ増やすたびに両方へ
  /// 同じ分岐を足す必要があった(片方を忘れると default に落ちて
  /// 「未対応コーデック」という誤った理由で黙って捨てられる)。その方針をここへ集約する。
  ///
  /// 派生クラスが与えるのは、対応コーデックの判定と種別ごとの処理だけ。
  /// </summary>
  public abstract class FLVRemuxContextBase
    : IRTMPContentSink
  {
    /// <summary>一度だけ出す警告の識別子。壊れた入力では毎タグ発生しうるため回数を絞る。</summary>
    private const string WarnKeyUnsupportedAudio = "unsupportedAudio";
    private const string WarnKeyUnsupportedVideo = "unsupportedVideo";
    private const string WarnKeyBrokenAudioTag   = "brokenAudioTag";
    private const string WarnKeyBrokenVideoTag   = "brokenVideoTag";

    private readonly HashSet<string> warned = new HashSet<string>();

    /// <summary>ログの先頭に付けるフィルタ名。例: "FLVToMKV"。</summary>
    protected abstract string FilterName { get; }
    protected abstract Logger Logger { get; }

    /// <summary>
    /// 同じ理由の警告を1回だけ出す。フラグを種類ごとに持つとリセット漏れが起きるため、
    /// 識別子で管理して <see cref="ResetWarnings"/> でまとめて消す。
    /// </summary>
    protected void WarnOnce(string key, string format, params object?[] args)
    {
      if (!warned.Add(key)) return;
      Logger.Warn(FilterName + ": " + format, args);
    }

    /// <summary>新しいストリームの開始時に警告の抑止状態を捨てる。</summary>
    protected void ResetWarnings()
    {
      warned.Clear();
    }

    /// <summary>この変換器が扱える音声コーデックか。</summary>
    protected abstract bool IsSupportedAudioCodec(string? fourcc);
    /// <summary>この変換器が扱える映像コーデックか。</summary>
    protected abstract bool IsSupportedVideoCodec(string? fourcc);

    /// <summary>音声のコーデック設定(AAC の AudioSpecificConfig 等)。</summary>
    protected abstract void OnAudioConfig(byte[] body, int offset);
    protected abstract void OnAudioFrame(RTMPMessage msg, int offset);
    /// <summary>
    /// 映像のコーデック設定(avcC/hvcC/av1C 等の生バイト)。
    /// コンテナ側の CodecID を引くのに FourCC が要るので、分類し直さずに済むよう渡す。
    /// </summary>
    protected abstract void OnVideoConfig(RTMPMessage msg, int offset, string? fourcc);
    protected abstract void OnVideoFrame(RTMPMessage msg, int offset, int compositionTime, bool keyframe);

    public void OnAudio(RTMPMessage msg)
    {
      var info = FLVTagClassifier.Classify(msg);
      switch (info.Kind) {
      case FLVTagKind.AudioSequenceEnd:
      case FLVTagKind.Control:
        // 健全な配信で普通に流れてくる制御パケット。黙って捨てる。
        return;
      case FLVTagKind.Unknown:
        // 構造を解釈できていないので FourCc も当てにならない。「未対応コーデック」として
        // 報告すると理由が誤りなうえ、1回だけの警告枠を切り詰めタグが使い切って
        // 本当に未対応なコーデックが無警告になる。
        WarnOnce(WarnKeyBrokenAudioTag, "解釈できない音声タグを破棄します (size={0})", msg.Body.Length);
        return;
      }
      if (!IsSupportedAudioCodec(info.FourCc)) {
        WarnOnce(WarnKeyUnsupportedAudio, "未対応の音声コーデック/構成のため破棄します (FourCC={0})", info.FourCc ?? "(none)");
        return;
      }
      switch (info.Kind) {
      case FLVTagKind.AudioSequenceHeader:
        OnAudioConfig(msg.Body, info.PayloadOffset);
        break;
      case FLVTagKind.AudioFrame:
        OnAudioFrame(msg, info.PayloadOffset);
        break;
      default:
        WarnOnce(WarnKeyUnsupportedAudio, "未対応の音声タグ種別のため破棄します (kind={0})", info.Kind);
        break;
      }
    }

    public void OnVideo(RTMPMessage msg)
    {
      var info = FLVTagClassifier.Classify(msg);
      switch (info.Kind) {
      case FLVTagKind.VideoSequenceEnd:
      case FLVTagKind.Control:
        return;
      case FLVTagKind.Unknown:
        WarnOnce(WarnKeyBrokenVideoTag, "解釈できない映像タグを破棄します (size={0})", msg.Body.Length);
        return;
      }
      if (!IsSupportedVideoCodec(info.FourCc)) {
        WarnOnce(WarnKeyUnsupportedVideo, "未対応の映像コーデック/構成のため破棄します (FourCC={0})", info.FourCc ?? "(none)");
        return;
      }
      switch (info.Kind) {
      case FLVTagKind.VideoSequenceHeader:
        OnVideoConfig(msg, info.PayloadOffset, info.FourCc);
        break;
      case FLVTagKind.VideoKeyFrame:
      case FLVTagKind.VideoInterFrame:
        OnVideoFrame(msg, info.PayloadOffset, info.CompositionTime, info.Kind==FLVTagKind.VideoKeyFrame);
        break;
      default:
        // MPEG2TSSequenceStart はコーデック設定の生バイトではないので CodecPrivate に使えない。
        WarnOnce(WarnKeyUnsupportedVideo, "未対応の映像タグ種別のため破棄します (kind={0})", info.Kind);
        break;
      }
    }

    public abstract void OnFLVHeader(FLVFileHeader header);
    public abstract void OnData(DataMessage msg);
  }

}
