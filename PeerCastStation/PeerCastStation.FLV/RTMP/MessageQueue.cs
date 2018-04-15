using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.FLV.RTMP
{
  class MessageQueue<T>
  {
    private SemaphoreSlim filledLock = new SemaphoreSlim(0, Int32.MaxValue);
    private ConcurrentQueue<T> queue = new ConcurrentQueue<T>();

    public void Enqueue(T item)
    {
      queue.Enqueue(item);
      filledLock.Release();
    }

    public async Task<T> DequeueAsync(CancellationToken cancel_token)
    {
    retry:
      await filledLock.WaitAsync(cancel_token).ConfigureAwait(false);
      T value;
      if (!queue.TryDequeue(out value)) {
        goto retry;
      }
      return value;
    }

  }
}
