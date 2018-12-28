using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.Core.IPC
{
  public class NamedPipeIPCClient
    : IPCClient
  {
    private WrappedPipeStream baseStream;

    public override bool Connected {
      get { return baseStream!=null && baseStream.IsConnected; }
    }

    internal NamedPipeIPCClient(IPCEndPoint remote_endpoint, PipeStream baseStream)
      : base(remote_endpoint)
    {
      this.baseStream = new WrappedPipeStream(baseStream);
    }

    public NamedPipeIPCClient(IPCEndPoint remote_endpoint)
      : base(remote_endpoint)
    {
      this.baseStream = null;
    }

    public override Stream GetStream()
    {
      return baseStream;
    }

    public override void Dispose()
    {
      baseStream?.Dispose();
      baseStream = null;
    }

    public override async Task ConnectAsync(CancellationToken cancellationToken)
    {
      if (baseStream!=null) throw new InvalidOperationException("Already connected");
      var stream = new NamedPipeClientStream(
        ".",
        RemoteEndPoint.Path,
        PipeDirection.InOut,
        PipeOptions.Asynchronous);
      await Task.Run(() => {
        do {
          cancellationToken.ThrowIfCancellationRequested();
          try {
            stream.Connect(50);
          }
          catch (TimeoutException) {
          }
        } while (!stream.IsConnected);
      }, cancellationToken).ConfigureAwait(false);
      baseStream = new WrappedPipeStream(stream);
    }

  }

}
