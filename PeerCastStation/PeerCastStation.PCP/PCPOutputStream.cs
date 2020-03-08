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
using System.IO;
using PeerCastStation.Core;
using System.Text.RegularExpressions;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Owin;
using PeerCastStation.Core.Http;
using Microsoft.Owin;

namespace PeerCastStation.PCP
{
  public static class PCPRelayOwinApp
  {
    private static readonly Regex ChannelIdPattern = new Regex(@"\A([0-9a-fA-F]{32})(?:\.(\w+))?\z", RegexOptions.Compiled);
    private struct ParsedRequest
    {
      public HttpStatusCode Status;
      public Guid ChannelId;
      public string Extension;
      public bool IsValid {
        get { return Status==HttpStatusCode.OK; }
      }
      public static ParsedRequest Parse(IOwinContext ctx)
      {
        var req = new ParsedRequest();
        var components = (ctx.Request.Path.HasValue ? ctx.Request.Path.Value : "/").Split('/');
        if (components.Length>2) {
          req.Status = HttpStatusCode.NotFound;
          return req;
        }
        if (String.IsNullOrEmpty(components[1])) {
          req.Status = HttpStatusCode.Forbidden;
          return req;
        }
        var md = ChannelIdPattern.Match(components[1]);
        if (!md.Success) {
          req.Status = HttpStatusCode.NotFound;
          return req;
        }
        var channelId = Guid.Parse(md.Groups[1].Value);
        var ext = md.Groups[2].Success ? md.Groups[2].Value : null;
        req.Status = HttpStatusCode.OK;
        req.ChannelId = channelId;
        req.Extension = ext;
        return req;
      }
    }

    class PeerInfo
    {
      public IPEndPoint RemoteEndPoint { get; set; }
      public Host Host { get; set; }
      public string UserAgent { get; set; }
    }

    class ChannelSink
      : IChannelSink,
        IContentSink
    {
      public class ChannelMessage
      {
        public enum MessageType {
          Broadcast,
          ChannelInfo,
          ChannelTrack,
          ContentHeader,
          ContentBody,
          Overflow,
        }

        public Timestamp Timestamp;
        public MessageType Type;
        public Content Content;
        public IAtomCollection Data;

        public static ChannelMessage CreateBroadcast(Atom value)
        {
          var collection = new AtomCollection();
          collection.Add(value);
          return new ChannelMessage {
            Timestamp = Timestamp.Now,
            Type = MessageType.Broadcast,
            Content = null,
            Data = collection,
          };
        }

        public static ChannelMessage CreateChannelInfo(ChannelInfo value)
        {
          return new ChannelMessage {
            Timestamp = Timestamp.Now,
            Type = MessageType.ChannelInfo,
            Content = null,
            Data = value.Extra,
          };
        }

        public static ChannelMessage CreateChannelTrack(ChannelTrack value)
        {
          return new ChannelMessage {
            Timestamp = Timestamp.Now,
            Type = MessageType.ChannelTrack,
            Content = null,
            Data = value.Extra,
          };
        }

        public static ChannelMessage CreateContentHeader(Content value)
        {
          return new ChannelMessage {
            Timestamp = Timestamp.Now,
            Type = MessageType.ContentHeader,
            Content = value,
            Data = null,
          };
        }

        public static ChannelMessage CreateContentBody(Content value)
        {
          return new ChannelMessage {
            Timestamp = Timestamp.Now,
            Type = MessageType.ContentBody,
            Content = value,
            Data = null,
          };
        }

        public static ChannelMessage CreateOverflow()
        {
          return new ChannelMessage {
            Timestamp = Timestamp.Now,
            Type = MessageType.Overflow,
            Content = null,
            Data = null,
          };
        }
      }

      private PeerInfo peer;
      private Content lastContent = null;
      private WaitableQueue<ChannelMessage> queue = new WaitableQueue<ChannelMessage>();
      private ConnectionInfoBuilder connectionInfo = new ConnectionInfoBuilder();
      private Func<float> getRecvRate = null;
      private Func<float> getSendRate = null;
      private CancellationTokenSource channelStopped = new CancellationTokenSource();
      private bool overflow = false;
      public CancellationToken ChannelStopped { get { return channelStopped.Token; } }
      public StopReason StopReason { get; private set; } = StopReason.OffAir;
      public PeerInfo Peer {
        get { return peer; }
        set {
          peer = value;
          var builder = new ConnectionInfoBuilder(connectionInfo.Build());
          builder.AgentName = peer.UserAgent ?? "";
          builder.LocalDirects = peer.Host.DirectCount;
          builder.LocalRelays = peer.Host.RelayCount;
          builder.RemoteEndPoint = peer.RemoteEndPoint;
          builder.RemoteName = peer.RemoteEndPoint.ToString();
          builder.RemoteSessionID = peer.Host.SessionID;
          builder.RemoteHostStatus = RemoteHostStatus.Receiving;
          if (peer.RemoteEndPoint.Address.IsSiteLocal()) {
            builder.RemoteHostStatus |= RemoteHostStatus.Local;
          }
          if (peer.Host.IsFirewalled) {
            builder.RemoteHostStatus |= RemoteHostStatus.Firewalled;
          }
          if (peer.Host.IsRelayFull) {
            builder.RemoteHostStatus |= RemoteHostStatus.RelayFull;
          }
          if (peer.Host.IsReceiving) {
            builder.RemoteHostStatus |= RemoteHostStatus.Receiving;
          }
          connectionInfo = builder;
        }
      }

      public ChannelSink(PeerInfo peer, Stream stream)
      {
        this.peer = peer;
        connectionInfo.AgentName = peer.UserAgent ?? "";
        connectionInfo.LocalDirects = peer.Host.DirectCount;
        connectionInfo.LocalRelays = peer.Host.RelayCount;
        connectionInfo.ProtocolName = "PCP Relay";
        connectionInfo.RecvRate = null;
        connectionInfo.SendRate = null;
        connectionInfo.ContentPosition = 0;
        connectionInfo.RemoteEndPoint = peer.RemoteEndPoint;
        connectionInfo.RemoteName = peer.RemoteEndPoint.ToString();
        connectionInfo.RemoteSessionID = peer.Host.SessionID;
        connectionInfo.RemoteHostStatus = RemoteHostStatus.Receiving;
        if (peer.RemoteEndPoint.Address.IsSiteLocal()) {
          connectionInfo.RemoteHostStatus |= RemoteHostStatus.Local;
        }
        if (peer.Host.IsFirewalled) {
          connectionInfo.RemoteHostStatus |= RemoteHostStatus.Firewalled;
        }
        if (peer.Host.IsRelayFull) {
          connectionInfo.RemoteHostStatus |= RemoteHostStatus.RelayFull;
        }
        if (peer.Host.IsReceiving) {
          connectionInfo.RemoteHostStatus |= RemoteHostStatus.Receiving;
        }
        connectionInfo.Status = ConnectionStatus.Connected;
        connectionInfo.Type = ConnectionType.Relay;
        if (stream is ConnectionStream) {
          getRecvRate = () => ((ConnectionStream)stream).ReadRate;
          getSendRate = () => ((ConnectionStream)stream).WriteRate;
        }
      }

      public Task<ChannelMessage> DequeueAsync(CancellationToken ct)
      {
        return queue.DequeueAsync(ct);
      }

      private void Enqueue(ChannelMessage msg)
      {
        if (channelStopped.IsCancellationRequested) return;
        if (!queue.TryPeek(out var nxtMsg) || (msg.Timestamp-nxtMsg.Timestamp).TotalMilliseconds<=5000) {
          queue.Enqueue(msg);
        }
        else if (!overflow) {
          overflow = true;
          queue.Enqueue(ChannelMessage.CreateOverflow());
        }
      }

      public ConnectionInfo GetConnectionInfo()
      {
        var builder = connectionInfo;
        builder.RecvRate = getRecvRate?.Invoke();
        builder.SendRate = getSendRate?.Invoke();
        builder.ContentPosition = lastContent?.Position ?? 0;
        return builder.Build();
      }

      public void OnBroadcast(Host from, Atom packet)
      {
        if (from.SessionID==Peer.Host.SessionID) return;
        Enqueue(ChannelMessage.CreateBroadcast(packet));
      }

      public void OnStopped(StopReason reason)
      {
        StopReason = reason;
        channelStopped.Cancel();
      }

      public void OnChannelInfo(ChannelInfo channel_info)
      {
        Enqueue(ChannelMessage.CreateChannelInfo(channel_info));
      }

      public void OnChannelTrack(ChannelTrack channel_track)
      {
        Enqueue(ChannelMessage.CreateChannelTrack(channel_track));
      }

      public void OnContent(Content content)
      {
        if (lastContent!=null && lastContent.Stream==content.Stream) {
          lastContent = content;
          Enqueue(ChannelMessage.CreateContentBody(content));
        }
      }

      public void OnContentHeader(Content content_header)
      {
        lastContent = content_header;
        Enqueue(ChannelMessage.CreateContentHeader(content_header));
      }

      public void OnStop(StopReason reason)
      {
        StopReason = reason;
        channelStopped.Cancel();
      }
    }

    private class PCPRelayHandler
    {
      private PeerCast peerCast;
      private Channel channel;
      private Logger logger;

      public PCPRelayHandler(Channel channel, Logger logger)
      {
        this.peerCast = channel.PeerCast;
        this.channel = channel;
        this.logger = logger;
      }

      private class HandshakeErrorException
        : Exception
      {
        public StopReason QuitCode { get; private set; }
        public HandshakeErrorException(StopReason code)
        {
          QuitCode = code;
        }
      }

      private async Task<bool> PingHostAsync(IPEndPoint target, Guid remote_session_id, CancellationToken cancel_token)
      {
        logger.Debug("Ping requested. Try to ping: {0}({1})", target, remote_session_id);
        bool result = false;
        try {
          var client = new System.Net.Sockets.TcpClient(target.AddressFamily);
          client.ReceiveTimeout = 2000;
          client.SendTimeout    = 2000;
          await client.ConnectAsync(target.Address, target.Port).ConfigureAwait(false);
          var stream = client.GetStream();
          await stream.WriteAsync(new Atom(Atom.PCP_CONNECT, 1), cancel_token).ConfigureAwait(false);
          var helo = new AtomCollection();
          helo.SetHeloSessionID(peerCast.SessionID);
          await stream.WriteAsync(new Atom(Atom.PCP_HELO, helo), cancel_token).ConfigureAwait(false);
          var res = await stream.ReadAtomAsync(cancel_token).ConfigureAwait(false);
          if (res.Name==Atom.PCP_OLEH) {
            var session_id = res.Children.GetHeloSessionID();
            if (session_id.HasValue && session_id.Value==remote_session_id) {
              logger.Debug("Ping succeeded");
              result = true;
            }
            else {
              logger.Debug("Ping failed. Remote SessionID mismatched");
            }
          }
          await stream.WriteAsync(new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT), cancel_token).ConfigureAwait(false);
          stream.Close();
          client.Close();
        }
        catch (InvalidDataException e) {
          logger.Debug("Ping failed");
          logger.Debug(e);
        }
        catch (System.Net.Sockets.SocketException e) {
          logger.Debug("Ping failed");
          logger.Debug(e);
        }
        catch (EndOfStreamException e) {
          logger.Debug("Ping failed");
          logger.Debug(e);
        }
        catch (IOException e) {
          logger.Debug("Ping failed");
          logger.Debug(e);
          if (!(e.InnerException is System.Net.Sockets.SocketException)) {
            throw;
          }
        }
        return result;
      }

      private async Task<PeerInfo> OnHandshakePCPHelo(Stream stream, IPEndPoint remoteEndPoint, Atom atom, CancellationToken cancel_token)
      {
        logger.Debug("Helo received");
        var session_id = atom.Children.GetHeloSessionID();
        int remote_port = 0;
        PeerInfo peer = null;
        if (session_id!=null) {
          var host = new HostBuilder();
          host.SessionID = session_id.Value;
          var port = atom.Children.GetHeloPort();
          var ping = atom.Children.GetHeloPing();
          if (port!=null) {
            remote_port = port.Value;
          }
          else if (ping!=null) {
            if (!remoteEndPoint.Address.IsSiteLocal() &&
                await PingHostAsync(new IPEndPoint(remoteEndPoint.Address, ping.Value), session_id.Value, cancel_token).ConfigureAwait(false)) {
              remote_port = ping.Value;
            }
            else {
              remote_port = 0;
            }
          }
          else {
            remote_port = 0;
          }
          if (remote_port!=0)  {
            var ip = new IPEndPoint(remoteEndPoint.Address, remote_port);
            if (host.GlobalEndPoint==null || !host.GlobalEndPoint.Equals(ip)) {
              host.GlobalEndPoint = ip;
            }
          }
          host.IsFirewalled = remote_port==0;
          host.Extra.Update(atom.Children);
          peer = new PeerInfo { Host=host.ToHost(), RemoteEndPoint=remoteEndPoint, UserAgent=atom.Children.GetHeloAgent() };
        }
        var oleh = new AtomCollection();
        if (remoteEndPoint!=null && remoteEndPoint.AddressFamily==channel.NetworkAddressFamily) {
          oleh.SetHeloRemoteIP(remoteEndPoint.Address);
        }
        oleh.SetHeloAgent(peerCast.AgentName);
        oleh.SetHeloSessionID(peerCast.SessionID);
        oleh.SetHeloRemotePort(remote_port);
        PCPVersion.SetHeloVersion(oleh);
        await stream.WriteAsync(new Atom(Atom.PCP_OLEH, oleh), cancel_token).ConfigureAwait(false);
        return peer;
      }

      private async Task SendQuit(Stream stream, StopReason code, CancellationToken cancellationToken)
      {
        switch (code) {
        case StopReason.None:
          break;
        case StopReason.Any:
          await stream.WriteAsync(new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT), cancellationToken).ConfigureAwait(false);
          break;
        case StopReason.SendTimeoutError:
          await stream.WriteAsync(new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT + Atom.PCP_ERROR_SKIP), cancellationToken).ConfigureAwait(false);
          break;
        case StopReason.BadAgentError:
          await stream.WriteAsync(new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT + Atom.PCP_ERROR_BADAGENT), cancellationToken).ConfigureAwait(false);
          break;
        case StopReason.ConnectionError:
          await stream.WriteAsync(new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT + Atom.PCP_ERROR_READ), cancellationToken).ConfigureAwait(false);
          break;
        case StopReason.NotIdentifiedError:
          await stream.WriteAsync(new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT + Atom.PCP_ERROR_NOTIDENTIFIED), cancellationToken).ConfigureAwait(false);
          break;
        case StopReason.UnavailableError:
          await stream.WriteAsync(new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT + Atom.PCP_ERROR_UNAVAILABLE), cancellationToken).ConfigureAwait(false);
          break;
        case StopReason.OffAir:
          await stream.WriteAsync(new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT + Atom.PCP_ERROR_OFFAIR), cancellationToken).ConfigureAwait(false);
          break;
        case StopReason.UserShutdown:
          await stream.WriteAsync(new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT + Atom.PCP_ERROR_SHUTDOWN), cancellationToken).ConfigureAwait(false);
          break;
        }
      }

      private IEnumerable<Host> SelectSourceHosts(IPEndPoint endpoint)
      {
        var rnd = new Random();
        return channel.Nodes.OrderByDescending(n =>
          ( n.GlobalEndPoint!=null ? 16000 : 0) +
          ( n.GlobalEndPoint!=null &&
            n.GlobalEndPoint.Address.Equals(endpoint.Address) ? 8000 : 0) +
          (!n.IsRelayFull ? 4000 : 0) +
          ( n.IsReceiving ? 2000 : 0) +
          (Math.Max(10-n.Hops, 0)*100) +
          (n.RelayCount*10) +
          rnd.NextDouble()
        ).Take(8);
      }

      private async Task<PeerInfo> DoHandshake(Stream stream, IPEndPoint remoteEndPoint, bool isRelayFull, CancellationToken cancellationToken)
      {
        PeerInfo peer = null;
        while (peer==null) {
          //Handshakeが5秒以内に完了しなければ切る
          //HELOでセッションIDを受け取るまでは他のパケットは無視
          var atom = await stream.ReadAtomAsync(cancellationToken).ConfigureAwait(false);
          if (atom.Name==Atom.PCP_HELO) {
            peer = await OnHandshakePCPHelo(stream, remoteEndPoint, atom, cancellationToken).ConfigureAwait(false);
            if (peer==null) {
              logger.Info("Helo has no SessionID");
              //セッションIDが無かった
              throw new HandshakeErrorException(StopReason.NotIdentifiedError);
            }
            else if ((peer.Host.Extra.GetHeloVersion() ?? 0)<1200) {
              logger.Info("Helo version {0} is too old", peer.Host.Extra.GetHeloVersion() ?? 0);
              //クライアントバージョンが無かった、もしくは古すぎ
              throw new HandshakeErrorException(StopReason.BadAgentError);
            }
            else if (isRelayFull) {
              logger.Debug("Handshake succeeded {0}({1}) but relay is full", peer.Host.GlobalEndPoint, peer.Host.SessionID.ToString("N"));
              //次に接続するべきホストを送ってQUIT
              foreach (var node in SelectSourceHosts(remoteEndPoint)) {
                if (peer.Host.SessionID==node.SessionID) continue;
                await SendHost(stream, node, cancellationToken).ConfigureAwait(false);
              }
              throw new HandshakeErrorException(StopReason.UnavailableError);
            }
            else {
              logger.Debug("Handshake succeeded {0}({1})", peer.Host.GlobalEndPoint, peer.Host.SessionID.ToString("N"));
              await stream.WriteAsync(new Atom(Atom.PCP_OK, (int)1), cancellationToken).ConfigureAwait(false);
            }
          }
        }
        return peer;
      }

      private async Task SendHost(Stream stream, Host node, CancellationToken cancellationToken)
      {
        var host_atom = new AtomCollection(node.Extra);
        var ip = host_atom.FindByName(Atom.PCP_HOST_IP);
        while (ip!=null) {
          host_atom.Remove(ip);
          ip = host_atom.FindByName(Atom.PCP_HOST_IP);
        }
        var port = host_atom.FindByName(Atom.PCP_HOST_PORT);
        while (port!=null) {
          host_atom.Remove(port);
          port = host_atom.FindByName(Atom.PCP_HOST_PORT);
        }
        host_atom.SetHostSessionID(node.SessionID);
        var globalendpoint = node.GlobalEndPoint ?? new IPEndPoint(IPAddress.Any, 0);
        host_atom.AddHostIP(globalendpoint.Address);
        host_atom.AddHostPort(globalendpoint.Port);
        var localendpoint  = node.LocalEndPoint ?? new IPEndPoint(IPAddress.Any, 0);
        host_atom.AddHostIP(localendpoint.Address);
        host_atom.AddHostPort(localendpoint.Port);
        host_atom.SetHostNumRelays(node.RelayCount);
        host_atom.SetHostNumListeners(node.DirectCount);
        host_atom.SetHostChannelID(channel.ChannelID);
        host_atom.SetHostFlags1(
          (node.IsFirewalled ? PCPHostFlags1.Firewalled : PCPHostFlags1.None) |
          (node.IsTracker ? PCPHostFlags1.Tracker : PCPHostFlags1.None) |
          (node.IsRelayFull ? PCPHostFlags1.None : PCPHostFlags1.Relay) |
          (node.IsDirectFull ? PCPHostFlags1.None : PCPHostFlags1.Direct) |
          (node.IsReceiving ? PCPHostFlags1.Receiving : PCPHostFlags1.None) |
          (node.IsControlFull ? PCPHostFlags1.None : PCPHostFlags1.ControlIn));
        await stream.WriteAsync(new Atom(Atom.PCP_HOST, host_atom), cancellationToken).ConfigureAwait(false);
        logger.Debug("Sending Node: {0}({1})", globalendpoint, node.SessionID.ToString("N"));
      }

      private Task OnPCPHelo(Stream stream, ChannelSink sink, Atom atom, CancellationToken cancel_token)
      {
        return Task.Delay(0);
      }

      private Task OnPCPOleh(Stream stream, ChannelSink sink, Atom atom, CancellationToken cancel_token)
      {
        return Task.Delay(0);
      }

      private Task OnPCPOk(Stream stream, ChannelSink sink, Atom atom, CancellationToken cancel_token)
      {
        return Task.Delay(0);
      }

      private Task OnPCPChan(Stream stream, ChannelSink sink, Atom atom, CancellationToken cancel_token)
      {
        return Task.Delay(0);
      }

      private Task OnPCPChanPkt(Stream stream, ChannelSink sink, Atom atom, CancellationToken cancel_token)
      {
        return Task.Delay(0);
      }

      private Task OnPCPChanInfo(Stream stream, ChannelSink sink, Atom atom, CancellationToken cancel_token)
      {
        return Task.Delay(0);
      }

      private Task OnPCPChanTrack(Stream stream, ChannelSink sink, Atom atom, CancellationToken cancel_token)
      {
        return Task.Delay(0);
      }

      private async Task OnPCPBcst(Stream stream, ChannelSink sink, Atom atom, CancellationToken cancel_token)
      {
        var dest = atom.Children.GetBcstDest();
        var ttl = atom.Children.GetBcstTTL();
        var hops = atom.Children.GetBcstHops();
        var from = atom.Children.GetBcstFrom();
        var group = atom.Children.GetBcstGroup();
        if (ttl != null &&
            hops != null &&
            group != null &&
            from != null &&
            dest != peerCast.SessionID &&
            ttl>0) {
          logger.Debug("Relaying BCST TTL: {0}, Hops: {1}", ttl, hops);
          var bcst = new AtomCollection(atom.Children);
          bcst.SetBcstTTL((byte)(ttl - 1));
          bcst.SetBcstHops((byte)(hops + 1));
          channel.Broadcast(sink.Peer.Host, new Atom(atom.Name, bcst), group.Value);
        }
        if (dest==null || dest==peerCast.SessionID) {
          logger.Debug("Processing BCST({0})", dest==null ? "(null)" : dest.Value.ToString("N"));
          foreach (var c in atom.Children) {
            await ProcessAtom(stream, sink, c, cancel_token).ConfigureAwait(false);
          }
        }
      }

      private Task OnPCPHost(Stream stream, ChannelSink sink, Atom atom, CancellationToken cancel_token)
      {
        var session_id = atom.Children.GetHostSessionID();
        if (session_id!=null) {
          var node = channel.Nodes.FirstOrDefault(x => x.SessionID.Equals(session_id));
          var host = new HostBuilder(node);
          if (node==null) {
            host.SessionID = (Guid)session_id;
          }
          host.Extra.Update(atom.Children);
          host.DirectCount = atom.Children.GetHostNumListeners() ?? 0;
          host.RelayCount = atom.Children.GetHostNumRelays() ?? 0;
          var flags1 = atom.Children.GetHostFlags1();
          if (flags1 != null) {
            host.IsFirewalled  = (flags1.Value & PCPHostFlags1.Firewalled) != 0;
            host.IsTracker     = (flags1.Value & PCPHostFlags1.Tracker) != 0;
            host.IsRelayFull   = (flags1.Value & PCPHostFlags1.Relay) == 0;
            host.IsDirectFull  = (flags1.Value & PCPHostFlags1.Direct) == 0;
            host.IsReceiving   = (flags1.Value & PCPHostFlags1.Receiving) != 0;
            host.IsControlFull = (flags1.Value & PCPHostFlags1.ControlIn) == 0;
          }

          var endpoints = atom.Children.GetHostEndPoints();
          if (endpoints.Length>0 && (host.GlobalEndPoint==null || !host.GlobalEndPoint.Equals(endpoints[0]))) {
            host.GlobalEndPoint = endpoints[0];
          }
          if (endpoints.Length>1 && (host.LocalEndPoint==null || !host.LocalEndPoint.Equals(endpoints[1]))) {
            host.LocalEndPoint = endpoints[1];
          }
          logger.Debug("Updating Node: {0}/{1}({2})", host.GlobalEndPoint, host.LocalEndPoint, host.SessionID.ToString("N"));
          channel.AddNode(host.ToHost());
          if (sink.Peer.Host.SessionID==host.SessionID) {
            sink.Peer = new PeerInfo { Host=host.ToHost(), UserAgent=sink.Peer.UserAgent, RemoteEndPoint=sink.Peer.RemoteEndPoint };
          }
        }
        return Task.Delay(0);
      }

      private Task OnPCPQuit(Stream stream, ChannelSink sink, Atom atom, CancellationToken cancel_token)
      {
        logger.Debug("Quit Received: {0}", atom.GetInt32());
        sink.OnStop(StopReason.None);
        return Task.Delay(0);
      }

      private async Task ProcessAtom(Stream stream, ChannelSink sink, Atom atom, CancellationToken cancel_token)
      {
             if (atom.Name==Atom.PCP_HELO)       await OnPCPHelo(stream, sink, atom, cancel_token).ConfigureAwait(false);
        else if (atom.Name==Atom.PCP_OLEH)       await OnPCPOleh(stream, sink, atom, cancel_token).ConfigureAwait(false);
        else if (atom.Name==Atom.PCP_OK)         await OnPCPOk(stream, sink, atom, cancel_token).ConfigureAwait(false);
        else if (atom.Name==Atom.PCP_CHAN)       await OnPCPChan(stream, sink, atom, cancel_token).ConfigureAwait(false);
        else if (atom.Name==Atom.PCP_CHAN_PKT)   await OnPCPChanPkt(stream, sink, atom, cancel_token).ConfigureAwait(false);
        else if (atom.Name==Atom.PCP_CHAN_INFO)  await OnPCPChanInfo(stream, sink, atom, cancel_token).ConfigureAwait(false);
        else if (atom.Name==Atom.PCP_CHAN_TRACK) await OnPCPChanTrack(stream, sink, atom, cancel_token).ConfigureAwait(false);
        else if (atom.Name==Atom.PCP_BCST)       await OnPCPBcst(stream, sink, atom, cancel_token).ConfigureAwait(false);
        else if (atom.Name==Atom.PCP_HOST)       await OnPCPHost(stream, sink, atom, cancel_token).ConfigureAwait(false);
        else if (atom.Name==Atom.PCP_QUIT)       await OnPCPQuit(stream, sink, atom, cancel_token).ConfigureAwait(false);
      }

      private async Task ReadAndProcessAtom(Stream stream, ChannelSink sink, CancellationToken cancellationToken)
      {
        while (!cancellationToken.IsCancellationRequested) {
          var atom = await stream.ReadAtomAsync(cancellationToken).ConfigureAwait(false);
          await ProcessAtom(stream, sink, atom, cancellationToken).ConfigureAwait(false);
        }
      }

      private async Task BcstChannelInfo(Stream stream, IAtomCollection channelInfo, IAtomCollection channelTrack, CancellationToken cancellationToken)
      {
        var chan = new AtomCollection();
        chan.SetChanID(channel.ChannelID);
        if (channelInfo!=null) chan.SetChanInfo(channelInfo);
        if (channelTrack!=null) chan.SetChanTrack(channelTrack);
        var bcst = new AtomCollection();
        bcst.SetBcstFrom(peerCast.SessionID);
        bcst.SetBcstGroup(BroadcastGroup.Relays);
        bcst.SetBcstHops(0);
        bcst.SetBcstTTL(11);
        PCPVersion.SetBcstVersion(bcst);
        bcst.SetBcstChannelID(channel.ChannelID);
        bcst.Add(new Atom(Atom.PCP_CHAN, chan));
        await stream.WriteAsync(new Atom(Atom.PCP_BCST, bcst), cancellationToken).ConfigureAwait(false);
      }

      private Atom CreateContentBodyPacket(Guid channelId, long pos, IEnumerable<byte> data, PCPChanPacketContinuation contFlag)
      {
        var chan = new AtomCollection();
        chan.SetChanID(channelId);
        var chan_pkt = new AtomCollection();
        chan_pkt.SetChanPktType(Atom.PCP_CHAN_PKT_DATA);
        chan_pkt.SetChanPktPos((uint)(pos & 0xFFFFFFFFU));
        if (contFlag!=PCPChanPacketContinuation.None) {
          chan_pkt.SetChanPktCont(contFlag);
        }
        chan_pkt.SetChanPktData(data.ToArray());
        chan.SetChanPkt(chan_pkt);
        return new Atom(Atom.PCP_CHAN, chan);
      }

      public static readonly int MaxBodyLength = 15*1024;
      protected IEnumerable<Atom> CreateContentBodyPacket(Guid channelId, Content content)
      {
        if (content.Data.Length>MaxBodyLength) {
          return Enumerable.Range(0, (content.Data.Length+MaxBodyLength-1)/MaxBodyLength)
            .Select(i =>
              CreateContentBodyPacket(
                channelId,
                i*MaxBodyLength+content.Position,
                content.Data.Skip(i*MaxBodyLength).Take(MaxBodyLength),
                content.ContFlag | (i==0 ? PCPChanPacketContinuation.None : PCPChanPacketContinuation.Fragment)
              )
            );
        }
        else {
          return Enumerable.Repeat(CreateContentBodyPacket(channelId, content.Position, content.Data, content.ContFlag), 1);
        }
      }

      private async Task SendRelayBody(Stream stream, ChannelSink sink, CancellationToken cancellationToken)
      {
        Content lastHeader = null;
        IAtomCollection channelInfo = null;
        IAtomCollection channelTrack = null;
        while (!cancellationToken.IsCancellationRequested) {
          var msg = await sink.DequeueAsync(cancellationToken).ConfigureAwait(false);
          switch (msg.Type) {
          case ChannelSink.ChannelMessage.MessageType.Broadcast:
            foreach (var atom in msg.Data) {
              await stream.WriteAsync(atom, cancellationToken).ConfigureAwait(false);
            }
            break;
          case ChannelSink.ChannelMessage.MessageType.ChannelInfo:
            channelInfo = msg.Data;
            if (channel.IsBroadcasting && lastHeader!=null) {
              await BcstChannelInfo(stream, channelInfo, channelTrack, cancellationToken).ConfigureAwait(false);
            }
            break;
          case ChannelSink.ChannelMessage.MessageType.ChannelTrack:
            channelTrack = msg.Data;
            if (channel.IsBroadcasting && lastHeader!=null) {
              await BcstChannelInfo(stream, channelInfo, channelTrack, cancellationToken).ConfigureAwait(false);
            }
            break;
          case ChannelSink.ChannelMessage.MessageType.ContentHeader:
            {
              lastHeader = msg.Content;
              var chan = new AtomCollection();
              chan.SetChanID(channel.ChannelID);
              var chan_pkt = new AtomCollection();
              chan_pkt.SetChanPktType(Atom.PCP_CHAN_PKT_HEAD);
              chan_pkt.SetChanPktPos((uint)(msg.Content.Position & 0xFFFFFFFFU));
              chan_pkt.SetChanPktData(msg.Content.Data);
              chan.SetChanPkt(chan_pkt);
              if (channelInfo!=null) chan.SetChanInfo(channelInfo);
              if (channelTrack!=null) chan.SetChanTrack(channelTrack);
              logger.Debug("Sending Header: {0}", msg.Content.Position);
              await stream.WriteAsync(new Atom(Atom.PCP_CHAN, chan), cancellationToken).ConfigureAwait(false);
            }
            break;
          case ChannelSink.ChannelMessage.MessageType.ContentBody:
            foreach (var atom in CreateContentBodyPacket(channel.ChannelID, msg.Content)) {
              await stream.WriteAsync(atom, cancellationToken).ConfigureAwait(false);
            }
            break;
          case ChannelSink.ChannelMessage.MessageType.Overflow:
            logger.Debug("Send Timedout");
            sink.OnStop(StopReason.SendTimeoutError);
            break;
          }
        }
      }

      private byte[] CreateRelayResponse(bool isRelayFull)
      {
        var mem = new MemoryStream();
        using (var s=new StreamWriter(mem, new System.Text.UTF8Encoding(false))) {
          var chaninfo = channel.ChannelInfo;
          s.NewLine = "\r\n";
          if (isRelayFull) {
            s.WriteLine($"HTTP/1.0 503 {HttpReasonPhrase.ServiceUnavailable}");
          }
          else {
            s.WriteLine($"HTTP/1.0 200 {HttpReasonPhrase.OK}");
          }
          s.WriteLine($"Server: {peerCast.AgentName}");
          s.WriteLine("Accept-Ranges: none");
          s.WriteLine($"x-audiocast-name: {chaninfo.Name}");
          s.WriteLine($"x-audiocast-bitrate: {chaninfo.Bitrate}");
          s.WriteLine($"x-audiocast-genre: {chaninfo.Genre ?? ""}");
          s.WriteLine($"x-audiocast-description: {chaninfo.Desc ?? ""}");
          s.WriteLine($"x-audiocast-url: {chaninfo.URL ?? ""}");
          s.WriteLine($"x-peercast-channelid: {channel.ChannelID.ToString("N").ToUpperInvariant()}");
          s.WriteLine("Content-Type:application/x-peercast-pcp");
          s.WriteLine();
        }
        return mem.ToArray();
      }

      public async Task ProcessStream(Stream stream, IPEndPoint remoteEndPoint, long requestPos, CancellationToken cancellationToken)
      {
        var isRelayFull = !channel.MakeRelayable(remoteEndPoint.Address.IsSiteLocal());
        PeerInfo peer = null;
        try {
          await stream.WriteBytesAsync(CreateRelayResponse(isRelayFull), cancellationToken).ConfigureAwait(false);
          using (var handshakeCT=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)) {
            handshakeCT.CancelAfter(5000);
            try {
              peer = await DoHandshake(stream, remoteEndPoint, isRelayFull, handshakeCT.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
              if (!cancellationToken.IsCancellationRequested) {
                logger.Info("Handshake timed out.");
                throw new HandshakeErrorException(StopReason.BadAgentError);
              }
              else {
                throw;
              }
            }
          }
          var sink = new ChannelSink(peer, stream);
          try {
            using (var cts=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, sink.ChannelStopped))
            using (channel.AddOutputStream(sink))
            using (channel.AddContentSink(sink, requestPos)) {
              await Task.WhenAll(
                ReadAndProcessAtom(stream, sink, cts.Token),
                SendRelayBody(stream, sink, cts.Token)
              ).ConfigureAwait(false);
            }
          }
          catch (OperationCanceledException) {
          }
          await BeforeQuitAsync(stream, sink, cancellationToken).ConfigureAwait(false);
          await SendQuit(stream, sink.StopReason, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) {
          await SendQuit(stream, StopReason.OffAir, cancellationToken).ConfigureAwait(false);
        }
        catch (HandshakeErrorException e) {
          await SendQuit(stream, e.QuitCode, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidDataException e) {
          await SendQuit(stream, StopReason.NotIdentifiedError, cancellationToken).ConfigureAwait(false);
          logger.Info(e);
        }
        catch (IOException e) {
          logger.Info(e);
        }
      }

      public async Task BeforeQuitAsync(Stream stream, ChannelSink channelSink, CancellationToken cancellationToken)
      {
        if (channelSink.StopReason==StopReason.UnavailableError) {
          //次に接続するべきホストを送ってQUIT
          foreach (var node in SelectSourceHosts(channelSink.Peer.RemoteEndPoint)) {
            if (channelSink.Peer.Host.SessionID==node.SessionID) continue;
            await SendHost(stream, node, cancellationToken).ConfigureAwait(false);
          }
        }
      }

      public static Task Invoke(IOwinContext ctx)
      {
        var remoteEndPoint = ctx.Request.GetRemoteEndPoint();
        var logger = new Logger(typeof(PCPRelayOwinApp), remoteEndPoint?.ToString() ?? "");
        var req = ParsedRequest.Parse(ctx);
        if (!req.IsValid) {
          ctx.Response.StatusCode = (int)req.Status;
          return Task.Delay(0);
        }
        var channel = ctx.GetPeerCast().Channels.FirstOrDefault(ch => ch.ChannelID==req.ChannelId);
        if (channel==null || channel.Status!=SourceStreamStatus.Receiving) {
          ctx.Response.StatusCode = (int)HttpStatusCode.NotFound;
          return Task.Delay(0);
        }
        if (!ctx.Request.GetPCPVersion().HasValue) {
          ctx.Response.StatusCode = (int)HttpStatusCode.BadRequest;
          return Task.Delay(0);
        }
        if (remoteEndPoint!=null &&
            !(channel.Network==NetworkType.IPv6 && ctx.Request.GetPCPVersion()==PCPVersion.ProtocolVersionIPv6 && remoteEndPoint.Address.AddressFamily==System.Net.Sockets.AddressFamily.InterNetworkV6) &&
            !(channel.Network==NetworkType.IPv4 && ctx.Request.GetPCPVersion()==PCPVersion.ProtocolVersionIPv4 && remoteEndPoint.Address.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork)) {
          ctx.Response.StatusCode = (int)HttpStatusCode.NotFound;
          return Task.Delay(0);
        }
        var requestPos = ctx.Request.GetPCPPos() ?? -1;
        ctx.Upgrade(async opaqueEnv => {
          var ct = (CancellationToken)opaqueEnv[OwinEnvironment.Opaque.CallCancelled];
          var stream = (Stream)opaqueEnv[OwinEnvironment.Opaque.Stream];
          stream.ReadTimeout = Timeout.Infinite;
          var handler = new PCPRelayHandler(channel, logger);
          await handler.ProcessStream(stream, remoteEndPoint, requestPos, ct).ConfigureAwait(false);
        });
        return Task.Delay(0);
      }
    }

    public static void BuildApp(IAppBuilder builder)
    {
      builder.Map("/channel", sub => {
        sub.MapMethod("GET", withmethod => {
          withmethod.Run(PCPRelayHandler.Invoke);
        });
      });
    }

  }

  [Plugin]
  class PCPOutputStreamPlugin
    : PluginBase
  {
    override public string Name { get { return "PCP Output"; } }

    private IDisposable appRegistration;
    override protected void OnAttach()
    {
      var owin = Application.Plugins.OfType<OwinHostPlugin>().FirstOrDefault();
      appRegistration = owin?.OwinHost?.Register(PCPRelayOwinApp.BuildApp);
    }

    override protected void OnDetach()
    {
      appRegistration?.Dispose();
      appRegistration = null;
    }

  }

}
