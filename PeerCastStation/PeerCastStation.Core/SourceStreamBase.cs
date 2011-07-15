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
  public abstract class SourceStreamFactoryBase
    : ISourceStreamFactory
  {
    protected PeerCast PeerCast { get; private set; }
    public SourceStreamFactoryBase(PeerCast peercast)
    {
      this.PeerCast = peercast;
    }

    public abstract string Name { get; }
    public abstract ISourceStream Create(Channel channel, Uri tracker);
  }

  public abstract class SourceStreamBase
    : ISourceStream
  {
    public PeerCast PeerCast { get; private set; }
    public Channel Channel { get; private set; }
    public Uri SourceUri { get; private set; }
    volatile bool isStopped;
    public bool IsStopped { get { return isStopped; } private set { isStopped = value; } }
    public event EventHandler Stopped;
    public bool HasError { get; protected set; }
    protected QueuedSynchronizationContext SyncContext { get; private set; }
    protected Logger Logger { get; private set; }

    private Thread mainThread;
    public SourceStreamBase(
      PeerCast peercast,
      Channel channel,
      Uri source_uri)
    {
      this.PeerCast = peercast;
      this.Channel = channel;
      this.SourceUri = source_uri;
      this.IsStopped = false;
      this.mainThread = new Thread(MainProc);
      this.mainThread.Name = this.GetType().Name;
      this.SyncContext = new QueuedSynchronizationContext();
      this.Logger = new Logger(this.GetType());
    }

    protected virtual void MainProc()
    {
      SynchronizationContext.SetSynchronizationContext(this.SyncContext);
      OnStarted();
      while (!IsStopped) {
        WaitEventAny();
        DoProcess();
      }
      OnStopped();
      Cleanup();
    }

    protected virtual void Cleanup()
    {
      EndConnection();
    }

    protected virtual void WaitEventAny()
    {
      SyncContext.EventHandle.WaitOne(10);
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

    protected virtual void DoReconnect()
    {
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

    protected void Stop(StopReason reason)
    {
      if (!IsStopped) {
        PostAction(() => {
          DoStop(reason);
        });
      }
    }

    public void Join()
    {
      if (mainThread!=null && mainThread.IsAlive) {
        mainThread.Join();
      }
    }

    public void Reconnect()
    {
      if (!IsStopped) {
        PostAction(() => {
          DoReconnect();
        });
      }
    }

    Stream inputStream = null;
    Stream outputStream = null;
    protected virtual void StartConnection(Stream input_stream, Stream output_stream)
    {
      if (inputStream!=null || outputStream!=null) {
        EndConnection();
      }
      else {
      }
      inputStream = input_stream;
      outputStream = output_stream;
      HasError = false;
      ProcessRecv();
      ProcessSend();
    }

    protected virtual void EndConnection()
    {
      if (inputStream!=null) {
        if (recvResult!=null && recvResult.IsCompleted) {
          try {
            int bytes = inputStream.EndRead(recvResult);
            if (bytes < 0) {
              OnError();
            }
          }
          catch (ObjectDisposedException) { }
          catch (IOException) {
            OnError();
          }
        }
      }
      if (outputStream!=null) {
        if (sendResult!=null) {
          try {
            outputStream.EndWrite(sendResult);
          }
          catch (ObjectDisposedException) { }
          catch (IOException) {
            OnError();
          }
        }
        if (!HasError && sendStream.Length>0) {
          var buf = sendStream.ToArray();
          try {
            outputStream.Write(buf, 0, buf.Length);
          }
          catch (ObjectDisposedException) { }
          catch (IOException) {
            OnError();
          }
        }
      }
      sendResult = null;
      recvResult = null;
      sendStream.SetLength(0);
      sendStream.Position = 0;
      recvStream.SetLength(0);
      recvStream.Position = 0;
      recvError = false;
      if (outputStream!=null) outputStream.Close();
      if (inputStream!=null) inputStream.Close();
      outputStream = null;
      inputStream = null;
    }

    MemoryStream recvStream = new MemoryStream();
    byte[] recvBuffer = new byte[8192];
    IAsyncResult recvResult = null;
    bool recvError = false;
    protected void ProcessRecv()
    {
      if (inputStream==null) return;
      if (recvResult!=null && recvResult.IsCompleted) {
        try {
          int bytes = inputStream.EndRead(recvResult);
          if (bytes>0) {
            recvStream.Seek(0, SeekOrigin.End);
            recvStream.Write(recvBuffer, 0, bytes);
            recvStream.Seek(0, SeekOrigin.Begin);
          }
          else if (bytes<0) {
            recvError = true;
          }
          else {
            recvError = true;
          }
        }
        catch (ObjectDisposedException) {}
        catch (IOException) {
          recvError = true;
        }
        recvResult = null;
      }
      if (!recvError && !HasError && recvResult==null) {
        try {
          recvResult = inputStream.BeginRead(recvBuffer, 0, recvBuffer.Length, null, null);
        }
        catch (ObjectDisposedException) {
        }
        catch (IOException) {
          recvError = true;
        }
      }
    }

    MemoryStream sendStream = new MemoryStream(8192);
    IAsyncResult sendResult = null;
    protected void ProcessSend()
    {
      if (outputStream==null) return;
      if (sendResult!=null && sendResult.IsCompleted) {
        try {
          outputStream.EndWrite(sendResult);
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
          sendResult = outputStream.BeginWrite(buf, 0, buf.Length, null, null);
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
      if (outputStream==null) throw new InvalidOperationException();
      sendStream.Write(bytes, 0, bytes.Length);
    }

    protected void Send(Atom atom)
    {
      if (outputStream==null) throw new InvalidOperationException();
      AtomWriter.Write(sendStream, atom);
    }

    protected bool Recv(Action<Stream> proc)
    {
      if (inputStream==null) throw new InvalidOperationException();
      bool res = false;
      recvStream.Seek(0, SeekOrigin.Begin);
      try {
        proc(recvStream);
        if (recvStream.Length>recvStream.Position) {
          var new_stream = new MemoryStream((int)Math.Max(8192, recvStream.Length - recvStream.Position));
          new_stream.Write(recvStream.GetBuffer(), (int)recvStream.Position, (int)(recvStream.Length - recvStream.Position));
          new_stream.Position = 0;
          recvStream = new_stream;
          if (recvStream.Length==0 && recvError) {
            OnError();
          }
        }
        else {
          recvStream.Position = 0;
          recvStream.SetLength(0);
        }
        res = true;
      }
      catch (EndOfStreamException) {
        if (recvError) {
          OnError();
        }
      }
      return res;
    }

    private SourceStreamStatus status = SourceStreamStatus.Idle;
    public SourceStreamStatus Status
    {
      get { return status; }
      protected set {
        if (status!=value) {
          status = value;
          if (StatusChanged!=null) StatusChanged(this, new SourceStreamStatusChangedEventArgs(status));
        }
      }
    }
    public event EventHandler<SourceStreamStatusChangedEventArgs> StatusChanged;
  }
}
