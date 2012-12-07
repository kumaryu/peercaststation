using System;
using System.Linq;

namespace PeerCastStation.Core
{
  public class RingbufferStream
    : System.IO.Stream
  {
    private int length   = 0;
    private int readPos  = 0;
    private int writePos = 0;
    private byte[] buffer;

    public RingbufferStream(int capacity)
    {
      buffer = new byte[capacity];
    }

    protected override void Dispose(bool disposing)
    {
      base.Dispose(disposing);
      buffer = null;
    }

    public byte[] GetBuffer()
    {
      return buffer.Concat(buffer).Skip(readPos).Take(length).ToArray();
    }

    public int Capacity {
      get {
        if (buffer==null) throw new ObjectDisposedException("buffer");
        return buffer.Length;
      }
    }
    public override bool CanRead { get { return true; } }
    public override bool CanSeek { get { return false; } }
    public override bool CanWrite { get { return true; } }

    public override void Flush()
    {
    }

    public override long Length { get { return length; } }

    public override long Position
    {
      get { throw new NotSupportedException(); }
      set { throw new NotSupportedException(); }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
      if (buffer==null) throw new ArgumentNullException("buffer");
      if (offset<0)     throw new ArgumentOutOfRangeException("offset");
      if (count<0)      throw new ArgumentOutOfRangeException("count");
      if (offset+count>buffer.Length) throw new ArgumentException();
      if (this.buffer==null) throw new ObjectDisposedException("buffer");
      if (length==0) return 0;
      count = Math.Min(count, length);
      var len_firsthalf = Capacity-readPos;
      Array.Copy(this.buffer, readPos, buffer, offset, Math.Min(count, len_firsthalf));
      if (count>len_firsthalf) {
        Array.Copy(this.buffer, 0, buffer, offset+len_firsthalf, count-len_firsthalf);
      }
      readPos = (readPos+count) % Capacity;
      length -= count;
      return count;
    }

    public override long Seek(long offset, System.IO.SeekOrigin origin)
    {
      throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
      throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
      if (buffer==null) throw new ArgumentNullException("buffer");
      if (offset<0)     throw new ArgumentOutOfRangeException("offset");
      if (count<0)      throw new ArgumentOutOfRangeException("count");
      if (offset+count>buffer.Length) throw new ArgumentException();
      if (this.buffer==null) throw new ObjectDisposedException("buffer");
      while (count>Capacity) {
        Write(buffer, offset, Capacity);
        offset += Capacity;
        count  -= Capacity;
      }
      var len_firsthalf = Capacity-writePos;
      Array.Copy(buffer, offset, this.buffer, writePos, Math.Min(count, len_firsthalf));
      if (count>len_firsthalf) {
        Array.Copy(buffer, offset+len_firsthalf, this.buffer, 0, count-len_firsthalf);
      }
      var len_free = Capacity-length;
      if (count>len_free) {
        readPos = (readPos+(count-len_free)) % Capacity;
      }
      writePos = (writePos+count) % Capacity;
      length = Math.Min(length+count, Capacity);
    }
  }
}
