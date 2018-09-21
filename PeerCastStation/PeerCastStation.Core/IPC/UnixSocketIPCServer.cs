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
    private string createdPath = null;
    public UnixSocketIPCServer(IPCEndPoint local_endpoint, IPCOption options)
      : base(local_endpoint, options)
    {
    }

    ~UnixSocketIPCServer()
    {
      Dispose(false);
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
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    protected void Dispose(bool disposing)
    {
      if (disposing) {
        socket?.Close();
        socket = null;
      }
      try {
        if (createdPath!=null) {
          File.Delete(createdPath);
        }
      }
      catch (Exception) {
      }
      createdPath = null;
    }

    private static void Chmod(string mode, string path)
    {
      var process = System.Diagnostics.Process.Start("chmod", mode + " " + path);
      process.WaitForExit();
    }

    public override void Start()
    {
      Stop();
      socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
      try {
        Directory.CreateDirectory(Path.GetDirectoryName(LocalEndPoint.Path));
        File.Delete(LocalEndPoint.Path);
      }
      catch (Exception) {
      }
      socket.Bind(LocalEndPoint);
      createdPath = LocalEndPoint.Path;
      if (Options.HasFlag(IPCOption.AcceptAnyUsers)) {
        Chmod("a+rw", createdPath);
      }
      socket.Listen(255);
    }

    public override void Stop()
    {
      Dispose();
    }

  }

}
