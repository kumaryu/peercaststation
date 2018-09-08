using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PeerCastStation.Core;

namespace PeerCastStation.FLV.RTMP
{
  public class RTMPConnection
    : IDisposable
  {
    private static Logger logger = new Logger(typeof(RTMPConnection));
    private Stream inputStream;
    private Stream outputStream;
    public RTMPConnection(Stream input_stream, Stream output_stream)
    {
      this.inputStream  = input_stream;
      this.outputStream = output_stream;
    }

    protected Stream InputStream  { get { return inputStream; } }
    protected Stream OutputStream { get { return outputStream; } }
    public string ClientName { get; private set; }

    private bool disposed = false;
    public virtual void Dispose()
    {
      disposed = true;
      this.OnClose();
      this.inputStream.Close();
      this.outputStream.Close();
    }

    protected virtual void OnClose()
    {
    }

    public void Close()
    {
      Dispose();
    }

    private class QueuedMessage
    {
      public enum MessageDirection {
        In,
        Out,
      }

      public static readonly System.Diagnostics.Stopwatch Stopwatch = new System.Diagnostics.Stopwatch();
      static QueuedMessage()
      {
        Stopwatch.Reset();
        Stopwatch.Start();
      }

      public MessageDirection Direction { get; private set; }
      public int ChunkStreamId { get; private set; }
      public RTMPMessage Message { get; private set; }
      public TimeSpan TimeStamp { get; private set; }
      public QueuedMessage(MessageDirection direction, int chunk_stream_id, RTMPMessage msg)
      {
        this.Direction     = direction;
        this.ChunkStreamId = chunk_stream_id;
        this.Message       = msg;
        this.TimeStamp     = Stopwatch.Elapsed;
      }
    }
    private MessageQueue<QueuedMessage> messageQueue = new MessageQueue<QueuedMessage>();

    protected void PostMessage(int chunk_stream_id, RTMPMessage msg)
    {
      messageQueue.Enqueue(new QueuedMessage(QueuedMessage.MessageDirection.Out, chunk_stream_id, msg));
    }

    public async Task Run(CancellationToken cancel_token)
    {
      try {
        cancel_token.ThrowIfCancellationRequested();
        await Handshake(cancel_token).ConfigureAwait(false);
        await RecvAndProcessMessages(cancel_token).ConfigureAwait(false);
      }
      catch (IOException e) {
        if (!disposed) {
          logger.Error(e);
        }
      }
      catch (AggregateException e) {
        if (!disposed) {
          logger.Error(e);
        }
      }
      finally {
        Close();
      }
    }

    protected async Task RecvAndProcessMessages(CancellationToken cancel_token)
    {
      var local_cancel = new CancellationTokenSource();
      cancel_token.Register(() => local_cancel.Cancel());
      var recv_message_task = Task.Run(async () => {
        try {
          while (!local_cancel.IsCancellationRequested) {
            await RecvMessage(messageQueue, local_cancel.Token).ConfigureAwait(false);
          }
        }
        finally {
          local_cancel.Cancel();
        }
      });
      while (!local_cancel.IsCancellationRequested) {
        var msg = await messageQueue.DequeueAsync(local_cancel.Token).ConfigureAwait(false);
        switch (msg.Direction) {
        case QueuedMessage.MessageDirection.In:
          await ProcessMessage(msg.Message, local_cancel.Token).ConfigureAwait(false);
          FlushBuffer();
          break;
        case QueuedMessage.MessageDirection.Out:
          await SendMessage(msg.ChunkStreamId, msg.Message, local_cancel.Token).ConfigureAwait(false);
          break;
        }
      }
      await recv_message_task.ConfigureAwait(false);
    }

    System.Diagnostics.Stopwatch timestampTimer = new System.Diagnostics.Stopwatch();

    private static readonly byte[] GenuineFMSKey = {
      0x47, 0x65, 0x6E, 0x75, 0x69, 0x6E, 0x65, 0x20, 0x41, 0x64, 0x6F, 0x62,
      0x65, 0x20, 0x46, 0x6C, 0x61, 0x73, 0x68, 0x20, 0x4D, 0x65, 0x64, 0x69,
      0x61, 0x20, 0x53, 0x65, 0x72, 0x76, 0x65, 0x72, 0x20, 0x30, 0x30, 0x31,
      0xF0, 0xEE, 0xC2, 0x4A, 0x80, 0x68, 0xBE, 0xE8, 0x2E, 0x00, 0xD0, 0xD1,
      0x02, 0x9E, 0x7E, 0x57, 0x6E, 0xEC, 0x5D, 0x2D, 0x29, 0x80, 0x6F, 0xAB,
      0x93, 0xB8, 0xE6, 0x36, 0xCF, 0xEB, 0x31, 0xAE,
    };

    private static readonly byte[] GenuineFPKey = {
      0x47, 0x65, 0x6E, 0x75, 0x69, 0x6E, 0x65, 0x20, 0x41, 0x64, 0x6F, 0x62,
      0x65, 0x20, 0x46, 0x6C, 0x61, 0x73, 0x68, 0x20, 0x50, 0x6C, 0x61, 0x79,
      0x65, 0x72, 0x20, 0x30, 0x30, 0x31, 0xF0, 0xEE, 0xC2, 0x4A, 0x80, 0x68,
      0xBE, 0xE8, 0x2E, 0x00, 0xD0, 0xD1, 0x02, 0x9E, 0x7E, 0x57, 0x6E, 0xEC,
      0x5D, 0x2D, 0x29, 0x80, 0x6F, 0xAB, 0x93, 0xB8, 0xE6, 0x36, 0xCF, 0xEB,
      0x31, 0xAE,
    };

    private enum DigestPosition {
      Unknown,
      First,
      Second,
    };

    private int GetDigestOffset(byte[] vec, DigestPosition pos)
    {
      switch (pos) {
      case DigestPosition.First:
        return (vec[8]+vec[9]+vec[10]+vec[11]) % 728 + 12;
      case DigestPosition.Second:
        return (vec[772]+vec[773]+vec[774]+vec[775]) % 728 + 776;
      default:
        throw new ArgumentException();
      }
    }

    private byte[] ComputeHandshakeDigest1(byte[] vec, byte[] key, int doffset)
    {
      var msg = new byte[vec.Length-32];
      Array.Copy(vec, 0, msg, 0, doffset);
      Array.Copy(vec, doffset+32, msg, doffset, vec.Length-32-doffset);
      var hasher = new System.Security.Cryptography.HMACSHA256(key);
      return hasher.ComputeHash(msg);
    }

    private byte[] ComputeHandshakeDigest2(byte[] keyvec, DigestPosition keypos, byte[] vec, byte[] key)
    {
      var doffset = GetDigestOffset(keyvec, keypos);
      var hasher1 = new System.Security.Cryptography.HMACSHA256(key);
      var key2 = hasher1.ComputeHash(keyvec, doffset, 32);
      var hasher2 = new System.Security.Cryptography.HMACSHA256(key2);
      return hasher2.ComputeHash(vec, 0, vec.Length-32);
    }

    private byte[] SetServerHandshakeDigest1(byte[] vec, DigestPosition pos)
    {
      var doffset = GetDigestOffset(vec, pos);
      var key = new byte[36];
      Array.Copy(GenuineFMSKey, key, 36);
      var hash = ComputeHandshakeDigest1(vec, key, doffset);
      Array.Copy(hash, 0, vec, doffset, 32);
      return vec;
    }

    private byte[] SetServerHandshakeDigest2(byte[] c1, DigestPosition pos)
    {
      var hash = ComputeHandshakeDigest2(c1, pos, c1, GenuineFMSKey);
      Array.Copy(hash, 0, c1, c1.Length-32, 32);
      return c1;
    }

    private bool ValidateClientHandshakeDigest(byte[] vec, int doffset)
    {
      var key = new byte[30];
      Array.Copy(GenuineFPKey, key, 30);
      var hash = ComputeHandshakeDigest1(vec, key, doffset);
      return Enumerable.Range(doffset, 32).Select(i => vec[i]).SequenceEqual(hash);
    }

    private DigestPosition ValidateClientHandshakeDigest1(byte[] vec)
    {
      if (ValidateClientHandshakeDigest(vec, GetDigestOffset(vec, DigestPosition.First))) {
        return DigestPosition.First;
      }
      if (ValidateClientHandshakeDigest(vec, GetDigestOffset(vec, DigestPosition.Second))) {
        return DigestPosition.Second;
      }
      return DigestPosition.Unknown;
    }

    private bool ValidateClientHandshakeDigest2(byte[] vec, byte[] s1, DigestPosition pos)
    {
      var hash = ComputeHandshakeDigest2(s1, pos, vec, GenuineFPKey);
      return Enumerable.Range(vec.Length-32, 32).Select(i => vec[i]).SequenceEqual(hash);
    }

    private async Task HandshakeNew(RTMPBinaryReader c1reader, CancellationToken cancel_token)
    {
      var s1 = new byte[1536];
      var rand = new Random();
      rand.NextBytes(s1);
      //timestamp
      s1[0] = s1[1] = s1[2] = s1[3] = 0;
      //version
      s1[4] = 3;
      s1[5] = 5;
      s1[6] = 1;
      s1[7] = 1;
      s1 = SetServerHandshakeDigest1(s1, DigestPosition.First);
      await SendAsync(s1, cancel_token).ConfigureAwait(false);

      var c1 = c1reader.ReadBytes(1536);
      var c1pos = ValidateClientHandshakeDigest1(c1);
      if (c1pos==DigestPosition.Unknown) {
        throw new InvalidDataException("C1 digest is not matched.");
      }

      var s2 = SetServerHandshakeDigest2(c1, c1pos);
      await SendAsync(s2, cancel_token).ConfigureAwait(false);

      using (var c2reader = await RecvAsync(1536, cancel_token).ConfigureAwait(false)) {
        var c2 = c2reader.ReadBytes(1536);
        if (!ValidateClientHandshakeDigest2(c2, s1, DigestPosition.First)) {
          throw new InvalidDataException("C2 digest is not matched.");
        }
      }
    }

    private async Task HandshakeOld(RTMPBinaryReader c1reader, CancellationToken cancel_token)
    {
      var s1vec = new byte[1528];
      var rand = new Random();
      rand.NextBytes(s1vec);
      await SendAsync(writer => {
        writer.Write(0);
        writer.Write(0);
        writer.Write(s1vec);
      }, cancel_token).ConfigureAwait(false);

      var c1time = c1reader.ReadInt32();
      var c1ver  = c1reader.ReadInt32();
      var c1vec  = c1reader.ReadBytes(1528);
      await SendAsync(writer => {
        writer.Write(c1time);
        writer.Write(c1ver);
        writer.Write(c1vec);
      }, cancel_token).ConfigureAwait(false);

      using (var reader=await RecvAsync(1536, cancel_token).ConfigureAwait(false)) {
        var c2time = reader.ReadInt32();
        var c2zero = reader.ReadInt32();
        var c2vec = reader.ReadBytes(1528);
        if (!s1vec.SequenceEqual(c2vec)) {
          throw new InvalidDataException("C2 random vector is not matched.");
        }
      }
    }

    private async Task Handshake(CancellationToken cancel_token)
    {
      using (var reader=await RecvAsync(1, cancel_token).ConfigureAwait(false)) {
        var c0 = reader.ReadByte();
        if (c0!=3) {
          throw new InvalidDataException();
        }
      }
      await SendAsync(new byte[] { 0x03 }, cancel_token).ConfigureAwait(false);

      using (var c1reader=await RecvAsync(1536, cancel_token).ConfigureAwait(false)) {
        var c1time = c1reader.ReadInt32();
        var c1ver  = c1reader.ReadInt32();
        if (c1ver==0) {
          c1reader.BaseStream.Seek(0, SeekOrigin.Begin);
          await HandshakeOld(c1reader, cancel_token).ConfigureAwait(false);
        }
        else {
          c1reader.BaseStream.Seek(0, SeekOrigin.Begin);
          await HandshakeNew(c1reader, cancel_token).ConfigureAwait(false);
        }
      }
      timestampTimer.Reset();
      timestampTimer.Start();
      logger.Debug("Handshake completed");
    }

    protected long Now {
      get { return timestampTimer.ElapsedMilliseconds; }
    }

    protected int ObjectEncoding {
      get { return objectEncoding; }
    }

    int nextClientId    = 1;
    int nextStreamId    = 1;
    int objectEncoding  = 0;
    int sendChunkSize   = 1536;
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
        await InputStream.ReadBytesAsync(buf, offset, len1, cancel_token).ConfigureAwait(false);
        receivedSize   += len1;
        sequenceNumber += len1;
        await SendMessage(2, new AckMessage(this.Now, 0, sequenceNumber), cancel_token).ConfigureAwait(false);
        var len2 = len - len1;
        await InputStream.ReadBytesAsync(buf, offset+len1, len2, cancel_token).ConfigureAwait(false);
        receivedSize    = len2; //reset
        sequenceNumber += len2;
      }
      else {
        await InputStream.ReadBytesAsync(buf, offset, len, cancel_token).ConfigureAwait(false);
        receivedSize   += len;
        sequenceNumber += len;
      }
      return len;
    }

    private async Task<byte[]> RecvStream(int len, CancellationToken cancel_token)
    {
      var buf = new byte[len];
      await RecvStream(buf, 0, len, cancel_token).ConfigureAwait(false);
      return buf;
    }

    protected class RTMPMessageBuilder
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
    private async Task<bool> RecvMessage(MessageQueue<QueuedMessage> messages, CancellationToken cancel_token)
    {
      var basic_header = (await RecvStream(1, cancel_token).ConfigureAwait(false))[0];
      var chunk_stream_id = basic_header & 0x3F;
      if (chunk_stream_id==0) {
        chunk_stream_id = (await RecvStream(1, cancel_token).ConfigureAwait(false))[0] + 64;
      }
      else if (chunk_stream_id==1) {
        var buf = await RecvStream(2, cancel_token).ConfigureAwait(false);
        chunk_stream_id = (buf[1]*256 | buf[0]) + 64;
      }

      RTMPMessageBuilder msg = null;
      RTMPMessageBuilder last_msg = null;
      if (!lastMessages.TryGetValue(chunk_stream_id, out last_msg)) {
        last_msg = RTMPMessageBuilder.NullPacket;
      }
      switch ((basic_header & 0xC0)>>6) {
      case 0:
        using (var reader=new RTMPBinaryReader(await RecvStream(11, cancel_token).ConfigureAwait(false))) {
          long timestamp  = reader.ReadUInt24();
          var body_length = reader.ReadUInt24();
          var type_id     = reader.ReadByte();
          var stream_id   = reader.ReadUInt32LE();
          if (timestamp==0xFFFFFF) {
            using (var ext_reader=new RTMPBinaryReader(await RecvStream(4, cancel_token).ConfigureAwait(false))) {
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
        using (var reader=new RTMPBinaryReader(await RecvStream(7, cancel_token).ConfigureAwait(false))) {
          long timestamp_delta = reader.ReadUInt24();
          var body_length      = reader.ReadUInt24();
          var type_id          = reader.ReadByte();
          if (timestamp_delta==0xFFFFFF) {
            using (var ext_reader=new RTMPBinaryReader(await RecvStream(4, cancel_token).ConfigureAwait(false))) {
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
        using (var reader=new RTMPBinaryReader(await RecvStream(3, cancel_token).ConfigureAwait(false))) {
          long timestamp_delta = reader.ReadUInt24();
          if (timestamp_delta==0xFFFFFF) {
            using (var ext_reader=new RTMPBinaryReader(await RecvStream(4, cancel_token).ConfigureAwait(false))) {
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
        cancel_token).ConfigureAwait(false);
      if (msg.ReceivedLength>=msg.BodyLength) {
        messages.Enqueue(new QueuedMessage(QueuedMessage.MessageDirection.In, chunk_stream_id, msg.ToMessage()));
      }
      return true; //TODO:接続エラー時はfalseを返す
    }

    protected async Task SendMessage(int chunk_stream_id, RTMPMessage msg, CancellationToken cancel_token)
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
          }, cancel_token).ConfigureAwait(false);
          fmt = 3;
          break;
        case 3:
          await SendAsync(writer => {
            writer.Write((byte)((fmt<<6) | chunk_stream_id));
            int chunk_len = Math.Min(sendChunkSize, msg.Body.Length-offset);
            writer.Write(msg.Body, offset, chunk_len);
            offset += chunk_len;
          }, cancel_token).ConfigureAwait(false);
          break;
        }
      }
    }

    protected virtual async Task ProcessMessages(IEnumerable<RTMPMessage> messages, CancellationToken cancel_token)
    {
      foreach (var msg in messages) {
        await ProcessMessage(msg, cancel_token).ConfigureAwait(false);
      }
      FlushBuffer();
    }

    protected virtual async Task ProcessMessage(RTMPMessage msg, CancellationToken cancel_token)
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
        await OnCommand(new CommandAMF3Message(msg), cancel_token).ConfigureAwait(false);
        break;
      case RTMPMessageType.CommandAMF0:
        await OnCommand(new CommandAMF0Message(msg), cancel_token).ConfigureAwait(false);
        break;
      case RTMPMessageType.Aggregate:
        await OnAggregate(new AggregateMessage(msg), cancel_token).ConfigureAwait(false);
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

    protected virtual Task OnSetChunkSize(SetChunkSizeMessage msg, CancellationToken cancel_token)
    {
      recvChunkSize = Math.Min(msg.ChunkSize, 0xFFFFFF);
      return Task.Delay(0);
    }

    protected virtual Task OnAbort(AbortMessage msg, CancellationToken cancel_token)
    {
      RTMPMessageBuilder builder;
      if (lastMessages.TryGetValue(msg.TargetChunkStream, out builder)) {
        builder.Abort();
      }
      return Task.Delay(0);
    }

    protected virtual Task OnUserControl(UserControlMessage msg, CancellationToken cancel_token)
    {
      return Task.Delay(0);
    }

    protected virtual Task OnSetWindowSize(SetWindowSizeMessage msg, CancellationToken cancel_token)
    {
      recvWindowSize = msg.WindowSize;
      receivedSize = 0;
      return Task.Delay(0);
    }

    protected virtual Task OnSetPeerBandwidth(SetPeerBandwidthMessage msg, CancellationToken cancel_token)
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

    protected virtual Task OnAudio(RTMPMessage msg, CancellationToken cancel_token)
    {
      return Task.Delay(0);
    }

    protected virtual Task OnVideo(RTMPMessage msg, CancellationToken cancel_token)
    {
      return Task.Delay(0);
    }

    protected virtual Task OnData(DataMessage msg, CancellationToken cancel_token)
    {
      return Task.Delay(0);
    }

    protected virtual void FlushBuffer()
    {
    }

    private async Task OnCommand(CommandMessage msg, CancellationToken cancel_token)
    {
      if (msg.StreamId==0) {
        logger.Debug("NetConnection command: {0}", msg.CommandName);
        //NetConnection commands
        switch (msg.CommandName) {
        case "connect":      await OnCommandConnect(msg, cancel_token).ConfigureAwait(false); break;
        case "call":         await OnCommandCall(msg, cancel_token).ConfigureAwait(false); break;
        case "close":        await OnCommandClose(msg, cancel_token).ConfigureAwait(false); break;
        case "createStream": await OnCommandCreateStream(msg, cancel_token).ConfigureAwait(false); break;
        case "deleteStream": await OnCommandDeleteStream(msg, cancel_token).ConfigureAwait(false); break;
        }
      }
      else {
        logger.Debug("NetStream ({0}) command: {1}", msg.StreamId, msg.CommandName);
        //NetStream commands
        switch (msg.CommandName) {
        case "publish": await OnCommandPublish(msg, cancel_token).ConfigureAwait(false); break;
        case "deleteStream": await OnCommandDeleteStream(msg, cancel_token).ConfigureAwait(false); break;
        case "play":         await OnCommandPlay(msg, cancel_token).ConfigureAwait(false); break;
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
      ClientName     = (string)msg.CommandObject["flashVer"];
      logger.Debug("connect: objectEncoding {0}, flashVer: {1}", objectEncoding, ClientName);
      await SendMessage(2, new SetChunkSizeMessage(this.Now, 0, sendChunkSize), cancel_token).ConfigureAwait(false);
      await SendMessage(2, new SetWindowSizeMessage(this.Now, 0, recvWindowSize), cancel_token).ConfigureAwait(false);
      await SendMessage(2, new SetPeerBandwidthMessage(this.Now, 0, sendWindowSize, PeerBandwidthLimitType.Hard), cancel_token).ConfigureAwait(false);
      await SendMessage(2, new UserControlMessage.StreamBeginMessage(this.Now, 0, 0), cancel_token).ConfigureAwait(false);
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
        await SendMessage(3, response, cancel_token).ConfigureAwait(false);
      }
    }

    protected virtual Task OnCommandCall(CommandMessage msg, CancellationToken cancel_token)
    {
      return Task.Delay(0);
    }

    protected virtual Task OnCommandClose(CommandMessage msg, CancellationToken cancel_token)
    {
      return Task.Delay(0);
    }

    protected virtual async Task OnCommandCreateStream(CommandMessage msg, CancellationToken cancel_token)
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
        await SendMessage(3, response, cancel_token).ConfigureAwait(false);
      }
    }

    protected virtual Task OnCommandDeleteStream(CommandMessage msg, CancellationToken cancel_token)
    {
      return Task.Delay(0);
    }

    protected virtual Task OnCommandPublish(CommandMessage msg, CancellationToken cancel_token)
    {
      timestampTimer.Start();
      return Task.Delay(0);
    }

    protected virtual Task OnCommandPlay(CommandMessage msg, CancellationToken cancel_token)
    {
      timestampTimer.Start();
      return Task.Delay(0);
    }

    protected virtual Task OnAggregate(AggregateMessage msg, CancellationToken cancel_token)
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
      return SendAsync(memstream.ToArray(), cancel_token);
    }

    protected async Task<RTMPBinaryReader> RecvAsync(int len, CancellationToken cancel_token)
    {
      var buf = await InputStream.ReadBytesAsync(len, cancel_token).ConfigureAwait(false);
      return new RTMPBinaryReader(new MemoryStream(buf, false), false);
    }

    protected Task SendAsync(byte[] data, CancellationToken cancel_token)
    {
      return OutputStream.WriteAsync(data, 0, data.Length, cancel_token);
    }


  }
}
