using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.Core
{
  public class RingbufferStream
    : System.IO.Stream
  {
    private long length   = 0;
    private long readPos  = 0;
    private long writePos = 0;
    private long position = 0;
    private byte[] internalBuffer;
    private bool readClosed = false;
    private bool writeClosed = false;
    private SemaphoreSlim readSemaphore = new SemaphoreSlim(0);
    private SemaphoreSlim writeSemaphore = new SemaphoreSlim(0);

    public RingbufferStream(int capacity)
    {
      internalBuffer = new byte[capacity];
    }

    protected override void Dispose(bool disposing)
    {
      if (disposing) {
        CloseWrite();
        CloseRead();
      }
      base.Dispose(disposing);
    }

    public void CloseWrite()
    {
      if (writeClosed) return;
      writeClosed = true;
      readSemaphore.Release();
    }

    public void CloseRead()
    {
      if (readClosed) return;
      readClosed = true;
      writeSemaphore.Release();
    }

    public override int ReadTimeout { get; set; } = Timeout.Infinite;
    public override int WriteTimeout { get; set; } = Timeout.Infinite;

    public int Capacity { get { return internalBuffer.Length; } }
    public long Available { get { return length; } }
    public override bool CanRead { get { return true; } }
    public override bool CanSeek { get { return false; } }
    public override bool CanWrite { get { return true; } }

    public override void Flush()
    {
    }
    
    public override long Length { get { return length; } }
    
    public override long Position {
      get { return position; }
      set { Seek(value-position, System.IO.SeekOrigin.Current); }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
      return ReadAsync(buffer, offset, count, CancellationToken.None).Result;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
      if (buffer==null) throw new ArgumentNullException("buffer");
      if (offset<0)     throw new ArgumentOutOfRangeException("offset");
      if (count<0)      throw new ArgumentOutOfRangeException("count");
      if (readClosed) throw new ObjectDisposedException("ReadClosed");
      if (length==0 && writeClosed) return 0;
    retry:
      while (length==0 && !writeClosed) {
        if (!await readSemaphore.WaitAsync(ReadTimeout, cancellationToken).ConfigureAwait(false)) {
          throw new IOTimeoutException();
        }
        if (readClosed) throw new ObjectDisposedException("ReadClosed");
      }
      lock (internalBuffer) {
        if (length==0 && !writeClosed) {
          goto retry;
        }
        count = Math.Min(count, (int)length);
        var len_firsthalf = Capacity-readPos;
        Array.Copy(internalBuffer, readPos, buffer, offset, Math.Min(count, len_firsthalf));
        if (count>len_firsthalf) {
          Array.Copy(internalBuffer, 0, buffer, offset+len_firsthalf, count-len_firsthalf);
        }
        readPos = (readPos+count) % Capacity;
        length -= count;
        position += count;
        writeSemaphore.Release();
        return count;
      }
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
      WriteAsync(buffer, offset, count, CancellationToken.None).Wait();
    }

    private int WriteInternal(byte[] buffer, int offset, int count)
    {
      lock (internalBuffer) {
        count = Math.Min(count, (int)(Capacity-length));
        if (count==0) return 0;
        var len_firsthalf = Capacity-writePos;
        Array.Copy(buffer, offset, internalBuffer, writePos, Math.Min(count, len_firsthalf));
        if (count>len_firsthalf) {
          Array.Copy(buffer, offset+len_firsthalf, internalBuffer, 0, count-len_firsthalf);
        }
        writePos = (writePos+count) % Capacity;
        length = Math.Min(length+count, Capacity);
        readSemaphore.Release();
        return count;
      }
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
      if (buffer==null) throw new ArgumentNullException("buffer");
      if (offset<0)     throw new ArgumentOutOfRangeException("offset");
      if (count<0)      throw new ArgumentOutOfRangeException("count");
      if (offset+count>buffer.Length) throw new ArgumentException();
      if (writeClosed) throw new ObjectDisposedException("WriteClosed");
      if (count==0) return;
      var len = WriteInternal(buffer, offset, count);
      offset += len;
      count  -= len;
      while (count>0 && !readClosed) {
        if (!await writeSemaphore.WaitAsync(WriteTimeout, cancellationToken).ConfigureAwait(false)) {
          throw new IOTimeoutException();
        }
        if (writeClosed) throw new ObjectDisposedException("WriteClosed");
        len = WriteInternal(buffer, offset, count);
        offset += len;
        count  -= len;
      }
    }

  }
}

