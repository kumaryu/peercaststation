using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.Core
{
  public class ConnectionStream
    : Stream
  {
    private RateCounter readBytesCounter  = new RateCounter(1000);
    private int         readTimeout       = Timeout.Infinite;
    private RateCounter writeBytesCounter = new RateCounter(1000);
    private int         writeTimeout      = Timeout.Infinite;
    private CancellationTokenSource closedCancelSource = new CancellationTokenSource();

    private RingbufferStream readBuffer = new RingbufferStream(64*1024);
    private RingbufferStream writeBuffer = new RingbufferStream(64*1024);
    private Task processTask;

    private async Task ProcessRead(Stream s)
    {
      readBuffer.WriteTimeout = Timeout.Infinite;
      var buf = new byte[64*1024];
      var cts = closedCancelSource;
      try {
        var len = await s.ReadAsync(buf, 0, buf.Length, cts.Token).ConfigureAwait(false);
        while (len>0) {
          await readBuffer.WriteAsync(buf, 0, len, cts.Token).ConfigureAwait(false);
          len = await s.ReadAsync(buf, 0, buf.Length, cts.Token).ConfigureAwait(false);
        }
      }
      catch (OperationCanceledException) {
      }
      catch (IOException) {
        if (!cts.IsCancellationRequested) {
          throw;
        }
      }
      catch (ObjectDisposedException) {
        if (!cts.IsCancellationRequested) {
          throw;
        }
      }
      finally {
        readBuffer.CloseWrite();
      }
    }

    private ManualResetWaitableEvent flushEvent = new ManualResetWaitableEvent(false);
    private async Task ProcessWrite(Stream s)
    {
      writeBuffer.ReadTimeout = Timeout.Infinite;
      var buf = new byte[64*1024];
      var cts = closedCancelSource;
      try {
        if (writeBuffer.Available==0) {
          flushEvent.Set();
        }
        var len = await writeBuffer.ReadAsync(buf, 0, buf.Length, cts.Token).ConfigureAwait(false);
        while (len>0) {
          await s.WriteAsync(buf, 0, len, CancellationToken.None).ConfigureAwait(false);
          if (writeBuffer.Available==0) {
            flushEvent.Set();
          }
          len = await writeBuffer.ReadAsync(buf, 0, buf.Length, cts.Token).ConfigureAwait(false);
        }
      }
      catch (OperationCanceledException) {
      }
      catch (IOException) {
        if (!cts.IsCancellationRequested) {
          throw;
        }
      }
      catch (ObjectDisposedException) {
        if (!cts.IsCancellationRequested) {
          throw;
        }
      }
      finally {
        writeBuffer.CloseRead();
      }
    }

    private void CheckException()
    {
      if (!processTask.IsFaulted) return;
      throw processTask.Exception.InnerException;
    }

    public Stream  ReadStream { get; private set; }
    public float   ReadRate { get { return readBytesCounter.Rate; } }
    public override bool CanRead { get { return ReadStream!=null; } }
    public override int ReadTimeout {
      get {
        if (!CanTimeout) throw new InvalidOperationException();
        return readTimeout;
      }
      set {
        if (!CanTimeout) throw new InvalidOperationException();
        readTimeout = value;
        if (ReadStream!=null && ReadStream.CanTimeout) {
          ReadStream.ReadTimeout = readTimeout;
        }
        readBuffer.ReadTimeout = value;
      }
    }

    public Stream  WriteStream { get; private set; }
    public float   WriteRate { get { return writeBytesCounter.Rate; } }
    public override bool CanWrite {
      get { return WriteStream!=null && WriteStream.CanWrite; }
    }
    public override int WriteTimeout {
      get {
        if (!CanTimeout) throw new InvalidOperationException();
        return writeTimeout;
      }
      set {
        if (!CanTimeout) throw new InvalidOperationException();
        writeTimeout = value;
        if (WriteStream!=null && WriteStream.CanTimeout) {
          WriteStream.WriteTimeout = writeTimeout;
        }
        writeBuffer.WriteTimeout = value;
      }
    }
    public override bool CanTimeout {
      get {
        return (ReadStream!=null ? ReadStream.CanTimeout : false) ||
               (WriteStream!=null ? WriteStream.CanTimeout : false);
      }
    }

    public override bool CanSeek {
      get { return false; }
    }

    public override long Length {
      get { throw new NotSupportedException(); }
    }

    public override long Position {
      get { throw new NotSupportedException(); }
      set { throw new NotSupportedException(); }
    }

    public ConnectionStream(Stream read_stream, Stream write_stream)
      : this(read_stream, write_stream, null)
    {
    }

    public ConnectionStream(
      Stream read_stream,
      Stream write_stream,
      byte[] header)
    {
      ReadStream  = read_stream;
      WriteStream = write_stream;
      if (ReadStream!=null && ReadStream.CanTimeout) {
        ReadStream.ReadTimeout = this.ReadTimeout;
      }
      if (WriteStream!=null && WriteStream.CanTimeout) {
        WriteStream.WriteTimeout = this.WriteTimeout;
      }
      if (header!=null && header.Length>0) {
        if (header.Length>readBuffer.Capacity) {
          throw new ArgumentOutOfRangeException(nameof(header));
        }
        readBuffer.Write(header, 0, header.Length);
      }
      processTask = Task.WhenAll(
        ProcessRead(ReadStream),
        ProcessWrite(WriteStream)
      );
    }

    public ConnectionStream(Stream base_stream)
      : this(base_stream, base_stream)
    {
    }

    public override void Flush()
    {
      FlushAsync().Wait();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
      if (closedCancelSource.IsCancellationRequested) throw new ObjectDisposedException(GetType().Name);
      if (WriteStream==null) return Task.FromResult(0);
      flushEvent.Reset();
      return flushEvent.WaitAsync(cancellationToken);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
      return ReadAsync(buffer, offset, count).Result;
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
      WriteAsync(buffer, offset, count).Wait();
    }

    public override async Task<int> ReadAsync(byte[] buf, int offset, int length, CancellationToken cancel_token)
    {
      if (closedCancelSource.IsCancellationRequested) throw new ObjectDisposedException(GetType().Name);
      CheckException();
      var len = await readBuffer.ReadAsync(buf, offset, length, cancel_token).ConfigureAwait(false);
      CheckException();
      readBytesCounter.Add(len);
      return len;
    }

    public async Task<byte[]> ReadAsync(int length, CancellationToken cancel_token)
    {
      if (closedCancelSource.IsCancellationRequested) throw new ObjectDisposedException(GetType().Name);
      CheckException();
      var buf = new byte[length];
      var offset = 0;
      while (offset<length) {
        cancel_token.ThrowIfCancellationRequested();
        var len = await readBuffer.ReadAsync(buf, offset, length-offset, cancel_token).ConfigureAwait(false);
        CheckException();
        if (len==0) throw new EndOfStreamException();
        else {
          offset += len;
          readBytesCounter.Add(len);
        }
      }
      return buf;
    }

    public Task<byte[]> ReadAsync(int length)
    {
      return ReadAsync(length, CancellationToken.None);
    }

    public async Task<int> ReadByteAsync(CancellationToken cancel_token)
    {
      if (closedCancelSource.IsCancellationRequested) throw new ObjectDisposedException(GetType().Name);
      CheckException();
      cancel_token.ThrowIfCancellationRequested();
      var buf = new byte[1];
      var len = await readBuffer.ReadAsync(buf, 0, 1, cancel_token).ConfigureAwait(false);
      CheckException();
      if (len==0) return -1;
      readBytesCounter.Add(1);
      return buf[0];
    }

    public Task<int> ReadByteAsync()
    {
      return ReadByteAsync(CancellationToken.None);
    }

    public override async Task WriteAsync(byte[] buf, int offset, int length, CancellationToken cancel_token)
    {
      if (closedCancelSource.IsCancellationRequested) throw new ObjectDisposedException(GetType().Name);
      CheckException();
      await writeBuffer.WriteAsync(buf, offset, length, cancel_token).ConfigureAwait(false);
      CheckException();
      if (!closedCancelSource.IsCancellationRequested && !cancel_token.IsCancellationRequested) {
        writeBytesCounter.Add(length);
      }
    }

    public Task WriteAsync(byte[] bytes, CancellationToken cancel_token)
    {
      return WriteAsync(bytes, 0, bytes.Length, cancel_token);
    }

    public Task WriteAsync(byte[] bytes)
    {
      return WriteAsync(bytes, 0, bytes.Length, CancellationToken.None);
    }

    protected override void Dispose(bool disposing)
    {
      if (disposing && !closedCancelSource.IsCancellationRequested) {
        closedCancelSource.Cancel();
        if (WriteStream!=null) WriteStream.Close();
        if (ReadStream!=null)  ReadStream.Close();
        try {
          processTask.Wait();
        }
        catch (AggregateException) {
        }
      }
      base.Dispose(disposing);
    }

    public async Task CloseAsync(CancellationToken cancel_token)
    {
      await FlushAsync(cancel_token).ConfigureAwait(false);
      Close();
    }

  }

}

