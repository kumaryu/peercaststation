using System.Net;
using System.Net.Sockets;

namespace PeerCastStation.Core.IPC
{
  public class IPCEndPoint
    : EndPoint
  {
    public string Path { get; private set; }

    public override AddressFamily AddressFamily {
      get { return AddressFamily.Unix; }
    }

    public IPCEndPoint(string path)
    {
      Path = path;
    }

    public override SocketAddress Serialize()
    {
      var pathBytes = System.Text.Encoding.Default.GetBytes(Path);
      var addr = new SocketAddress(this.AddressFamily, 2+pathBytes.Length+1);
      for (var i=0; i<pathBytes.Length; i++) {
        addr[2+i] = pathBytes[i];
      }
      addr[2+pathBytes.Length] = 0;
      return addr;
    }
  }

}
