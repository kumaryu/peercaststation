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

    public static IPCClient Create(string path)
    {
      switch (Environment.OSVersion.Platform) {
      case PlatformID.Win32NT:
      case PlatformID.Win32S:
      case PlatformID.Win32Windows:
      case PlatformID.WinCE:
      case PlatformID.Xbox:
        return new NamedPipeIPCClient(path);
      case PlatformID.MacOSX:
      case PlatformID.Unix:
      default:
        return new UnixSocketIPCClient(path);
      }
    }
  }

}
