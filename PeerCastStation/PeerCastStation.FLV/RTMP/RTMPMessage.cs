using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using PeerCastStation.FLV.AMF;

namespace PeerCastStation.FLV.RTMP
{
  public enum RTMPMessageType
  {
    SetChunkSize     = 1,
    Abort            = 2,
    Ack              = 3,
    UserControl      = 4,
    SetWindowSize    = 5,
    SetPeerBandwidth = 6,
    Audio            = 8,
    Video            = 9,
    DataAMF3         = 15,
    SharedObjectAMF3 = 16,
    CommandAMF3      = 17,
    DataAMF0         = 18,
    SharedObjectAMF0 = 19,
    CommandAMF0      = 20,
    Aggregate        = 22,
  }

  public class RTMPMessage
  {
    public RTMPMessageType MessageType { get; private set; }
    public long Timestamp { get; private set; }
    public long StreamId  { get; private set; }
    public byte[] Body    { get; private set; }

    public RTMPMessage(
      RTMPMessageType message_type,
      long timestamp,
      long stream_id,
      byte[] body)
    {
      MessageType = message_type;
      Timestamp   = timestamp;
      StreamId    = stream_id;
      Body        = body;
    }

    protected RTMPMessage(RTMPMessage x)
    {
      Timestamp     = x.Timestamp;
      MessageType   = x.MessageType;
      StreamId      = x.StreamId;
      Body          = x.Body;
    }
  }

  public class SetChunkSizeMessage
    : RTMPMessage
  {
    public int ChunkSize { get; private set; }
    public SetChunkSizeMessage(long timestamp, long stream_id, int chunk_size)
      : base(RTMPMessageType.SetChunkSize, timestamp, stream_id, CreateBody(chunk_size))
    {
      ChunkSize = chunk_size;
    }

    public SetChunkSizeMessage(RTMPMessage x)
      : base(x)
    {
      using (var reader=new RTMPBinaryReader(new MemoryStream(x.Body))) {
        ChunkSize = (int)Math.Min(0x7FFFFFFL, reader.ReadUInt32());
      }
    }

    private static byte[] CreateBody(int chunk_size)
    {
      var s = new MemoryStream();
      using (var writer=new RTMPBinaryWriter(s)) {
        writer.WriteUInt32(Math.Max(1, Math.Min(chunk_size, 0x7FFFFFFF)));
      }
      return s.ToArray();
    }
  }

  public class AbortMessage
    : RTMPMessage
  {
    public int TargetChunkStream { get; private set; }
    public AbortMessage(long timestamp, long stream_id, int target_chunk_stream)
      : base(RTMPMessageType.Abort, timestamp, stream_id, CreateBody(target_chunk_stream))
    {
      TargetChunkStream = target_chunk_stream;
    }

    public AbortMessage(RTMPMessage x)
      : base(x)
    {
      using (var reader=new RTMPBinaryReader(new MemoryStream(x.Body))) {
        TargetChunkStream = (int)reader.ReadUInt32();
      }
    }

    private static byte[] CreateBody(int target_chunk_stream)
    {
      var s = new MemoryStream();
      using (var writer=new RTMPBinaryWriter(s)) {
        writer.WriteUInt32(target_chunk_stream);
      }
      return s.ToArray();
    }
  }

  public class AckMessage
    : RTMPMessage
  {
    public long SequenceNumber { get; private set; }
    public AckMessage(long timestamp, long stream_id, long sequence_number)
      : base(RTMPMessageType.Ack, timestamp, stream_id, CreateBody(sequence_number))
    {
      SequenceNumber = sequence_number;
    }

    public AckMessage(RTMPMessage x)
      : base(x)
    {
      using (var reader=new RTMPBinaryReader(new MemoryStream(x.Body))) {
        SequenceNumber = reader.ReadUInt32();
      }
    }

    private static byte[] CreateBody(long sequence_number)
    {
      var s = new MemoryStream();
      using (var writer=new RTMPBinaryWriter(s)) {
        writer.WriteUInt32(sequence_number & 0xFFFFFFFF);
      }
      return s.ToArray();
    }
  }

  public class SetWindowSizeMessage
    : RTMPMessage
  {
    public long WindowSize { get; private set; }
    public SetWindowSizeMessage(long timestamp, long stream_id, long window_size)
      : base(RTMPMessageType.SetWindowSize, timestamp, stream_id, CreateBody(window_size))
    {
      WindowSize = window_size;
    }

    public SetWindowSizeMessage(RTMPMessage x)
      : base(x)
    {
      using (var reader=new RTMPBinaryReader(new MemoryStream(x.Body))) {
        WindowSize = reader.ReadUInt32();
      }
    }

    private static byte[] CreateBody(long window_size)
    {
      var s = new MemoryStream();
      using (var writer=new RTMPBinaryWriter(s)) {
        writer.WriteUInt32(window_size);
      }
      return s.ToArray();
    }
  }

  public enum PeerBandwidthLimitType
  {
    Hard    = 0,
    Soft    = 1,
    Dynamic = 2,
  }

  public class SetPeerBandwidthMessage
    : RTMPMessage
  {
    public long PeerBandwidth { get; private set; }
    public PeerBandwidthLimitType LimitType { get; private set; }
    public SetPeerBandwidthMessage(long timestamp, long stream_id, long bandwidth, PeerBandwidthLimitType limit_type)
      : base(RTMPMessageType.SetPeerBandwidth, timestamp, stream_id, CreateBody(bandwidth, limit_type))
    {
      PeerBandwidth = bandwidth;
    }

    public SetPeerBandwidthMessage(RTMPMessage x)
      : base(x)
    {
      using (var reader=new RTMPBinaryReader(new MemoryStream(x.Body))) {
        PeerBandwidth = reader.ReadUInt32();
        LimitType = (PeerBandwidthLimitType)reader.ReadByte();
      }
    }

    private static byte[] CreateBody(long bandwidth, PeerBandwidthLimitType limit_type)
    {
      var s = new MemoryStream();
      using (var writer=new RTMPBinaryWriter(s)) {
        writer.WriteUInt32(bandwidth);
        writer.Write((byte)limit_type);
      }
      return s.ToArray();
    }
  }

  public enum UserControlMessageType
  {
    StreamBegin      = 0,
    StreamEOF        = 1,
    StreamDry        = 2,
    SetBufferLength  = 3,
    StreamIsRecorded = 4,
    PingRequest      = 6,
    PingResponse     = 7,
  }

  public class UserControlMessage
    : RTMPMessage
  {
    public UserControlMessageType UserControlMessageType { get; private set; }
    public byte[] UserControlMessagePayload { get; private set; }
    public UserControlMessage(long timestamp, long stream_id, UserControlMessageType message_type, byte[] payload)
      : base(RTMPMessageType.UserControl, timestamp, stream_id, CreateBody(message_type, payload))
    {
      UserControlMessageType = message_type;
      UserControlMessagePayload = payload;
    }

    public UserControlMessage(RTMPMessage x)
      : base(x)
    {
      using (var reader=new RTMPBinaryReader(new MemoryStream(x.Body))) {
        UserControlMessageType = (UserControlMessageType)reader.ReadUInt16();
        UserControlMessagePayload = reader.ReadBytes(x.Body.Length-2);
      }
    }

    private static byte[] CreateBody(UserControlMessageType message_type, byte[] payload)
    {
      var s = new MemoryStream();
      using (var writer=new RTMPBinaryWriter(s)) {
        writer.WriteUInt16((int)message_type);
        writer.Write(payload);
      }
      return s.ToArray();
    }

    public class StreamBeginMessage
      : RTMPMessage
    {
      public long TargetStreamId { get; private set; }
      public StreamBeginMessage(
        long timestamp, 
        long stream_id, 
        long target_stream_id)
        : base(RTMPMessageType.UserControl, timestamp, stream_id, CreateBody(target_stream_id))
      {
        TargetStreamId = target_stream_id;
      }

      public StreamBeginMessage(RTMPMessage x)
        : base(x)
      {
        using (var reader=new RTMPBinaryReader(new MemoryStream(x.Body))) {
          var type = (UserControlMessageType)reader.ReadUInt16();
          TargetStreamId = reader.ReadUInt32();
        }
      }

      private static byte[] CreateBody(long target_stream_id)
      {
        var s = new MemoryStream();
        using (var writer=new RTMPBinaryWriter(s)) {
          writer.WriteUInt16((int)UserControlMessageType.StreamBegin);
          writer.WriteUInt32(target_stream_id);
        }
        return s.ToArray();
      }
    }

    public class StreamEOFMessage
      : RTMPMessage
    {
      public long TargetStreamId { get; private set; }
      public StreamEOFMessage(
        long timestamp, 
        long stream_id, 
        long target_stream_id)
        : base(RTMPMessageType.UserControl, timestamp, stream_id, CreateBody(target_stream_id))
      {
        TargetStreamId = target_stream_id;
      }

      public StreamEOFMessage(RTMPMessage x)
        : base(x)
      {
        using (var reader=new RTMPBinaryReader(new MemoryStream(x.Body))) {
          var type = (UserControlMessageType)reader.ReadUInt16();
          TargetStreamId = reader.ReadUInt32();
        }
      }

      private static byte[] CreateBody(long target_stream_id)
      {
        var s = new MemoryStream();
        using (var writer=new RTMPBinaryWriter(s)) {
          writer.WriteUInt16((int)UserControlMessageType.StreamEOF);
          writer.WriteUInt32(target_stream_id);
        }
        return s.ToArray();
      }
    }

    public class StreamDryMessage
      : RTMPMessage
    {
      public long TargetStreamId { get; private set; }
      public StreamDryMessage(
        long timestamp, 
        long stream_id, 
        long target_stream_id)
        : base(RTMPMessageType.UserControl, timestamp, stream_id, CreateBody(target_stream_id))
      {
        TargetStreamId = target_stream_id;
      }

      public StreamDryMessage(RTMPMessage x)
        : base(x)
      {
        using (var reader=new RTMPBinaryReader(new MemoryStream(x.Body))) {
          var type = (UserControlMessageType)reader.ReadUInt16();
          TargetStreamId = reader.ReadUInt32();
        }
      }

      private static byte[] CreateBody(long target_stream_id)
      {
        var s = new MemoryStream();
        using (var writer=new RTMPBinaryWriter(s)) {
          writer.WriteUInt16((int)UserControlMessageType.StreamDry);
          writer.WriteUInt32(target_stream_id);
        }
        return s.ToArray();
      }
    }

    public class SetBufferLengthMessage
      : RTMPMessage
    {
      public long TargetStreamId { get; private set; }
      public TimeSpan BufferLength { get; private set; }
      public SetBufferLengthMessage(
        long timestamp, 
        long stream_id, 
        long target_stream_id,
        TimeSpan buffer_length)
        : base(RTMPMessageType.UserControl, timestamp, stream_id, CreateBody(target_stream_id, buffer_length))
      {
        TargetStreamId = target_stream_id;
        BufferLength   = buffer_length;
      }

      public SetBufferLengthMessage(RTMPMessage x)
        : base(x)
      {
        using (var reader=new RTMPBinaryReader(new MemoryStream(x.Body))) {
          var type = (UserControlMessageType)reader.ReadUInt16();
          TargetStreamId = reader.ReadUInt32();
          BufferLength   = TimeSpan.FromMilliseconds(reader.ReadUInt32());
        }
      }

      private static byte[] CreateBody(long target_stream_id, TimeSpan buffer_length)
      {
        var s = new MemoryStream();
        using (var writer=new RTMPBinaryWriter(s)) {
          writer.WriteUInt16((int)UserControlMessageType.SetBufferLength);
          writer.WriteUInt32(target_stream_id);
          writer.WriteUInt32((long)buffer_length.TotalMilliseconds);
        }
        return s.ToArray();
      }
    }

    public class StreamIsRecordedMessage
      : RTMPMessage
    {
      public long TargetStreamId { get; private set; }
      public StreamIsRecordedMessage(
        long timestamp,
        long stream_id,
        long target_stream_id)
        : base(RTMPMessageType.UserControl, timestamp, stream_id, CreateBody(target_stream_id))
      {
        TargetStreamId = target_stream_id;
      }

      public StreamIsRecordedMessage(RTMPMessage x)
        : base(x)
      {
        using (var reader=new RTMPBinaryReader(new MemoryStream(x.Body))) {
          var type = (UserControlMessageType)reader.ReadUInt16();
          TargetStreamId = reader.ReadUInt32();
        }
      }

      private static byte[] CreateBody(long target_stream_id)
      {
        var s = new MemoryStream();
        using (var writer=new RTMPBinaryWriter(s)) {
          writer.WriteUInt16((int)UserControlMessageType.StreamIsRecorded);
          writer.WriteUInt32(target_stream_id);
        }
        return s.ToArray();
      }
    }

    public class PingRequestMessage
      : RTMPMessage
    {
      public long LocalTimestamp { get; private set; }
      public PingRequestMessage(
        long timestamp, 
        long stream_id, 
        long local_timestamp)
        : base(RTMPMessageType.UserControl, timestamp, stream_id, CreateBody(local_timestamp))
      {
        LocalTimestamp = local_timestamp;
      }

      public PingRequestMessage(RTMPMessage x)
        : base(x)
      {
        using (var reader=new RTMPBinaryReader(new MemoryStream(x.Body))) {
          var type = (UserControlMessageType)reader.ReadUInt16();
          LocalTimestamp = reader.ReadUInt32();
        }
      }

      private static byte[] CreateBody(long local_timestamp)
      {
        var s = new MemoryStream();
        using (var writer=new RTMPBinaryWriter(s)) {
          writer.WriteUInt16((int)UserControlMessageType.PingRequest);
          writer.WriteUInt32(local_timestamp);
        }
        return s.ToArray();
      }
    }

    public class PingResponseMessage
      : RTMPMessage
    {
      public long LocalTimestamp { get; private set; }
      public PingResponseMessage(
        long timestamp, 
        long stream_id, 
        long local_timestamp)
        : base(RTMPMessageType.UserControl, timestamp, stream_id, CreateBody(local_timestamp))
      {
        LocalTimestamp = local_timestamp;
      }

      public PingResponseMessage(RTMPMessage x)
        : base(x)
      {
        using (var reader=new RTMPBinaryReader(new MemoryStream(x.Body))) {
          var type = (UserControlMessageType)reader.ReadUInt16();
          LocalTimestamp = reader.ReadUInt32();
        }
      }

      private static byte[] CreateBody(long local_timestamp)
      {
        var s = new MemoryStream();
        using (var writer=new RTMPBinaryWriter(s)) {
          writer.WriteUInt16((int)UserControlMessageType.PingResponse);
          writer.WriteUInt32(local_timestamp);
        }
        return s.ToArray();
      }
    }

  }

  public abstract class DataMessage
    : RTMPMessage
  {
    public abstract string          PropertyName { get; }
    public abstract IList<AMFValue> Arguments    { get; }
    protected DataMessage(
      RTMPMessageType type,
      long timestamp, 
      long stream_id, 
      byte[] body)
      : base(type, timestamp, stream_id, body)
    {
    }

    protected DataMessage(RTMPMessage x)
      : base(x)
    {
    }
  }

  public class DataAMF3Message
    : DataMessage
  {
    private string propertyName;
    private IList<AMFValue> arguments;
    public override string PropertyName       { get { return propertyName; } }
    public override IList<AMFValue> Arguments { get { return arguments; } }
    public DataAMF3Message(
      long timestamp, 
      long stream_id, 
      string property_name,
      IEnumerable<AMFValue> arguments)
      : base(RTMPMessageType.DataAMF3, timestamp, stream_id, CreateBody(property_name, arguments))
    {
      this.propertyName = property_name;
      this.arguments = arguments.ToArray();
    }

    public DataAMF3Message(RTMPMessage x)
      : base(x)
    {
      using (var reader=new AMF0Reader(new MemoryStream(x.Body))) {
				reader.BaseStream.ReadByte();
        this.propertyName = (string)reader.ReadValue();
        var arguments = new List<AMFValue>();
        while (reader.BaseStream.Position<reader.BaseStream.Length) {
          arguments.Add(reader.ReadValue());
        }
        this.arguments = arguments;
      }
    }

    private static byte[] CreateBody(string property_name, IEnumerable<AMFValue> arguments)
    {
      var s = new MemoryStream();
      using (var writer=new AMF0Writer(s)) {
				writer.BaseStream.WriteByte(0);
        writer.WriteString(property_name);
        foreach (var arg in arguments) {
          writer.WriteValue(arg);
        }
      }
      return s.ToArray();
    }
  }

  public class DataAMF0Message
    : DataMessage
  {
    private string propertyName;
    private IList<AMFValue> arguments;
    public override string PropertyName       { get { return propertyName; } }
    public override IList<AMFValue> Arguments { get { return arguments; } }
    public DataAMF0Message(
      long timestamp, 
      long stream_id, 
      string property_name,
      IEnumerable<AMFValue> arguments)
      : base(RTMPMessageType.DataAMF0, timestamp, stream_id, CreateBody(property_name, arguments))
    {
      this.propertyName = property_name;
      this.arguments = arguments.ToArray();
    }

    public DataAMF0Message(RTMPMessage x)
      : base(x)
    {
      using (var reader=new AMF0Reader(new MemoryStream(x.Body))) {
        this.propertyName = (string)reader.ReadValue();
        var arguments = new List<AMFValue>();
        while (reader.BaseStream.Position<reader.BaseStream.Length) {
          arguments.Add(reader.ReadValue());
        }
        this.arguments = arguments;
      }
    }

    private static byte[] CreateBody(string property_name, IEnumerable<AMFValue> arguments)
    {
      var s = new MemoryStream();
      using (var writer=new AMF0Writer(s)) {
        writer.WriteString(property_name);
        foreach (var arg in arguments) {
          writer.WriteValue(arg);
        }
      }
      return s.ToArray();
    }
  }

  public abstract class CommandMessage
    : RTMPMessage
  {
    public abstract string   CommandName   { get; }
    public abstract int      TransactionId { get; }
    public abstract AMFValue CommandObject { get; }
    public abstract IList<AMFValue> Arguments { get; }
    protected CommandMessage(
      RTMPMessageType type,
      long timestamp,
      long stream_id,
      byte[] body)
      : base(type, timestamp, stream_id, body)
    {
    }

    protected CommandMessage(RTMPMessage x)
      : base(x)
    {
    }

    public static CommandMessage Create(
      int version,
      long timestamp, 
      long stream_id, 
      string command_name,
      int transaction_id,
      AMFValue command_object,
      params AMFValue[] arguments)
    {
      switch (version) {
      case 0:
        return new CommandAMF0Message(timestamp, stream_id, command_name, transaction_id, command_object, arguments);
      case 3:
        return new CommandAMF3Message(timestamp, stream_id, command_name, transaction_id, command_object, arguments);
      default:
        throw new ArgumentException("Unsupported serialize version", "version");
      }
    }

  }

  public class CommandAMF3Message
    : CommandMessage
  {
    private string   commandName;
    private int      transactionId;
    private AMFValue commandObject;
    private IList<AMFValue> arguments;
    public override string   CommandName   { get { return commandName; } }
    public override int      TransactionId { get { return transactionId; } }
    public override AMFValue CommandObject { get { return commandObject; } }
    public override IList<AMFValue> Arguments { get { return arguments; } }
    public CommandAMF3Message(
      long timestamp, 
      long stream_id, 
      string command_name,
      int transaction_id,
      AMFValue command_object,
      IEnumerable<AMFValue> arguments)
      : base(RTMPMessageType.CommandAMF3, timestamp, stream_id, CreateBody(command_name, transaction_id, command_object, arguments))
    {
      this.commandName   = command_name;
      this.transactionId = transaction_id;
      this.commandObject = command_object;
      this.arguments     = arguments.ToArray();
    }

    public CommandAMF3Message(RTMPMessage x)
      : base(x)
    {
      using (var reader=new AMF0Reader(new MemoryStream(x.Body))) {
				reader.BaseStream.ReadByte();
        this.commandName   = (string)reader.ReadValue();
        this.transactionId = (int)reader.ReadValue();
        this.commandObject = reader.ReadValue();
        if (AMFValue.IsNull(CommandObject)) {
          this.commandObject = null;
        }
        var args = new List<AMFValue>();
        while (reader.BaseStream.Position<reader.BaseStream.Length) {
          args.Add(reader.ReadValue());
        }
        this.arguments = args;
      }
    }

    private static byte[] CreateBody(
      string command_name,
      int transaction_id,
      AMFValue command_object,
      IEnumerable<AMFValue> arguments)
    {
      var s = new MemoryStream();
      using (var writer=new AMF0Writer(s)) {
				writer.BaseStream.WriteByte(0);
        writer.WriteString(command_name);
        writer.WriteNumber(transaction_id);
        writer.WriteValue(command_object);
        if (arguments!=null) {
          foreach (var arg in arguments) {
            writer.WriteValue(arg);
          }
        }
      }
      return s.ToArray();
    }
  }

  public class CommandAMF0Message
    : CommandMessage
  {
    private string   commandName;
    private int      transactionId;
    private AMFValue commandObject;
    private IList<AMFValue> arguments;
    public override string   CommandName   { get { return commandName; } }
    public override int      TransactionId { get { return transactionId; } }
    public override AMFValue CommandObject { get { return commandObject; } }
    public override IList<AMFValue> Arguments { get { return arguments; } }
    public CommandAMF0Message(
      long timestamp, 
      long stream_id, 
      string command_name,
      int transaction_id,
      AMFValue command_object,
      IEnumerable<AMFValue> arguments)
      : base(RTMPMessageType.CommandAMF0, timestamp, stream_id, CreateBody(command_name, transaction_id, command_object, arguments))
    {
      this.commandName   = command_name;
      this.transactionId = transaction_id;
      this.commandObject = command_object;
      this.arguments     = arguments.ToArray();
    }

    public CommandAMF0Message(RTMPMessage x)
      : base(x)
    {
      using (var reader=new AMF0Reader(new MemoryStream(x.Body))) {
        this.commandName   = (string)reader.ReadValue();
        this.transactionId = (int)reader.ReadValue();
        this.commandObject = reader.ReadValue();
        if (AMFValue.IsNull(CommandObject)) {
          this.commandObject = null;
        }
        var args = new List<AMFValue>();
        while (reader.BaseStream.Position<reader.BaseStream.Length) {
          args.Add(reader.ReadValue());
        }
        this.arguments = args;
      }
    }

    private static byte[] CreateBody(
      string command_name,
      int transaction_id,
      AMFValue command_object,
      IEnumerable<AMFValue> arguments)
    {
      var s = new MemoryStream();
      using (var writer=new AMF0Writer(s)) {
        writer.WriteString(command_name);
        writer.WriteNumber(transaction_id);
        writer.WriteValue(command_object);
        if (arguments!=null) {
          foreach (var arg in arguments) {
            writer.WriteValue(arg);
          }
        }
      }
      return s.ToArray();
    }
  }

  public class AggregateMessage
    : RTMPMessage
  {
    public IList<RTMPMessage> Messages { get; private set; }
    public AggregateMessage(
      long timestamp, 
      long stream_id, 
      IEnumerable<RTMPMessage> messages)
      : base(RTMPMessageType.CommandAMF0, timestamp, stream_id, CreateBody(timestamp, messages))
    {
      Messages = messages.ToArray();
    }

    public AggregateMessage(RTMPMessage x)
      : base(x)
    {
      var messages = new List<RTMPMessage>();
      using (var reader=new RTMPBinaryReader(new MemoryStream(x.Body))) {
        while (reader.BaseStream.Position<reader.BaseStream.Length) {
          var message_type = (RTMPMessageType)reader.ReadByte();
          var length       = reader.ReadUInt24();
          var timestamp    = reader.ReadUInt32();
          var stream_id    = reader.ReadUInt24();
          var body         = reader.ReadBytes(length);
          var msg = new RTMPMessage(
            message_type,
            timestamp + x.Timestamp,
            x.StreamId,
            body);
          var prevlen = reader.ReadUInt32();
          if (prevlen==body.Length+11) {
            messages.Add(msg);
          }
        }
      }
    }

    private static byte[] CreateBody(
      long timestamp,
      IEnumerable<RTMPMessage> messages)
    {
      var s = new MemoryStream();
      using (var writer=new RTMPBinaryWriter(s)) {
        foreach (var msg in messages) {
          writer.Write((byte)msg.MessageType);
          writer.WriteUInt24(msg.Body.Length);
          var t = msg.Timestamp-timestamp;
          writer.WriteUInt24((int)t & 0xFFFFFF);
          writer.Write((byte)((t>>24) & 0xFF));
          writer.WriteUInt24((int)msg.StreamId);
          writer.Write(msg.Body, 0, msg.Body.Length);
          writer.Write(msg.Body.Length+11);
        }
      }
      return s.ToArray();
    }
  }

}
