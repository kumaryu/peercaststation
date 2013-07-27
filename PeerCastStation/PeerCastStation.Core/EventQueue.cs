using System;
using System.Collections.Generic;
using System.Threading;

namespace PeerCastStation.Core
{
  public class EventQueue<T>
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
      T value;
      TryDequeue(Timeout.Infinite, out value);
      return value;
    }

    public T Dequeue(int timeout_ms)
    {
      T value;
      if (TryDequeue(timeout_ms, out value)) {
        return value;
      }
      else {
        throw new TimeoutException();
      }
    }

    public T Dequeue(TimeSpan timeout)
    {
      T value;
      if (TryDequeue(timeout, out value)) {
        return value;
      }
      else {
        throw new TimeoutException();
      }
    }

    public bool TryDequeue(int timeout_ms, out T value)
    {
      lock (queue) {
        if (semaphore.WaitOne(timeout_ms, true)) {
          if (queue.Count==1) waitHandle.Reset();
          value = queue.Dequeue();
          return true;
        }
        else {
          value = default(T);
          return false;
        }
      }
    }

    public bool TryDequeue(TimeSpan timeout, out T value)
    {
      lock (queue) {
        if (semaphore.WaitOne(timeout, true)) {
          if (queue.Count==1) waitHandle.Reset();
          value = queue.Dequeue();
          return true;
        }
        else {
          value = default(T);
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
