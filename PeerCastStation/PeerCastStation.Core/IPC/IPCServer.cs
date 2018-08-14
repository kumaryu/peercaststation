using System;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.Core.IPC
{
  public abstract class IPCServer
    : IDisposable
  {
    public IPCEndPoint LocalEndPoint { get; private set; }
    public IPCOption Options { get; private set; }

    public IPCServer(IPCEndPoint local_endpoint, IPCOption options)
    {
      LocalEndPoint = local_endpoint;
      Options = options;
    }

    public abstract void Start();

    public abstract void Stop();

    public abstract Task<IPCClient> AcceptAsync(CancellationToken cancellationToken);

    public abstract void Dispose();

    public static IPCServer Create(string path)
    {
      return Create(new IPCEndPoint(path), IPCOption.None);
    }

    public static IPCServer Create(string path, IPCOption options)
    {
      return Create(new IPCEndPoint(path), options);
    }

    public static IPCServer Create(IPCEndPoint local_endpoint)
    {
      return Create(local_endpoint, IPCOption.None);
    }

    public static IPCServer Create(IPCEndPoint local_endpoint, IPCOption options)
    {
      switch (Environment.OSVersion.Platform) {
      case PlatformID.Win32NT:
      case PlatformID.Win32S:
      case PlatformID.Win32Windows:
      case PlatformID.WinCE:
      case PlatformID.Xbox:
        return new NamedPipeIPCServer(local_endpoint, options);
      case PlatformID.MacOSX:
      case PlatformID.Unix:
      default:
        return new UnixSocketIPCServer(local_endpoint, options);
      }
    }

  }

}
