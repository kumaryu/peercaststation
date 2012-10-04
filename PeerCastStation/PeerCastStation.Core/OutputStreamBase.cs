// PeerCastStation, a P2P streaming servent.
// Copyright (C) 2011 Ryuichi Sakamoto (kumaryu@kumaryu.net)
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
using System;
using System.IO;
using System.Net;
using System.Threading;

namespace PeerCastStation.Core
{
  public abstract class OutputStreamFactoryBase
    : MarshalByRefObject,
      IOutputStreamFactory
  {
    protected PeerCast PeerCast { get; private set; }
    public OutputStreamFactoryBase(PeerCast peercast)
    {
      this.PeerCast = peercast;
    }

    public abstract string Name { get; }
    public abstract OutputStreamType OutputStreamType { get; }
    public virtual int Priority { get { return 0; } }
    public abstract IOutputStream Create(Stream input_stream, Stream output_stream, EndPoint remote_endpoint, Guid channel_id, byte[] header);
    public abstract Guid? ParseChannelID(byte[] header);
  }

  public abstract class OutputStreamBase
    : MarshalByRefObject,
      IOutputStream
  {
    public PeerCast PeerCast { get; private set; }
    public Stream InputStream { get; private set; }
    public Stream OutputStream { get; private set; }
    public EndPoint RemoteEndPoint { get; private set; }
    public Channel Channel { get; private set; }
    public bool IsLocal { get; private set; }
    public int UpstreamRate
    {
      get {
        if (IsLocal || Channel==null) {
          return 0;
        }
        else {
          return GetUpstreamRate();
        }
      }
    }
    volatile bool isStopped;
    public bool IsStopped { get { return isStopped; } private set { isStopped = value; } }
    public event EventHandler Stopped;
    public bool HasError { get; private set; }
    public float SendRate { get { return sendBytesCounter.Rate; } }
    public float RecvRate { get { return recvBytesCounter.Rate; } }
    protected QueuedSynchronizationContext SyncContext { get; private set; }
    protected Logger Logger { get; private set; }

    private Thread mainThread;
    public OutputStreamBase(
      PeerCast peercast,
      Stream input_stream,
      Stream output_stream,
      EndPoint remote_endpoint,
      Channel channel,
      byte[] header)
    {
      this.PeerCast = peercast;
      this.InputStream = input_stream;
      this.OutputStream = output_stream;
      this.RemoteEndPoint = remote_endpoint;
      this.Channel = channel;
      var ip = remote_endpoint as IPEndPoint;
      this.IsLocal = ip!=null ? Utils.IsSiteLocal(ip.Address) : true;
      this.IsStopped = false;
      this.mainThread = new Thread(MainProc);
      this.mainThread.Name = this.GetType().Name;
      this.SyncContext = new QueuedSynchronizationContext();
      this.Logger = new Logger(this.GetType());
      if (header!=null) {
        this.recvStream.Write(header, 0, header.Length);
      }
    }

    protected virtual int GetUpstreamRate()
    {
      return 0;
    }

    protected virtual void MainProc()
    {
      SynchronizationContext.SetSynchronizationContext(this.SyncContext);
      OnStarted();
      while (!IsStopped) {
        WaitEventAny();
        DoProcess();
      }
      Cleanup();
      OnStopped();
    }

    protected virtual void Cleanup()
    {
      if (recvResult!=null && recvResult.IsCompleted) {
        try {
          int bytes = InputStream.EndRead(recvResult);
          if (bytes < 0) {
            OnError();
          }
          else {
            recvBytesCounter.Add(bytes);
          }
        }
        catch (ObjectDisposedException) {}
        catch (IOException) {
          OnError();
        }
      }
      if (sendResult!=null) {
        try {
          OutputStream.EndWrite(sendResult);
          sendBytesCounter.Add((int)sendResult.AsyncState);
        }
        catch (ObjectDisposedException) {}
        catch (IOException) {
          OnError();
        }
      }
      if (!HasError && sendStream.Length>0) {
        var buf = sendStream.ToArray();
        try {
          OutputStream.Write(buf, 0, buf.Length);
        }
        catch (ObjectDisposedException) {}
        catch (IOException) {
          OnError();
        }
      }
      recvResult = null;
      sendResult = null;
      sendStream.SetLength(0);
      sendStream.Position = 0;
      recvStream.SetLength(0);
      recvStream.Position = 0;
      this.InputStream.Close();
      this.OutputStream.Close();
    }

    protected virtual void WaitEventAny()
    {
      if (recvResult!=null && sendResult!=null) {
        WaitHandle.WaitAny(new WaitHandle[] {
          recvResult.AsyncWaitHandle,
          sendResult.AsyncWaitHandle,
          SyncContext.EventHandle
        }, 10);
      }
      else if (recvResult!=null) {
        WaitHandle.WaitAny(new WaitHandle[] {
          recvResult.AsyncWaitHandle,
          SyncContext.EventHandle
        }, 10);
      }
      else if (sendResult!=null) {
        WaitHandle.WaitAny(new WaitHandle[] {
          sendResult.AsyncWaitHandle,
          SyncContext.EventHandle
        }, 10);
      }
      else {
        SyncContext.EventHandle.WaitOne(10);
      }
    }

    protected virtual void OnStarted()
    {
    }

    protected virtual void OnStopped()
    {
      if (Stopped!=null) {
        Stopped(this, new EventArgs());
      }
    }

    protected virtual void DoProcess()
    {
      ProcessRecv();
      OnIdle();
      ProcessSend();
      SyncContext.ProcessAll();
    }

    protected virtual void DoStart()
    {
      try {
        if ((mainThread.ThreadState & (ThreadState.Stopped | ThreadState.Unstarted))!=0) {
          IsStopped = false;
          mainThread.Start();
        }
        else {
          throw new InvalidOperationException("Output Streams is already started");
        }
      }
      catch (ThreadStateException) {
        throw new InvalidOperationException("Output Streams is already started");
      }
    }

    protected enum StopReason
    {
      None,
      Any,
      UserShutdown,
      OffAir,
      ConnectionError,
      NotIdentifiedError,
      BadAgentError,
      UnavailableError,
    }

    protected virtual void DoStop(StopReason reason)
    {
      IsStopped = true;
    }

    protected virtual void DoPost(Host from, Atom packet)
    {
    }

    protected virtual void PostAction(Action proc)
    {
      SyncContext.Post(dummy => { proc(); }, null);
    }

    protected virtual void OnIdle()
    {
    }

    protected virtual void OnError()
    {
      HasError = true;
      Stop(StopReason.ConnectionError);
    }

    public void Start()
    {
      DoStart();
    }

    public void Post(Host from, Atom packet)
    {
      if (!IsStopped) {
        PostAction(() => {
          DoPost(from, packet);
        });
      }
    }

    public void Stop()
    {
      if (!IsStopped) {
        PostAction(() => {
          DoStop(StopReason.UserShutdown);
        });
      }
    }

    public void Join()
    {
      if (mainThread!=null && mainThread.IsAlive) {
        mainThread.Join();
      }
    }

    protected void Stop(StopReason reason)
    {
      if (!IsStopped) {
        PostAction(() => {
          DoStop(reason);
        });
      }
    }

    RateCounter recvBytesCounter = new RateCounter(1000);
    MemoryStream recvStream = new MemoryStream();
    byte[] recvBuffer = new byte[8192];
    IAsyncResult recvResult = null;
    private void ProcessRecv()
    {
      if (recvResult!=null && recvResult.IsCompleted) {
        try {
          int bytes = InputStream.EndRead(recvResult);
          if (bytes>0) {
            recvBytesCounter.Add(bytes);
            recvStream.Seek(0, SeekOrigin.End);
            recvStream.Write(recvBuffer, 0, bytes);
            recvStream.Seek(0, SeekOrigin.Begin);
          }
          else if (bytes<0) {
            OnError();
          }
        }
        catch (ObjectDisposedException) {}
        catch (IOException) {
          OnError();
        }
        recvResult = null;
      }
      if (!HasError && recvResult==null) {
        try {
          recvResult = InputStream.BeginRead(recvBuffer, 0, recvBuffer.Length, null, null);
        }
        catch (ObjectDisposedException) {
        }
        catch (IOException) {
          OnError();
        }
      }
    }

    private RateCounter sendBytesCounter = new RateCounter(1000);
    MemoryStream sendStream = new MemoryStream(8192);
    IAsyncResult sendResult = null;
    private void ProcessSend()
    {
      if (sendResult!=null && sendResult.IsCompleted) {
        try {
          OutputStream.EndWrite(sendResult);
          sendBytesCounter.Add((int)sendResult.AsyncState);
        }
        catch (ObjectDisposedException) {
        }
        catch (IOException) {
          OnError();
        }
        sendResult = null;
      }
      if (!HasError && sendResult==null && sendStream.Length>0) {
        var buf = sendStream.ToArray();
        sendStream.SetLength(0);
        sendStream.Position = 0;
        try {
          sendResult = OutputStream.BeginWrite(buf, 0, buf.Length, null, buf.Length);
        }
        catch (ObjectDisposedException) {
        }
        catch (IOException) {
          OnError();
        }
      }
    }

    protected void Send(byte[] bytes)
    {
      sendStream.Write(bytes, 0, bytes.Length);
    }

    protected void Send(Atom atom)
    {
      AtomWriter.Write(sendStream, atom);
    }

    protected Atom RecvAtom()
    {
      Atom res = null;
      if (recvStream.Length>=8 && Recv(s => { res = AtomReader.Read(s); })) {
        return res;
      }
      else {
        return null;
      }
    }

    protected bool Recv(Action<Stream> proc)
    {
      bool res = false;
      recvStream.Seek(0, SeekOrigin.Begin);
      try {
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
        }
        res = true;
      }
      catch (EndOfStreamException) {
      }
      return res;
    }

    public abstract OutputStreamType OutputStreamType { get; }
  }
}
