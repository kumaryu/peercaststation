using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.Core.IPC
{
  class WrappedPipeStream
    : Stream
  {
    private PipeStream baseStream;
    public WrappedPipeStream(PipeStream baseStream)
    {
      this.baseStream = baseStream;
    }

    protected override void Dispose(bool disposing)
    {
      if (disposing) {
        baseStream.Dispose();
      }
      base.Dispose(disposing);
    }

    private int writeTimeout = -1;
    public override int WriteTimeout {
      get { return writeTimeout; }
      set { writeTimeout = value; }
    }

    private int readTimeout = -1;
    public override int ReadTimeout {
      get { return readTimeout; }
      set { readTimeout = value; }
    }

    public override bool CanRead {
      get { return baseStream.CanRead; }
    }

    public override bool CanSeek {
      get { return baseStream.CanSeek; }
    }

    public override bool CanWrite {
      get { return baseStream.CanWrite; }
    }

    public override long Length {
      get { return baseStream.Length; }
    }

    public override long Position {
      get { return baseStream.Position; }
      set { baseStream.Position = value; }
    }

    public bool IsConnected {
      get { return baseStream.IsConnected; }
    }

    public override void Flush()
    {
      FlushAsync().Wait();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
      return baseStream.FlushAsync(cancellationToken);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
      try {
        var task = ReadAsync(buffer, offset, count, CancellationToken.None);
        task.Wait();
        return task.Result;
      }
      catch (AggregateException ex) {
        throw ex.InnerException;
      }
    }

    private T ThrowTimeout<T>()
    {
      throw new IOException();
    }

    private Task<T> TimeoutAfterTask<T>(int ms)
    {
      return Task
        .Delay(ms)
        .ContinueWith(prev => ThrowTimeout<T>());
    }

    private Task TimeoutAfterTask(int ms)
    {
      return Task
        .Delay(ms)
        .ContinueWith(prev => throw new IOException());
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
      if (ReadTimeout>0) {
        return Task.WhenAny(
          TimeoutAfterTask<int>(ReadTimeout),
          baseStream.ReadAsync(buffer, offset, count, cancellationToken)
        ).Unwrap();
      }
      else {
        return baseStream.ReadAsync(buffer, offset, count, cancellationToken);
      }
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
      return baseStream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
      baseStream.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
      try {
        var task = WriteAsync(buffer, offset, count, CancellationToken.None);
        task.Wait();
      }
      catch (AggregateException ex) {
        throw ex.InnerException;
      }
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
      if (WriteTimeout>0) {
        return Task.WhenAny(
          TimeoutAfterTask(WriteTimeout),
          baseStream.WriteAsync(buffer, offset, count, cancellationToken)
        ).Unwrap();
      }
      else {
        return baseStream.WriteAsync(buffer, offset, count, cancellationToken);
      }
    }

  }

}
