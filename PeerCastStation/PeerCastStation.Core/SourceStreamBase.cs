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

    public abstract string           Name { get; }
    public abstract string           Scheme { get; }
    public abstract SourceStreamType Type { get; }
    public abstract Uri              DefaultUri { get; }
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
    public event StreamStoppedEventHandler Stopped;
    public float SendRate { get { return sourceConnection!=null ? sourceConnection.SendRate : 0.0f; } }
    public float RecvRate { get { return sourceConnection!=null ? sourceConnection.RecvRate : 0.0f; } }

    protected enum SourceStreamEventType {
      None,
      Start,
      Stop,
      Post,
      Reconnect,
      ConnectionStopped,
    }
    protected class SourceStreamEvent 
    {
      public SourceStreamEventType Type { get; private set; }
      public SourceStreamEvent(SourceStreamEventType type)
      {
        this.Type = type;
      }
    }

    protected class StartEvent
      : SourceStreamEvent
    {
      public Uri SourceUri { get; private set; }
      public StartEvent(Uri source_uri)
        : base(SourceStreamEventType.Start)
      {
        this.SourceUri = source_uri;
      }
    }

    protected class StopEvent
      : SourceStreamEvent
    {
      public StopReason StopReason { get; private set; }
      public StopEvent(StopReason reason)
        : base(SourceStreamEventType.Stop)
      {
        this.StopReason = reason;
      }
    }

    protected class PostEvent
      : SourceStreamEvent
    {
      public Host From    { get; private set; }
      public Atom Message { get; private set; }
      public PostEvent(Host from, Atom message)
        : base(SourceStreamEventType.Post)
      {
        this.From    = from;
        this.Message = message;
      }
    }

    protected class ReconnectEvent
      : SourceStreamEvent
    {
      public ReconnectEvent()
        : base(SourceStreamEventType.Reconnect)
      {
      }
    }

    protected class ConnectionStoppedEvent
      : SourceStreamEvent
    {
      public SourceConnectionBase Connection { get; private set; }
      public StopReason           StopReason { get; private set; }
      public ConnectionStoppedEvent(SourceConnectionBase connection, StopReason reason)
        : base(SourceStreamEventType.ConnectionStopped)
      {
        this.Connection = connection;
        this.StopReason = reason;
      }
    }

    protected EventQueue<SourceStreamEvent> EventQueue { get; private set; }

    protected Logger Logger { get; private set; }
    protected SourceConnectionBase sourceConnection;
    protected Thread               sourceConnectionThread;

    public abstract ConnectionInfo GetConnectionInfo();
    protected abstract SourceConnectionBase CreateConnection(Uri source_uri);
    protected abstract void OnConnectionStopped(ConnectionStoppedEvent msg);

    public SourceStreamBase(
      PeerCast peercast,
      Channel  channel,
      Uri      source_uri)
    {
      this.PeerCast  = peercast;
      this.Channel   = channel;
      this.SourceUri = source_uri;
      this.StoppedReason = StopReason.None;
      this.EventQueue = new EventQueue<SourceStreamEvent>();
      this.Logger = new Logger(this.GetType());
      ThreadPool.RegisterWaitForSingleObject(this.EventQueue.WaitHandle, OnEvent, null, Timeout.Infinite, true);
    }

    protected virtual void OnEvent(object state, bool timedout)
    {
      SourceStreamEvent msg;
      while (this.EventQueue.TryDequeue(0, out msg)) {
        ProcessEvent(msg);
      }
      if (!IsStopped) {
        ThreadPool.RegisterWaitForSingleObject(this.EventQueue.WaitHandle, OnEvent, null, Timeout.Infinite, true);
      }
    }

    protected virtual void OnSourceConnectionStopped(object sender, StreamStoppedEventArgs args)
    {
      EventQueue.Enqueue(new ConnectionStoppedEvent(sender as SourceConnectionBase, args.StopReason));
    }

    protected void StartConnection(Uri source_uri)
    {
      if (sourceConnection!=null) {
        sourceConnection.Stopped -= OnSourceConnectionStopped;
        StopConnection(StopReason.UserReconnect);
      }
      sourceConnection = CreateConnection(source_uri);
      sourceConnection.Stopped += OnSourceConnectionStopped;
      sourceConnectionThread = new Thread(state => {
#if !DEBUG
        try
#endif
        {
          sourceConnection.Run();
        }
#if !DEBUG
        catch (Exception e) {
          Logger.Fatal("Unhandled exception");
          Logger.Fatal(e);
          throw;
        }
#endif
      });
      sourceConnectionThread.Start();
    }

    protected void StopConnection(StopReason reason)
    {
      if (sourceConnection!=null) {
        sourceConnection.Stop(reason);
        sourceConnectionThread.Join();
      }
    }

    protected virtual void ProcessEvent(SourceStreamEvent msg)
    {
      switch (msg.Type) {
      case SourceStreamEventType.None:
        break;
      case SourceStreamEventType.Start:
        OnStarted(msg as StartEvent);
        break;
      case SourceStreamEventType.Stop:
        OnStopped(msg as StopEvent);
        break;
      case SourceStreamEventType.Post:
        OnPosted(msg as PostEvent);
        break;
      case SourceStreamEventType.Reconnect:
        OnReconnected(msg as ReconnectEvent);
        break;
      case SourceStreamEventType.ConnectionStopped:
        OnConnectionStopped(msg as ConnectionStoppedEvent);
        break;
      }
    }

    protected virtual void OnStarted(StartEvent msg)
    {
      if (msg.SourceUri!=null) {
        this.SourceUri = msg.SourceUri;
        StartConnection(msg.SourceUri);
      }
      else {
        Stop(StopReason.NoHost);
      }
    }

    protected virtual void OnStopped(StopEvent msg)
    {
      StoppedReason = msg.StopReason;
      StopConnection(msg.StopReason);
      if (Stopped!=null) {
        Stopped(this, new StreamStoppedEventArgs(msg.StopReason));
      }
    }

    protected virtual void OnPosted(PostEvent msg)
    {
      if (sourceConnection!=null && !sourceConnection.IsStopped) {
        sourceConnection.Post(msg.From, msg.Message);
      }
    }

    protected virtual void OnReconnected(ReconnectEvent msg)
    {
      OnStarted(new StartEvent(this.SourceUri));
    }

    public void Start()
    {
      EventQueue.Enqueue(new StartEvent(this.SourceUri));
    }

    public void Post(Host from, Atom packet)
    {
      EventQueue.Enqueue(new PostEvent(from, packet));
    }

    public void Stop()
    {
      EventQueue.Enqueue(new StopEvent(StopReason.UserShutdown));
    }

    protected void Stop(StopReason reason)
    {
      EventQueue.Enqueue(new StopEvent(reason));
    }

    public void Reconnect()
    {
      EventQueue.Enqueue(new ReconnectEvent());
    }

    public void Reconnect(Uri source_uri)
    {
      EventQueue.Enqueue(new StartEvent(source_uri));
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
