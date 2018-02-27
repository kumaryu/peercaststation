using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.Core.IPC
{
  internal class UnixEndPoint
    : EndPoint
  {
    private string path;
    public UnixEndPoint(string path)
    {
      this.path = path;
    }

    public override AddressFamily AddressFamily {
      get { return AddressFamily.Unix; }
    }

    public override SocketAddress Serialize()
    {
      var pathBytes = System.Text.Encoding.Default.GetBytes(path);
      var addr = new SocketAddress(this.AddressFamily, 2+pathBytes.Length+1);
      for (var i=0; i<pathBytes.Length; i++) {
        addr[2+i] = pathBytes[i];
      }
      addr[2+pathBytes.Length] = 0;
      return addr;
    }
  }

  public class UnixSocketIPCServer
    : IPCServer
  {
    private Socket socket;
    public UnixSocketIPCServer(string path)
      : base(path)
    {
    }

    public override async Task<IPCClient> AcceptAsync(CancellationToken cancellationToken)
    {
      var sock = 
        await new TaskFactory(cancellationToken)
        .FromAsync(socket.BeginAccept, socket.EndAccept, null)
        .ConfigureAwait(false);
      return new UnixSocketIPCClient(Path, new NetworkStream(sock, true));
    }

    public override void Dispose()
    {
      Stop();
    }

    public override void Start()
    {
      Stop();
      socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
      socket.Bind(new UnixEndPoint(Path));
      socket.Listen(255);
    }

    public override void Stop()
    {
      if (socket==null) return;
      try {
        File.Delete(Path);
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
