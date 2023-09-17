﻿using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace PeerCastStation.Core
{

  public abstract record ChannelSourceMessage();
  public record ChannelSourceMessageStop(StopReason StopReason) : ChannelSourceMessage();
  public record ChannelSourceMessageReconnect() : ChannelSourceMessage();
  public record ChannelSourceMessagePost(Host? From, Atom Message) : ChannelSourceMessage();

  public interface ISourceConnectionResult
  {
    StopReason StopReason { get; }
  }

  public class SourceConnectionResult
    : ISourceConnectionResult
  {
    public StopReason StopReason { get; }
    public SourceConnectionResult(StopReason stopReason)
    {
      StopReason = stopReason;
    }
  }

  public interface ISourceConnection
  {
    Uri        SourceUri     { get; }
    StopReason StoppedReason { get; }

    ConnectionInfo GetConnectionInfo();
    Task<ISourceConnectionResult> Run(WaitableQueue<ChannelSourceMessage> channelSourceMessages);
  }

  public abstract class SourceConnectionBase
    : ISourceConnection
  {
    protected ConnectionStatus Status { get; set; }
    public PeerCast   PeerCast { get; private set; }
    public Channel    Channel { get; private set; }
    public Uri        SourceUri { get; private set; }
    public StopReason StoppedReason { get; private set; }
    protected Logger Logger { get; private set; }
    private Task runningTask = Task.CompletedTask;

    public SourceConnectionBase(
        PeerCast peercast,
        Channel  channel,
        Uri      source_uri)
    {
      this.PeerCast      = peercast;
      this.Channel       = channel;
      this.SourceUri     = source_uri;
      this.StoppedReason = StopReason.None;
      this.Logger        = new Logger(this.GetType(), source_uri?.ToString() ?? "");
      this.Status        = ConnectionStatus.Idle;
    }

    public async Task<ISourceConnectionResult> Run(WaitableQueue<ChannelSourceMessage> channelSourceMessages)
    {
      this.Status = ConnectionStatus.Connecting;
      var isStopped = new CancellationTokenSourceWithArg<StopReason>();
      var postMessageQueue = new WaitableQueue<(Host? From, Atom Message)>();
      var msgTask = Task.Run(async () => {
        await foreach (var msg in channelSourceMessages.ForEach().WithCancellation(isStopped.Token.CancellationToken).ConfigureAwait(false)) {
          switch (msg) {
          case ChannelSourceMessageStop stopMsg:
            isStopped.TryCancel(stopMsg.StopReason);
            Logger.Debug($"Stop requested by reason {stopMsg.StopReason}");
            break;
          case ChannelSourceMessageReconnect:
            isStopped.TryCancel(StopReason.UserReconnect);
            Logger.Debug($"Stop requested by reason {StopReason.UserReconnect}");
            break;
          case ChannelSourceMessagePost postMsg:
            postMessageQueue.Enqueue((postMsg.From, postMsg.Message));
            break;
          }
        }
      });
      var sourceTask = Task.Run(async () => {
        return await DoProcessSource(postMessageQueue, isStopped.Token).ConfigureAwait(false);
      });
      runningTask = sourceTask;
      await Task.WhenAll(msgTask, sourceTask).ConfigureAwait(false);
      return await sourceTask.ConfigureAwait(false);
    }

    protected Task GetRunningTask()
    {
      return runningTask;
    }

    protected abstract Task<ISourceConnectionResult> DoProcessSource(WaitableQueue<(Host? From, Atom Message)> postMessages, CancellationTokenWithArg<StopReason> cancellationToken);

    public abstract ConnectionInfo GetConnectionInfo();
  }

  public abstract class TCPSourceConnectionBase
    : SourceConnectionBase
  {
    protected class SourceConnectionClient
      : IDisposable
    {
      public TcpClient? Client { get; private set; }
      public ConnectionStream Stream { get; private set; }
      public float RecvRate { get { return Stream.ReadRate; } }
      public float SendRate { get { return Stream.WriteRate; } }
      public SourceConnectionClient(TcpClient client)
      {
        this.Client = client;
        this.Client.NoDelay = true;
        this.Client.ReceiveBufferSize = 256 * 1024;
        this.Client.SendBufferSize = 256 * 1024;
        var stream = client.GetStream();
        this.Stream = new ConnectionStream(client.Client, stream);
        this.Stream.ReadTimeout = 10000;
        this.Stream.WriteTimeout = 10000;
      }

      public SourceConnectionClient(ConnectionStream stream)
      {
        this.Stream = stream;
        this.Stream.ReadTimeout = 10000;
        this.Stream.WriteTimeout = 10000;
      }

      private IPEndPoint? remoteEndPoint = null;
      public IPEndPoint? RemoteEndPoint {
        get {
          if (remoteEndPoint!=null) {
            return remoteEndPoint;
          }
          else if (Client?.Connected ?? false) {
            remoteEndPoint = this.Client.Client.RemoteEndPoint as IPEndPoint;
            return remoteEndPoint;
          }
          else {
            remoteEndPoint = Stream.RemoteEndPoint;
            return remoteEndPoint;
          }
        }
      }

      public void Dispose()
      {
        remoteEndPoint = null;
        Stream.Close();
        Client?.Close();
      }
    }

    public TCPSourceConnectionBase(
        PeerCast peercast,
        Channel  channel,
        Uri      source_uri)
      : base(peercast, channel, source_uri)
    {
    }

    protected virtual void OnStarted(SourceConnectionClient connection)
    {
    }

    protected virtual void OnStopped()
    {
    }

    protected SourceConnectionClient? connection;
    protected override async Task<ISourceConnectionResult> DoProcessSource(WaitableQueue<(Host? From, Atom Message)> postMessages, CancellationTokenWithArg<StopReason> cancellationToken)
    {
      try {
        connection = await DoConnect(SourceUri, cancellationToken).ConfigureAwait(false);
        if (connection==null) {
          return new SourceConnectionResult(StopReason.ConnectionError);
        }
      }
      catch (OperationCanceledWithArgException<StopReason> ex) {
        connection = null;
        return new SourceConnectionResult(ex.Value);
      }
      catch (OperationCanceledException) {
        connection = null;
        return new SourceConnectionResult(StopReason.UserShutdown);
      }
      catch (BindErrorException e) {
        connection = null;
        Logger.Error(e);
        return new SourceConnectionResult(StopReason.NoHost);
      }
      if (!cancellationToken.IsCancellationRequested) {
        OnStarted(connection);
        ISourceConnectionResult result;
        try {
          result = await DoProcess(connection, postMessages, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledWithArgException<StopReason> ex) {
          result = new SourceConnectionResult(ex.Value);
        }
        catch (OperationCanceledException) {
          result = new SourceConnectionResult(StopReason.UserShutdown);
        }
        OnStopped();
        await DoClose(connection).ConfigureAwait(false);
        return result;
      }
      else {
        await DoClose(connection).ConfigureAwait(false);
        return new SourceConnectionResult(cancellationToken.Value);
      }
    }

    protected abstract Task<SourceConnectionClient?> DoConnect(Uri source, CancellationTokenWithArg<StopReason> cancellationToken);

    protected virtual async Task DoClose(SourceConnectionClient connection)
    {
      await connection.Stream.FlushAsync().ConfigureAwait(false);
      connection.Dispose();
      Logger.Debug("closed");
      this.Status = ConnectionStatus.Error;
    }

    protected abstract Task<ISourceConnectionResult> DoProcess(SourceConnectionClient connection, WaitableQueue<(Host? From, Atom Message)> postMessages, CancellationTokenWithArg<StopReason> cancellationToken);
  }

}
