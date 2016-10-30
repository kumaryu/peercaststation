using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using PeerCastStation.Core;

namespace PeerCastStation.FLV.RTMP
{
  public class RTMPSourceStreamFactory
    : SourceStreamFactoryBase
  {
    public RTMPSourceStreamFactory(PeerCast peercast)
      : base(peercast)
    {
    }

    public override string Name {
      get { return "RTMP Source"; }
    }

    public override string Scheme {
      get { return "rtmp"; }
    }

    public override SourceStreamType Type {
      get { return SourceStreamType.Broadcast; }
    }

    public override Uri DefaultUri {
      get { return new Uri("rtmp://localhost/live/livestream"); }
    }

    public override bool IsContentReaderRequired {
      get { return false; }
    }

    public override ISourceStream Create(Channel channel, Uri source, IContentReader reader)
    {
      return new RTMPSourceStream(PeerCast, channel, source);
    }
  }

  public class RTMPSourceConnection
    : SourceConnectionBase
  {
    public RTMPSourceConnection(PeerCast peercast, Channel channel, Uri source_uri, bool use_content_bitrate)
      : base(peercast, channel, source_uri)
    {
      this.flvBuffer = new FLVContentBuffer(channel, new ChannelContentSink(channel, use_content_bitrate));
      this.useContentBitrate = use_content_bitrate;
    }

    private class ConnectionStoppedExcception : ApplicationException {}
    private class BindErrorException
      : ApplicationException
    {
      public BindErrorException(string message)
        : base(message)
      {
      }
    }
    private FLVContentBuffer flvBuffer;
    private bool useContentBitrate;

    public override ConnectionInfo GetConnectionInfo()
    {
      ConnectionStatus status;
      switch (state) {
      case ConnectionState.Waiting:   status = ConnectionStatus.Connecting; break;
      case ConnectionState.Connected: status = ConnectionStatus.Connecting; break;
      case ConnectionState.Receiving: status = ConnectionStatus.Connected; break;
      case ConnectionState.Error:     status = ConnectionStatus.Error; break;
      default:                        status = ConnectionStatus.Idle; break;
      }
      IPEndPoint endpoint = null;
      if (connection!=null) {
        endpoint = connection.RemoteEndPoint;
      }
      return new ConnectionInfo(
        "RTMP Source",
        ConnectionType.Source,
        status,
        SourceUri.ToString(),
        endpoint,
        (endpoint!=null && endpoint.Address.IsSiteLocal()) ? RemoteHostStatus.Local : RemoteHostStatus.None,
        flvBuffer.Position,
        RecvRate,
        SendRate,
        null,
        null,
        clientName);
    }

    private enum ConnectionState {
      Waiting,
      Connected,
      Receiving,
      Error,
      Closed,
    };
    private ConnectionState state = ConnectionState.Waiting;
    private string clientName = "";

    private IEnumerable<IPEndPoint> GetBindAddresses(Uri uri)
    {
      IEnumerable<IPAddress> addresses;
      if (uri.HostNameType==UriHostNameType.IPv4 ||
          uri.HostNameType==UriHostNameType.IPv6) {
        addresses = new IPAddress[] { IPAddress.Parse(uri.Host) };
      }
      else {
        try {
          addresses = Dns.GetHostAddresses(uri.DnsSafeHost);
        }
        catch (SocketException) {
          return Enumerable.Empty<IPEndPoint>();
        }
      }
      return addresses.Select(addr => new IPEndPoint(addr, uri.Port<0 ? 1935 : uri.Port));
    }

    protected override async Task<SourceConnectionClient> DoConnect(Uri source, CancellationToken cancellationToken)
    {
      TcpClient client = null;
      var bind_addr = GetBindAddresses(source);
      if (bind_addr.Count()==0) {
        this.state = ConnectionState.Error;
        throw new BindErrorException(String.Format("Cannot resolve bind address: {0}", source.DnsSafeHost));
      }
      var listeners = bind_addr.Select(addr => new TcpListener(addr)).ToArray();
      try {
        var cancel_task = cancellationToken.CreateCancelTask<TcpClient>();
        var tasks = listeners.Select(listener => {
          listener.Start(1);
          Logger.Debug("Listening on {0}", listener.LocalEndpoint);
          return listener.AcceptTcpClientAsync();
        }).Concat(Enumerable.Repeat(cancel_task, 1)).ToArray();
        var result = await Task.WhenAny(tasks);
        if (!result.IsCanceled) {
          client = result.Result;
          Logger.Debug("Client accepted");
        }
        else {
          Logger.Debug("Listen cancelled");
        }
      }
      catch (SocketException) {
        this.state = ConnectionState.Error;
        throw new BindErrorException(String.Format("Cannot bind address: {0}", bind_addr));
      }
      finally {
        foreach (var listener in listeners) {
          listener.Stop();
        }
      }
      if (client!=null) {
        return new SourceConnectionClient(client);
      }
      else {
        return null;
      }
    }

    protected override async Task DoProcess(CancellationToken cancellationToken)
    {
      this.state = ConnectionState.Waiting;
      try {
        await Handshake(cancellationToken);
        await ProcessRTMPMessages(cancellationToken);
        this.state = ConnectionState.Closed;
      }
      catch (BindErrorException e) {
        Logger.Error(e);
        Stop(StopReason.NoHost);
        this.state = ConnectionState.Error;
      }
      catch (IOException e) {
        Logger.Error(e);
        Stop(StopReason.ConnectionError);
        this.state = ConnectionState.Error;
      }
      catch (ConnectionStoppedExcception) {
        this.state = ConnectionState.Closed;
      }
    }

    protected async Task ProcessRTMPMessages(CancellationToken cancel_token)
    {
      this.state = ConnectionState.Connected;
      var messages = new Queue<RTMPMessage>();
      while (!cancel_token.IsCancellationRequested && 
             await RecvMessage(messages, cancel_token)) {
        await ProcessMessages(messages, cancel_token);
        messages.Clear();
      }
    }

    protected override void DoPost(Host from, Atom packet)
    {
      //Do nothing
    }

    System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
    private async Task<bool> Handshake(CancellationToken cancel_token)
    {
      Logger.Debug("Handshake start");
      var rand = new Random();
      var c0 = await RecvAsync(1, cancel_token);
      await connection.Stream.WriteByteAsync(0x03, cancel_token);
      var s1vec = new byte[1528];
      rand.NextBytes(s1vec);
      await SendAsync(writer => {
        writer.Write(0);
        writer.Write(0);
        writer.Write(s1vec);
      }, cancel_token);
      using (var reader=await RecvAsync(1536, cancel_token)) {
        await SendAsync(writer => {
          writer.Write(reader.ReadInt32());
          writer.Write(reader.ReadInt32());
          writer.Write(reader.ReadBytes(1528));
        }, cancel_token);
      }
      using (var reader=await RecvAsync(1536, cancel_token)) {
        reader.ReadInt32();
        reader.ReadInt32();
        if (!s1vec.SequenceEqual(reader.ReadBytes(1528))) {
          Logger.Debug("Handshake failed");
          return false;
        }
      }
      timer.Reset();
      timer.Start();
      Logger.Debug("Handshake succeeded");
      return true;
    }

    private long Now {
      get { return timer.ElapsedMilliseconds; }
    }

    int nextClientId    = 1;
    int nextStreamId    = 1;
    int objectEncoding  = 0;
    int sendChunkSize   = 128;
    int recvChunkSize   = 128;
    long sendWindowSize = 0x7FFFFFFF;
    PeerBandwidthLimitType sendWindowLimitType = PeerBandwidthLimitType.Hard;
    long recvWindowSize = 0x7FFFFFFF;
    long receivedSize   = 0;
    long sequenceNumber = 0;
    private async Task<int> RecvStream(byte[] buf, int offset, int len, CancellationToken cancel_token)
    {
      if (len+receivedSize>=recvWindowSize) {
        var len1 = (int)(recvWindowSize-receivedSize);
        await connection.Stream.ReadBytesAsync(buf, offset, len1, cancel_token);
        receivedSize   += len1;
        sequenceNumber += len1;
        await SendMessage(2, new AckMessage(this.Now, 0, sequenceNumber), cancel_token);
        var len2 = len - len1;
        await connection.Stream.ReadBytesAsync(buf, offset+len1, len2, cancel_token);
        receivedSize    = len2; //reset
        sequenceNumber += len2;
      }
      else {
        await connection.Stream.ReadBytesAsync(buf, offset, len, cancel_token);
        receivedSize   += len;
        sequenceNumber += len;
      }
      return len;
    }

    private async Task<byte[]> RecvStream(int len, CancellationToken cancel_token)
    {
      var buf = new byte[len];
      await RecvStream(buf, 0, len, cancel_token);
      return buf;
    }

    private class RTMPMessageBuilder
    {
      public long Timestamp            { get; set; }
      public long TimestampDelta       { get; set; }
      public int  TypeId               { get; set; }
      public long ChunkMessageStreamId { get; set; }
      public int  BodyLength           { get; set; }
      public int  ReceivedLength       { get; set; }
      public byte[] Body               { get; set; }

      public static readonly RTMPMessageBuilder NullPacket = new RTMPMessageBuilder(null, 0, 0, 0, 0);

      public RTMPMessageBuilder(
        RTMPMessageBuilder x,
        long timestamp,
        int  type_id,
        long chunk_message_stream_id,
        int  body_length)
      {
        Timestamp            = timestamp;
        TimestampDelta       = x!=null ? timestamp-x.Timestamp : 0;
        TypeId               = type_id;
        ChunkMessageStreamId = chunk_message_stream_id;
        BodyLength           = body_length;
        ReceivedLength       = 0;
        Body                 = new byte[BodyLength];
      }

      public RTMPMessageBuilder(
        RTMPMessageBuilder x,
        long timestamp_delta,
        int  type_id,
        int  body_length)
      {
        Timestamp            = x.Timestamp + timestamp_delta;
        TimestampDelta       = timestamp_delta;
        TypeId               = type_id;
        ChunkMessageStreamId = x.ChunkMessageStreamId;
        BodyLength           = body_length;
        ReceivedLength       = 0;
        Body                 = new byte[BodyLength];
      }

      public RTMPMessageBuilder(
        RTMPMessageBuilder x,
        long timestamp_delta)
      {
        Timestamp            = x.Timestamp + timestamp_delta;
        TimestampDelta       = timestamp_delta;
        TypeId               = x.TypeId;
        ChunkMessageStreamId = x.ChunkMessageStreamId;
        BodyLength           = x.BodyLength;
        ReceivedLength       = 0;
        Body                 = new byte[BodyLength];
      }

      public RTMPMessageBuilder(RTMPMessageBuilder x)
      {
        Timestamp            = x.Timestamp + x.TimestampDelta;
        TypeId               = x.TypeId;
        ChunkMessageStreamId = x.ChunkMessageStreamId;
        BodyLength           = x.BodyLength;
        ReceivedLength       = 0;
        Body                 = new byte[BodyLength];
      }

      public RTMPMessage ToMessage()
      {
        return new RTMPMessage((RTMPMessageType)TypeId, Timestamp, ChunkMessageStreamId, Body);
      }

      public void Abort()
      {
        this.ReceivedLength = this.BodyLength;
      }
    }

    private Dictionary<int, RTMPMessageBuilder> lastMessages = new Dictionary<int,RTMPMessageBuilder>();
    private async Task<bool> RecvMessage(Queue<RTMPMessage> messages, CancellationToken cancel_token)
    {
      var basic_header = (await RecvStream(1, cancel_token))[0];
      var chunk_stream_id = basic_header & 0x3F;
      if (chunk_stream_id==0) {
        chunk_stream_id = (await RecvStream(1, cancel_token))[0] + 64;
      }
      else if (chunk_stream_id==1) {
        var buf = await RecvStream(2, cancel_token);
        chunk_stream_id = (buf[1]*256 | buf[0]) + 64;
      }

      RTMPMessageBuilder msg = null;
      RTMPMessageBuilder last_msg = null;
      if (!lastMessages.TryGetValue(chunk_stream_id, out last_msg)) {
        last_msg = RTMPMessageBuilder.NullPacket;
      }
      switch ((basic_header & 0xC0)>>6) {
      case 0:
        using (var reader=new RTMPBinaryReader(await RecvStream(11, cancel_token))) {
          long timestamp  = reader.ReadUInt24();
          var body_length = reader.ReadUInt24();
          var type_id     = reader.ReadByte();
          var stream_id   = reader.ReadUInt32LE();
          if (timestamp==0xFFFFFF) {
            using (var ext_reader=new RTMPBinaryReader(await RecvStream(4, cancel_token))) {
              timestamp = ext_reader.ReadUInt32();
            }
          }
          msg = new RTMPMessageBuilder(
            last_msg,
            timestamp,
            type_id,
            stream_id,
            body_length);
          lastMessages[chunk_stream_id] = msg;
        }
        break;
      case 1:
        using (var reader=new RTMPBinaryReader(await RecvStream(7, cancel_token))) {
          long timestamp_delta = reader.ReadUInt24();
          var body_length      = reader.ReadUInt24();
          var type_id          = reader.ReadByte();
          if (timestamp_delta==0xFFFFFF) {
            using (var ext_reader=new RTMPBinaryReader(await RecvStream(4, cancel_token))) {
              timestamp_delta = ext_reader.ReadUInt32();
            }
          }
          msg = new RTMPMessageBuilder(
            last_msg,
            timestamp_delta,
            type_id,
            body_length);
          lastMessages[chunk_stream_id] = msg;
        }
        break;
      case 2:
        using (var reader=new RTMPBinaryReader(await RecvStream(3, cancel_token))) {
          long timestamp_delta = reader.ReadUInt24();
          if (timestamp_delta==0xFFFFFF) {
            using (var ext_reader=new RTMPBinaryReader(await RecvStream(4, cancel_token))) {
              timestamp_delta = ext_reader.ReadUInt32();
            }
          }
          msg = new RTMPMessageBuilder(last_msg, timestamp_delta);
          lastMessages[chunk_stream_id] = msg;
        }
        break;
      case 3:
        msg = last_msg;
        if (msg.ReceivedLength>=msg.BodyLength) {
          msg = new RTMPMessageBuilder(last_msg);
          lastMessages[chunk_stream_id] = msg;
        }
        break;
      }

      msg.ReceivedLength += await RecvStream(
        msg.Body,
        msg.ReceivedLength,
        Math.Min(recvChunkSize, msg.BodyLength-msg.ReceivedLength),
        cancel_token);
      if (msg.ReceivedLength>=msg.BodyLength) {
        messages.Enqueue(msg.ToMessage());
      }
      return true; //TODO:接続エラー時はfalseを返す
    }

    private async Task SendMessage(int chunk_stream_id, RTMPMessage msg, CancellationToken cancel_token)
    {
      int offset = 0;
      int fmt = 0;
      while (msg.Body.Length-offset>0) {
        switch (fmt) {
        case 0:
          await SendAsync(writer => {
            writer.Write((byte)((fmt<<6) | chunk_stream_id));
            if (msg.Timestamp>0xFFFFFF) {
              writer.WriteUInt24(0xFFFFFF);
            }
            else {
              writer.WriteUInt24((int)msg.Timestamp);
            }
            writer.WriteUInt24(msg.Body.Length);
            writer.Write((byte)msg.MessageType);
            writer.WriteUInt32LE(msg.StreamId);
            if (msg.Timestamp>0xFFFFFF) {
              writer.WriteUInt32(msg.Timestamp);
            }
            int chunk_len = Math.Min(sendChunkSize, msg.Body.Length-offset);
            writer.Write(msg.Body, offset, chunk_len);
            offset += chunk_len;
          }, cancel_token);
          fmt = 3;
          break;
        case 3:
          await SendAsync(writer => {
            writer.Write((byte)((fmt<<6) | chunk_stream_id));
            int chunk_len = Math.Min(sendChunkSize, msg.Body.Length-offset);
            writer.Write(msg.Body, offset, chunk_len);
            offset += chunk_len;
          }, cancel_token);
          break;
        }
      }
    }

    private async Task ProcessMessages(IEnumerable<RTMPMessage> messages, CancellationToken cancel_token)
    {
      foreach (var msg in messages) {
        await ProcessMessage(msg, cancel_token);
      }
    }

    private async Task ProcessMessage(RTMPMessage msg, CancellationToken cancel_token)
    {
      switch (msg.MessageType) {
      case RTMPMessageType.SetChunkSize:
        await OnSetChunkSize(new SetChunkSizeMessage(msg), cancel_token);
        break;
      case RTMPMessageType.Abort:
        await OnAbort(new AbortMessage(msg), cancel_token);
        break;
      case RTMPMessageType.Ack:
        //Do nothing
        break;
      case RTMPMessageType.UserControl:
        await OnUserControl(new UserControlMessage(msg), cancel_token);
        break;
      case RTMPMessageType.SetWindowSize:
        await OnSetWindowSize(new SetWindowSizeMessage(msg), cancel_token);
        break;
      case RTMPMessageType.SetPeerBandwidth:
        await OnSetPeerBandwidth(new SetPeerBandwidthMessage(msg), cancel_token);
        break;
      case RTMPMessageType.Audio:
        await OnAudio(msg, cancel_token);
        break;
      case RTMPMessageType.Video:
        await OnVideo(msg, cancel_token);
        break;
      case RTMPMessageType.DataAMF3:
        await OnData(new DataAMF3Message(msg), cancel_token);
        break;
      case RTMPMessageType.DataAMF0:
        await OnData(new DataAMF0Message(msg), cancel_token);
        break;
      case RTMPMessageType.CommandAMF3:
        await OnCommand(new CommandAMF3Message(msg), cancel_token);
        break;
      case RTMPMessageType.CommandAMF0:
        await OnCommand(new CommandAMF0Message(msg), cancel_token);
        break;
      case RTMPMessageType.Aggregate:
        await OnAggregate(new AggregateMessage(msg), cancel_token);
        break;
      case RTMPMessageType.SharedObjectAMF3:
      case RTMPMessageType.SharedObjectAMF0:
        //TODO:Not implemented
        break;
      default:
        //TODO:Unknown message
        break;
      }
    }

    private Task OnSetChunkSize(SetChunkSizeMessage msg, CancellationToken cancel_token)
    {
      recvChunkSize = Math.Min(msg.ChunkSize, 0xFFFFFF);
      return Task.Delay(0);
    }

    private Task OnAbort(AbortMessage msg, CancellationToken cancel_token)
    {
      RTMPMessageBuilder builder;
      if (lastMessages.TryGetValue(msg.TargetChunkStream, out builder)) {
        builder.Abort();
      }
      return Task.Delay(0);
    }

    private Task OnUserControl(UserControlMessage msg, CancellationToken cancel_token)
    {
      return Task.Delay(0);
    }

    private Task OnSetWindowSize(SetWindowSizeMessage msg, CancellationToken cancel_token)
    {
      recvWindowSize = msg.WindowSize;
      receivedSize = 0;
      return Task.Delay(0);
    }

    private Task OnSetPeerBandwidth(SetPeerBandwidthMessage msg, CancellationToken cancel_token)
    {
      switch (msg.LimitType) {
      case PeerBandwidthLimitType.Hard:
        sendWindowSize = msg.PeerBandwidth;
        sendWindowLimitType = msg.LimitType;
        break;
      case PeerBandwidthLimitType.Soft:
        sendWindowSize = Math.Min(sendWindowSize, msg.PeerBandwidth);
        sendWindowLimitType = msg.LimitType;
        break;
      case PeerBandwidthLimitType.Dynamic:
        if (sendWindowLimitType==PeerBandwidthLimitType.Hard) {
          sendWindowSize = msg.PeerBandwidth;
          sendWindowLimitType = msg.LimitType;
        }
        break;
      }
      return Task.Delay(0);
    }

    private Task OnAudio(RTMPMessage msg, CancellationToken cancel_token)
    {
      flvBuffer.OnAudio(msg);
      return Task.Delay(0);
    }

    private Task OnVideo(RTMPMessage msg, CancellationToken cancel_token)
    {
      flvBuffer.OnVideo(msg);
      return Task.Delay(0);
    }

    private Task OnData(DataMessage msg, CancellationToken cancel_token)
    {
      flvBuffer.OnData(msg);
      return Task.Delay(0);
    }

    private async Task OnCommand(CommandMessage msg, CancellationToken cancel_token)
    {
      if (msg.StreamId==0) {
        Logger.Debug("NetConnection command: {0}", msg.CommandName);
        //NetConnection commands
        switch (msg.CommandName) {
        case "connect":      await OnCommandConnect(msg, cancel_token); break;
        case "call":         await OnCommandCall(msg, cancel_token); break;
        case "close":        await OnCommandClose(msg, cancel_token); break;
        case "createStream": await OnCommandCreateStream(msg, cancel_token); break;
        case "deleteStream": await OnCommandDeleteStream(msg, cancel_token); break;
        }
      }
      else {
        Logger.Debug("NetStream ({0}) command: {1}", msg.StreamId, msg.CommandName);
        //NetStream commands
        switch (msg.CommandName) {
        case "publish": await OnCommandPublish(msg, cancel_token); break;
        case "deleteStream": await OnCommandDeleteStream(msg, cancel_token); break;
        case "play":
        case "play2":
        case "closeStream":
        case "receiveAudio":
        case "receiveVideo":
        case "seek":
        case "pause":
        default:
          break;
        }
      }
    }

    private async Task OnCommandConnect(CommandMessage msg, CancellationToken cancel_token)
    {
      objectEncoding = ((int)msg.CommandObject["objectEncoding"])==3 ? 3 : 0;
      clientName     = (string)msg.CommandObject["flashVer"];
      Logger.Debug("connect: objectEncoding {0}, flashVer: {1}", objectEncoding, clientName);
      await SendMessage(2, new SetWindowSizeMessage(this.Now, 0, recvWindowSize), cancel_token);
      await SendMessage(2, new SetPeerBandwidthMessage(this.Now, 0, sendWindowSize, PeerBandwidthLimitType.Hard), cancel_token);
      await SendMessage(2, new UserControlMessage.StreamBeginMessage(this.Now, 0, 0), cancel_token);
      var response = CommandMessage.Create(
        objectEncoding,
        this.Now,
        msg.StreamId,
        "_result",
        msg.TransactionId,
        new AMF.AMFValue(new AMF.AMFObject {
          { "fmsVer",       "FMS/3,5,5,2004" },
          { "capabilities", 31 },
          { "mode",         1 },
        }),
        new AMF.AMFValue(new AMF.AMFObject {
          { "level",          "status" },
          { "code",           "NetConnection.Connect.Success" },
          { "description",    "Connection succeeded" },
          { "data",           new AMF.AMFObject { { "version", "3,5,5,2004" } } },
          { "clientId",       nextClientId++ },
          { "objectEncoding", objectEncoding },
        })
      );
      if (msg.TransactionId!=0) {
        await SendMessage(3, response, cancel_token);
      }
    }

    private Task OnCommandCall(CommandMessage msg, CancellationToken cancel_token)
    {
      return Task.Delay(0);
    }

    private Task OnCommandClose(CommandMessage msg, CancellationToken cancel_token)
    {
      return Task.Delay(0);
    }

    private async Task OnCommandCreateStream(CommandMessage msg, CancellationToken cancel_token)
    {
      var new_stream_id = nextStreamId++;
      var response = CommandMessage.Create(
        objectEncoding,
        this.Now,
        msg.StreamId,
        "_result",
        msg.TransactionId,
        null,
        new AMF.AMFValue(new_stream_id)
      );
      if (msg.TransactionId!=0) {
        await SendMessage(3, response, cancel_token);
      }
    }

    private Task OnCommandDeleteStream(CommandMessage msg, CancellationToken cancel_token)
    {
      Stop(StopReason.OffAir);
      return Task.Delay(0);
    }

    private async Task OnCommandPublish(CommandMessage msg, CancellationToken cancel_token)
    {
      var name = (string)msg.Arguments[0];
      var type = (string)msg.Arguments[1];
      Logger.Debug("publish: name {0}, type: {1}", name, type);
      await SendMessage(2, new UserControlMessage.StreamBeginMessage(this.Now, 0, msg.StreamId), cancel_token);
      var status = CommandMessage.Create(
        objectEncoding,
        this.Now,
        msg.StreamId,
        "onStatus",
        0,
        null,
        new AMF.AMFValue(new AMF.AMFObject {
          { "level",       "status" },
          { "code",        "NetStream.Publish.Start" },
          { "description", name },
        })
      );
      await SendMessage(3, status, cancel_token);
      var result = CommandMessage.Create(
        objectEncoding,
        this.Now,
        msg.StreamId,
        "_result",
        msg.TransactionId,
        null
      );
      if (msg.TransactionId!=0) {
        await SendMessage(3, result, cancel_token);
      }
      this.state = ConnectionState.Receiving;
    }

    private Task OnAggregate(AggregateMessage msg, CancellationToken cancel_token)
    {
      return ProcessMessages(msg.Messages, cancel_token);
    }

    protected Task SendAsync(Action<RTMPBinaryWriter> proc, CancellationToken cancel_token)
    {
      var memstream = new MemoryStream();
      using (memstream) {
        using (var writer=new RTMPBinaryWriter(memstream)) {
          proc.Invoke(writer);
        }
      }
      return connection.Stream.WriteAsync(memstream.ToArray(), cancel_token);
    }

    protected async Task<RTMPBinaryReader> RecvAsync(int len, CancellationToken cancel_token)
    {
      var buf = await connection.Stream.ReadBytesAsync(len, cancel_token);
      return new RTMPBinaryReader(new MemoryStream(buf, false), false);
    }

    protected Task SendAsync(byte[] data, CancellationToken cancel_token)
    {
      return connection.Stream.WriteAsync(data, cancel_token);
    }

  }

  public class RTMPSourceStream
    : SourceStreamBase
  {
    public RTMPSourceStream(PeerCast peercast, Channel channel, Uri source_uri)
      : base(peercast, channel, source_uri)
    {
      this.UseContentBitrate = channel.ChannelInfo==null || channel.ChannelInfo.Bitrate==0;
    }

    public bool UseContentBitrate { get; private set; }

    public override SourceStreamType Type {
      get { return SourceStreamType.Broadcast; }
    }

    public override ConnectionInfo GetConnectionInfo()
    {
      if (sourceConnection!=null) {
        return sourceConnection.GetConnectionInfo();
      }
      else {
        ConnectionStatus status;
        switch (StoppedReason) {
        case StopReason.UserReconnect: status = ConnectionStatus.Connecting; break;
        case StopReason.UserShutdown:  status = ConnectionStatus.Idle; break;
        default:                       status = ConnectionStatus.Error; break;
        }
        IPEndPoint endpoint = null;
        string client_name = "";
        return new ConnectionInfo(
          "RTMP Source",
          ConnectionType.Source,
          status,
          SourceUri.ToString(),
          endpoint,
          RemoteHostStatus.None,
          null,
          null,
          null,
          null,
          null,
          client_name);
      }
    }

    protected override ISourceConnection CreateConnection(Uri source_uri)
    {
      return new RTMPSourceConnection(PeerCast, Channel, source_uri, UseContentBitrate);
    }

    protected override void OnConnectionStopped(ISourceConnection connection, StopReason reason)
    {
      switch (reason) {
      case StopReason.UserReconnect:
        break;
      case StopReason.UserShutdown:
        Stop(reason);
        break;
      case StopReason.NoHost:
        Stop(reason);
        break;
      default:
        Task.Delay(3000).ContinueWith(prev => Reconnect());
        break;
      }
    }

  }

  [Plugin]
  class RTMPSourceStreamPlugin
    : PluginBase
  {
    override public string Name { get { return "RTMP Source"; } }

    private RTMPSourceStreamFactory factory;
    override protected void OnAttach()
    {
      if (factory==null) factory = new RTMPSourceStreamFactory(Application.PeerCast);
      Application.PeerCast.SourceStreamFactories.Add(factory);
    }

    override protected void OnDetach()
    {
      Application.PeerCast.SourceStreamFactories.Remove(factory);
    }
  }

}
