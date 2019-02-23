using System;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.Core
{
  public class ManualResetWaitableEvent
  {
    private SemaphoreSlim semaphore = new SemaphoreSlim(0);
    private int notified;

    public ManualResetWaitableEvent(bool initialValue)
    {
      notified = initialValue ? 1 : 0;
    }

    public async Task WaitAsync(CancellationToken cancellationToken)
    {
      while (notified==0) {
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
      }
    }

    public void Set()
    {
      var old = Interlocked.Exchange(ref notified, 1);
      if (old==0) {
        semaphore.Release();
      }
    }

    public void Reset()
    {
      notified = 0;
    }
  }

  public class AutoResetWaitableEvent
  {
    private SemaphoreSlim semaphore = new SemaphoreSlim(0);
    private int notified;

    public AutoResetWaitableEvent(bool initialValue)
    {
      notified = initialValue ? 1 : 0;
    }

    public async Task WaitAsync(CancellationToken cancellationToken)
    {
      var old = Interlocked.CompareExchange(ref notified, 1, 0);
      while (old==0) {
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        old = Interlocked.CompareExchange(ref notified, 1, 0);
      }
    }

    public void Set()
    {
      var old = Interlocked.Exchange(ref notified, 1);
      if (old==0) {
        semaphore.Release();
      }
    }

    public void Reset()
    {
      notified = 0;
    }
  }

}

