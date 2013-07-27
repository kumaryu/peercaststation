using System;
using System.IO;
using System.Threading;

namespace PeerCastStation.Core
{
  public class StreamConnection
    : IDisposable
  {
    private ManualResetEvent recvEvent        = new ManualResetEvent(false);
    private RateCounter      recvBytesCounter = new RateCounter(1000);
    private int              recvTimeout      = Timeout.Infinite;
    private MemoryStream     recvStream       = new MemoryStream();
    private byte[]           recvBuffer       = new byte[64*1024];
    private IAsyncResult     recvResult       = null;
    private Exception        recvException    = null;
    private object           recvLock         = new Object();
    private RateCounter      sendBytesCounter = new RateCounter(1000);
    private int              sendTimeout      = Timeout.Infinite;
    private MemoryStream     sendStream       = new MemoryStream(8192);
    private IAsyncResult     sendResult       = null;
    private Exception        sendException    = null;
    private object           sendLock         = new Object();
    private bool             closing          = false;
    private Stream           inputStream      = null;
    private Stream           outputStream     = null;

    public Stream     InputStream       { get { return inputStream; } }
    public WaitHandle ReceiveWaitHandle { get { return recvEvent; } }
    public int        ReceiveTimeout    { get { return recvTimeout; } set { recvTimeout = value; } }
    public float      ReceiveRate       { get { return recvBytesCounter.Rate; } }
    public Exception  ReceiveError      { get { return recvException; } }
    public Stream     OutputStream      { get { return outputStream; } }
    public int        SendTimeout       { get { return sendTimeout; } set { sendTimeout = value; } }
    public float      SendRate          { get { return sendBytesCounter.Rate; } }
    public Exception  SendError         { get { return sendException; } }
    public bool       IsDisposed        { get { return closing; } }

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

    public StreamConnection(Stream input_stream, Stream output_stream)
    {
      inputStream  = input_stream;
      outputStream = output_stream;
      sendStream.SetLength(0);
      sendStream.Position = 0;
      recvStream.SetLength(0);
      recvStream.Position = 0;
      recvException = null;
      sendException = null;
      StartReceive();
    }

    public void Dispose()
    {
      Close();
    }

    private void OnReceive(IAsyncResult ar)
    {
      lock (recvLock) {
        try {
          int bytes = inputStream.EndRead(ar);
          if (bytes>0) {
            recvBytesCounter.Add(bytes);
            recvStream.Seek(0, SeekOrigin.End);
            recvStream.Write(recvBuffer, 0, bytes);
            recvStream.Seek(0, SeekOrigin.Begin);
          }
          else {
            if (!closing) recvException = new EndOfStreamException();
          }
        }
        catch (ObjectDisposedException e) {
          if (!closing) recvException = e;
        }
        catch (IOException e) {
          if (!closing) recvException = e;
        }
        recvResult = null;
        recvEvent.Set();
        if (recvException==null && !closing) {
          try {
            recvResult = inputStream.BeginRead(recvBuffer, 0, recvBuffer.Length, OnReceive, null);
            if (recvTimeout>=0) {
              ThreadPool.RegisterWaitForSingleObject(recvResult.AsyncWaitHandle, OnReceiveTimeout, recvResult, recvTimeout, true);
            }
          }
          catch (ObjectDisposedException e) {
            recvException = e;
            recvEvent.Set();
          }
          catch (IOException e) {
            recvException = e;
            recvEvent.Set();
          }
        }
      }
    }

    private void OnReceiveTimeout(object ar, bool timedout)
    {
      lock (recvLock) {
        if (!timedout || closing) return;
        if (((IAsyncResult)ar).IsCompleted) return;
        recvException = new TimeoutException();
        recvEvent.Set();
      }
    }

    private void OnSend(IAsyncResult ar)
    {
      lock (sendLock) {
        bool err = false;
        try {
          outputStream.EndWrite(ar);
          sendBytesCounter.Add(((SendState)ar.AsyncState).Length);
        }
        catch (ObjectDisposedException e) {
          err = true;
          if (!closing) sendException = e;
        }
        catch (IOException e) {
          err = true;
          if (!closing) sendException = e;
        }
        sendResult = null;
        if (sendException==null && !err && sendStream.Length>0) {
          var buf = sendStream.ToArray();
          sendStream.SetLength(0);
          sendStream.Position = 0;
          try {
            var state = new SendState(buf.Length);
            sendResult = outputStream.BeginWrite(buf, 0, buf.Length, null, state);
            ThreadPool.RegisterWaitForSingleObject(sendResult.AsyncWaitHandle, OnSendTimeout, sendResult, sendTimeout, true);
          }
          catch (ObjectDisposedException e) {
            if (!closing) sendException = e;
          }
          catch (IOException e) {
            if (!closing) sendException = e;
          }
        }
      }
    }

    private void OnSendTimeout(object ar, bool timedout)
    {
      lock (sendLock) {
        if (timedout && !((IAsyncResult)ar).IsCompleted) {
          if (closing) return;
          sendException = new TimeoutException();
        }
        else {
          OnSend((IAsyncResult)ar);
        }
      }
    }

    public void Send(byte[] bytes)
    {
      if (outputStream==null) throw new InvalidOperationException();
      RethrowExceptions();
      lock (sendLock) {
        sendStream.Write(bytes, 0, bytes.Length);
      }
      StartSend();
    }

    public void Send(Action<Stream> proc)
    {
      if (outputStream==null) throw new InvalidOperationException();
      RethrowExceptions();
      lock (sendLock) {
        proc(sendStream);
      }
      StartSend();
    }

    public bool Recv(Action<Stream> proc)
    {
      lock (recvLock) {
        if (inputStream==null) throw new InvalidOperationException();
        bool res = false;
        recvStream.Seek(0, SeekOrigin.Begin);
        try {
          if (recvStream.Length==0) throw new EndOfStreamException();
          proc(recvStream);
          if (recvStream.Length>recvStream.Position) {
            var new_stream = new MemoryStream((int)Math.Max(8192, recvStream.Length - recvStream.Position));
            new_stream.Write(recvStream.GetBuffer(), (int)recvStream.Position, (int)(recvStream.Length - recvStream.Position));
            new_stream.Position = 0;
            recvStream = new_stream;
          }
          else {
            recvStream.Position = 0;
            recvStream.SetLength(0);
            recvEvent.Reset();
          }
          res = true;
          if (recvException!=null) recvEvent.Set();
        }
        catch (EndOfStreamException) {
          recvEvent.Reset();
          RethrowExceptions();
        }
        return res;
      }
    }

    private void RethrowExceptions()
    {
      lock (sendLock) {
        if (sendException!=null) throw new IOException("送信エラーが発生しました", sendException);
      }
      lock (recvLock) {
        if (recvException!=null) throw new IOException("受信エラーが発生しました", recvException);
      }
    }

    private void StartReceive()
    {
      if (inputStream==null) return;
      lock (recvLock) {
        if (recvException!=null || closing) return;
        try {
          recvResult = inputStream.BeginRead(recvBuffer, 0, recvBuffer.Length, OnReceive, null);
          if (recvTimeout>=0) {
            ThreadPool.RegisterWaitForSingleObject(recvResult.AsyncWaitHandle, OnReceiveTimeout, recvResult, recvTimeout, true);
          }
        }
        catch (ObjectDisposedException e) {
          recvException = e;
          recvEvent.Set();
        }
        catch (IOException e) {
          recvException = e;
          recvEvent.Set();
        }
      }
    }

    private void StartSend()
    {
      if (outputStream==null) return;
      lock (sendLock) {
        if (sendResult!=null || sendException!=null || closing || sendStream.Length==0) return;
        var buf = sendStream.ToArray();
        sendStream.SetLength(0);
        sendStream.Position = 0;
        try {
          var state = new SendState(buf.Length);
          sendResult = outputStream.BeginWrite(buf, 0, buf.Length, null, state);
          ThreadPool.RegisterWaitForSingleObject(sendResult.AsyncWaitHandle, OnSendTimeout, sendResult, sendTimeout, true);
        }
        catch (ObjectDisposedException e) {
          sendException = e;
        }
        catch (IOException e) {
          sendException = e;
        }
      }
    }

    private void CleanupRecv()
    {
    }

    private void CleanupSend()
    {
      if (outputStream==null) return;
      IAsyncResult sending_result;
      do {
        lock (sendLock) {
          sending_result = sendResult;
        }
        if (sending_result!=null) {
          if (!sending_result.AsyncWaitHandle.WaitOne(sendTimeout)) {
            break;
          }
        }
      } while (sending_result!=null);
    }

    public void Close()
    {
      closing = true;
      CleanupRecv();
      CleanupSend();
      if (outputStream!=null) outputStream.Close();
      if (inputStream!=null)  inputStream.Close();
    }

  }
}
