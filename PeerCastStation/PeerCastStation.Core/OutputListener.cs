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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Net;
using System.Net.Sockets;

namespace PeerCastStation.Core
{
  /// <summary>
  /// 接続待ち受け処理を扱うクラスです
  /// </summary>
  public class OutputListener
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
    public OutputStreamType LocalOutputAccepts  { get; set; }
    /// <summary>
    /// リンクグローバルな接続先に許可する出力ストリームタイプを取得および設定します。
    /// </summary>
    public OutputStreamType GlobalOutputAccepts { get; set; }

    private TcpListener server;
    /// <summary>
    /// 指定したエンドポイントで接続待ち受けをするOutputListenerを初期化します
    /// </summary>
    /// <param name="peercast">所属するPeerCastオブジェクト</param>
    /// <param name="ip">待ち受けをするエンドポイント</param>
    /// <param name="local_accepts">リンクローカルな接続先に許可する出力ストリームタイプ</param>
    /// <param name="global_accepts">リンクグローバルな接続先に許可する出力ストリームタイプ</param>
    internal OutputListener(
      PeerCast peercast,
      IPEndPoint ip,
      OutputStreamType local_accepts,
      OutputStreamType global_accepts)
    {
      this.PeerCast = peercast;
      this.LocalOutputAccepts  = local_accepts;
      this.GlobalOutputAccepts = global_accepts;
      server = new TcpListener(ip);
      server.Start();
      listenerThread = new Thread(ListenerThreadFunc);
      listenerThread.Name = String.Format("OutputListenerThread:{0}", ip);
      listenerThread.Start(server);
    }

    private Thread listenerThread = null;
    private void ListenerThreadFunc(object arg)
    {
      logger.Debug("Listener thread started");
      var server = (TcpListener)arg;
      while (!IsClosed) {
        try {
          var client = server.AcceptTcpClient();
          logger.Info("Client connected {0}", client.Client.RemoteEndPoint);
          var output_thread = new Thread(OutputThreadFunc);
          lock (outputThreads) {
            outputThreads.Add(output_thread);
          }
          output_thread.Name = String.Format("OutputThread:{0}", client.Client.RemoteEndPoint);
          output_thread.Start(client);
        }
        catch (SocketException e) {
          if (!IsClosed) logger.Error(e);
        }
      }
      logger.Debug("Listener thread finished");
    }

    /// <summary>
    /// 接続を待ち受けを終了します
    /// </summary>
    internal void Stop()
    {
      logger.Debug("Stopping listener");
      IsClosed = true;
      server.Stop();
      listenerThread.Join();
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
        else if (Utils.IsSiteLocal(remote_endpoint.Address)) {
          return RemoteType.SiteLocal;
        }
        else {
          return RemoteType.Global;
        }
    }

    private IOutputStreamFactory FindMatchedFactory(RemoteType remote_type, NetworkStream stream, out List<byte> header, out Guid channel_id)
    {
      var output_factories = PeerCast.OutputStreamFactories.OrderBy(factory => factory.Priority);
      header = new List<byte>();
      channel_id = Guid.Empty;
      bool eos = false;
      while (!eos && header.Count<=4096) {
        try {
          do {
            var val = stream.ReadByte();
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
          if (remote_type==RemoteType.SiteLocal && (factory.OutputStreamType & this.LocalOutputAccepts)==0) continue;
          if (remote_type==RemoteType.Global    && (factory.OutputStreamType & this.GlobalOutputAccepts)==0) continue;
          var cid = factory.ParseChannelID(header_ary);
          if (cid.HasValue) {
            channel_id = cid.Value;
            return factory;
          }
        }
      }
      return null;
    }

    private static List<Thread> outputThreads = new List<Thread>();
    private void OutputThreadFunc(object arg)
    {
      logger.Debug("Output thread started");
      var client = (TcpClient)arg;
      var stream = client.GetStream();
      stream.WriteTimeout = 3000;
      stream.ReadTimeout = 3000;
      IOutputStream output_stream = null;
      Channel channel = null;
      try {
        var remote_endpoint = (IPEndPoint)client.Client.RemoteEndPoint;
        List<byte> header;
        Guid channel_id;
        var factory = FindMatchedFactory(GetRemoteType(remote_endpoint), stream, out header, out channel_id);
        if (factory!=null) {
          output_stream = factory.Create(stream, stream, remote_endpoint, channel_id, header.ToArray());
          channel = PeerCast.Channels.FirstOrDefault(c => c.ChannelID==channel_id);
          if (channel!=null) {
            channel.AddOutputStream(output_stream);
          }
          logger.Debug("Output stream started");
          var wait_stopped = new EventWaitHandle(false, EventResetMode.ManualReset);
          output_stream.Stopped += (sender, args) => { wait_stopped.Set(); };
          output_stream.Start();
          wait_stopped.WaitOne();
        }
        else {
          logger.Debug("No protocol matched");
        }
      }
      finally {
        logger.Debug("Closing client connection");
        if (output_stream!=null && channel!=null) {
          channel.RemoveOutputStream(output_stream);
        }
        stream.Close();
        client.Close();
        lock (outputThreads) {
          outputThreads.Remove(Thread.CurrentThread);
        }
        logger.Debug("Output thread finished");
      }
    }
  }
}
