using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.Core
{
  public class StreamConnection2
    : IDisposable
  {
    public static readonly int RecvWindowSize = 64*1024;
    public static readonly int SendWindowSize = 16*1024;
    private RateCounter  recvBytesCounter = new RateCounter(1000);
    private int          recvTimeout      = Timeout.Infinite;
    private RateCounter  sendBytesCounter = new RateCounter(1000);
    private int          sendTimeout      = Timeout.Infinite;
    private Stream       inputStream      = null;
    private Stream       outputStream     = null;
    private MemoryStream headerStream     = new MemoryStream();

    public Stream  InputStream    { get { return inputStream; } }
    public int     ReceiveTimeout { get { return recvTimeout; } set { recvTimeout = value; } }
    public float   ReceiveRate    { get { return recvBytesCounter.Rate; } }
    public Stream  OutputStream   { get { return outputStream; } }
    public int     SendTimeout    { get { return sendTimeout; } set { sendTimeout = value; } }
    public float   SendRate       { get { return sendBytesCounter.Rate; } }

    class SendState
    {
      public int  Length { get; private set; }
      public long Id     { get; private set; }
      private static long serialNumber = 0;
      public SendState(int len)
      {
        this.Length = len;
        this.Id = serialNumber++;
      }
    }

    public StreamConnection2(Stream input_stream, Stream output_stream)
      : this(input_stream, output_stream, null)
    {
    }

    public StreamConnection2(Stream input_stream, Stream output_stream, byte[] header)
    {
      inputStream  = input_stream;
      outputStream = output_stream;
      if (header!=null && header.Length>0) {
        headerStream.Write(header, 0, header.Length);
      }
    }

    public void Dispose()
    {
      Close();
    }

    private CancellationTokenSource closedCancelSource = new CancellationTokenSource();

    private Task lastRecvTask = Task.Delay(0);
    public Task<int> RecvAsync(byte[] buf, int offset, int length, CancellationToken cancel_token)
    {
      var task = lastRecvTask.ContinueWith(async prev => {
        var timeout_cancelsource = new CancellationTokenSource(SendTimeout);
        var timeout_token = timeout_cancelsource.Token;
        timeout_token.Register(() => Close());
        var cancelsource = CancellationTokenSource.CreateLinkedTokenSource(
          closedCancelSource.Token,
          timeout_token,
          cancel_token);
        int len = 0;
        if (headerStream.Position<headerStream.Length) {
          len = headerStream.Read(buf, offset, length);
        }
        if (length-len>0) {
          len += await inputStream.ReadAsync(buf, offset+len, length-len, cancelsource.Token);
        }
        recvBytesCounter.Add(len);
        return len;
      }).Unwrap();
      lastRecvTask = task;
      return task;
    }

    public Task<int> RecvAsync(byte[] buf, int offset, int length)
    {
      return RecvAsync(buf, offset, length, CancellationToken.None);
    }

    public Task<byte[]> RecvAsync(int length, CancellationToken cancel_token)
    {
      var buf = new byte[length];
      var task = lastRecvTask.ContinueWith(async (prev) => {
        var timeout_cancelsource = new CancellationTokenSource(SendTimeout);
        var timeout_token = timeout_cancelsource.Token;
        timeout_token.Register(() => Close());
        var cancelsource = CancellationTokenSource.CreateLinkedTokenSource(
          closedCancelSource.Token,
          timeout_token,
          cancel_token);
        var offset = 0;
        while (offset<length) {
          cancelsource.Token.ThrowIfCancellationRequested();
          int len = 0;
          if (headerStream.Position<headerStream.Length) {
            len = headerStream.Read(buf, offset, length-offset);
          }
          else {
            len = await inputStream.ReadAsync(buf, offset, length-offset, cancelsource.Token);
          }
          if (len==0) throw new IOException();
          else {
            offset += len;
            recvBytesCounter.Add(len);
          }
        }
        return buf;
      }).Unwrap();
      lastRecvTask = task;
      return task;
    }

    public Task<byte[]> RecvAsync(int length)
    {
      return RecvAsync(length, CancellationToken.None);
    }

    private Task lastSendTask = Task.Delay(0);

    public Task SendAsync(byte[] buf, int offset, int length, CancellationToken cancel_token)
    {
      var task = lastSendTask.ContinueWith(async prev => {
        var timeout_cancelsource = new CancellationTokenSource(SendTimeout);
        var timeout_token = timeout_cancelsource.Token;
        timeout_token.Register(() => Close());
        var cancelsource = CancellationTokenSource.CreateLinkedTokenSource(
          closedCancelSource.Token,
          timeout_token,
          cancel_token);
        await outputStream.WriteAsync(buf, offset, length, cancelsource.Token);
        if (!cancelsource.IsCancellationRequested) {
          sendBytesCounter.Add(length);
        }
      }, closedCancelSource.Token).Unwrap();
      lastSendTask = task;
      return task;
    }

    public Task SendAsync(byte[] buf, int offset, int length)
    {
      return SendAsync(buf, offset, length, CancellationToken.None);
    }

    public Task SendAsync(byte[] bytes, CancellationToken cancel_token)
    {
      return SendAsync(bytes, 0, bytes.Length, cancel_token);
    }

    public Task SendAsync(byte[] bytes)
    {
      return SendAsync(bytes, 0, bytes.Length, CancellationToken.None);
    }

    private void RethrowExceptions()
    {
      if (lastSendTask.IsCompleted && lastSendTask.IsFaulted) {
        throw new IOException("送信エラーが発生しました", lastSendTask.Exception);
      }
      if (lastRecvTask.IsCompleted && lastRecvTask.IsFaulted) {
        throw new IOException("受信エラーが発生しました", lastRecvTask.Exception);
      }
    }

    public void CheckErrors()
    {
      RethrowExceptions();
    }

    public void Close()
    {
      lastRecvTask.Wait(ReceiveTimeout);
      lastSendTask.Wait(SendTimeout);
      closedCancelSource.Cancel();
      if (outputStream!=null) outputStream.Close();
      if (inputStream!=null)  inputStream.Close();
    }

  }
}
