using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Net;

namespace PeerCastStation.Core.Http
{
  public class ChunkedRequestStream
    : Stream
  {
    public Stream BaseStream { get; private set; }
    public override bool CanRead {
      get { return true; }
    }

    public override bool CanSeek {
      get { return false; }
    }

    public override bool CanWrite {
      get { return false; }
    }

    public override long Length {
      get { throw new NotSupportedException(); }
    }

    public override long Position {
      get { throw new NotSupportedException(); }
      set { throw new NotSupportedException(); }
    }

    public override void Flush()
    {
      throw new NotSupportedException();
    }

    private bool leaveOpen;

    public ChunkedRequestStream(Stream baseStream, bool leaveOpen)
    {
      if (!baseStream.CanRead) throw new ArgumentException("BaseStream must be readable");
      this.BaseStream = baseStream;
      this.leaveOpen = leaveOpen;
    }

    public ChunkedRequestStream(Stream baseStream)
      : this(baseStream, false)
    {
    }

    protected override void Dispose(bool disposing)
    {
      base.Dispose(disposing);
      if (!disposing || leaveOpen) return;
      this.BaseStream.Dispose();
    }

    private async Task<List<byte>> ReadLineAsync(List<byte> bytes, CancellationToken cancellationToken)
    {
      while (bytes.Count<2 || bytes[bytes.Count-2]!='\r' || bytes[bytes.Count-1]!='\n') {
        var b = await BaseStream.ReadByteAsync(cancellationToken).ConfigureAwait(false);
        if (b<0) throw new IOException();
        bytes.Add((byte)b);
      }
      return bytes;
    }

    private int currentChunkSize = 0;
    private bool completed = false;
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
      if (completed) return 0;
      var bytes = new List<byte>();
      while (currentChunkSize==0) {
        bytes = await ReadLineAsync(bytes, cancellationToken).ConfigureAwait(false);
        var line = System.Text.Encoding.ASCII.GetString(bytes.ToArray(), 0, bytes.Count-2);
        bytes.Clear();
        if (line!="") {
          int len;
          if (Int32.TryParse(
              line.Split(';')[0],
              System.Globalization.NumberStyles.AllowHexSpecifier,
              System.Globalization.CultureInfo.InvariantCulture.NumberFormat,
              out len)) {
            if (len==0) {
              //trailer-part
              await ReadLineAsync(bytes, cancellationToken).ConfigureAwait(false);
              completed = true;
              return 0;
            }
            else {
              currentChunkSize = len;
            }
          }
          else {
            throw new HttpErrorException(HttpStatusCode.BadRequest);
          }
        }
      }
      if (currentChunkSize>0) {
        int len = await BaseStream.ReadAsync(buffer, offset, Math.Min(count, currentChunkSize), cancellationToken).ConfigureAwait(false);
        if (len>=0) {
          offset += len;
          count  -= len;
          currentChunkSize -= len;
        }
        if (currentChunkSize==0) {
          await BaseStream.ReadByteAsync(cancellationToken).ConfigureAwait(false); //\r
          await BaseStream.ReadByteAsync(cancellationToken).ConfigureAwait(false); //\n
        }
        return len;
      }
      else {
        return 0;
      }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
      return ReadAsync(buffer, offset, count, CancellationToken.None).Result;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
      throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
      throw new NotSupportedException();
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
      throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
      throw new NotSupportedException();
    }

  }

}
