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
      set { localOutputAccepts = value; }
    }
    private OutputStreamType localOutputAccepts;

    /// <summary>
    /// リンクローカルな接続先に対して認証が必要かどうかを取得および設定します。
    /// </summary>
    public bool LocalAuthorizationRequired { get; set; }

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
      }
    }
    private OutputStreamType globalOutputAccepts;

    /// <summary>
    /// リンクグローバルな接続先に対して認証が必要かどうかを取得および設定します。
    /// </summary>
    public bool GlobalAuthorizationRequired { get; set; }

    /// <summary>
    /// 認証用IDとパスワードの組を取得および設定します
    /// </summary>
    public AuthenticationKey AuthenticationKey { get; set; }

    public AccessControlInfo  AccessControlInfo { get; private set; }
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
      this.LocalAuthorizationRequired  = false;
      this.GlobalAuthorizationRequired = true;
      this.AuthenticationKey = AuthenticationKey.Generate();
      this.AccessControlInfo = new AccessControlInfo(
        this.localOutputAccepts,
        this.LocalAuthorizationRequired,
        this.globalOutputAccepts,
        this.GlobalAuthorizationRequired,
        this.AuthenticationKey);
      this.ConnectionHandler = connection_handler;
      server = new TcpListener(ip);
      server.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
      server.Start();
      listenTask = StartListen(server, cancellationSource.Token);
    }

    public void ResetAuthenticationKey()
    {
      this.AuthenticationKey = AuthenticationKey.Generate();
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
            var client_task = ConnectionHandler.HandleClient(client, this.AccessControlInfo);
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
      client.ReceiveBufferSize = 64*1024;
      client.SendBufferSize    = 64*1024;
      var stream = client.GetStream();
      stream.WriteTimeout = 3000;
      stream.ReadTimeout = 3000;
      try {
        var remote_endpoint = (IPEndPoint)client.Client.RemoteEndPoint;
        var handler = await CreateMatchedHandler(remote_endpoint, stream, acinfo);
        if (handler!=null) {
          logger.Debug("Output stream started");
          var wait_task = new TaskCompletionSource<bool>();
          handler.Stopped += (sender, args) => {
            wait_task.SetResult(true);
          };
          handler.Start();
          await wait_task.Task;
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

    private async Task<int> ReadByteAsync(System.IO.Stream stream)
    {
      var buf = new byte[1];
      var len = await stream.ReadAsync(buf, 0, 1);
      if (len<1) return -1;
      else       return buf[0];
    }

    private enum RemoteType {
      Loopback,
      SiteLocal,
      Global,
    }

    private RemoteType GetRemoteType(IPEndPoint remote_endpoint)
    {
      if (remote_endpoint.Address.Equals(IPAddress.Loopback) ||
          remote_endpoint.Address.Equals(IPAddress.IPv6Loopback)) {
        return RemoteType.Loopback;
      }
      else if (remote_endpoint.Address.IsSiteLocal()) {
        return RemoteType.SiteLocal;
      }
      else {
        return RemoteType.Global;
      }
    }

    private async Task<IOutputStream> CreateMatchedHandler(
        IPEndPoint remote_endpoint,
        NetworkStream stream,
        AccessControlInfo acinfo)
    {
      var output_factories = PeerCast.OutputStreamFactories.OrderBy(factory => factory.Priority);
      var header = new List<byte>();
      RemoteType remote_type = GetRemoteType(remote_endpoint);
      bool eos = false;
      while (!eos && header.Count<=4096) {
        try {
          do {
            var val = await ReadByteAsync(stream);
            if (val < 0) {
              eos = true;
            }
            else {
              header.Add((byte)val);
            }
          } while (stream.DataAvailable);
        }
        catch (System.IO.IOException) {
          eos = true;
        }
        var header_ary = header.ToArray();
        foreach (var factory in output_factories) {
          if (remote_type==RemoteType.SiteLocal && (factory.OutputStreamType & acinfo.LocalOutputAccepts )==0) continue;
          if (remote_type==RemoteType.Global    && (factory.OutputStreamType & acinfo.GlobalOutputAccepts)==0) continue;
          var channel_id = factory.ParseChannelID(header_ary);
          if (channel_id.HasValue) {
            switch (remote_type) {
            case RemoteType.Loopback:
              acinfo = new AccessControlInfo(
                acinfo.LocalOutputAccepts,
                acinfo.LocalAuthorizationRequired,
                acinfo.GlobalOutputAccepts,
                acinfo.GlobalAuthorizationRequired,
                null);
              break;
            case RemoteType.SiteLocal:
              acinfo = new AccessControlInfo(
                acinfo.LocalOutputAccepts,
                acinfo.LocalAuthorizationRequired,
                acinfo.GlobalOutputAccepts,
                acinfo.GlobalAuthorizationRequired,
                acinfo.LocalAuthorizationRequired ? acinfo.AuthenticationKey : null);
              break;
            case RemoteType.Global:
              acinfo = new AccessControlInfo(
                acinfo.LocalOutputAccepts,
                acinfo.LocalAuthorizationRequired,
                acinfo.GlobalOutputAccepts,
                acinfo.GlobalAuthorizationRequired,
                acinfo.GlobalAuthorizationRequired ? acinfo.AuthenticationKey : null);
              break;
            }
            return factory.Create(
              stream,
              stream,
              remote_endpoint,
              acinfo,
              channel_id.Value,
              header.ToArray());
          }
        }
      }
      return null;
    }

  }

}
