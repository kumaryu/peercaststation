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
    public Channel Channel { get; private set; }
    public Uri SourceUri { get; private set; }
    public StopReason StoppedReason { get; private set; }
    public bool IsStopped { get { return StoppedReason!=StopReason.None; } }

    protected Logger Logger { get; private set; }
    private TaskCompletionSource<StopReason> ranTaskSource = new TaskCompletionSource<StopReason>();
    private bool disposed = false;

    protected abstract ConnectionInfo GetConnectionInfo(ISourceConnection? sourceConnection);
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
      Channel channel,
      Uri source_uri)
    {
      this.PeerCast  = peercast;
      this.Channel   = channel;
      this.SourceUri = source_uri;
      this.StoppedReason = StopReason.None;
      this.Logger = new Logger(this.GetType(), source_uri.ToString());
    }

    public abstract SourceStreamType Type { get; }

    public SourceStreamStatus Status {
      get {
        switch (GetConnectionInfo().Status) {
        case ConnectionStatus.Connected: return SourceStreamStatus.Receiving;
        case ConnectionStatus.Connecting: return SourceStreamStatus.Searching;
        case ConnectionStatus.Error: return SourceStreamStatus.Error;
        case ConnectionStatus.Idle: return SourceStreamStatus.Idle;
        }
        return SourceStreamStatus.Idle;
      }
    }

    protected virtual Uri? SelectSourceHost()
    {
      return SourceUri;
    }

    protected virtual void IgnoreSourceHost(Uri source)
    {
    }

    protected virtual Task OnNoSourceHost(CancellationToken cancellationToken)
    {
      Stop(StopReason.NoHost);
      return Task.CompletedTask;
    }

    private Task<StopReason>? runningTask;
    public async Task<StopReason> Run(CancellationToken cancellationToken)
    {
      if (runningTask!=null) {
        throw new InvalidOperationException();
      }
      runningTask = RunInternal(cancellationToken);
      StoppedReason = await runningTask.ConfigureAwait(false);
      return StoppedReason;
    }

    WaitableQueue<ChannelSourceMessage> messageQueue = new();
    private ISourceConnection? currentSourceConnection = null;

    public async Task<StopReason> RunInternal(CancellationToken cancellationToken)
    {
      if (disposed) throw new ObjectDisposedException(this.GetType().Name);
      StopReason result = StopReason.UserShutdown;
      try {
        while (!cancellationToken.IsCancellationRequested) {
          var source = SelectSourceHost();
          if (source==null) {
            await OnNoSourceHost(cancellationToken).ConfigureAwait(false);
          }
          else {
            using var _ = cancellationToken.Register(() => Stop(StopReason.UserShutdown));
            var args = new ConnectionStoppedArgs();
            try {
              var conn = CreateConnection(source);
              currentSourceConnection = conn;
              try {
                args.Reason = await conn.Run(messageQueue).ConfigureAwait(false);
                Logger.Debug($"Connection stopped by reason {args.Reason}");
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
              Logger.Debug($"Cleaning up connection (closed by {args.Reason})");
              OnConnectionStopped(conn, args);
            }
            finally {
              currentSourceConnection = null;
            }
            if (args.IgnoreSource!=null) {
              IgnoreSourceHost(args.IgnoreSource);
            }
            if (args.Reconnect) {
              if (args.Delay>0 && !cancellationToken.IsCancellationRequested) {
                try {
                  await Task.Delay(args.Delay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) {
                }
              }
            }
            else if (args.Reason!=StopReason.UserReconnect) {
              result = args.Reason;
              break;
            }
          }
        }
        return result;
      }
      catch (Exception ex) {
        Logger.Error(ex);
        return StopReason.NotIdentifiedError;
      }
    }

    public void Stop(StopReason reason)
    {
      messageQueue.Enqueue(new ChannelSourceMessageStop(reason));
    }

    public void Reconnect()
    {
      messageQueue.Enqueue(new ChannelSourceMessageReconnect());
    }

    public void Post(Host? from, Atom packet)
    {
      messageQueue.Enqueue(new ChannelSourceMessagePost(from, packet));
    }

    public ConnectionInfo GetConnectionInfo()
    {
      return GetConnectionInfo(currentSourceConnection);
    }


  }

}
