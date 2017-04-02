using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.Core
{
  public static class CancellationTokenExtension
  {
    public static Task CreateCancelTask(this CancellationToken cancellationToken)
    {
      var cancel_task = new TaskCompletionSource<bool>();
      cancellationToken.Register(() => cancel_task.TrySetCanceled());
      return cancel_task.Task;
    }

    public static Task<T> CreateCancelTask<T>(this CancellationToken cancellationToken)
    {
      var cancel_task = new TaskCompletionSource<T>();
      cancellationToken.Register(() => cancel_task.TrySetCanceled());
      return cancel_task.Task;
    }
  }
}
