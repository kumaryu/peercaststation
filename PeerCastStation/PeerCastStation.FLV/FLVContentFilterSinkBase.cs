// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Philmist
using System;
using System.Threading;
using System.Threading.Tasks;
using PeerCastStation.Core;
using PeerCastStation.FLV.RTMP;

namespace PeerCastStation.FLV
{
  /// <summary>
  /// FLV を別コンテナへ変換するコンテンツフィルタの共通土台。
  /// </summary>
  /// <remarks>
  /// 上流からの呼び出しを一旦キューへ積み、専用タスクで順に取り出して変換する構造は
  /// FLVToMKV / FLVToMPEG2TS で完全に同じで、キューの停止条件やフォルト時に
  /// 下流を止める手当てのような間違えやすい部分まで重複していた。ここへ集約する。
  ///
  /// 派生クラスが与えるのは実際の変換ループ(<see cref="ProcessMessagesLoopAsync"/>)と、
  /// 下流へ通知するチャンネル情報の3つの文字列だけ。
  /// </remarks>
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
    private readonly IContentSink targetSink;
    /// <summary>キューへの投入と「消費者がいなくなった」の確定を直列化する。</summary>
    private readonly object queueLock = new object();
    private bool acceptingMessages = true;
    private Task? processorTask = null;

    /// <summary>上流から積まれ、変換ループが取り出すメッセージキュー。</summary>
    protected WaitableQueue<ContentMessage> MessageQueue { get; } = new WaitableQueue<ContentMessage>();

    /// <summary>ChanInfoType に載せるコンテンツ種別。例: "MKV"。</summary>
    protected abstract string ContentType { get; }
    /// <summary>ChanInfoStreamType に載せる MIME タイプ。例: "video/x-matroska"。</summary>
    protected abstract string MimeType { get; }
    /// <summary>ChanInfoStreamExt に載せる拡張子。例: ".mkv"。</summary>
    protected abstract string ContentExtension { get; }

    /// <summary>
    /// 上流 Content をタグへ解くための変換器と解析バッファの組。
    /// 派生ごとに作り方が違うだけで、ループでの扱いは同じ。
    /// </summary>
    protected sealed class ContentProcessor
    {
      public IRTMPContentSink Context { get; }
      private readonly FLVParseBuffer parseBuffer = new FLVParseBuffer();

      public ContentProcessor(IRTMPContentSink context)
      {
        Context = context;
      }

      public void Feed(Content content)
      {
        parseBuffer.Feed(content.Data.Span, Context);
      }
    }

    /// <summary>
    /// 変換器を作る。ループの構造(キューの取り出し、停止条件、OnStop の呼び出し)は
    /// 基底が持つので、派生は出力先 sink と変換器の組み立てだけを担当する。
    /// </summary>
    protected abstract ContentProcessor CreateProcessor(IContentSink targetSink);

    /// <summary>
    /// 変換ループ本体。Stop が出るまでキューから取り出し続け、最後に targetSink.OnStop を呼ぶ。
    /// 停止条件と OnStop の呼び忘れは下流を止められなくなる間違いなので、ここに一本化する。
    /// </summary>
    private async Task ProcessMessagesLoopAsync(IContentSink targetSink, CancellationToken cancellationToken)
    {
      var processor = CreateProcessor(targetSink);
      var msg = await MessageQueue.DequeueAsync(cancellationToken).ConfigureAwait(false);
      while (msg.Type!=ContentMessage.MessageType.Stop) {
        switch (msg.Type) {
        case ContentMessage.MessageType.ChannelInfo:
          targetSink.OnChannelInfo(msg.ChannelInfo);
          break;
        case ContentMessage.MessageType.ChannelTrack:
          targetSink.OnChannelTrack(msg.ChannelTrack);
          break;
        case ContentMessage.MessageType.ContentHeader:
        case ContentMessage.MessageType.ContentBody:
          processor.Feed(msg.Content);
          break;
        }
        msg = await MessageQueue.DequeueAsync(cancellationToken).ConfigureAwait(false);
      }
      targetSink.OnStop(msg.StopReason);
    }

    protected FLVContentFilterSinkBase(IContentSink sink, Logger logger)
    {
      this.targetSink = sink;
      this.logger = logger;
    }

    /// <summary>
    /// 変換ループを開始する。<see cref="IContentFilter.Activate"/> が、オブジェクトを
    /// 完全に構築し終えてから呼ぶこと。
    /// </summary>
    /// <remarks>
    /// コンストラクタから起動すると、派生クラスのコンストラクタ本体より先に
    /// <see cref="ProcessMessagesLoopAsync"/> が走り出す。今は派生のコンストラクタが
    /// どちらも空なので表面化しないが、引数を1つ増やして状態を初期化した時点で、
    /// ループ側がその状態を null のまま掴んで NullReferenceException になる。
    /// それは ProcessMessagesAsync の catch に拾われて下流が NotIdentifiedError で
    /// 止まるため、症状は「配信が途中で死ぬ」でログには構築順の話が出てこない。
    /// </remarks>
    public void Start()
    {
      if (processorTask!=null) {
        throw new InvalidOperationException("変換ループは既に開始しています");
      }
      processorTask = ProcessMessagesAsync(targetSink, CancellationToken.None);
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
        // 下流を明示的に停止し、以後の enqueue は TryEnqueue で短絡させる。
        // 停止理由は上流の都合ではなくフィルタが落ちたことなので NotIdentifiedError でよい。
        logger.Error(e);
        targetSink.OnStop(StopReason.NotIdentifiedError);
      }
      finally {
        // ここから先に積まれたメッセージを取り出す者はいない。
        lock (queueLock) {
          acceptingMessages = false;
        }
      }
    }

    /// <summary>
    /// 消費者が生きている間だけキューへ積む。
    /// </summary>
    /// <remarks>
    /// 「タスクが完了しているか調べてから積む」形にすると、調べた直後にタスクが
    /// 終了した場合に消費者のいないキューへ積んでしまうため、投入と打ち切りの確定を
    /// 同じロックで直列化する。
    /// </remarks>
    private bool TryEnqueue(ContentMessage message)
    {
      lock (queueLock) {
        if (!acceptingMessages) return false;
        MessageQueue.Enqueue(message);
        return true;
      }
    }

    public void OnChannelInfo(ChannelInfo channel_info)
    {
      var info = new AtomCollection(channel_info.Extra);
      info.SetChanInfoType(ContentType);
      info.SetChanInfoStreamType(MimeType);
      info.SetChanInfoStreamExt(ContentExtension);
      TryEnqueue(new ContentMessage { Type=ContentMessage.MessageType.ChannelInfo, ChannelInfo=new ChannelInfo(info) });
    }

    public void OnChannelTrack(ChannelTrack channel_track)
    {
      TryEnqueue(new ContentMessage { Type=ContentMessage.MessageType.ChannelTrack, ChannelTrack=channel_track });
    }

    public void OnContent(Content content)
    {
      TryEnqueue(new ContentMessage { Type=ContentMessage.MessageType.ContentBody, Content=content });
    }

    public void OnContentHeader(Content content_header)
    {
      TryEnqueue(new ContentMessage { Type=ContentMessage.MessageType.ContentHeader, Content=content_header });
    }

    public void OnStop(StopReason reason)
    {
      var task = processorTask;
      if (task==null) return;
      // TryEnqueue が false のときは変換ループが既に終了しており、下流の OnStop は
      // ループ自身か ProcessMessagesAsync の catch が呼び済み。二重には送らない。
      TryEnqueue(new ContentMessage { Type=ContentMessage.MessageType.Stop, StopReason=reason });
      task.Wait();
    }
  }

}
