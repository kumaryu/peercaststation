using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.Core.IPC
{
  public class UnixSocketIPCServer
    : IPCServer
  {
    private Socket socket;
    public UnixSocketIPCServer(IPCEndPoint local_endpoint)
      : base(local_endpoint)
    {
    }

    public override async Task<IPCClient> AcceptAsync(CancellationToken cancellationToken)
    {
      var sock = 
        await new TaskFactory(cancellationToken)
        .FromAsync(socket.BeginAccept, socket.EndAccept, null)
        .ConfigureAwait(false);
      return new UnixSocketIPCClient(LocalEndPoint, sock);
    }

    public override void Dispose()
    {
      Stop();
    }

    public override void Start()
    {
      Stop();
      socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
      socket.Bind(LocalEndPoint);
      socket.Listen(255);
    }

    public override void Stop()
    {
      if (socket==null) return;
      try {
        File.Delete(LocalEndPoint.Path);
      }
      catch (IOException) {
      }
      catch (UnauthorizedAccessException) {
      }
      socket.Close();
      socket = null;
    }

  }

}
