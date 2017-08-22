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
using System.Threading.Tasks;

namespace PeerCastStation.Core
{
  public class BindErrorException
    : ApplicationException
  {
    public BindErrorException(string message)
      : base(message)
    {
    }
  }

  public abstract class SourceStreamFactoryBase
    : ISourceStreamFactory
  {
    public PeerCast PeerCast { get; private set; }
    public SourceStreamFactoryBase(PeerCast peercast)
    {
      this.PeerCast = peercast;
    }

    public abstract string           Name { get; }
    public abstract string           Scheme { get; }
    public abstract SourceStreamType Type { get; }
    public abstract Uri              DefaultUri { get; }
    public abstract bool             IsContentReaderRequired { get; }
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
    public Channel  Channel { get; private set; }
    public Uri      SourceUri { get; private set; }
    public StopReason StoppedReason { get; private set; }
    public bool       IsStopped { get { return StoppedReason!=StopReason.None; } }
    public float SendRate { get { return sourceConnection!=null ? sourceConnection.SendRate : 0.0f; } }
    public float RecvRate { get { return sourceConnection!=null ? sourceConnection.RecvRate : 0.0f; } }

    protected Logger Logger { get; private set; }
    protected ISourceConnection sourceConnection;
    protected Task              sourceConnectionTask;
    private Task eventTask = Task.Delay(0);
    private TaskCompletionSource<StopReason> ranTaskSource;
    private bool disposed = false;

    protected Task Queue(Action action)
    {
      eventTask = eventTask.ContinueWith(prev => {
        if (prev.IsFaulted) return;
        action.Invoke();
      });
      return eventTask;
    }

    public abstract ConnectionInfo GetConnectionInfo();
    protected abstract ISourceConnection CreateConnection(Uri source_uri);
    protected abstract void OnConnectionStopped(ISourceConnection connection, StopReason reason);

    public SourceStreamBase(
      PeerCast peercast,
      Channel  channel,
      Uri      source_uri)
    {
      this.PeerCast  = peercast;
      this.Channel   = channel;
      this.SourceUri = source_uri;
      this.StoppedReason = StopReason.None;
      this.Logger = new Logger(this.GetType(), source_uri.ToString());
    }

    public void Dispose()
    {
      disposed = true;
      Queue(() => { DoStop(StopReason.UserShutdown); }).Wait();
    }

    protected void StartConnection(Uri source_uri)
    {
      if (sourceConnection!=null) {
        StopConnection(StopReason.UserReconnect);
      }
      var conn = CreateConnection(source_uri);
      sourceConnection = conn;
      sourceConnectionTask = sourceConnection
        .Run()
        .ContinueWith(prev => {
          if (prev.IsFaulted) {
            Queue(() => { OnConnectionStopped(conn, StopReason.NotIdentifiedError); });
          }
          else if (prev.IsCanceled) {
            Queue(() => { OnConnectionStopped(conn, StopReason.UserShutdown); });
          }
          else {
            Queue(() => { OnConnectionStopped(conn, prev.Result); });
          }
        });
    }

    protected void StopConnection(StopReason reason)
    {
      if (sourceConnection==null) return;
      sourceConnection.Stop(reason);
      sourceConnectionTask.Wait();
    }

    protected virtual void DoStart()
    {
      if (this.SourceUri==null) {
        Stop(StopReason.NoHost);
        return;
      }
      StartConnection(this.SourceUri);
    }

    protected virtual void DoStop(StopReason reason)
    {
      StoppedReason = reason;
      StopConnection(reason);
      ranTaskSource.TrySetResult(reason);
    }

    protected virtual void DoPost(Host from, Atom message)
    {
      if (sourceConnection==null || sourceConnection.IsStopped) return;
      sourceConnection.Post(from, message);
    }

    protected virtual void DoReconnect()
    {
      if (sourceConnection!=null) {
        StopConnection(StopReason.UserReconnect);
      }
      DoStart();
    }

    public Task<StopReason> Run()
    {
      if (disposed) throw new ObjectDisposedException(this.GetType().Name);
      ranTaskSource = new TaskCompletionSource<StopReason>();
      Queue(() => { DoStart(); });
      return ranTaskSource.Task;
    }

    public void Post(Host from, Atom packet)
    {
      if (disposed) throw new ObjectDisposedException(this.GetType().Name);
      Queue(() => { DoPost(from, packet); });
    }

    protected void Stop(StopReason reason)
    {
      Queue(() => { DoStop(reason); });
    }

    public void Reconnect()
    {
      if (disposed) throw new ObjectDisposedException(this.GetType().Name);
      Queue(() => { DoReconnect(); });
    }

    public abstract SourceStreamType Type { get; }

    public SourceStreamStatus Status
    {
      get {
        switch (GetConnectionInfo().Status) {
        case ConnectionStatus.Connected:  return SourceStreamStatus.Receiving;
        case ConnectionStatus.Connecting: return SourceStreamStatus.Searching;
        case ConnectionStatus.Error:      return SourceStreamStatus.Error;
        case ConnectionStatus.Idle:       return SourceStreamStatus.Idle;
        }
        return SourceStreamStatus.Idle;
      }
    }
  }

}
