using System;
using System.IO;

namespace PeerCastStation.Core
{
  public class PrependedStream
    : Stream
  {
    private byte[] prependedData;
    private int    position = 0;
    private bool   leaveOpen;
    public Stream BaseStream { get; private set; }

    public PrependedStream(byte[] data, Stream base_stream, bool leave_open)
    {
      this.prependedData = data;
      this.BaseStream    = base_stream;
      this.leaveOpen     = leave_open;
    }

    public PrependedStream(byte[] data, Stream base_stream)
      : this(data, base_stream, false)
    {
    }

    protected override void Dispose(bool disposing)
    {
      if (!disposing || leaveOpen) return;
      BaseStream.Dispose();
    }

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

    public override long Position
    {
      get { throw new NotSupportedException(); }
      set { throw new NotSupportedException(); }
    }

    public override void Flush()
    {
      BaseStream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
      if (position<prependedData.Length) {
        var len = Math.Min(prependedData.Length-position, count);
        Array.Copy(prependedData, position, buffer, offset, len);
        position += len;
        count    -= len;
        offset   += len;
        if (count<=0) return len;
      }
      return BaseStream.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
      throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
      throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
      throw new NotSupportedException();
    }
  }
}
