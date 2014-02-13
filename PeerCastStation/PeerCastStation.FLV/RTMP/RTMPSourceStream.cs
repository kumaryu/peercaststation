using System;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
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

    public override ISourceStream Create(Channel channel, Uri source, IContentReader reader)
    {
      return new RTMPSourceStream(PeerCast, channel, source);
    }
  }

  internal class RTMPSourceChannel
  {
    public string        Name          { get; private set; }
    public Channel       TargetChannel { get; private set; }
    public long          Position      { get { return position; } }
    private long         position        = 0;
    private int          streamIndex     = -1;
    private DateTime     streamOrigin;
    private long         timestampOrigin = 0;
    private DataMessage  metadata        = null;
    private RTMPMessage  audioHeader     = null;
    private RTMPMessage  videoHeader     = null;
    private MemoryStream bodyBuffer      = new MemoryStream();

    public RTMPSourceChannel(string name, Channel target_channel)
    {
      this.Name          = name;
      this.TargetChannel = target_channel;
    }

    private void SetDataFrame(DataMessage msg)
    {
      var name = (string)msg.Arguments[0];
      var data_msg = new DataAMF0Message(msg.Timestamp, 0, name, new AMF.AMFValue[] { msg.Arguments[1] });
      switch (name) {
      case "onMetaData":
        metadata = data_msg;
        var info = new AtomCollection(TargetChannel.ChannelInfo.Extra);
        info.SetChanInfoType("FLV");
        info.SetChanInfoStreamType("video/x-flv");
        info.SetChanInfoStreamExt(".flv");
        if (metadata.Arguments[0].Type==AMF.AMFValueType.ECMAArray || metadata.Arguments[0].Type==AMF.AMFValueType.Object){
          var bitrate = 0.0;
          var val = metadata.Arguments[0]["maxBitrate"];
          if (!AMF.AMFValue.IsNull(val)) {
            double maxBitrate;
            string maxBitrateStr = System.Text.RegularExpressions.Regex.Replace((string)val, @"([\d]+)k", "$1");
            if (double.TryParse(maxBitrateStr, out maxBitrate)) {
              bitrate += maxBitrate;
            }
          }
          else if (!AMF.AMFValue.IsNull(val = metadata.Arguments[0]["videodatarate"])) {
            bitrate += (double)val;
          }
          if (!AMF.AMFValue.IsNull(val = metadata.Arguments[0]["audiodatarate"])) {
            bitrate += (double)val;
          }
          info.SetChanInfoBitrate((int)bitrate);
        }
        this.TargetChannel.ChannelInfo = new ChannelInfo(info);
        OnHeaderChanged(data_msg);
        break;
      }
      OnContentChanged(data_msg);
    }

    private void ClearDataFrame(DataMessage msg)
    {
      var name = (string)msg.Arguments[0];
      switch (name) {
      case "onMetaData":
        metadata = null;
        break;
      }
    }

    public void OnData(DataMessage msg)
    {
      switch (msg.PropertyName) {
      case "@setDataFrame":
        SetDataFrame(msg);
        break;

      case "@clearDataFrame":
        ClearDataFrame(msg);
        break;
      }
    }

    public void OnVideo(RTMPMessage msg)
    {
      if (IsAVCHeader(msg)) {
        videoHeader = msg;
        OnHeaderChanged(msg);
      }
      OnContentChanged(msg);
    }

    public void OnAudio(RTMPMessage msg)
    {
      if (IsAACHeader(msg)) {
        audioHeader = msg;
        OnHeaderChanged(msg);
      }
      OnContentChanged(msg);
    }

    private bool IsAVCHeader(RTMPMessage msg)
    {
      return
         msg.MessageType==RTMPMessageType.Video &&
         msg.Body.Length>3 &&
        (msg.Body[0]==0x17 &&
         msg.Body[1]==0x00 &&
         msg.Body[2]==0x00 &&
         msg.Body[3]==0x00);
    }

    private bool IsAACHeader(RTMPMessage msg)
    {
      return
         msg.MessageType==RTMPMessageType.Audio &&
         msg.Body.Length>1 &&
        (msg.Body[0]==0xAF &&
         msg.Body[1]==0x00);
    }

    private void WriteMessage(Stream stream, RTMPMessage msg, long time_origin)
    {
      var timestamp = Math.Max(0, msg.Timestamp-time_origin);
      using (var writer=new RTMPBinaryWriter(stream, true)) {
        writer.Write((byte)msg.MessageType);
        writer.WriteUInt24(msg.Body.Length);
        writer.WriteUInt24((int)timestamp & 0xFFFFFF);
        writer.Write((byte)((timestamp>>24) & 0xFF));
        writer.WriteUInt24(0);
        writer.Write(msg.Body, 0, msg.Body.Length);
        writer.Write(msg.Body.Length+11);
      }
    }

    private void OnHeaderChanged(RTMPMessage msg)
    {
      FlushContents();
      var s = new MemoryStream();
      using (s) {
        using (var writer=new RTMPBinaryWriter(s, true)) {
          writer.Write((byte)'F');
          writer.Write((byte)'L');
          writer.Write((byte)'V');
          writer.Write((byte)1);
          writer.Write((byte)5);
          writer.WriteUInt32(9);
          writer.WriteUInt32(0);
        }
        if (metadata!=null)    WriteMessage(s, metadata,    0xFFFFFFFF);
        if (audioHeader!=null) WriteMessage(s, audioHeader, 0xFFFFFFFF);
        if (videoHeader!=null) WriteMessage(s, videoHeader, 0xFFFFFFFF);
      }
      streamIndex     = TargetChannel.GenerateStreamID();
      streamOrigin    = DateTime.Now;
      timestampOrigin = msg.Timestamp;
      var bytes = s.ToArray();
      TargetChannel.ContentHeader = new Content(streamIndex, TimeSpan.Zero, position, bytes);
      position += bytes.Length;
    }

    private void OnContentChanged(RTMPMessage content)
    {
      if (streamIndex<0) OnHeaderChanged(content);
      WriteMessage(bodyBuffer, content, timestampOrigin);
      if (bodyBuffer.Length>=7500) {
        FlushContents();
      }
    }

    public void FlushContents()
    {
      if (bodyBuffer.Length<=0) return;
      TargetChannel.Contents.Add(new Content(streamIndex, DateTime.Now-streamOrigin, position, bodyBuffer.ToArray()));
      position += bodyBuffer.Length;
      bodyBuffer.SetLength(0);
    }

  }

  public class RTMPSourceConnection
    : SourceConnectionBase
  {
    public RTMPSourceConnection(PeerCast peercast, Channel channel, Uri source_uri)
      : base(peercast, channel, source_uri)
    {
      this.rtmpChannel = new RTMPSourceChannel("livestream", channel);
    }

    private class ConnectionStoppedExcception : ApplicationException {}
    private TcpClient client;
    private RTMPSourceChannel rtmpChannel;

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
      if (client!=null && client.Connected) {
        endpoint = (IPEndPoint)client.Client.RemoteEndPoint;
      }
      return new ConnectionInfo(
        "RTMP Source",
        ConnectionType.Source,
        status,
        SourceUri.ToString(),
        endpoint,
        (endpoint!=null && Utils.IsSiteLocal(endpoint.Address)) ? RemoteHostStatus.Local : RemoteHostStatus.None,
        rtmpChannel.Position,
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

    protected override StreamConnection DoConnect(Uri source)
    {
      TcpClient client = null;
      var listener = new TcpListener(IPAddress.Any, 1935);
      try {
        listener.Start(1);
        Logger.Debug("Listening on 0.0.0.0:1935");
        var ar = listener.BeginAcceptTcpClient(null, null);
        WaitAndProcessEvents(ar.AsyncWaitHandle, stopped => {
          if (ar.IsCompleted) {
            client = listener.EndAcceptTcpClient(ar);
          }
          return null;
        });
        Logger.Debug("Client accepted");
      }
      catch (SocketException) {
        //Runで処理するのでここではスルーする
        throw;
      }
      finally {
        listener.Stop();
      }
      if (client!=null) {
        this.client = client;
        return new StreamConnection(client.GetStream(), client.GetStream());
      }
      else {
        return null;
      }
    }

    protected override void DoClose(StreamConnection connection)
    {
      this.connection.Close();
      this.client.Close();
      Logger.Debug("Closed");
    }

    public override void Run()
    {
      this.state = ConnectionState.Waiting;
      try {
        OnStarted();
        if (connection!=null && !IsStopped) {
          Handshake();
          DoProcess();
        }
        this.state = ConnectionState.Closed;
      }
      catch (SocketException e) {
        Logger.Error(e);
        DoStop(StopReason.NoHost);
        this.state = ConnectionState.Error;
      }
      catch (IOException e) {
        Logger.Error(e);
        DoStop(StopReason.ConnectionError);
        this.state = ConnectionState.Error;
      }
      catch (ConnectionStoppedExcception e) {
        this.state = ConnectionState.Closed;
      }
      SyncContext.ProcessAll();
      OnStopped();
    }

    protected override void DoProcess()
    {
      this.state = ConnectionState.Connected;
      var messages = new Queue<RTMPMessage>();
      while (!IsStopped && RecvMessage(messages)) {
        ProcessMessages(messages);
        messages.Clear();
      }
    }

    protected override void DoPost(Host from, Atom packet)
    {
      //Do nothing
    }

    System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
    private bool Handshake()
    {
      Logger.Debug("Handshake start");
      var rand = new Random();
      var c0 = Recv(1);
      Send(new byte[] { 0x03 });
      var s1vec = new byte[1528];
      rand.NextBytes(s1vec);
      Send(s => {
        using (var writer=new RTMPBinaryWriter(s)) {
          writer.Write(0);
          writer.Write(0);
          writer.Write(s1vec);
        }
      });
      using (var reader=new RTMPBinaryReader(new MemoryStream(Recv(1536)))) {
        Send(s => {
          using (var writer=new RTMPBinaryWriter(s)) {
            writer.Write(reader.ReadInt32());
            writer.Write(reader.ReadInt32());
            writer.Write(reader.ReadBytes(1528));
          }
        });
      }
      using (var reader=new RTMPBinaryReader(new MemoryStream(Recv(1536)))) {
        reader.ReadInt32();
        reader.ReadInt32();
        if (!s1vec.Equals(reader.ReadBytes(1528))) {
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
    long sendWindowSize = long.MaxValue;
    PeerBandwidthLimitType sendWindowLimitType = PeerBandwidthLimitType.Hard;
    long recvWindowSize = long.MaxValue;
    long receivedSize   = 0;
    long sequenceNumber = 0;
    int RecvStream(byte[] buf, int offset, int len)
    {
      if (len+receivedSize>=recvWindowSize) {
        var len1 = (int)(recvWindowSize-receivedSize);
        Recv(buf, offset, len1);
        receivedSize   += len1;
        sequenceNumber += len1;
        SendMessage(2, new AckMessage(this.Now, 0, sequenceNumber));
        var len2 = len - (int)(recvWindowSize-receivedSize);
        Recv(buf, offset+len1, len2);
        receivedSize   += len2;
        sequenceNumber += len2;
      }
      else {
        Recv(buf, offset, len);
        receivedSize   += len;
        sequenceNumber += len;
      }
      return len;
    }

    byte[] RecvStream(int len)
    {
      var buf = new byte[len];
      RecvStream(buf, 0, len);
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

    Dictionary<int, RTMPMessageBuilder> lastMessages = new Dictionary<int,RTMPMessageBuilder>();
    private bool RecvMessage(Queue<RTMPMessage> messages)
    {
      var basic_header = RecvStream(1)[0];
      var chunk_stream_id = basic_header & 0x3F;
      if (chunk_stream_id==0) {
        chunk_stream_id = RecvStream(1)[0] + 64;
      }
      else if (chunk_stream_id==1) {
        var buf = RecvStream(2);
        chunk_stream_id = (buf[1]*256 | buf[0]) + 64;
      }

      RTMPMessageBuilder msg = null;
      RTMPMessageBuilder last_msg = null;
      lastMessages.TryGetValue(chunk_stream_id, out last_msg);
      switch ((basic_header & 0xC0)>>6) {
      case 0:
        using (var reader=new RTMPBinaryReader(new MemoryStream(RecvStream(11)))) {
          long timestamp  = reader.ReadUInt24();
          var body_length = reader.ReadUInt24();
          var type_id     = reader.ReadByte();
          var stream_id   = reader.ReadUInt32LE();
          if (timestamp==0xFFFFFF) {
            using (var ext_reader=new RTMPBinaryReader(new MemoryStream(RecvStream(4)))) {
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
        using (var reader=new RTMPBinaryReader(new MemoryStream(RecvStream(7)))) {
          long timestamp_delta = reader.ReadUInt24();
          var body_length      = reader.ReadUInt24();
          var type_id          = reader.ReadByte();
          if (timestamp_delta==0xFFFFFF) {
            using (var ext_reader=new RTMPBinaryReader(new MemoryStream(RecvStream(4)))) {
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
        using (var reader=new RTMPBinaryReader(new MemoryStream(RecvStream(3)))) {
          long timestamp_delta = reader.ReadUInt24();
          if (timestamp_delta==0xFFFFFF) {
            using (var ext_reader=new RTMPBinaryReader(new MemoryStream(RecvStream(4)))) {
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
        }
        break;
      }

      msg.ReceivedLength += RecvStream(
        msg.Body,
        msg.ReceivedLength,
        Math.Min(recvChunkSize, msg.BodyLength-msg.ReceivedLength));
      if (msg.ReceivedLength>=msg.BodyLength) {
        messages.Enqueue(msg.ToMessage());
      }
      return true; //TODO:接続エラー時はfalseを返す
    }

    void SendMessage(int chunk_stream_id, RTMPMessage msg)
    {
      int offset = 0;
      int fmt = 0;
      while (msg.Body.Length-offset>0) {
        switch (fmt) {
        case 0:
          Send(s => {
            using (var writer=new RTMPBinaryWriter(s)) {
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
            }
          });
          fmt = 3;
          break;
        case 3:
          Send(s => {
            using (var writer=new RTMPBinaryWriter(s)) {
              writer.Write((byte)((fmt<<6) | chunk_stream_id));
              int chunk_len = Math.Min(sendChunkSize, msg.Body.Length-offset);
              writer.Write(msg.Body, offset, chunk_len);
              offset += chunk_len;
            }
          });
          break;
        }
      }
    }

    private void ProcessMessages(IEnumerable<RTMPMessage> messages)
    {
      foreach (var msg in messages) {
        ProcessMessage(msg);
      }
    }

    private void ProcessMessage(RTMPMessage msg)
    {
      switch (msg.MessageType) {
      case RTMPMessageType.SetChunkSize:
        OnSetChunkSize(new SetChunkSizeMessage(msg));
        break;
      case RTMPMessageType.Abort:
        OnAbort(new AbortMessage(msg));
        break;
      case RTMPMessageType.Ack:
        //Do nothing
        break;
      case RTMPMessageType.UserControl:
        OnUserControl(new UserControlMessage(msg));
        break;
      case RTMPMessageType.SetWindowSize:
        OnSetWindowSize(new SetWindowSizeMessage(msg));
        break;
      case RTMPMessageType.SetPeerBandwidth:
        OnSetPeerBandwidth(new SetPeerBandwidthMessage(msg));
        break;
      case RTMPMessageType.Audio:
        OnAudio(msg);
        break;
      case RTMPMessageType.Video:
        OnVideo(msg);
        break;
      case RTMPMessageType.DataAMF3:
        OnData(new DataAMF3Message(msg));
        break;
      case RTMPMessageType.DataAMF0:
        OnData(new DataAMF0Message(msg));
        break;
      case RTMPMessageType.CommandAMF3:
        OnCommand(new CommandAMF3Message(msg));
        break;
      case RTMPMessageType.CommandAMF0:
        OnCommand(new CommandAMF0Message(msg));
        break;
      case RTMPMessageType.Aggregate:
        OnAggregate(new AggregateMessage(msg));
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

    void OnSetChunkSize(SetChunkSizeMessage msg)
    {
      recvChunkSize = Math.Min(msg.ChunkSize, 0xFFFFFF);
    }

    void OnAbort(AbortMessage msg)
    {
      RTMPMessageBuilder builder;
      if (lastMessages.TryGetValue(msg.TargetChunkStream, out builder)) {
        builder.Abort();
      }
    }

    void OnUserControl(UserControlMessage msg)
    {
    }

    void OnSetWindowSize(SetWindowSizeMessage msg)
    {
      recvWindowSize = msg.WindowSize;
   }

    void OnSetPeerBandwidth(SetPeerBandwidthMessage msg)
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
    }

    void OnAudio(RTMPMessage msg)
    {
      rtmpChannel.OnAudio(msg);
    }

    void OnVideo(RTMPMessage msg)
    {
      rtmpChannel.OnVideo(msg);
    }

    void OnData(DataMessage msg)
    {
      rtmpChannel.OnData(msg);
    }

    void OnCommand(CommandMessage msg)
    {
      if (msg.StreamId==0) {
        Logger.Debug("NetConnection command: {0}", msg.CommandName);
        //NetConnection commands
        switch (msg.CommandName) {
        case "connect":      OnCommandConnect(msg); break;
        case "call":         OnCommandCall(msg); break;
        case "close":        OnCommandClose(msg); break;
        case "createStream": OnCommandCreateStream(msg); break;
        }
      }
      else {
        Logger.Debug("NetStream ({0}) command: {1}", msg.StreamId, msg.CommandName);
        //NetStream commands
        switch (msg.CommandName) {
        case "publish": OnCommandPublish(msg); break;
        case "play":
        case "play2":
        case "deleteStream":
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

    void OnCommandConnect(CommandMessage msg)
    {
      objectEncoding = ((int)msg.CommandObject["objectEncoding"])==3 ? 3 : 0;
      clientName     = (string)msg.CommandObject["flashVer"];
      Logger.Debug("connect: objectEncoding {0}, flashVer: {1}", objectEncoding, clientName);
      SendMessage(2, new SetWindowSizeMessage(this.Now, 0, 0xFFFFFFFF));
      SendMessage(2, new SetPeerBandwidthMessage(this.Now, 0, 0xFFFFFFFF, PeerBandwidthLimitType.Hard));
      SendMessage(2, new UserControlMessage.StreamBeginMessage(this.Now, 0, 0));
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
      SendMessage(3, response);
    }

    void OnCommandCall(CommandMessage msg)
    {
    }

    void OnCommandClose(CommandMessage msg)
    {
    }

    void OnCommandCreateStream(CommandMessage msg)
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
      SendMessage(3, response);
    }

    void OnCommandPublish(CommandMessage msg)
    {
      var name = (string)msg.Arguments[0];
      var type = (string)msg.Arguments[1];
      Logger.Debug("publish: name {0}, type: {1}", name, type);
      SendMessage(2, new UserControlMessage.StreamBeginMessage(this.Now, 0, msg.StreamId));
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
      SendMessage(3, status);
      if (msg.TransactionId==0) return;
      var result = CommandMessage.Create(
        objectEncoding,
        this.Now,
        msg.StreamId,
        "_result",
        msg.TransactionId,
        null
      );
      SendMessage(3, status);
      this.state = ConnectionState.Receiving;
    }

    void OnAggregate(AggregateMessage msg)
    {
      ProcessMessages(msg.Messages);
    }

    protected void Recv(byte[] dst, int offset, int len)
    {
      RecvUntil(stream => stream.Read(dst, offset, len)>=len);
    }

    protected void RecvUntil(Func<Stream,bool> proc)
    {
      WaitAndProcessEvents(connection.ReceiveWaitHandle, stopped => {
        if (stopped) {
          throw new ConnectionStoppedExcception();
        }
        else if (connection.Recv(proc)) {
          return null;
        }
        else {
          return connection.ReceiveWaitHandle;
        }
      });
    }

    protected byte[] Recv(int len)
    {
      var dst = new byte[len];
      Recv(dst, 0, len);
      return dst;
    }

    protected void Send(Action<Stream> proc)
    {
      connection.Send(proc);
    }

    protected void Send(byte[] data, int offset, int len)
    {
      connection.Send(data, offset, len);
    }

    protected void Send(byte[] data)
    {
      Send(data, 0, data.Length);
    }

    protected bool WaitAndProcessEvents(WaitHandle wait_handle, Func<bool,WaitHandle> on_signal)
    {
      var handles = new WaitHandle[] {
        SyncContext.EventHandle,
        null,
      };
      bool event_processed = false;
      while (wait_handle!=null) {
        handles[1] = wait_handle;
        var idx = WaitHandle.WaitAny(handles);
        if (idx==0) {
          SyncContext.ProcessAll();
          if (IsStopped) {
            wait_handle = on_signal(IsStopped);
          }
          event_processed = true;
        }
        else {
          wait_handle = on_signal(IsStopped);
        }
      }
      if (!event_processed) {
        SyncContext.ProcessAll();
      }
      return true;
    }

    protected bool WaitAndProcessEvents(IAsyncResult ar, Func<IAsyncResult,bool,IAsyncResult> on_signal)
    {
      if (ar==null) {
        SyncContext.ProcessAll();
        return true;
      }
      return WaitAndProcessEvents(ar.AsyncWaitHandle, stopped => {
        ar = on_signal(ar, stopped);
        if (ar!=null) return ar.AsyncWaitHandle;
        else          return null;
      });
    }

  }

  public class RTMPSourceStream
    : SourceStreamBase
  {
    public RTMPSourceStream(PeerCast peercast, Channel channel, Uri source_uri)
      : base(peercast, channel, source_uri)
    {
    }

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
      return new RTMPSourceConnection(PeerCast, Channel, source_uri);
    }

    protected override void OnConnectionStopped(SourceStreamBase.ConnectionStoppedEvent msg)
    {
      switch (msg.StopReason) {
      case StopReason.UserReconnect:
        break;
      case StopReason.UserShutdown:
        Stop(msg.StopReason);
        break;
      case StopReason.NoHost:
        Stop(msg.StopReason);
        break;
      default:
        Reconnect();
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
