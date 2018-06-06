using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.Core.IPC
{
  public class NamedPipeIPCServer
    : IPCServer
  {
    private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
    public NamedPipeIPCServer(IPCEndPoint local_endpoint)
      : base(local_endpoint)
    {
    }

    private Task CancelTask(CancellationToken cancellationToken)
    {
      var source = new TaskCompletionSource<bool>();
      cancellationToken.Register(() => source.SetCanceled());
      return source.Task;
    }

    public override async Task<IPCClient> AcceptAsync(CancellationToken cancellationToken)
    {
      var pipe = new NamedPipeServerStream(
        LocalEndPoint.Path,
        PipeDirection.InOut,
        NamedPipeServerStream.MaxAllowedServerInstances,
        PipeTransmissionMode.Byte,
        PipeOptions.Asynchronous);
      using (var cts=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cancellationTokenSource.Token)) {
        cts.Token.Register(pipe.Close);
        try {
          await new TaskFactory(cancellationToken)
            .FromAsync(pipe.BeginWaitForConnection, pipe.EndWaitForConnection, null)
            .ConfigureAwait(false);
          return new NamedPipeIPCClient(LocalEndPoint, pipe);
        }
        catch (ObjectDisposedException) {
          if (cts.IsCancellationRequested) {
            throw new OperationCanceledException();
          }
          else {
            throw;
          }
        }
      }

    }

    public override void Dispose()
    {
      cancellationTokenSource.Dispose();
    }

    public override void Start()
    {
      cancellationTokenSource.Dispose();
      cancellationTokenSource = new CancellationTokenSource();
    }

    public override void Stop()
    {
      cancellationTokenSource.Cancel();
    }
  }

}
