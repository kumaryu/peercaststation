using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;

namespace PeerCastStation.Core
{
  public static class StreamExtension
  {
    public static async Task<int> ReadByteAsync(this Stream stream, CancellationToken cancel_token)
    {
      var buf = new byte[1];
      var len = await stream.ReadAsync(buf, 0, 1, cancel_token);
      if (len==0) return -1;
      else        return buf[0];
    }

    public static Task<int> ReadByteAsync(this Stream stream)
    {
      return stream.ReadByteAsync(CancellationToken.None);
    }
  }

}
