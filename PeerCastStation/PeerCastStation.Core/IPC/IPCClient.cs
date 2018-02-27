using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.Core.IPC
{
  public abstract class IPCClient
    : IDisposable
  {
    public string Path { get; private set; }
    public abstract bool Connected { get; }
    public IPCClient(string path)
    {
      Path = path;
    }

    public abstract Task ConnectAsync(CancellationToken cancellationToken);

    public abstract Stream GetStream();

    public abstract void Dispose();
    public void Close()
    {
      Dispose();
    }
  }

}
