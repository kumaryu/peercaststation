using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading;

namespace PeerCastStation.Core.Http
{
  /// <summary>
  /// ストリームからHTTPリクエストを読み取るクラスです
  /// </summary>
  public class HttpRequestReader
    : IDisposable
  {
    private bool leaveOpen;
    public Stream BaseStream { get; private set; }

    public HttpRequestReader(Stream baseStream, bool leaveOpen)
    {
      BaseStream = baseStream;
      this.leaveOpen = leaveOpen;
    }

    public HttpRequestReader(Stream baseStream)
      : this(baseStream, false)
    {
    }

    public void Dispose()
    {
      if (leaveOpen) return;
      BaseStream.Dispose();
    }

    public async Task<HttpRequest> ReadAsync(CancellationToken cancel_token)
    {
      var requests = new List<string>();
      var buf = new System.Text.StringBuilder();
      do {
        var value = await BaseStream.ReadByteAsync(cancel_token).ConfigureAwait(false);
        if (value<0) return null;
        buf.Append((char)value);
        if (buf.Length>2 && buf[buf.Length-2]=='\r' && buf[buf.Length-1]=='\n') {
          requests.Add(buf.ToString(0, buf.Length-2));
          buf.Clear();
        }
      } while (buf.Length<2 || buf[0]!='\r' || buf[1]!='\n');
      return HttpRequest.ParseRequest(requests);
    }
  }

}

