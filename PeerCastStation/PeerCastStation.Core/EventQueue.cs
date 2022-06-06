using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace PeerCastStation.Core
{
  public class EventQueue<T> where T: class
  {
    private Queue<T> queue = new Queue<T>();
    private Semaphore semaphore = new Semaphore(0, Int32.MaxValue);
    private ManualResetEvent waitHandle = new ManualResetEvent(false);

    public EventQueue()
    {
    }

    public int Count {
      get { lock (queue) { return queue.Count; } }
    }

    public WaitHandle WaitHandle {
      get { return waitHandle; }
    }

    public T Dequeue()
    {
      TryDequeue(Timeout.Infinite, out var value);
      return value!;
    }

    public T Dequeue(int timeout_ms)
    {
      if (TryDequeue(timeout_ms, out var value)) {
        return value;
      }
      else {
        throw new TimeoutException();
      }
    }

    public T Dequeue(TimeSpan timeout)
    {
      if (TryDequeue(timeout, out var value)) {
        return value;
      }
      else {
        throw new TimeoutException();
      }
    }

    public bool TryDequeue(int timeout_ms, [NotNullWhen(true)] out T? value)
    {
      lock (queue) {
        if (semaphore.WaitOne(timeout_ms, true)) {
          if (queue.Count==1) waitHandle.Reset();
          value = queue.Dequeue();
          return true;
        }
        else {
          value = default;
          return false;
        }
      }
    }

    public bool TryDequeue(TimeSpan timeout, [NotNullWhen(true)] out T? value)
    {
      lock (queue) {
        if (semaphore.WaitOne(timeout, true)) {
          if (queue.Count==1) waitHandle.Reset();
          value = queue.Dequeue();
          return true;
        }
        else {
          value = default;
          return false;
        }
      }
    }

    public void Enqueue(T value)
    {
      lock (queue) {
        queue.Enqueue(value);
        waitHandle.Set();
        semaphore.Release();
      }
    }

    public void Clear()
    {
      lock (queue) {
        while (semaphore.WaitOne(0));
        waitHandle.Reset();
        queue.Clear();
      }
    }

  }
}
