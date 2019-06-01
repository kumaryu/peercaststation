using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.Core.Http
{
  public class ChunkedResponseStream
    : ResponseStream
  {
    public ResponseStream BaseStream { get; private set; }
    public override bool CanRead {
      get { return false; }
    }

    public override bool CanSeek {
      get { return false; }
    }

    public override bool CanWrite {
      get { return true; }
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
      BaseStream.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
      return BaseStream.FlushAsync(cancellationToken);
    }

    public ChunkedResponseStream(ResponseStream baseStream)
    {
      BaseStream = baseStream;
    }

    protected override void Dispose(bool disposing)
    {
      base.Dispose(disposing);
      if (disposing) {
        BaseStream.Dispose();
      }
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
      throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
      throw new NotSupportedException();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
      throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
      throw new NotSupportedException();
    }

    private static readonly byte[] CRLF = { (byte)'\r', (byte)'\n' };
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
      if (!CanWrite) throw new NotSupportedException();
      MemoryStream buf;
      using (buf=new MemoryStream()) {
        var header = System.Text.Encoding.ASCII.GetBytes(count.ToString("X"));
        buf.Write(header, 0, header.Length);
        buf.Write(CRLF,   0, CRLF.Length);
        buf.Write(buffer, offset, count);
        buf.Write(CRLF,   0, CRLF.Length);
      }
      var ary = buf.ToArray();
      return BaseStream.WriteAsync(ary, 0, ary.Length, cancellationToken);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
      if (!CanWrite) throw new NotSupportedException();
      MemoryStream buf;
      using (buf=new MemoryStream()) {
        var header = System.Text.Encoding.ASCII.GetBytes(count.ToString("X"));
        buf.Write(header, 0, header.Length);
        buf.Write(CRLF,   0, CRLF.Length);
        buf.Write(buffer, offset, count);
        buf.Write(CRLF,   0, CRLF.Length);
      }
      var ary = buf.ToArray();
      BaseStream.Write(ary, 0, ary.Length);
    }

    private static readonly byte[] LastChunk = { (byte)'0', (byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n' };
    public override async Task CompleteAsync(CancellationToken cancellationToken)
    {
      await BaseStream.WriteAsync(LastChunk, 0, LastChunk.Length).ConfigureAwait(false);
      await BaseStream.CompleteAsync(cancellationToken).ConfigureAwait(false);
    }
  }

}

