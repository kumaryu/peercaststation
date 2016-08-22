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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace PeerCastStation.Core
{
  public interface IConnectionHandler
  {
    Task HandleClient(TcpClient client, AccessControlInfo acinfo);
  }

  /// <summary>
  /// 接続待ち受け処理を扱うクラスです
  /// </summary>
  public class OutputListener
    : IDisposable
  {
    private static Logger logger = new Logger(typeof(OutputListener));
    /// <summary>
    /// 所属しているPeerCastオブジェクトを取得します
    /// </summary>
    public PeerCast PeerCast { get; private set; }
    /// <summary>
    /// 待ち受けが閉じられたかどうかを取得します
    /// </summary>
    public bool IsClosed { get; private set; }
    /// <summary>
    /// 接続待ち受けをしているエンドポイントを取得します
    /// </summary>
    public IPEndPoint LocalEndPoint { get { return (IPEndPoint)server.LocalEndpoint; } }

    /// <summary>
    /// リンクローカルな接続先に許可する出力ストリームタイプを取得および設定します。
    /// </summary>
    public OutputStreamType LocalOutputAccepts  {
      get { return localOutputAccepts;  }
      set {
        localOutputAccepts = value;
        UpdateLocalAccessControlInfo();
      }
    }
    private OutputStreamType localOutputAccepts;

    /// <summary>
    /// リンクローカルな接続先に対して認証が必要かどうかを取得および設定します。
    /// </summary>
    public bool LocalAuthorizationRequired {
      get {
        return localAuthorizationRequired;
      }
      set {
        localAuthorizationRequired = value;
        UpdateLocalAccessControlInfo();
      }
    }
    private bool localAuthorizationRequired = false;

    /// <summary>
    /// リンクグローバルな接続先に許可する出力ストリームタイプを取得および設定します。
    /// </summary>
    public OutputStreamType GlobalOutputAccepts {
      get { return globalOutputAccepts; }
      set {
        if ((globalOutputAccepts & OutputStreamType.Relay)!=(value & OutputStreamType.Relay)) {
          if ((value & OutputStreamType.Relay)!=0) PeerCast.OnListenPortOpened();
          else                                     PeerCast.OnListenPortClosed();
        }
        globalOutputAccepts = value;
        UpdateGlobalAccessControlInfo();
      }
    }
    private OutputStreamType globalOutputAccepts;

    /// <summary>
    /// リンクグローバルな接続先に対して認証が必要かどうかを取得および設定します。
    /// </summary>
    public bool GlobalAuthorizationRequired {
      get { return globalAuthorizationRequired; }
      set {
        globalAuthorizationRequired = value;
        UpdateGlobalAccessControlInfo();
      }
    }
    private bool globalAuthorizationRequired = true;

    /// <summary>
    /// 認証用IDとパスワードの組を取得および設定します
    /// </summary>
    public AuthenticationKey AuthenticationKey {
      get { return authenticationKey; }
      set {
        this.authenticationKey = value;
        UpdateLocalAccessControlInfo();
        UpdateGlobalAccessControlInfo();
      }
    }
    private AuthenticationKey authenticationKey = AuthenticationKey.Generate();

    private void UpdateLocalAccessControlInfo()
    {
      this.LocalAccessControlInfo = new AccessControlInfo(
        this.localOutputAccepts,
        this.LocalAuthorizationRequired,
        this.authenticationKey);
    }

    private void UpdateGlobalAccessControlInfo()
    {
      this.GlobalAccessControlInfo = new AccessControlInfo(
        this.globalOutputAccepts,
        this.GlobalAuthorizationRequired,
        this.authenticationKey);
    }

    public AccessControlInfo  LoopbackAccessControlInfo  { get; private set; }
    public AccessControlInfo  LocalAccessControlInfo  { get; private set; }
    public AccessControlInfo  GlobalAccessControlInfo { get; private set; }
    public IConnectionHandler ConnectionHandler { get; private set; }

    private TcpListener server;
    private CancellationTokenSource cancellationSource = new CancellationTokenSource();
    private Task listenTask;
    /// <summary>
    /// 指定したエンドポイントで接続待ち受けをするOutputListenerを初期化します
    /// </summary>
    /// <param name="peercast">所属するPeerCastオブジェクト</param>
    /// <param name="ip">待ち受けをするエンドポイント</param>
    /// <param name="local_accepts">リンクローカルな接続先に許可する出力ストリームタイプ</param>
    /// <param name="global_accepts">リンクグローバルな接続先に許可する出力ストリームタイプ</param>
    internal OutputListener(
      PeerCast peercast,
      IConnectionHandler connection_handler,
      IPEndPoint ip,
      OutputStreamType local_accepts,
      OutputStreamType global_accepts)
    {
      this.PeerCast = peercast;
      this.localOutputAccepts  = local_accepts;
      this.globalOutputAccepts = global_accepts;
      this.LoopbackAccessControlInfo = new AccessControlInfo(
        OutputStreamType.All,
        false,
        null);
      UpdateLocalAccessControlInfo();
      UpdateGlobalAccessControlInfo();
      this.ConnectionHandler = connection_handler;
      server = new TcpListener(ip);
      server.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
      server.Start(Int32.MaxValue);
      listenTask = StartListen(server, cancellationSource.Token);
    }

    public void ResetAuthenticationKey()
    {
      this.AuthenticationKey = AuthenticationKey.Generate();
    }

    private AccessControlInfo GetAccessControlInfo(IPEndPoint remote_endpoint)
    {
      if (remote_endpoint==null) return this.GlobalAccessControlInfo;
      if (remote_endpoint.Address.Equals(IPAddress.Loopback) ||
          remote_endpoint.Address.Equals(IPAddress.IPv6Loopback)) {
        return this.LoopbackAccessControlInfo;
      }
      else if (remote_endpoint.Address.IsSiteLocal()) {
        return this.LocalAccessControlInfo;
      }
      else {
        return this.GlobalAccessControlInfo;
      }
    }

    private Task StartListen(TcpListener server, CancellationToken cancel_token)
    {
      server.Start();
      return Task<Task>.Factory.StartNew(async () => {
        try {
          cancel_token.Register(() => {
            server.Stop();
          });
          while (!cancel_token.IsCancellationRequested) {
            var client = await server.AcceptTcpClientAsync();
            logger.Info("Client connected {0}", client.Client.RemoteEndPoint);
            var client_task = ConnectionHandler.HandleClient(
              client,
              GetAccessControlInfo(client.Client.RemoteEndPoint as IPEndPoint));
          }
        }
        catch (SocketException e) {
          if (!IsClosed) throw;
        }
        catch (ObjectDisposedException) {
        }
      }).Unwrap();
    }

    /// <summary>
    /// 接続を待ち受けを終了します
    /// </summary>
    public void Stop()
    {
      logger.Debug("Stopping listener");
      cancellationSource.Cancel();
      IsClosed = true;
      server.Stop();
      listenTask.Wait();
    }

    public void Dispose()
    {
      Stop();
    }
  }

  public class ConnectionHandler
    : IConnectionHandler
  {
    private static Logger logger = new Logger(typeof(ConnectionHandler));
    public PeerCast PeerCast { get; private set; }

    public ConnectionHandler(PeerCast peercast)
    {
      this.PeerCast = peercast;
    }

    public async Task HandleClient(TcpClient client, AccessControlInfo acinfo)
    {
      logger.Debug("Output thread started");
      client.ReceiveBufferSize = 16*1024;
      client.SendBufferSize    = 16*1024;
      client.NoDelay = true;
      var stream = client.GetStream();
      int trying = 0;
      try {
        retry:
        stream.WriteTimeout = 3000;
        stream.ReadTimeout  = 3000;
        var remote_endpoint = (IPEndPoint)client.Client.RemoteEndPoint;
        var handler = await CreateMatchedHandler(remote_endpoint, stream, acinfo);
        if (handler!=null) {
          logger.Debug("Output stream started {0}", trying);
          var result = await handler.Start();
          switch (result) {
          case HandlerResult.Continue:
            trying++;
            goto retry;
          case HandlerResult.Close:
          case HandlerResult.Error:
          default:
            break;
          }
        }
        else {
          logger.Debug("No protocol handler matched");
        }
      }
      finally {
        logger.Debug("Closing client connection");
        stream.Close();
        client.Close();
      }
    }

    private async Task<IOutputStream> CreateMatchedHandler(
        IPEndPoint remote_endpoint,
        NetworkStream stream,
        AccessControlInfo acinfo)
    {
      var output_factories = PeerCast.OutputStreamFactories.OrderBy(factory => factory.Priority);
      var header = new byte[4096];
      int offset = 0;
      using (var cancel_source=new CancellationTokenSource(TimeSpan.FromMilliseconds(3000))) {
        var cancel_token = cancel_source.Token;
        cancel_token.Register(() => stream.Close());
        try {
          while (offset<header.Length) {
            var len = await stream.ReadAsync(header, offset, header.Length-offset);
            if (len==0) break;
            offset += len;
            var header_ary = header.Take(offset).ToArray();
            foreach (var factory in output_factories) {
              if ((acinfo.Accepts & factory.OutputStreamType) == 0) continue;
              var channel_id = factory.ParseChannelID(header_ary);
              if (channel_id.HasValue) {
                return factory.Create(
                  stream,
                  stream,
                  remote_endpoint,
                  acinfo,
                  channel_id.Value,
                  header_ary);
              }
            }
          }
        }
        catch (System.ObjectDisposedException) {
        }
        catch (System.IO.IOException) {
        }
      }
      return null;
    }

  }

}
