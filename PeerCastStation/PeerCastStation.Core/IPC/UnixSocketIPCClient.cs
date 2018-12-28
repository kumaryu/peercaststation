using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.Core.IPC
{
  public class UnixSocketIPCClient
    : IPCClient
  {
    private NetworkStream baseStream;

    public override bool Connected {
      get { return baseStream!=null; }
    }

    internal UnixSocketIPCClient(IPCEndPoint remote_endpoint, Socket socket)
      : base(remote_endpoint)
    {
      this.baseStream = new NetworkStream(socket, true);
    }

    public UnixSocketIPCClient(IPCEndPoint remote_endpoint)
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
      if (Connected) throw new InvalidOperationException("Already connected");
      var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
      await Task.Factory.FromAsync(socket.BeginConnect, socket.EndConnect, RemoteEndPoint, null).ConfigureAwait(false);
      baseStream = new NetworkStream(socket, true);
    }

  }

}
