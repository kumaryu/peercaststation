using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.Core
{
  public static class CancellationTokenExtension
  {
    public static async Task CreateCancelTask(this CancellationToken cancellationToken)
    {
      var cancel_task = new TaskCompletionSource<bool>();
      using var _ = cancellationToken.Register(() => cancel_task.TrySetCanceled(), false);
      await cancel_task.Task.ConfigureAwait(false);
    }

    public static async Task<T> CreateCancelTask<T>(this CancellationToken cancellationToken)
    {
      var cancel_task = new TaskCompletionSource<T>();
      using var _ = cancellationToken.Register(() => cancel_task.TrySetCanceled(), false);
      return await cancel_task.Task.ConfigureAwait(false);
    }
  }
}
