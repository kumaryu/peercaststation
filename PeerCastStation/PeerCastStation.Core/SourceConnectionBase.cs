﻿using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace PeerCastStation.Core
{
  public interface ISourceConnection
  {
    Uri        SourceUri     { get; }
    StopReason StoppedReason { get; }
    bool       IsStopped     { get; }
    float      SendRate      { get; }
    float      RecvRate      { get; }

    ConnectionInfo GetConnectionInfo();
    Task<StopReason> Run();
    void Post(Host from, Atom packet);
    void Stop(StopReason reason);
  }

  public abstract class SourceConnectionBase
    : ISourceConnection
  {
    protected ConnectionStatus Status { get; set; }

    public PeerCast   PeerCast { get; private set; }
    public Channel    Channel { get; private set; }
    public Uri        SourceUri { get; private set; }
    public StopReason StoppedReason { get; private set; }
    public float      SendRate { get { return connection!=null ? connection.Stream.WriteRate : 0; } }
    public float      RecvRate { get { return connection!=null ? connection.Stream.ReadRate  : 0; } }

    private CancellationTokenSource isStopped = new CancellationTokenSource();
    public bool IsStopped {
      get { return isStopped.IsCancellationRequested; }
    }
    protected CancellationToken StoppedCancellationToken {
      get { return isStopped.Token; }
    }

    protected Logger Logger { get; private set; }

    protected class SourceConnectionClient
      : IDisposable
    {
      public TcpClient Client { get; private set; }
      public ConnectionStream Stream { get; private set; }
      public SourceConnectionClient(TcpClient client)
      {
        this.Client = client;
        var stream = client.GetStream();
        this.Stream = new ConnectionStream(stream, stream);
        this.Stream.ReadTimeout = 10000;
        this.Stream.WriteTimeout = 10000;
      }

      private IPEndPoint remoteEndPoint = null;
      public IPEndPoint RemoteEndPoint {
        get {
          if (remoteEndPoint!=null) {
            return remoteEndPoint;
          }
          else if (this.Client.Connected) {
            remoteEndPoint = this.Client.Client.RemoteEndPoint as IPEndPoint;
            return remoteEndPoint;
          }
          else {
            return null;
          }
        }
      }

      public void Dispose()
      {
        remoteEndPoint = null;
        this.Stream.Close();
        this.Client.Close();
      }
    }
    protected SourceConnectionClient connection;

    public SourceConnectionBase(
        PeerCast peercast,
        Channel  channel,
        Uri      source_uri)
    {
      this.PeerCast      = peercast;
      this.Channel       = channel;
      this.SourceUri     = source_uri;
      this.StoppedReason = StopReason.None;
      this.Logger        = new Logger(this.GetType(), source_uri.ToString());
      this.Status        = ConnectionStatus.Idle;
    }

    protected virtual void OnStarted()
    {
    }

    protected virtual void OnStopped()
    {
    }

    public async Task<StopReason> Run()
    {
      this.Status = ConnectionStatus.Connecting;
      try {
        connection = await DoConnect(SourceUri, isStopped.Token);
      }
      catch (OperationCanceledException) {
        connection = null;
      }
      if (connection==null) {
        Stop(StopReason.ConnectionError);
      }
      if (!IsStopped) {
        OnStarted();
        try {
          await DoProcess(isStopped.Token);
        }
        catch (OperationCanceledException) {
        }
        OnStopped();
      }
      if (connection!=null) {
        await DoClose(connection);
      }
      return StoppedReason;
    }

    public void Post(Host from, Atom packet)
    {
      if (IsStopped) return;
      DoPost(from, packet);
    }

    public void Stop()
    {
      Stop(StopReason.UserShutdown);
    }

    public virtual void Stop(StopReason reason)
    {
      if (reason==StopReason.None) throw new ArgumentException("Invalid value", "reason");
      if (IsStopped) return;
      StoppedReason = reason;
      isStopped.Cancel();
    }

    protected abstract Task<SourceConnectionClient> DoConnect(Uri source, CancellationToken cancellationToken);

    protected virtual async Task DoClose(SourceConnectionClient connection)
    {
      await connection.Stream.FlushAsync();
      connection.Dispose();
      Logger.Debug("closed");
      this.Status = ConnectionStatus.Error;
    }

    protected virtual void DoPost(Host from, Atom packet)
    {
    }

    protected abstract Task DoProcess(CancellationToken cancellationToken);

    public abstract ConnectionInfo GetConnectionInfo();
  }

}
