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
  public abstract class OutputStreamFactoryBase
    : IOutputStreamFactory
  {
    protected PeerCast PeerCast { get; private set; }
    public OutputStreamFactoryBase(PeerCast peercast)
    {
      this.PeerCast = peercast;
    }

    public abstract string Name { get; }
    public abstract OutputStreamType OutputStreamType { get; }
    public virtual int Priority { get { return 0; } }
    public abstract IOutputStream Create(Stream input_stream, Stream output_stream, EndPoint remote_endpoint, AccessControlInfo access_control, Guid channel_id, byte[] header);
    public abstract Guid? ParseChannelID(byte[] header);
  }

  public abstract class OutputStreamBase
    : IOutputStream
  {
    private ConnectionStream connection;
    protected ConnectionStream Connection { get { return connection; } }
    protected Logger Logger { get; private set; }
    public Channel Channel { get; private set; }
    public PeerCast PeerCast { get; private set; }
    public EndPoint RemoteEndPoint { get; private set; }
    public AccessControlInfo AccessControlInfo { get; private set; }

    /// <summary>
    /// 元になるストリーム、チャンネル、リクエストからHTTPOutputStreamを初期化します
    /// </summary>
    /// <param name="peercast">所属するPeerCast</param>
    /// <param name="input_stream">元になる受信ストリーム</param>
    /// <param name="output_stream">元になる送信ストリーム</param>
    /// <param name="remote_endpoint">接続先のアドレス</param>
    /// <param name="access_control">接続可否および認証の情報</param>
    /// <param name="channel">所属するチャンネル。無い場合はnull</param>
    /// <param name="request">クライアントからのリクエスト</param>
    public OutputStreamBase(
      PeerCast peercast,
      Stream input_stream,
      Stream output_stream,
      EndPoint remote_endpoint,
      AccessControlInfo access_control,
      Channel channel,
      byte[] header)
    {
      this.Logger = new Logger(this.GetType(), remote_endpoint!=null ? remote_endpoint.ToString() : "");
      this.connection = new ConnectionStream(
        header!=null && header.Length>0 ? new PrependedStream(header, input_stream) : input_stream,
        output_stream);
      this.connection.ReadTimeout = 10000;
      this.connection.WriteTimeout = 10000;
      this.PeerCast = peercast;
      this.RemoteEndPoint = remote_endpoint;
      this.AccessControlInfo = access_control;
      this.Channel = channel;
      var ip = remote_endpoint as IPEndPoint;
      this.IsLocal = ip!=null ? ip.Address.IsSiteLocal() : true;
    }

    public abstract OutputStreamType OutputStreamType { get; }

    public bool IsLocal { get; private set; }
    public bool HasError { get; private set; }

    public int UpstreamRate {
      get {
        if (IsLocal || Channel==null) {
          return 0;
        }
        else {
          return GetUpstreamRate();
        }
      }
    }

    protected virtual int GetUpstreamRate()
    {
      return 0;
    }

    public abstract ConnectionInfo GetConnectionInfo();

    private CancellationTokenSource isStopped = new CancellationTokenSource();
    public bool IsStopped {
      get { return isStopped.IsCancellationRequested; }
    }
    public StopReason StoppedReason { get; private set; }

    protected virtual Task OnStarted(CancellationToken cancel_token)
    {
      if (this.Channel!=null) {
        this.Channel.AddOutputStream(this);
      }
      return Task.Delay(0);
    }

    protected virtual Task OnStopped(CancellationToken cancel_token)
    {
      if (this.Channel!=null) {
        this.Channel.RemoveOutputStream(this);
      }
      return Task.Delay(0);
    }

    protected virtual Task OnError(Exception err, CancellationToken cancel_token)
    {
      HasError = true;
      Stop(StopReason.ConnectionError);
      HandlerResult = HandlerResult.Error;
      Logger.Info(err);
      return Task.Delay(0);
    }

    protected Task OnError(Exception err)
    {
      return OnError(err, isStopped.Token);
    }

    protected abstract Task<StopReason> DoProcess(CancellationToken cancel_token);

    protected HandlerResult HandlerResult { get; set; }

    public virtual async Task<HandlerResult> Start()
    {
      try {
        Logger.Debug("Starting");
        try {
          await OnStarted(isStopped.Token).ConfigureAwait(false);
          try {
            Stop(await DoProcess(isStopped.Token).ConfigureAwait(false));
          }
          catch (IOException err) {
            await OnError(err, isStopped.Token).ConfigureAwait(false);
          }
          catch (OperationCanceledException) {
          }
          await OnStopped(isStopped.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) {
        }
        finally {
          var timeout_source = new CancellationTokenSource(TimeSpan.FromMilliseconds(connection.WriteTimeout));
          if (HandlerResult!=HandlerResult.Continue) {
            await connection.CloseAsync(timeout_source.Token).ConfigureAwait(false);
          }
        }
        Logger.Debug("Finished");
        return HandlerResult;
      }
      catch (Exception e) {
        Logger.Error(e);
        if (StoppedReason==StopReason.None) {
          StoppedReason = StopReason.NotIdentifiedError;
        }
        return HandlerResult.Error;
      }
    }

    protected virtual Task DoPost(Host from, Atom packet, CancellationToken cancel_token)
    {
      return Task.Delay(0);
    }

    public void Post(Host from, Atom packet)
    {
      if (isStopped.IsCancellationRequested) return;
      DoPost(from, packet, isStopped.Token);
    }

    public void Stop()
    {
      Stop(StopReason.UserShutdown);
    }

    public void Stop(StopReason reason)
    {
      StoppedReason = reason;
      isStopped.Cancel();
    }

    public Task WaitForStoppedAsync()
    {
      var task = new TaskCompletionSource<bool>();
      isStopped.Token.Register(() => task.TrySetResult(true));
      return task.Task;
    }

    public Task WaitForStoppedAsync(CancellationToken cancel_token)
    {
      var task = new TaskCompletionSource<bool>();
      cancel_token.Register(() => task.TrySetCanceled());
      isStopped.Token.Register(() => task.TrySetResult(true));
      return task.Task;
    }

    private static string ParseEndPoint(string text)
    {
      var ipv4port = System.Text.RegularExpressions.Regex.Match(text, @"\A(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}):(\d{1,5})\z");
      var ipv6port = System.Text.RegularExpressions.Regex.Match(text, @"\A\[([a-fA-F0-9:]+)\]:(\d{1,5})\z");
      var hostport = System.Text.RegularExpressions.Regex.Match(text, @"\A([a-zA-Z](?:[a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z](?:[a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?)*):(\d{1,5})\z");
      var ipv4addr = System.Text.RegularExpressions.Regex.Match(text, @"\A(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})\z");
      var ipv6addr = System.Text.RegularExpressions.Regex.Match(text, @"\A([a-fA-F0-9:.]+)\z");
      var hostaddr = System.Text.RegularExpressions.Regex.Match(text, @"\A([a-zA-Z](?:[a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z](?:[a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?)*)\z");
      if (ipv4port.Success) {
        IPAddress addr;
        int port;
        if (IPAddress.TryParse(ipv4port.Groups[1].Value, out addr) &&
            addr.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork &&
            Int32.TryParse(ipv4port.Groups[2].Value, out port) &&
            0<port && port<=65535) {
          return new IPEndPoint(addr, port).ToString();
        }
      }
      if (ipv6port.Success) {
        IPAddress addr;
        int port;
        if (IPAddress.TryParse(ipv6port.Groups[1].Value, out addr) &&
            addr.AddressFamily==System.Net.Sockets.AddressFamily.InterNetworkV6 &&
            Int32.TryParse(ipv6port.Groups[2].Value, out port) &&
            0<port && port<=65535) {
          return new IPEndPoint(addr, port).ToString();
        }
      }
      if (hostport.Success) {
        string host = hostport.Groups[1].Value;
        int port;
        if (Int32.TryParse(hostport.Groups[2].Value, out port) && 0<port && port<=65535) {
          return String.Format("{0}:{1}", host, port);
        }
      }
      if (ipv4addr.Success) {
        IPAddress addr;
        if (IPAddress.TryParse(ipv4addr.Groups[1].Value, out addr) &&
            addr.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork) {
          return addr.ToString();
        }
      }
      if (ipv6addr.Success) {
        IPAddress addr;
        if (IPAddress.TryParse(ipv6addr.Groups[1].Value, out addr) &&
            addr.AddressFamily==System.Net.Sockets.AddressFamily.InterNetworkV6) {
          return addr.ToString();
        }
      }
      if (hostaddr.Success) {
        string host = hostaddr.Groups[1].Value;
        return host;
      }
      return null;
    }

    public static Uri CreateTrackerUri(Guid channel_id, string tip)
    {
      if (tip==null) return null;
      var endpoint = ParseEndPoint(tip);
      if (endpoint!=null) {
        return new Uri(String.Format("pcp://{0}/{1}", endpoint, channel_id));
      }
      else {
        return null;
      }
    }

  }

}
