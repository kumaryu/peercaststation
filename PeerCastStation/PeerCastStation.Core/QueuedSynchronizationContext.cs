// PeerCastStation, a P2P streaming servent.
// Copyright (C) 2011 Ryuichi Sakamoto (kumaryu@kumaryu.net)
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
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
      this.EventHandle = new AutoResetEvent(false);
    }

    /// <summary>
    /// キューにメッセージが入った時にセットされるイベントハンドルを取得します
    /// </summary>
    public AutoResetEvent EventHandle { get; private set; }

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
    /// <returns>空のキューを持つ新しいQueuedSynchronizationContextインスタンス</returns>
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
        EventHandle.Set();
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
        EventHandle.Set();
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
