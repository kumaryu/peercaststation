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
    public PeerCast PeerCast { get; private set; }
    public SourceStreamFactoryBase(PeerCast peercast)
    {
      this.PeerCast = peercast;
    }

    public abstract string Name { get; }
    public abstract string Scheme { get; }
    public virtual ISourceStream Create(Channel channel, Uri tracker)
    {
      throw new NotImplementedException();
    }

    public virtual ISourceStream Create(Channel channel, Uri source, IContentReader reader)
    {
      throw new NotImplementedException();
    }
  }

  public abstract class SourceStreamBase
    : ISourceStream
  {
    public PeerCast PeerCast { get; private set; }
    public Channel Channel { get; private set; }
    public Uri SourceUri { get; private set; }
    volatile bool isStopped;
    public bool IsStopped { get { return isStopped; } private set { isStopped = value; } }
    public StopReason StoppedReason { get; private set; }
    public event StreamStoppedEventHandler Stopped;
    public bool  HasError { get; protected set; }
    protected QueuedSynchronizationContext SyncContext { get; private set; }
    protected Logger Logger { get; private set; }
    protected StreamConnection connection;

    public float SendRate {
      get { return connection!=null ? connection.SendRate : 0; }
    }
    public float RecvRate {
      get { return connection!=null ? connection.ReceiveRate : 0; }
    }

    public abstract ConnectionInfo GetConnectionInfo();

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
      if (connection!=null) {
        WaitHandle.WaitAny(new WaitHandle[] {
          SyncContext.EventHandle,
          connection.ReceiveWaitHandle,
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
        Stopped(this, new StreamStoppedEventArgs(this.StoppedReason));
      }
    }

    protected virtual void DoProcess()
    {
      OnIdle();
      SyncContext.ProcessAll();
    }

    protected virtual void DoStart()
    {
      try {
        if (mainThread.IsAlive) {
          Stop(StopReason.UserShutdown);
          mainThread.Join();
        }
        if ((mainThread.ThreadState & ThreadState.Unstarted)==0) {
          mainThread = new Thread(MainProc);
          mainThread.Name = this.GetType().Name;
        }
        IsStopped = false;
        mainThread.Start();
      }
      catch (ThreadStateException) {
        throw new InvalidOperationException("Source Streams is already started");
      }
    }

    protected virtual void DoReconnect(Uri source_uri)
    {
    }

    protected virtual void DoStop(StopReason reason)
    {
      StoppedReason = reason;
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

    protected object startLock = new object();
    public void Start()
    {
      lock (startLock) {
        DoStart();
      }
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
          DoReconnect(null);
        });
      }
      else {
        Start();
      }
    }

    public void Reconnect(Uri source_uri)
    {
      if (!IsStopped) {
        PostAction(() => {
          if (source_uri!=null) {
            SourceUri = source_uri;
          }
          DoReconnect(source_uri);
        });
      }
      else {
        if (source_uri!=null) {
          SourceUri = source_uri;
        }
        Start();
      }
    }

    protected virtual void StartConnection(Stream input_stream, Stream output_stream)
    {
      if (connection!=null) connection.Close();
      connection = new StreamConnection(input_stream, output_stream);
      HasError = false;
    }

    protected virtual void EndConnection()
    {
      connection.Close();
    }

    protected void Send(byte[] bytes)
    {
      connection.Send(bytes);
    }

    protected void Send(Atom atom)
    {
      connection.Send(stream => {
        AtomWriter.Write(stream, atom);
      });
    }

    protected bool Recv(Action<Stream> proc)
    {
      try {
        return connection.Recv(proc);
      }
      catch (IOException) {
        OnError();
        return false;
      }
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
