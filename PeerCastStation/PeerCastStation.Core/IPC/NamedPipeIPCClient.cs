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
    private PipeStream baseStream;

    public override bool Connected {
      get { return baseStream!=null && baseStream.IsConnected; }
    }

    internal NamedPipeIPCClient(string path, PipeStream baseStream)
      : base(path)
    {
      this.baseStream = baseStream;
    }

    public NamedPipeIPCClient(string path)
      : base(path)
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
        Path,
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
      baseStream = stream;
    }

  }

}
