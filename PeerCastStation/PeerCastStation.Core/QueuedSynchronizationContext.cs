using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace PeerCastStation.Core
{
  /// <summary>
  /// コールバックをキューに入れて処理するSynchronizationContextの実装です
  /// </summary>
  public class QueuedSynchronizationContext
    : SynchronizationContext
  {
    private class Message
    {
      private QueuedSynchronizationContext owner;
      private AutoResetEvent completedEvent;
      private SendOrPostCallback callback;
      private object state;

      public Message(
        QueuedSynchronizationContext owner,
        SendOrPostCallback callback,
        object state,
        bool waitable)
      {
        this.owner = owner;
        this.completedEvent = waitable ? new AutoResetEvent(false) : null;
        this.callback = callback;
        this.state = state;
      }

      public void Invoke()
      {
        owner.OperationStarted();
        this.callback.Invoke(this.state);
        owner.OperationCompleted();
        if (completedEvent!=null) completedEvent.Set();
      }

      public void Wait()
      {
        completedEvent.WaitOne();
      }
    }

    private Queue<Message> queue = new Queue<Message>();

    /// <summary>
    /// 空のキューを持つ同期コンテキストを初期化します
    /// </summary>
    public QueuedSynchronizationContext()
    {
    }

    /// <summary>
    /// キューが空かどうかを取得します
    /// </summary>
    public bool IsEmpty
    {
      get
      {
        bool res;
        lock (((ICollection)queue).SyncRoot) {
          res = queue.Count == 0;
        }
        return res;
      }
    }

    /// <summary>
    /// 同期コンテキストのコピーを作成します
    /// </summary>
    /// <returns><空のキューを持つ新しいQueuedSynchronizationContextインスタンス</returns>
    public override SynchronizationContext CreateCopy()
    {
      return new QueuedSynchronizationContext();
    }

    /// <summary>
    /// キューに非同期メッセージを入れます
    /// </summary>
    /// <param name="d">呼び出すSendOrPostCallbackデリゲート</param>
    /// <param name="state">デリゲートに渡されるオブジェクト</param>
    public override void Post(SendOrPostCallback d, object state)
    {
      var msg = new Message(this, d, state, false);
      lock (((ICollection)queue).SyncRoot) {
        queue.Enqueue(msg);
      }
    }

    /// <summary>
    /// キューに非同期メッセージを入れ、処理されるまで待ちます
    /// </summary>
    /// <param name="d">呼び出すSendOrPostCallbackデリゲート</param>
    /// <param name="state">デリゲートに渡されるオブジェクト</param>
    public override void Send(SendOrPostCallback d, object state)
    {
      var msg = new Message(this, d, state, true);
      lock (((ICollection)queue).SyncRoot) {
        queue.Enqueue(msg);
      }
      msg.Wait();
    }

    /// <summary>
    /// キューにある非同期メッセージを一つ処理します。
    /// キューが空の場合は何もしません
    /// </summary>
    /// <returns>メッセージがあり処理した場合はtrue、メッセージが無かった場合はfalse</returns>
    public bool Process()
    {
      Message msg = null;
      lock (((ICollection)queue).SyncRoot) {
        if (queue.Count > 0) {
          msg = queue.Dequeue();
        }
      }
      if (msg != null) {
        msg.Invoke();
        return true;
      }
      else {
        return false;
      }
    }

    /// <summary>
    /// キューにある全ての非同期メッセージを処理します。
    /// キューが空の場合は何もしません
    /// </summary>
    public void ProcessAll()
    {
      bool res = Process();
      while (res) res = Process();
    }
  }
}
