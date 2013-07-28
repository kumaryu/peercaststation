using System;
using System.Collections.Generic;
using System.Threading;

namespace PeerCastStation.Core
{
  public abstract class SourceConnectionBase
  {
    public PeerCast   PeerCast { get; private set; }
    public Channel    Channel { get; private set; }
    public Uri        SourceUri { get; private set; }
    public StopReason StoppedReason { get; private set; }
    public bool       IsStopped { get { return StoppedReason!=StopReason.None; } }
    public float      SendRate { get { return connection!=null ? connection.SendRate    : 0; } }
    public float      RecvRate { get { return connection!=null ? connection.ReceiveRate : 0; } }

    public event StreamStoppedEventHandler Stopped;

    protected QueuedSynchronizationContext SyncContext { get; private set; }
    protected Logger Logger { get; private set; }
    protected StreamConnection connection;

    public abstract ConnectionInfo      GetConnectionInfo();
    protected abstract void             DoProcess();
    protected abstract StreamConnection DoConnect(Uri source);
    protected abstract void             DoClose(StreamConnection connection);
    protected abstract void             DoPost(Host from, Atom packet);

    public SourceConnectionBase(
      PeerCast peercast,
      Channel channel,
      Uri source_uri)
    {
      this.PeerCast      = peercast;
      this.Channel       = channel;
      this.SourceUri     = source_uri;
      this.StoppedReason = StopReason.None;
      this.SyncContext   = new QueuedSynchronizationContext();
      this.Logger        = new Logger(this.GetType());
    }

    public virtual void Run()
    {
      SynchronizationContext.SetSynchronizationContext(this.SyncContext);
      OnStarted();
      while (!IsStopped) {
        WaitEventAny();
        DoProcess();
        SyncContext.ProcessAll();
      }
      OnStopped();
    }

    protected virtual void WaitEventAny()
    {
      WaitHandle.WaitAny(new WaitHandle[] {
        SyncContext.EventHandle,
        connection.ReceiveWaitHandle,
      }, 10);
    }

    protected virtual void OnStarted()
    {
      connection = DoConnect(SourceUri);
      if (connection==null) {
        DoStop(StopReason.ConnectionError);
      }
    }

    protected virtual void OnStopped()
    {
      if (Stopped!=null) {
        Stopped(this, new StreamStoppedEventArgs(this.StoppedReason));
      }
      if (connection!=null) {
        DoClose(connection);
      }
    }

    protected virtual void DoStop(StopReason reason)
    {
      if (reason==StopReason.None) throw new ArgumentException("Invalid value", "reason");
      if (!IsStopped) {
        StoppedReason = reason;
      }
    }

    public void Post(Host from, Atom packet)
    {
      if (!IsStopped) {
        SyncContext.Post(dummy => {
          DoPost(from, packet);
        }, null);
      }
    }

    public void Stop()
    {
      Stop(StopReason.UserShutdown);
    }

    public void Stop(StopReason reason)
    {
      if (!IsStopped) {
        SyncContext.Post(dummy => {
          DoStop(reason);
        }, null);
      }
    }
  }
}
