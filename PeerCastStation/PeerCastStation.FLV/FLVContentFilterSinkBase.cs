// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Philmist
using System;
using System.Threading;
using System.Threading.Tasks;
using PeerCastStation.Core;

namespace PeerCastStation.FLV
{
  /// <summary>
  /// FLV を別コンテナへ変換するコンテンツフィルタの共通土台。
  ///
  /// 上流からの呼び出しを一旦キューへ積み、専用タスクで順に取り出して変換する構造は
  /// FLVToMKV / FLVToMPEG2TS で完全に同じで、キューの停止条件やフォルト時に
  /// 下流を止める手当てのような間違えやすい部分まで重複していた。ここへ集約する。
  ///
  /// 派生クラスが与えるのは実際の変換ループ(<see cref="ProcessMessagesLoopAsync"/>)と、
  /// 下流へ通知するチャンネル情報の3つの文字列だけ。
  /// </summary>
  public abstract class FLVContentFilterSinkBase
    : IContentSink
  {
    protected struct ContentMessage
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

    private readonly Logger logger;
    private readonly Task processorTask;

    /// <summary>上流から積まれ、変換ループが取り出すメッセージキュー。</summary>
    protected WaitableQueue<ContentMessage> MessageQueue { get; } = new WaitableQueue<ContentMessage>();

    /// <summary>ChanInfoType に載せるコンテンツ種別。例: "MKV"。</summary>
    protected abstract string ContentType { get; }
    /// <summary>ChanInfoStreamType に載せる MIME タイプ。例: "video/x-matroska"。</summary>
    protected abstract string MimeType { get; }
    /// <summary>ChanInfoStreamExt に載せる拡張子。例: ".mkv"。</summary>
    protected abstract string ContentExtension { get; }

    /// <summary>
    /// 変換ループ本体。<see cref="MessageQueue"/> から Stop が出るまで取り出し続け、
    /// 最後に targetSink.OnStop を呼ぶところまでが責務。
    /// </summary>
    protected abstract Task ProcessMessagesLoopAsync(IContentSink targetSink, CancellationToken cancellationToken);

    /// <remarks>
    /// ここで起動するタスクは派生クラスのコンストラクタ本体より先に走り出す。
    /// 派生側は変換ループで使う状態をフィールド初期化子(基底コンストラクタより前に走る)か
    /// ループ内のローカル変数に置くこと。
    /// </remarks>
    protected FLVContentFilterSinkBase(IContentSink sink, Logger logger)
    {
      this.logger = logger;
      this.processorTask = ProcessMessagesAsync(sink, CancellationToken.None);
    }

    private async Task ProcessMessagesAsync(IContentSink targetSink, CancellationToken cancellationToken)
    {
      try {
        await ProcessMessagesLoopAsync(targetSink, cancellationToken).ConfigureAwait(false);
      }
      catch (OperationCanceledException) {
        targetSink.OnStop(StopReason.UserShutdown);
      }
      catch (Exception e) {
        // 例外でこのタスクが落ちたまま OnContent が enqueue を続けると、消費者のいない
        // 無制限キューにストリームビットレートで積み上がりメモリリークになる。
        // 下流を明示的に停止し、以後の enqueue は各メソッドの IsCompleted チェックで短絡させる。
        logger.Error(e);
        targetSink.OnStop(StopReason.NotIdentifiedError);
      }
    }

    /// <summary>
    /// 処理タスクが終了(正常終了・フォルトいずれも)した後は消費者がいないため、
    /// enqueue し続けるとキューが無制限に成長する。積むのをやめる。
    /// </summary>
    private bool IsProcessorAlive {
      get { return !processorTask.IsCompleted; }
    }

    public void OnChannelInfo(ChannelInfo channel_info)
    {
      if (!IsProcessorAlive) return;
      var info = new AtomCollection(channel_info.Extra);
      info.SetChanInfoType(ContentType);
      info.SetChanInfoStreamType(MimeType);
      info.SetChanInfoStreamExt(ContentExtension);
      MessageQueue.Enqueue(new ContentMessage { Type=ContentMessage.MessageType.ChannelInfo, ChannelInfo=new ChannelInfo(info) });
    }

    public void OnChannelTrack(ChannelTrack channel_track)
    {
      if (!IsProcessorAlive) return;
      MessageQueue.Enqueue(new ContentMessage { Type=ContentMessage.MessageType.ChannelTrack, ChannelTrack=channel_track });
    }

    public void OnContent(Content content)
    {
      if (!IsProcessorAlive) return;
      MessageQueue.Enqueue(new ContentMessage { Type=ContentMessage.MessageType.ContentBody, Content=content });
    }

    public void OnContentHeader(Content content_header)
    {
      if (!IsProcessorAlive) return;
      MessageQueue.Enqueue(new ContentMessage { Type=ContentMessage.MessageType.ContentHeader, Content=content_header });
    }

    public void OnStop(StopReason reason)
    {
      // 既にフォルトしている場合、下流の OnStop は ProcessMessagesAsync の catch が
      // 呼び済み。Wait() は完了済みタスクに対して即座に返る(例外も握り潰し済み)。
      if (IsProcessorAlive) {
        MessageQueue.Enqueue(new ContentMessage { Type=ContentMessage.MessageType.Stop, StopReason=reason });
      }
      processorTask.Wait();
    }
  }

}
