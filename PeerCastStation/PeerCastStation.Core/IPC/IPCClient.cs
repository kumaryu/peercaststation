using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.Core.IPC
{
  public abstract class IPCClient
    : IDisposable
  {
    public IPCEndPoint RemoteEndPoint { get; private set; }
    public abstract bool Connected { get; }
    public IPCClient(IPCEndPoint remote_endpoint)
    {
      RemoteEndPoint = remote_endpoint;
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
      return Create(new IPCEndPoint(path));
    }

    public static IPCClient Create(IPCEndPoint remote_endpoint)
    {
      switch (Environment.OSVersion.Platform) {
      case PlatformID.Win32NT:
      case PlatformID.Win32S:
      case PlatformID.Win32Windows:
      case PlatformID.WinCE:
      case PlatformID.Xbox:
        return new NamedPipeIPCClient(remote_endpoint);
      case PlatformID.MacOSX:
      case PlatformID.Unix:
      default:
        return new UnixSocketIPCClient(remote_endpoint);
      }
    }

  }

}
