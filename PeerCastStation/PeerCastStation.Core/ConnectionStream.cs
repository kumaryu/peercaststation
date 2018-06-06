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
    private bool        leaveOpen = false;
    private CancellationTokenSource closedCancelSource = new CancellationTokenSource();

    public Stream  ReadStream { get; private set; }
    public float   ReadRate { get { return readBytesCounter.Rate; } }
    public override bool CanRead { get { return ReadStream!=null; } }
    public override int ReadTimeout {
      get {
        //if (!CanTimeout) throw new InvalidOperationException();
        return readTimeout;
      }
      set {
        //if (!CanTimeout) throw new InvalidOperationException();
        readTimeout = value;
        if (ReadStream!=null && ReadStream.CanTimeout) {
          ReadStream.ReadTimeout = readTimeout;
        }
      }
    }

    public Stream  WriteStream { get; private set; }
    public float   WriteRate { get { return writeBytesCounter.Rate; } }
    public override bool CanWrite {
      get { return WriteStream!=null && WriteStream.CanWrite; }
    }
    public override int WriteTimeout {
      get {
        //if (!CanTimeout) throw new InvalidOperationException();
        return writeTimeout;
      }
      set {
        //if (!CanTimeout) throw new InvalidOperationException();
        writeTimeout = value;
        if (WriteStream!=null && WriteStream.CanTimeout) {
          WriteStream.WriteTimeout = writeTimeout;
        }
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

    public ConnectionStream(
      Stream read_stream,
      Stream write_stream,
      bool leave_open)
    {
      ReadStream  = read_stream;
      WriteStream = write_stream;
      leaveOpen   = leave_open;
      if (ReadStream!=null && ReadStream.CanTimeout) {
        ReadStream.ReadTimeout = this.ReadTimeout;
      }
      if (WriteStream!=null && WriteStream.CanTimeout) {
        WriteStream.WriteTimeout = this.WriteTimeout;
      }
    }

    public ConnectionStream(
      Stream read_stream,
      Stream write_stream)
    {
      ReadStream  = read_stream;
      WriteStream = write_stream;
      if (ReadStream!=null && ReadStream.CanTimeout) {
        ReadStream.ReadTimeout = this.ReadTimeout;
      }
      if (WriteStream!=null && WriteStream.CanTimeout) {
        WriteStream.WriteTimeout = this.WriteTimeout;
      }
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
      return WaitOrCancelTask(WriteStream.FlushAsync(cancellationToken), cancellationToken);
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

    private Task<int> WaitOrCancelTask(Task<int> task, CancellationToken cancel_token)
    {
      return task.ContinueWith(prev => {
        if (prev.IsCanceled) throw new OperationCanceledException();
        if (prev.IsFaulted)  throw prev.Exception.InnerException;
        return prev.Result;
      }, cancel_token);
    }

    private Task WaitOrCancelTask(Task task, CancellationToken cancel_token)
    {
      return task.ContinueWith(prev => {
        if (prev.IsCanceled) throw new OperationCanceledException();
        if (prev.IsFaulted)  throw prev.Exception.InnerException;
      }, cancel_token);
    }

    private CancellationTokenSource LinkTimeoutCancelTokenSource(int timeout, CancellationToken cancel_token)
    {
      if (timeout>0) {
        return CancellationTokenSource.CreateLinkedTokenSource(
          closedCancelSource.Token,
          (new CancellationTokenSource(timeout)).Token,
          cancel_token
        );
      }
      else {
        return CancellationTokenSource.CreateLinkedTokenSource(
          closedCancelSource.Token,
          cancel_token
        );
      }
    }

    public override async Task<int> ReadAsync(byte[] buf, int offset, int length, CancellationToken cancel_token)
    {
      if (closedCancelSource.IsCancellationRequested) throw new ObjectDisposedException(GetType().Name);
      using (var cancelsource = LinkTimeoutCancelTokenSource(ReadTimeout, cancel_token)) {
        var len = await WaitOrCancelTask(ReadStream.ReadAsync(buf, offset, length, cancelsource.Token), cancelsource.Token).ConfigureAwait(false);
        readBytesCounter.Add(len);
        return len;
      }
    }

    public async Task<byte[]> ReadAsync(int length, CancellationToken cancel_token)
    {
      if (closedCancelSource.IsCancellationRequested) throw new ObjectDisposedException(GetType().Name);
      using (var cancelsource = LinkTimeoutCancelTokenSource(ReadTimeout, cancel_token)) {
        var buf = new byte[length];
        var offset = 0;
        while (offset<length) {
          cancelsource.Token.ThrowIfCancellationRequested();
          var len = await WaitOrCancelTask(ReadStream.ReadAsync(buf, offset, length-offset, cancelsource.Token), cancelsource.Token).ConfigureAwait(false);
          if (len==0) throw new EndOfStreamException();
          else {
            offset += len;
            readBytesCounter.Add(len);
          }
        }
        return buf;
      }
    }

    public Task<byte[]> ReadAsync(int length)
    {
      return ReadAsync(length, CancellationToken.None);
    }

    public async Task<int> ReadByteAsync(CancellationToken cancel_token)
    {
      if (closedCancelSource.IsCancellationRequested) throw new ObjectDisposedException(GetType().Name);
      using (var cancelsource = LinkTimeoutCancelTokenSource(ReadTimeout, cancel_token)) {
        cancelsource.Token.ThrowIfCancellationRequested();
        var buf = new byte[1];
        var len = await WaitOrCancelTask(ReadStream.ReadAsync(buf, 0, 1, cancelsource.Token), cancelsource.Token).ConfigureAwait(false);
        if (len==0) return -1;
        readBytesCounter.Add(1);
        return buf[0];
      }
    }

    public Task<int> ReadByteAsync()
    {
      return ReadByteAsync(CancellationToken.None);
    }

    public override async Task WriteAsync(byte[] buf, int offset, int length, CancellationToken cancel_token)
    {
      if (closedCancelSource.IsCancellationRequested) throw new ObjectDisposedException(GetType().Name);
      using (var cancelsource = LinkTimeoutCancelTokenSource(WriteTimeout, cancel_token)) {
        await WaitOrCancelTask(WriteStream.WriteAsync(buf, offset, length, cancelsource.Token), cancelsource.Token).ConfigureAwait(false);
        if (!cancelsource.IsCancellationRequested) {
          writeBytesCounter.Add(length);
        }
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
      if (disposing) {
        closedCancelSource.Cancel();
        if (!leaveOpen) {
          if (WriteStream!=null) WriteStream.Close();
          if (ReadStream!=null)  ReadStream.Close();
        }
      }
      base.Dispose(disposing);
    }

    public Task CloseAsync(CancellationToken cancel_token)
    {
      return FlushAsync(cancel_token).ContinueWith(prev => Close());
    }

  }

}
