using System;
using System.Net;
using System.Net.Sockets;
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
    private CancellationTokenSource readCompletedCancelSource = new CancellationTokenSource();
    private CancellationTokenSource writeCompletedCancelSource = new CancellationTokenSource();

    private RingbufferStream readBuffer = new RingbufferStream(64*1024);
    private RingbufferStream writeBuffer = new RingbufferStream(64*1024);
    private Task writeTask;
    private Task readTask;
    private Socket socket;

    public IPEndPoint? LocalEndPoint { get; }
    public IPEndPoint? RemoteEndPoint { get; }
    public CancellationToken ReadCompleted { get { return readCompletedCancelSource.Token; } }
    public CancellationToken WriteCompleted { get { return writeCompletedCancelSource.Token; } }

    private async Task ProcessRead(Stream s)
    {
      readBuffer.WriteTimeout = Timeout.Infinite;
      var buf = new byte[64*1024];
      var ct = closedCancelSource.Token;
      var canceledTask = new TaskCompletionSource<int>();
      try {
        using (ct.Register(() => canceledTask.SetCanceled())) {
          var completed = await Task.WhenAny(s.ReadAsync(buf, 0, buf.Length, ct), canceledTask.Task).ConfigureAwait(false);
          var len = await completed.ConfigureAwait(false);
          while (len>0) {
            await readBuffer.WriteAsync(buf, 0, len, ct).ConfigureAwait(false);
            completed = await Task.WhenAny(s.ReadAsync(buf, 0, buf.Length, ct), canceledTask.Task).ConfigureAwait(false);
            len = await completed.ConfigureAwait(false);
          }
        }
        socket.Shutdown(SocketShutdown.Receive);
      }
      catch (OperationCanceledException) {
        socket.Shutdown(SocketShutdown.Receive);
      }
      catch (IOException) {
        if (!ct.IsCancellationRequested) {
          throw;
        }
      }
      catch (ObjectDisposedException) {
        if (!ct.IsCancellationRequested) {
          throw;
        }
      }
      finally {
        readBuffer.CloseWrite();
        readCompletedCancelSource.Cancel();
      }
    }

    private static readonly TimeSpan WriteBufferReadTimeout = TimeSpan.FromMilliseconds(1000);
    private async Task ProcessWrite(Stream s)
    {
      writeBuffer.ReadTimeout = Timeout.Infinite;
      var buf = new byte[64*1024];
      var ct = CancellationToken.None;
      try {
        async ValueTask<int> ReadFromBuffer(byte[] buf)
        {
          int readCount;
          do {
            readCount = await writeBuffer.ReadAsync(buf, 0, buf.Length, WriteBufferReadTimeout, ct).ConfigureAwait(false);
            if (readCount<0) {
              await s.WriteAsync(Array.Empty<byte>(), 0, 0, ct).ConfigureAwait(false);
            }
          } while (readCount<0);
          return readCount;
        }
        var len = await ReadFromBuffer(buf).ConfigureAwait(false);
        while (len>0) {
          await s.WriteAsync(buf, 0, len, ct).ConfigureAwait(false);
          len = await ReadFromBuffer(buf).ConfigureAwait(false);
        }
        await s.FlushAsync(ct).ConfigureAwait(false);
        socket.Shutdown(SocketShutdown.Send);
      }
      catch (IOException) {
        if (!ct.IsCancellationRequested) {
          throw;
        }
      }
      catch (ObjectDisposedException) {
        if (!ct.IsCancellationRequested) {
          throw;
        }
      }
      finally {
        writeBuffer.CloseRead();
        closedCancelSource.CancelAfter(CloseTimeout);
        writeCompletedCancelSource.Cancel();
      }
    }

    private void CheckReadException()
    {
      if (!readTask.IsFaulted) return;
      if (readTask.Exception==null) return;
      if (readTask.Exception.InnerException==null) {
        throw readTask.Exception;
      }
      else {
        throw readTask.Exception.InnerException;
      }
    }

    private void CheckWriteException()
    {
      if (!writeTask.IsFaulted) return;
      if (writeTask.Exception==null) return;
      if (writeTask.Exception.InnerException==null) {
        throw writeTask.Exception;
      }
      else {
        throw writeTask.Exception.InnerException;
      }
    }


    public Stream  ReadStream { get; }
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

    public Stream  WriteStream { get; }
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

    public int CloseTimeout { get; set; } = 4000;

    public override bool CanTimeout {
      get {
        return ReadStream.CanTimeout || WriteStream.CanTimeout;
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
      Socket socket,
      Stream base_stream,
      byte[]? header)
    {
      this.socket = socket;
      ReadStream  = base_stream;
      WriteStream = base_stream;
      LocalEndPoint = socket.LocalEndPoint as IPEndPoint;
      RemoteEndPoint = socket.RemoteEndPoint as IPEndPoint;
      if (ReadStream.CanTimeout) {
        ReadStream.ReadTimeout = this.ReadTimeout;
      }
      if (WriteStream.CanTimeout) {
        WriteStream.WriteTimeout = this.WriteTimeout;
      }
      if (header!=null && header.Length>0) {
        if (header.Length>readBuffer.Capacity) {
          throw new ArgumentOutOfRangeException(nameof(header));
        }
        readBuffer.Write(header, 0, header.Length);
      }
      readTask = ProcessRead(base_stream);
      writeTask = ProcessWrite(base_stream);
    }

    public ConnectionStream(Socket socket, Stream base_stream)
      : this(socket, base_stream, null)
    {
    }

    public override void Flush()
    {
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
      return Task.FromResult(true);
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
      CheckReadException();
      var len = await readBuffer.ReadAsync(buf, offset, length, cancel_token).ConfigureAwait(false);
      CheckReadException();
      readBytesCounter.Add(len);
      return len;
    }

    public async Task<byte[]> ReadAsync(int length, CancellationToken cancel_token)
    {
      if (closedCancelSource.IsCancellationRequested) throw new ObjectDisposedException(GetType().Name);
      CheckReadException();
      var buf = new byte[length];
      var offset = 0;
      while (offset<length) {
        cancel_token.ThrowIfCancellationRequested();
        var len = await readBuffer.ReadAsync(buf, offset, length-offset, cancel_token).ConfigureAwait(false);
        CheckReadException();
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
      CheckReadException();
      cancel_token.ThrowIfCancellationRequested();
      var buf = new byte[1];
      var len = await readBuffer.ReadAsync(buf, 0, 1, cancel_token).ConfigureAwait(false);
      CheckReadException();
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
      CheckWriteException();
      await writeBuffer.WriteAsync(buf, offset, length, cancel_token).ConfigureAwait(false);
      CheckWriteException();
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
        writeBuffer.CloseWrite();
        readBuffer.CloseRead();
        try {
          Task.WhenAll(writeTask, readTask).Wait();
        }
        catch (AggregateException) {
        }
        closedCancelSource.Cancel();
        if (WriteStream!=null) WriteStream.Close();
        if (ReadStream!=null)  ReadStream.Close();
        closedCancelSource.Dispose();
      }
      base.Dispose(disposing);
    }

    public async Task CloseAsync(CancellationToken cancel_token)
    {
      if (!closedCancelSource.IsCancellationRequested) {
        writeBuffer.CloseWrite();
        readBuffer.CloseRead();
        try {
          await Task.WhenAll(writeTask, readTask).ConfigureAwait(false);
        }
        catch (Exception) {
        }
        closedCancelSource.Cancel();
        if (WriteStream!=null) WriteStream.Close();
        if (ReadStream!=null)  ReadStream.Close();
      }
      Close();
    }

  }

}

