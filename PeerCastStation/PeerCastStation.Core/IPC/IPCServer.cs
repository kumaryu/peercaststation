using System;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.Core.IPC
{
  public abstract class IPCServer
    : IDisposable
  {
    public string Path { get; private set; }
    public IPCServer(string path)
    {
      Path = path;
    }

    public abstract void Start();

    public abstract void Stop();

    public abstract Task<IPCClient> AcceptAsync(CancellationToken cancellationToken);

    public abstract void Dispose();

    public static IPCServer Create(string path)
    {
      switch (Environment.OSVersion.Platform) {
      case PlatformID.Win32NT:
      case PlatformID.Win32S:
      case PlatformID.Win32Windows:
      case PlatformID.WinCE:
      case PlatformID.Xbox:
        return new NamedPipeIPCServer(path);
      case PlatformID.MacOSX:
      case PlatformID.Unix:
      default:
        return new UnixSocketIPCServer(path);
      }
    }
  }
}
