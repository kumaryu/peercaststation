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
      await locker.WaitAsync(cancellationToken).ConfigureAwait(false);
      T result;
      while (!queue.TryDequeue(out result)) {
        await locker.WaitAsync(cancellationToken).ConfigureAwait(false);
      }
      return result;
    }
  }

}
