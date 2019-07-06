using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.Core
{
  public class WaitableQueue<T>
  {
    private SemaphoreSlim locker = new SemaphoreSlim(0);
    private ConcurrentQueue<T> queue = new ConcurrentQueue<T>();

    public void Enqueue(T value)
    {
      queue.Enqueue(value);
      locker.Release();
    }

    public async Task<T> DequeueAsync(CancellationToken cancellationToken)
    {
      T result;
      while (!queue.TryDequeue(out result)) {
        await locker.WaitAsync(cancellationToken).ConfigureAwait(false);
      }
      return result;
    }

    public bool TryDequeue(out T result)
    {
      return queue.TryDequeue(out result);
    }

    public bool TryPeek(out T result)
    {
      return queue.TryPeek(out result);
    }

  }

}
