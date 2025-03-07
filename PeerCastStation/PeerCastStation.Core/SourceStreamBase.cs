﻿// PeerCastStation, a P2P streaming servent.
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
    public abstract Uri?             DefaultUri { get; }
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
    public float SendRate { get { return sourceConnection.SendRate; } }
    public float RecvRate { get { return sourceConnection.RecvRate; } }

    protected class ConnectionWrapper
    {
      public ISourceConnection? Connection { get; private set; }
      public Uri? SourceUri { get { return Connection?.SourceUri; } }
      public Task Task { get; private set; }
      public bool IsCompleted { get { return Task.IsCompleted; } }
      public float SendRate { get { return Task.IsCompleted ? 0.0f : Connection?.SendRate ?? 0.0f; } }
      public float RecvRate { get { return Task.IsCompleted ? 0.0f : Connection?.RecvRate ?? 0.0f; } }

      public ConnectionWrapper()
      {
        Connection = null;
        Task = Task.FromResult(StopReason.NotIdentifiedError);
      }

      private ConnectionWrapper(ISourceConnection connection, Func<ConnectionWrapper,Task<StopReason>,Task> taskFunc)
      {
        Connection = connection;
        Task = taskFunc(this, connection.Run());
      }

      public void StopAndWait(StopReason reason)
      {
        if (IsCompleted) return;
        Connection?.Stop(reason);
        Task.Wait();
      }

      public void Post(Host? from, Atom message)
      {
        if (IsCompleted) return;
        Connection?.Post(from, message);
      }

      public ConnectionInfo? GetConnectionInfo()
      {
        if (Connection!=null && !IsCompleted) {
          return Connection.GetConnectionInfo();
        }
        else {
          return null;
        }
      }

      static public ConnectionWrapper Run(ISourceConnection connection, Func<ConnectionWrapper,Task<StopReason>,Task> taskFunc)
      {
        return new ConnectionWrapper(connection, taskFunc);
      }
    }

    protected Logger Logger { get; private set; }
    protected ConnectionWrapper sourceConnection = new ConnectionWrapper();
    private TaskCompletionSource<StopReason> ranTaskSource = new TaskCompletionSource<StopReason>();
    private bool disposed = false;

    protected class ActionQueue
    {
      private Task lastTask = Task.Delay(0);
      private object? lastTaskId = null;
      public bool Aborted { get; private set; } = false;

      public void Abort()
      {
        Aborted = true;
      }

      public Task Queue(object taskId, Action action)
      {
        lock (this) {
          if (Object.Equals(lastTaskId, taskId) && !lastTask.IsCompleted) return lastTask;
          lastTask = lastTask.ContinueWith(prev => {
            if (Aborted) return;
            try {
              action.Invoke();
            }
            catch (Exception ex) {
              OnUnhandledException(ex);
            }
          });
          lastTaskId = taskId;
          return lastTask;
        }
      }

      public Task Queue(object taskId, Func<Task> action)
      {
        lock (this) {
          if (Object.Equals(lastTaskId, taskId) && !lastTask.IsCompleted) return lastTask;
          lastTask = lastTask.ContinueWith(async prev => {
            if (Aborted) return;
            try {
              await action.Invoke();
            }
            catch (Exception ex) {
              OnUnhandledException(ex);
            }
          });
          lastTaskId = taskId;
          return lastTask;
        }
      }
      
      private void OnUnhandledException(Exception ex)
      {
        UnhandledException?.Invoke(this, new UnhandledExceptionEventArgs(ex, false));
      }
      public event UnhandledExceptionEventHandler? UnhandledException;
    }
    private ActionQueue actionQueue;

    protected void Queue(object taskId, Action action)
    {
      actionQueue.Queue(taskId, action);
    }

    protected void QueueAndWait(object taskId, Action action)
    {
      actionQueue.Queue(taskId, action).Wait();
    }

    protected void Queue(object taskId, Func<Task> action)
    {
      actionQueue.Queue(taskId, action);
    }

    public abstract ConnectionInfo GetConnectionInfo();
    protected abstract ISourceConnection CreateConnection(Uri source_uri);

    public class ConnectionStoppedArgs
    {
      public StopReason Reason { get; set; }
      public int Delay { get; set; } = 0;
      public Uri? IgnoreSource { get; set; } = null;
      public bool Reconnect { get; set; } = false;
    }

    protected virtual void OnConnectionStopped(ISourceConnection connection, ConnectionStoppedArgs args)
    {
    }

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
      actionQueue = new ActionQueue();
      actionQueue.UnhandledException += ActionQueue_UnhandledException;
    }

    private void ActionQueue_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
      Logger.Error((Exception)e.ExceptionObject);
    }

    public void Dispose()
    {
      disposed = true;
      QueueAndWait(StopReason.UserShutdown, () => { DoStopStream(StopReason.UserShutdown); });
    }

    protected void StartConnection(Uri source_uri)
    {
      if (!sourceConnection.IsCompleted) throw new InvalidOperationException("Connection is already started");
      sourceConnection = ConnectionWrapper.Run(CreateConnection(source_uri), async (conn,prev) => {
        var args = new ConnectionStoppedArgs();
        try {
          var result = await prev.ConfigureAwait(false);
          args.Reason = result;
          Logger.Debug($"Connection stopped by reason {result}");
        }
        catch (OperationCanceledException) {
          args.Reason = StopReason.UserShutdown;
          Logger.Debug("Connection stopped by canceled");
        }
        catch (Exception e) {
          args.Reason = StopReason.NotIdentifiedError;
          Logger.Debug("Connection stopped by Error");
          Logger.Error(e);
        }
        Queue("CONNECTION_CLEANUP", async () => {
          Logger.Debug($"Cleaning up connection (closed by {args.Reason})");
          OnConnectionStopped(conn.Connection!, args);
          if (sourceConnection==conn) {
            if (args.IgnoreSource!=null) {
              IgnoreSourceHost(args.IgnoreSource);
            }
            if (args.Reconnect) {
              if (args.Delay>0) {
                await Task.Delay(args.Delay).ConfigureAwait(false);
              }
              if (!disposed) {
                DoReconnect();
              }
            }
            else if (args.Reason!=StopReason.UserReconnect) {
              DoStopStream(args.Reason);
            }
          }
        });
      });
    }

    protected void StopConnection(StopReason reason)
    {
      sourceConnection.StopAndWait(reason);
    }

    protected virtual Uri? SelectSourceHost()
    {
      return SourceUri;
    }

    protected virtual void IgnoreSourceHost(Uri source)
    {
    }

    protected virtual void DoStartStream()
    {
      var source = SelectSourceHost();
      if (source==null) {
        DoStopStream(StopReason.NoHost);
        return;
      }
      StartConnection(source);
    }

    protected virtual void DoStopStream(StopReason reason)
    {
      StoppedReason = reason;
      StopConnection(reason);
      ranTaskSource.TrySetResult(reason);
    }

    protected virtual void DoPost(Host? from, Atom message)
    {
      sourceConnection.Post(from, message);
    }

    protected virtual void DoReconnect()
    {
      StopConnection(StopReason.UserReconnect);
      DoStartStream();
    }

    public Task<StopReason> Run()
    {
      if (disposed) throw new ObjectDisposedException(this.GetType().Name);
      ranTaskSource = new TaskCompletionSource<StopReason>();
      Queue("SOURCESTREAM_RUN", () => { DoStartStream(); });
      return ranTaskSource.Task;
    }

    public void Post(Host? from, Atom packet)
    {
      if (disposed) throw new ObjectDisposedException(this.GetType().Name);
      Queue(Tuple.Create(from, packet), () => { DoPost(from, packet); });
    }

    protected void Stop(StopReason reason)
    {
      Queue("SOURCESTREAM_STOP", () => { DoStopStream(reason); });
    }

    public void Reconnect()
    {
      if (disposed) throw new ObjectDisposedException(this.GetType().Name);
      Queue("SOURCESTREAM_RECONNECT", () => { DoReconnect(); });
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
