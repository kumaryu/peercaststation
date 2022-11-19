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
    : TCPSourceConnectionBase
  {
    private class ConnectionStopException : Exception
    {
      public StopReason StopReason { get; }
      public ConnectionStopException(StopReason reason)
        : base()
      {
        StopReason = reason;
      }
    }

    public RTMPSourceConnection(PeerCast peercast, Channel channel, Uri source_uri, bool use_content_bitrate)
      : base(peercast, channel, source_uri)
    {
      IContentSink sink = new ChannelContentSink(channel, use_content_bitrate);
      sink =
        System.Text.RegularExpressions.Regex.Matches(source_uri.Query, @"(&|\?)([^&=]+)=([^&=]+)")
          .Cast<System.Text.RegularExpressions.Match>()
          .Where(param => Uri.UnescapeDataString(param.Groups[2].Value).ToLowerInvariant()=="filters")
          .SelectMany(param => Uri.UnescapeDataString(param.Groups[3].Value).Split(','))
          .Select(name => PeerCast.ContentFilters.FirstOrDefault(filter => filter.Name.ToLowerInvariant()==name.ToLowerInvariant()))
          .NotNull()
          .Aggregate(sink, (r,filter) => filter.Activate(r));
      this.flvBuffer = new FLVContentBuffer(channel, new AsynchronousContentSink(sink));
      this.useContentBitrate = use_content_bitrate;
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
      IPEndPoint? endpoint = null;
      if (connection!=null) {
        endpoint = connection.RemoteEndPoint;
      }
      return new ConnectionInfoBuilder {
        ProtocolName     = "RTMP Source",
        Type             = ConnectionType.Source,
        Status           = status,
        RemoteName       = SourceUri.ToString(),
        RemoteEndPoint   = endpoint,
        RemoteHostStatus = (endpoint!=null && endpoint.Address.IsSiteLocal()) ? RemoteHostStatus.Local : RemoteHostStatus.None,
        ContentPosition  = flvBuffer.Position,
        RecvRate         = connection?.RecvRate,
        SendRate         = connection?.SendRate,
        AgentName        = clientName,
      }.Build();
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

    protected override async Task<SourceConnectionClient?> DoConnect(Uri source, CancellationTokenWithArg<StopReason> cancellationToken)
    {
      TcpClient? client = null;
      var bind_addr = GetBindAddresses(source);
      if (bind_addr.Count()==0) {
        this.state = ConnectionState.Error;
        throw new BindErrorException(String.Format("Cannot resolve bind address: {0}", source.DnsSafeHost));
      }
      var listeners = bind_addr.Select(addr => { 
        var listener = new TcpListener(addr);
        if (addr.AddressFamily==AddressFamily.InterNetworkV6) {
          listener.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, true);
        }
        return listener;
      }).ToArray();
      try {
        var cancel_task = cancellationToken.CancellationToken.CreateCancelTask<TcpClient>();
        var tasks = listeners.Select(listener => {
          listener.Start(1);
          Logger.Debug("Listening on {0}", listener.LocalEndpoint);
          return listener.AcceptTcpClientAsync();
        }).Concat(Enumerable.Repeat(cancel_task, 1)).ToArray();
        var result = await Task.WhenAny(tasks).ConfigureAwait(false);
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
        var c = new SourceConnectionClient(client);
        c.Stream.CloseTimeout = 0;
        return c;
      }
      else {
        return null;
      }
    }

    protected override async Task<StopReason> DoProcess(SourceConnectionClient connection, WaitableQueue<(Host? From, Atom Message)> postMessages, CancellationTokenWithArg<StopReason> cancellationToken)
    {
      this.state = ConnectionState.Waiting;
      try {
        if (await Handshake(connection, cancellationToken).ConfigureAwait(false)) {
          await ProcessRTMPMessages(connection, cancellationToken).ConfigureAwait(false);
        }
        this.state = ConnectionState.Closed;
        return StopReason.OffAir;
      }
      catch (ConnectionStopException e) {
        this.state = ConnectionState.Closed;
        return e.StopReason;
      }
      catch (IOException e) {
        Logger.Error(e);
        this.state = ConnectionState.Error;
        return StopReason.ConnectionError;
      }
      catch (OperationCanceledException) {
        this.state = ConnectionState.Closed;
        if (cancellationToken.IsCancellationRequested) {
          return cancellationToken.Value;
        }
        else {
          return StopReason.ConnectionError;
        }
      }
    }

    protected async Task ProcessRTMPMessages(SourceConnectionClient connection, CancellationToken cancel_token)
    {
      this.state = ConnectionState.Connected;
      var messages = new Queue<RTMPMessage>();
      while (!cancel_token.IsCancellationRequested && 
             await RecvMessage(connection.Stream, messages, cancel_token).ConfigureAwait(false)) {
        await ProcessMessages(connection.Stream, messages, cancel_token).ConfigureAwait(false);
        messages.Clear();
      }
    }

    System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
    private async Task<bool> Handshake(SourceConnectionClient connection, CancellationToken cancel_token)
    {
      Logger.Debug("Handshake start");
      var rand = new Random();
      var c0 = await RecvAsync(connection.Stream, 1, cancel_token).ConfigureAwait(false);
      await connection.Stream.WriteByteAsync(0x03, cancel_token).ConfigureAwait(false);
      var s1vec = new byte[1528];
      rand.NextBytes(s1vec);
      await SendAsync(connection.Stream, writer => {
        writer.Write(0);
        writer.Write(0);
        writer.Write(s1vec);
      }, cancel_token).ConfigureAwait(false);
      using (var reader=await RecvAsync(connection.Stream, 1536, cancel_token).ConfigureAwait(false)) {
        await SendAsync(connection.Stream, writer => {
          writer.Write(reader.ReadInt32());
          writer.Write(reader.ReadInt32());
          writer.Write(reader.ReadBytes(1528));
        }, cancel_token).ConfigureAwait(false);
      }
      using (var reader=await RecvAsync(connection.Stream, 1536, cancel_token).ConfigureAwait(false)) {
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
    private async Task<int> RecvStream(Stream stream, byte[] buf, int offset, int len, CancellationToken cancel_token)
    {
      if (len+receivedSize>=recvWindowSize) {
        var len1 = (int)(recvWindowSize-receivedSize);
        await stream.ReadBytesAsync(buf, offset, len1, cancel_token).ConfigureAwait(false);
        receivedSize   += len1;
        sequenceNumber += len1;
        await SendMessage(stream, 2, new AckMessage(this.Now, 0, sequenceNumber), cancel_token).ConfigureAwait(false);
        var len2 = len - len1;
        await stream.ReadBytesAsync(buf, offset+len1, len2, cancel_token).ConfigureAwait(false);
        receivedSize    = len2; //reset
        sequenceNumber += len2;
      }
      else {
        await stream.ReadBytesAsync(buf, offset, len, cancel_token).ConfigureAwait(false);
        receivedSize   += len;
        sequenceNumber += len;
      }
      return len;
    }

    private async Task<byte[]> RecvStream(Stream stream, int len, CancellationToken cancel_token)
    {
      var buf = new byte[len];
      await RecvStream(stream, buf, 0, len, cancel_token).ConfigureAwait(false);
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
        RTMPMessageBuilder? x,
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
    private async Task<bool> RecvMessage(Stream stream, Queue<RTMPMessage> messages, CancellationToken cancel_token)
    {
      var basic_header = (await RecvStream(stream, 1, cancel_token).ConfigureAwait(false))[0];
      var chunk_stream_id = basic_header & 0x3F;
      if (chunk_stream_id==0) {
        chunk_stream_id = (await RecvStream(stream, 1, cancel_token).ConfigureAwait(false))[0] + 64;
      }
      else if (chunk_stream_id==1) {
        var buf = await RecvStream(stream, 2, cancel_token).ConfigureAwait(false);
        chunk_stream_id = (buf[1]*256 | buf[0]) + 64;
      }

      RTMPMessageBuilder msg;
      RTMPMessageBuilder? last_msg;
      if (!lastMessages.TryGetValue(chunk_stream_id, out last_msg)) {
        last_msg = RTMPMessageBuilder.NullPacket;
      }
      switch ((basic_header & 0xC0)>>6) {
      case 0:
      default:
        using (var reader=new RTMPBinaryReader(await RecvStream(stream, 11, cancel_token).ConfigureAwait(false))) {
          long timestamp  = reader.ReadUInt24();
          var body_length = reader.ReadUInt24();
          var type_id     = reader.ReadByte();
          var stream_id   = reader.ReadUInt32LE();
          if (timestamp==0xFFFFFF) {
            using (var ext_reader=new RTMPBinaryReader(await RecvStream(stream, 4, cancel_token).ConfigureAwait(false))) {
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
        using (var reader=new RTMPBinaryReader(await RecvStream(stream, 7, cancel_token).ConfigureAwait(false))) {
          long timestamp_delta = reader.ReadUInt24();
          var body_length      = reader.ReadUInt24();
          var type_id          = reader.ReadByte();
          if (timestamp_delta==0xFFFFFF) {
            using (var ext_reader=new RTMPBinaryReader(await RecvStream(stream, 4, cancel_token).ConfigureAwait(false))) {
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
        using (var reader=new RTMPBinaryReader(await RecvStream(stream, 3, cancel_token).ConfigureAwait(false))) {
          long timestamp_delta = reader.ReadUInt24();
          if (timestamp_delta==0xFFFFFF) {
            using (var ext_reader=new RTMPBinaryReader(await RecvStream(stream, 4, cancel_token).ConfigureAwait(false))) {
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
        stream,
        msg.Body,
        msg.ReceivedLength,
        Math.Min(recvChunkSize, msg.BodyLength-msg.ReceivedLength),
        cancel_token).ConfigureAwait(false);
      if (msg.ReceivedLength>=msg.BodyLength) {
        messages.Enqueue(msg.ToMessage());
      }
      return true; //TODO:接続エラー時はfalseを返す
    }

    private async Task SendMessage(Stream stream, int chunk_stream_id, RTMPMessage msg, CancellationToken cancel_token)
    {
      int offset = 0;
      int fmt = 0;
      while (msg.Body.Length-offset>0) {
        switch (fmt) {
        case 0:
          await SendAsync(stream, writer => {
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
          }, cancel_token).ConfigureAwait(false);
          fmt = 3;
          break;
        case 3:
          await SendAsync(stream, writer => {
            writer.Write((byte)((fmt<<6) | chunk_stream_id));
            int chunk_len = Math.Min(sendChunkSize, msg.Body.Length-offset);
            writer.Write(msg.Body, offset, chunk_len);
            offset += chunk_len;
          }, cancel_token).ConfigureAwait(false);
          break;
        }
      }
    }

    private async Task ProcessMessages(Stream stream, IEnumerable<RTMPMessage> messages, CancellationToken cancel_token)
    {
      foreach (var msg in messages) {
        await ProcessMessage(stream, msg, cancel_token).ConfigureAwait(false);
      }
    }

    private async Task ProcessMessage(Stream stream, RTMPMessage msg, CancellationToken cancel_token)
    {
      switch (msg.MessageType) {
      case RTMPMessageType.SetChunkSize:
        await OnSetChunkSize(new SetChunkSizeMessage(msg), cancel_token).ConfigureAwait(false);
        break;
      case RTMPMessageType.Abort:
        await OnAbort(new AbortMessage(msg), cancel_token).ConfigureAwait(false);
        break;
      case RTMPMessageType.Ack:
        //Do nothing
        break;
      case RTMPMessageType.UserControl:
        await OnUserControl(new UserControlMessage(msg), cancel_token).ConfigureAwait(false);
        break;
      case RTMPMessageType.SetWindowSize:
        await OnSetWindowSize(new SetWindowSizeMessage(msg), cancel_token).ConfigureAwait(false);
        break;
      case RTMPMessageType.SetPeerBandwidth:
        await OnSetPeerBandwidth(new SetPeerBandwidthMessage(msg), cancel_token).ConfigureAwait(false);
        break;
      case RTMPMessageType.Audio:
        await OnAudio(msg, cancel_token).ConfigureAwait(false);
        break;
      case RTMPMessageType.Video:
        await OnVideo(msg, cancel_token).ConfigureAwait(false);
        break;
      case RTMPMessageType.DataAMF3:
        await OnData(new DataAMF3Message(msg), cancel_token).ConfigureAwait(false);
        break;
      case RTMPMessageType.DataAMF0:
        await OnData(new DataAMF0Message(msg), cancel_token).ConfigureAwait(false);
        break;
      case RTMPMessageType.CommandAMF3:
        await OnCommand(stream, new CommandAMF3Message(msg), cancel_token).ConfigureAwait(false);
        break;
      case RTMPMessageType.CommandAMF0:
        await OnCommand(stream, new CommandAMF0Message(msg), cancel_token).ConfigureAwait(false);
        break;
      case RTMPMessageType.Aggregate:
        await OnAggregate(stream, new AggregateMessage(msg), cancel_token).ConfigureAwait(false);
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
      return Task.CompletedTask;
    }

    private Task OnAbort(AbortMessage msg, CancellationToken cancel_token)
    {
      if (lastMessages.TryGetValue(msg.TargetChunkStream, out var builder)) {
        builder.Abort();
      }
      return Task.CompletedTask;
    }

    private Task OnUserControl(UserControlMessage msg, CancellationToken cancel_token)
    {
      return Task.CompletedTask;
    }

    private Task OnSetWindowSize(SetWindowSizeMessage msg, CancellationToken cancel_token)
    {
      recvWindowSize = msg.WindowSize;
      receivedSize = 0;
      return Task.CompletedTask;
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
      return Task.CompletedTask;
    }

    private Task OnAudio(RTMPMessage msg, CancellationToken cancel_token)
    {
      flvBuffer.OnAudio(msg);
      return Task.CompletedTask;
    }

    private Task OnVideo(RTMPMessage msg, CancellationToken cancel_token)
    {
      flvBuffer.OnVideo(msg);
      return Task.CompletedTask;
    }

    private Task OnData(DataMessage msg, CancellationToken cancel_token)
    {
      flvBuffer.OnData(msg);
      return Task.CompletedTask;
    }

    private async Task OnCommand(Stream stream, CommandMessage msg, CancellationToken cancel_token)
    {
      if (msg.StreamId==0) {
        Logger.Debug("NetConnection command: {0}", msg.CommandName);
        //NetConnection commands
        switch (msg.CommandName) {
        case "connect":      await OnCommandConnect(stream, msg, cancel_token).ConfigureAwait(false); break;
        case "call":         await OnCommandCall(msg, cancel_token).ConfigureAwait(false); break;
        case "close":        await OnCommandClose(msg, cancel_token).ConfigureAwait(false); break;
        case "createStream": await OnCommandCreateStream(stream, msg, cancel_token).ConfigureAwait(false); break;
        case "deleteStream": await OnCommandDeleteStream(msg, cancel_token).ConfigureAwait(false); break;
        }
      }
      else {
        Logger.Debug("NetStream ({0}) command: {1}", msg.StreamId, msg.CommandName);
        //NetStream commands
        switch (msg.CommandName) {
        case "publish": await OnCommandPublish(stream, msg, cancel_token).ConfigureAwait(false); break;
        case "deleteStream": await OnCommandDeleteStream(msg, cancel_token).ConfigureAwait(false); break;
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

    private async Task OnCommandConnect(Stream stream, CommandMessage msg, CancellationToken cancel_token)
    {
      objectEncoding = ((int)msg.CommandObject["objectEncoding"])==3 ? 3 : 0;
      clientName     = (string?)msg.CommandObject["flashVer"] ?? "";
      Logger.Debug($"connect: objectEncoding {objectEncoding}, flashVer: {clientName}");
      await SendMessage(stream, 2, new SetWindowSizeMessage(this.Now, 0, recvWindowSize), cancel_token).ConfigureAwait(false);
      await SendMessage(stream, 2, new SetPeerBandwidthMessage(this.Now, 0, sendWindowSize, PeerBandwidthLimitType.Hard), cancel_token).ConfigureAwait(false);
      await SendMessage(stream, 2, new UserControlMessage.StreamBeginMessage(this.Now, 0, 0), cancel_token).ConfigureAwait(false);
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
        await SendMessage(stream, 3, response, cancel_token).ConfigureAwait(false);
      }
    }

    private Task OnCommandCall(CommandMessage msg, CancellationToken cancel_token)
    {
      return Task.CompletedTask;
    }

    private Task OnCommandClose(CommandMessage msg, CancellationToken cancel_token)
    {
      return Task.CompletedTask;
    }

    private async Task OnCommandCreateStream(Stream stream, CommandMessage msg, CancellationToken cancel_token)
    {
      var new_stream_id = nextStreamId++;
      var response = CommandMessage.Create(
        objectEncoding,
        this.Now,
        msg.StreamId,
        "_result",
        msg.TransactionId,
        AMF.AMFValue.Null,
        new AMF.AMFValue(new_stream_id)
      );
      if (msg.TransactionId!=0) {
        await SendMessage(stream, 3, response, cancel_token).ConfigureAwait(false);
      }
    }

    private Task OnCommandDeleteStream(CommandMessage msg, CancellationToken cancel_token)
    {
      throw new ConnectionStopException(StopReason.OffAir);
    }

    private async Task OnCommandPublish(Stream stream, CommandMessage msg, CancellationToken cancel_token)
    {
      var name = (string?)msg.Arguments[0];
      var type = (string?)msg.Arguments[1];
      Logger.Debug($"publish: name {name}, type: {type}");
      await SendMessage(stream, 2, new UserControlMessage.StreamBeginMessage(this.Now, 0, msg.StreamId), cancel_token).ConfigureAwait(false);
      var status = CommandMessage.Create(
        objectEncoding,
        this.Now,
        msg.StreamId,
        "onStatus",
        0,
        AMF.AMFValue.Null,
        new AMF.AMFValue(new AMF.AMFObject {
          { "level",       "status" },
          { "code",        "NetStream.Publish.Start" },
          { "description", name ?? "" },
        })
      );
      await SendMessage(stream, 3, status, cancel_token).ConfigureAwait(false);
      var result = CommandMessage.Create(
        objectEncoding,
        this.Now,
        msg.StreamId,
        "_result",
        msg.TransactionId,
        AMF.AMFValue.Null
      );
      if (msg.TransactionId!=0) {
        await SendMessage(stream, 3, result, cancel_token).ConfigureAwait(false);
      }
      this.state = ConnectionState.Receiving;
    }

    private Task OnAggregate(Stream stream, AggregateMessage msg, CancellationToken cancel_token)
    {
      return ProcessMessages(stream, msg.Messages, cancel_token);
    }

    protected ValueTask SendAsync(Stream stream, Action<RTMPBinaryWriter> proc, CancellationToken cancel_token)
    {
      var memstream = new MemoryStream();
      using (memstream) {
        using (var writer=new RTMPBinaryWriter(memstream)) {
          proc.Invoke(writer);
        }
      }
      return stream.WriteAsync(memstream.ToArray(), cancel_token);
    }

    protected async Task<RTMPBinaryReader> RecvAsync(Stream stream, int len, CancellationToken cancel_token)
    {
      var buf = await stream.ReadBytesAsync(len, cancel_token).ConfigureAwait(false);
      return new RTMPBinaryReader(new MemoryStream(buf, false), false);
    }

    protected ValueTask SendAsync(Stream stream, byte[] data, CancellationToken cancel_token)
    {
      return stream.WriteAsync(data, cancel_token);
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

    protected override ConnectionInfo GetConnectionInfo(ISourceConnection? sourceConnection)
    {
      var connInfo = sourceConnection?.GetConnectionInfo();
      if (connInfo!=null) {
        return connInfo;
      }
      else {
        ConnectionStatus status;
        switch (StoppedReason) {
        case StopReason.UserReconnect: status = ConnectionStatus.Connecting; break;
        case StopReason.UserShutdown:  status = ConnectionStatus.Idle; break;
        default:                       status = ConnectionStatus.Error; break;
        }
        string client_name = "";
        return new ConnectionInfoBuilder {
          ProtocolName     = "RTMP Source",
          Type             = ConnectionType.Source,
          Status           = status,
          RemoteName       = SourceUri.ToString(),
          RemoteEndPoint   = null,
          RemoteHostStatus = RemoteHostStatus.None,
          AgentName        = client_name,
        }.Build();
      }
    }

    protected override ISourceConnection CreateConnection(Uri source_uri)
    {
      return new RTMPSourceConnection(PeerCast, Channel, source_uri, UseContentBitrate);
    }

    protected override void OnConnectionStopped(ISourceConnection connection, ConnectionStoppedArgs args)
    {
      switch (args.Reason) {
      case StopReason.UserReconnect:
      case StopReason.UserShutdown:
      case StopReason.NoHost:
        break;
      default:
        args.Delay = 3000;
        args.Reconnect = true;
        break;
      }
    }

  }

  [Plugin]
  class RTMPSourceStreamPlugin
    : PluginBase
  {
    override public string Name { get { return "RTMP Source"; } }

    private RTMPSourceStreamFactory? factory;
    override protected void OnAttach(PeerCastApplication app)
    {
      if (factory==null) factory = new RTMPSourceStreamFactory(app.PeerCast);
      app.PeerCast.SourceStreamFactories.Add(factory);
    }

    override protected void OnDetach(PeerCastApplication app)
    {
      var f = Interlocked.Exchange(ref factory, null);
      if (f!=null) {
        app.PeerCast.SourceStreamFactories.Remove(f);
      }
    }
  }

}
