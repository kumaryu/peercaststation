using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.Core.IPC
{
  public class NamedPipeIPCServer
    : IPCServer
  {
    public NamedPipeIPCServer(string path)
      : base(path)
    {
    }

    public override async Task<IPCClient> AcceptAsync(CancellationToken cancellationToken)
    {
      var pipe = new NamedPipeServerStream(
        Path,
        PipeDirection.InOut,
        NamedPipeServerStream.MaxAllowedServerInstances,
        PipeTransmissionMode.Byte,
        PipeOptions.Asynchronous);
      await new TaskFactory(cancellationToken)
        .FromAsync(pipe.BeginWaitForConnection, pipe.EndWaitForConnection, null)
        .ConfigureAwait(false);
      return new NamedPipeIPCClient(Path, pipe);
    }

    public override void Dispose()
    {
    }

    public override void Start()
    {
    }

    public override void Stop()
    {
    }
  }

}
