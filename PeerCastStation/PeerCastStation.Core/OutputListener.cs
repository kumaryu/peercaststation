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
    Task HandleClient(EndPoint localEndPoint, TcpClient client, AccessControlInfo acinfo, CancellationToken cancellationToken);
  }

  public record ListenerInfo(PortStatus Status, IPEndPoint EndPoint, AccessControlInfo AccessControl)
  {
    public NetworkType NetworkType => EndPoint.AddressFamily.GetNetworkType();
    public NetworkLocality Locality => EndPoint.Address.GetAddressLocality();
  }


  //public interface IListener
  //{
  //  NetworkType NetworkType { get; }
  //  IPEndPoint LocalEndPoint { get; }
  //  IPEndPoint? GlobalEndPoint { get; }
  //  PortStatus Status { get; }
  //  AccessControlInfo LoopbackAccessControlInfo  { get; }
  //  AccessControlInfo LocalAccessControlInfo  { get; }
  //  AccessControlInfo GlobalAccessControlInfo { get; }
  //}

  //public static class ListenerExtensions
  //{
  //  public static AccessControlInfo GetAccessControlInfo(this IListener listener, NetworkLocality locality)
  //  {
  //    return locality switch {
  //      NetworkLocality.Loopback => listener.LoopbackAccessControlInfo,
  //      NetworkLocality.Local => listener.LocalAccessControlInfo,
  //      NetworkLocality.Global => listener.GlobalAccessControlInfo,
  //      _ => throw new ArgumentException($"Unsupported value {locality}", nameof(locality))
  //    };
  //  }
  //}

  /// <summary>
  /// 接続待ち受け処理を扱うクラスです
  /// </summary>
  public class OutputListener
    : IDisposable
  {
    private static Logger logger = new Logger(typeof(OutputListener));
    public static int MaxPendingConnections { get; set; } = Int32.MaxValue;
    /// <summary>
    /// 所属しているPeerCastオブジェクトを取得します
    /// </summary>
    public PeerCast PeerCast { get; private set; }
    private int isClosed = 0;
    /// <summary>
    /// 待ち受けが閉じられたかどうかを取得します
    /// </summary>
    public bool IsClosed { get { return isClosed!=0; } }
    /// <summary>
    /// 接続待ち受けをしているエンドポイントを取得します
    /// </summary>
    public IPEndPoint LocalEndPoint { get { return (IPEndPoint)server.LocalEndpoint; } }

    private IPAddress? globalAddress = null;
    public IPAddress? GlobalAddress {
      get { return globalAddress; }
      set {
        if (globalAddress==null || value==null || globalAddress.GetAddressLocality()<=value.GetAddressLocality()) {
          globalAddress = value;
        }
      }
    }

    public IPEndPoint? GlobalEndPoint {
      get {
        if (globalAddress==null) {
          return null;
        }
        else {
          return new IPEndPoint(globalAddress, ((IPEndPoint)server.LocalEndpoint).Port);
        }
      }
    }

    private bool bound = false;
    private PortStatus portStatus = PortStatus.Unknown;
    public PortStatus Status {
      get {
        if (!bound) return PortStatus.Unavailable;
        return portStatus;
      }
      set {
        portStatus = value;
      }
    }

    /// <summary>
    /// リンクローカルな接続先に許可する出力ストリームタイプを取得および設定します。
    /// </summary>
    public OutputStreamType LocalOutputAccepts  {
      get { return localOutputAccepts;  }
      set {
        localOutputAccepts = value;
        LocalAccessControlInfo = UpdateLocalAccessControlInfo();
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
        LocalAccessControlInfo = UpdateLocalAccessControlInfo();
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
          Status = PortStatus.Unknown;
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
        GlobalAccessControlInfo = UpdateGlobalAccessControlInfo();
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
        LocalAccessControlInfo = UpdateLocalAccessControlInfo();
        GlobalAccessControlInfo = UpdateGlobalAccessControlInfo();
      }
    }
    private AuthenticationKey authenticationKey = AuthenticationKey.Generate();

    private AccessControlInfo UpdateLocalAccessControlInfo()
    {
      return new AccessControlInfo(
        this.localOutputAccepts,
        this.LocalAuthorizationRequired,
        this.authenticationKey);
    }

    private AccessControlInfo UpdateGlobalAccessControlInfo()
    {
      return new AccessControlInfo(
        this.globalOutputAccepts,
        this.GlobalAuthorizationRequired,
        this.authenticationKey);
    }

    public AccessControlInfo  LoopbackAccessControlInfo  { get; set; }
    public AccessControlInfo  LocalAccessControlInfo  { get; private set; }
    public AccessControlInfo  GlobalAccessControlInfo { get; private set; }
    public IConnectionHandler ConnectionHandler { get; private set; }

    private static IPAddress[]? localAddresses = null;
    private static IEnumerable<IPAddress> GetInterfaceAddresses()
    {
      if (localAddresses==null) {
        localAddresses = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
          .Where(intf => intf.OperationalStatus==System.Net.NetworkInformation.OperationalStatus.Up)
          .Where(intf => intf.NetworkInterfaceType!=System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
          .Select(intf => intf.GetIPProperties())
          .SelectMany(prop => prop.UnicastAddresses)
          .Select(uaddr => uaddr.Address)
          .ToArray();
      }
      return localAddresses;
    }

    public IEnumerable<ListenerInfo> GetListenerInfos()
    {
      if (LocalEndPoint.Port!=0) {
        switch (LocalEndPoint.AddressFamily) {
        case AddressFamily.InterNetwork:
          yield return new ListenerInfo(portStatus, new IPEndPoint(IPAddress.Loopback, LocalEndPoint.Port), LoopbackAccessControlInfo);
          break;
        case AddressFamily.InterNetworkV6:
          yield return new ListenerInfo(portStatus, new IPEndPoint(IPAddress.IPv6Loopback, LocalEndPoint.Port), LoopbackAccessControlInfo);
          break;
        default:
          break;
        }
        foreach (var localAddr in GetInterfaceAddresses().Where(addr => addr.AddressFamily==LocalEndPoint.AddressFamily)) {
          yield return new ListenerInfo(portStatus, new IPEndPoint(localAddr, LocalEndPoint.Port), LocalAccessControlInfo);
        }
        var globalAddress = GlobalAddress;
        if (globalAddress!=null) {
          yield return new ListenerInfo(portStatus, new IPEndPoint(globalAddress, LocalEndPoint.Port), GlobalAccessControlInfo);
        }
      }
    }

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
      PeerCast = peercast;
      localOutputAccepts  = local_accepts;
      globalOutputAccepts = global_accepts;
      LoopbackAccessControlInfo = new AccessControlInfo(OutputStreamType.All, false, AuthenticationKey.Generate());
      LocalAccessControlInfo = UpdateLocalAccessControlInfo();
      GlobalAccessControlInfo = UpdateGlobalAccessControlInfo();
      ConnectionHandler = connection_handler;
      server = new TcpListener(ip);
      if (ip.AddressFamily==AddressFamily.InterNetworkV6) {
        server.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, true);
      }
      listenTask = StartListen(server, cancellationSource.Token);
    }

    public void ResetAuthenticationKey()
    {
      this.AuthenticationKey = AuthenticationKey.Generate();
    }

    private AccessControlInfo GetAccessControlInfo(IPEndPoint? remote_endpoint)
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

    private async Task StartListen(TcpListener server, CancellationToken cancel_token)
    {
      try {
        server.Start(MaxPendingConnections);
        using (cancel_token.Register(() => server.Stop(), false)) {
          while (!cancel_token.IsCancellationRequested) {
            bound = true;
            var client = await server.AcceptTcpClientAsync().ConfigureAwait(false);
            logger.Info($"Client connected {client.Client.RemoteEndPoint}");
            var client_task = ConnectionHandler.HandleClient(
              server.LocalEndpoint,
              client,
              GetAccessControlInfo(client.Client.RemoteEndPoint as IPEndPoint),
              cancel_token);
          }
        }
      }
      catch (SocketException) {
        bound = false;
      }
      catch (ObjectDisposedException) {
        bound = false;
      }
    }

    /// <summary>
    /// 接続を待ち受けを終了します
    /// </summary>
    public void Stop()
    {
      if (Interlocked.Exchange(ref isClosed, 1)!=0) return;
      logger.Debug("Stopping listener");
      cancellationSource.Cancel();
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
    public IPeerCast PeerCast { get; private set; }

    public ConnectionHandler(IPeerCast peercast)
    {
      this.PeerCast = peercast;
    }

    public async Task HandleClient(
      EndPoint localEndPoint,
      TcpClient client,
      NetworkStream stream,
      AccessControlInfo acinfo,
      CancellationToken cancellationToken)
    {
      logger.Debug("Output thread started");
      client.ReceiveBufferSize = 256*1024;
      client.SendBufferSize    = 256*1024;
      client.NoDelay = true;
      int trying = 0;
      try {
        retry:
        stream.WriteTimeout = 3000;
        stream.ReadTimeout  = 3000;
        var handler = await CreateMatchedHandler(
          client.Client,
          stream,
          acinfo,
          cancellationToken).ConfigureAwait(false);
        if (handler!=null) {
          logger.Debug("Output stream started {0}", trying);
          var result = await handler.Start(cancellationToken).ConfigureAwait(false);
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
      catch (OperationCanceledException) {
      }
      finally {
        client.Client.Shutdown(SocketShutdown.Send);
        logger.Debug("Closing client connection");
        stream.Close();
        client.Close();
      }
    }

    public Task HandleClient(
      EndPoint localEndPoint,
      TcpClient client,
      AccessControlInfo acinfo,
      CancellationToken cancellationToken)
    {
      return HandleClient(localEndPoint, client, client.GetStream(), acinfo, cancellationToken);
    }

    private async Task<IOutputStream?> CreateMatchedHandler(
        Socket socket,
        NetworkStream stream,
        AccessControlInfo acinfo,
        CancellationToken cancellationToken)
    {
      var output_factories = PeerCast.OutputStreamFactories.OrderBy(factory => factory.Priority);
      var header = new byte[4096];
      int offset = 0;
      using (var cancel_source=new CancellationTokenSource(TimeSpan.FromMilliseconds(3000)))
      using (cancellationToken.Register(() => stream.Close(), false))
      using (cancel_source.Token.Register(() => stream.Close(), false)) {
        try {
          while (offset<header.Length) {
            var len = await stream.ReadAsync(header, offset, header.Length-offset, cancellationToken).ConfigureAwait(false);
            if (len==0) break;
            offset += len;
            var header_ary = header.Take(offset).ToArray();
            foreach (var factory in output_factories) {
              if (factory.TryCreate(header_ary, acinfo, () => new ConnectionStream(socket, stream, header_ary), out var outputStream)) {
                return outputStream;
              }
            }
          }
        }
        catch (ObjectDisposedException) {
        }
        catch (IOException) {
        }
      }
      return null;
    }

  }

}
