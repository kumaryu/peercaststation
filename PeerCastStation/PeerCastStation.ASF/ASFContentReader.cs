#nullable enable
using System;
using System.Linq;
using System.IO;
using PeerCastStation.Core;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Buffers.Binary;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace PeerCastStation.ASF
{
  internal static class StreamExtentions
  {
    public static async ValueTask<IMemoryOwner<byte>> ReadBytesAsync(this Stream stream, int length, CancellationToken cancellationToken)
    {
      var buffer = MemoryPool<byte>.Shared.Rent(length);
      try {
        var pos = 0;
        while (pos<length) {
          var len = await stream.ReadAsync(buffer.Memory.Slice(pos, length - pos), cancellationToken).ConfigureAwait(false);
          if (len==0) {
            throw new EndOfStreamException();
          }
          pos += len;
        }
        return buffer;
      }
      catch (Exception) {
        buffer.Dispose();
        throw;
      }
    }

  }

  internal interface IASFObjectData
  {
    ReadOnlyMemory<byte> RawData { get; }
  }

  internal sealed class ASFObjectData
    : IASFObjectData
  {
    public ReadOnlyMemory<byte> RawData { get; }

    public ASFObjectData(ReadOnlyMemory<byte> rawData)
    {
      RawData = rawData;
    }
  }

  internal sealed class FilePropertiesObjectData
    : IASFObjectData
  {
    public static readonly Guid Guid = new Guid("8CABDCA1-A947-11CF-8EE4-00C00C205365");

    [Flags]
    public enum FilePropertiesObjectFlags : int {
      Broadcast = 0x1,
      Seekable = 0x2,
    }

    public ReadOnlyMemory<byte> RawData { get; }
    public Guid FileId => new Guid(RawData.Span.Slice(0, 16));
    public DateTimeOffset CreationDate => new DateTimeOffset(1601, 1, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(BinaryPrimitives.ReadInt64LittleEndian(RawData.Span.Slice(16)));
    public long DataPacketsCount => BinaryPrimitives.ReadInt64LittleEndian(RawData.Span.Slice(24));
    public TimeSpan PlayDuration => new TimeSpan(BinaryPrimitives.ReadInt64LittleEndian(RawData.Span.Slice(32)));
    public TimeSpan SendDuration => new TimeSpan(BinaryPrimitives.ReadInt64LittleEndian(RawData.Span.Slice(40)));
    public TimeSpan Preroll => TimeSpan.FromMilliseconds(BinaryPrimitives.ReadInt64LittleEndian(RawData.Span.Slice(40)));
    public FilePropertiesObjectFlags Flags => (FilePropertiesObjectFlags)BinaryPrimitives.ReadInt32LittleEndian(RawData.Span.Slice(48));
    public int MinimumDataPacketSize => BinaryPrimitives.ReadInt32LittleEndian(RawData.Span.Slice(52));
    public int MaximumDataPacketSize => BinaryPrimitives.ReadInt32LittleEndian(RawData.Span.Slice(56));
    public int MaximumBitrate => BinaryPrimitives.ReadInt32LittleEndian(RawData.Span.Slice(60));

    public FilePropertiesObjectData(ReadOnlyMemory<byte> rawData)
    {
      RawData = rawData;
    }
  }

  internal sealed class ContentDescriptionObject
    : IASFObjectData
  {
    public static readonly Guid Guid = new Guid("75B22633-668E-11CF-A6D9-00AA0062CE6C");
    public ReadOnlyMemory<byte> RawData { get; }
    public string Title { get; }
    public string Author { get; }
    public string Copyright { get; }
    public string Description { get; }
    public string Rating { get; }

    public ContentDescriptionObject(ReadOnlyMemory<byte> rawData)
    {
      RawData = rawData;
      var s = RawData.Span;
      var titleLen = BinaryPrimitives.ReadUInt16LittleEndian(s.Slice(0));
      var authorLen = BinaryPrimitives.ReadUInt16LittleEndian(s.Slice(2));
      var copyrightLen = BinaryPrimitives.ReadUInt16LittleEndian(s.Slice(4));
      var descriptionLen = BinaryPrimitives.ReadUInt16LittleEndian(s.Slice(6));
      var ratingLen = BinaryPrimitives.ReadUInt16LittleEndian(s.Slice(8));
      int pos = 10;
      Title = System.Text.Encoding.Unicode.GetString(s.Slice(pos, titleLen));
      pos += titleLen;
      Author = System.Text.Encoding.Unicode.GetString(s.Slice(pos, authorLen));
      pos += authorLen;
      Copyright = System.Text.Encoding.Unicode.GetString(s.Slice(pos, copyrightLen));
      pos += copyrightLen;
      Description = System.Text.Encoding.Unicode.GetString(s.Slice(pos, descriptionLen));
      pos += descriptionLen;
      Rating = System.Text.Encoding.Unicode.GetString(s.Slice(pos, ratingLen));
    }
  }

  internal sealed class StreamPropertiesObject
    : IASFObjectData
  {
    public static readonly Guid Guid = new Guid("B7DC0791-A9B7-11CF-8EE6-00C00C205365");
    public static readonly Guid StreamTypeAudio = new Guid(0xF8699E40, 0x5B4D, 0x11CF, 0xA8,0xFD,0x00,0x80,0x5F,0x5C,0x44,0x2B);
    public static readonly Guid StreamTypeVideo = new Guid(0xBC19EFC0, 0x5B4D, 0x11CF, 0xA8,0xFD,0x00,0x80,0x5F,0x5C,0x44,0x2B);

    public ReadOnlyMemory<byte> RawData { get; }
    public Guid StreamType => new Guid(RawData.Span.Slice(0, 16));
    public Guid ErrorCorrectionType => new Guid(RawData.Span.Slice(16, 16));
    public TimeSpan TimeOffset => new TimeSpan(BinaryPrimitives.ReadInt64LittleEndian(RawData.Span.Slice(32)));
    public int Flags => BinaryPrimitives.ReadInt32LittleEndian(RawData.Span.Slice(48));
    public ReadOnlyMemory<byte> TypeSpecificData {
      get {
        var len = BinaryPrimitives.ReadInt32LittleEndian(RawData.Span.Slice(40));
        return RawData.Slice(56, len);
      }
    }
    public ReadOnlyMemory<byte> ErrorCorrectionData {
      get {
        var len1 = BinaryPrimitives.ReadInt32LittleEndian(RawData.Span.Slice(40));
        var len2 = BinaryPrimitives.ReadInt32LittleEndian(RawData.Span.Slice(44));
        return RawData.Slice(56+len1, len2);
      }
    }

    public StreamPropertiesObject(ReadOnlyMemory<byte> rawData)
    {
      RawData = rawData;
    }

  }

  internal sealed class StreamBitratePropertiesObject
    : IASFObjectData
  {
    public static readonly Guid Guid = new Guid("7BF875CE-468D-11D1-8D82-006097C9A2B2");

    public struct Record
    {
      public readonly int StreamNumber;
      public readonly int AverageBitrate;
      public Record(int streamNumber, int averageBitrate)
      {
        StreamNumber = streamNumber;
        AverageBitrate = averageBitrate;
      }
    }

    public ReadOnlyMemory<byte> RawData { get; }
    public Record[] Records { get; }

    public StreamBitratePropertiesObject(ReadOnlyMemory<byte> rawData)
    {
      RawData = rawData;
      var count = BinaryPrimitives.ReadUInt16LittleEndian(RawData.Span);
      Records = new Record[count];
      for (int i=0; i<count; i++) {
        var s = RawData.Span.Slice(2 + 6*i);
        Records[i] = new Record(
          BinaryPrimitives.ReadUInt16LittleEndian(s) & 0x7F,
          BinaryPrimitives.ReadInt32LittleEndian(s.Slice(2))
        );
      }
    }

  }

  internal sealed class TopLevelHeaderObjectData
    : IASFObjectData
  {
    public static readonly Guid Guid = new Guid("75B22630-668E-11CF-A6D9-00AA0062CE6C");

    public ReadOnlyMemory<byte> RawData { get; }
    public IReadOnlyList<ASFObject> Objects { get; }

    public TopLevelHeaderObjectData(ReadOnlyMemory<byte> rawData)
    {
      RawData = rawData;
      var count = BinaryPrimitives.ReadInt32LittleEndian(RawData.Span);
      var mem = RawData.Slice(6);
      var objects = new ASFObject[count];
      for (int i=0; i<count; i++) {
        var (obj, nxt) = ASFObject.ReadFromMemory(mem);
        objects[i] = obj;
        mem = nxt;
      }
      Objects = objects;
    }

  }

  internal class ASFObject
  {
    public Guid Type { get; }
    public IASFObjectData Data { get; }

    public ASFObject(Guid type, IASFObjectData data)
    {
      Type = type;
      Data = data;
    }

    public static (ASFObject, ReadOnlyMemory<byte>) ReadFromMemory(ReadOnlyMemory<byte> memory)
    {
      var type = new Guid(memory.Span.Slice(0, 16));
      var length = BinaryPrimitives.ReadInt64LittleEndian(memory.Span.Slice(16));
      var rawData = memory.Slice(24, (int)length-24);
      return type switch {
        var t when t==TopLevelHeaderObjectData.Guid => (new ASFObject(type, new TopLevelHeaderObjectData(rawData)), memory.Slice((int)length)),
        var t when t==FilePropertiesObjectData.Guid => (new ASFObject(type, new FilePropertiesObjectData(rawData)), memory.Slice((int)length)),
        var t when t==ContentDescriptionObject.Guid => (new ASFObject(type, new ContentDescriptionObject(rawData)), memory.Slice((int)length)),
        var t when t==StreamPropertiesObject.Guid => (new ASFObject(type, new StreamPropertiesObject(rawData)), memory.Slice((int)length)),
        var t when t==StreamBitratePropertiesObject.Guid => (new ASFObject(type, new StreamBitratePropertiesObject(rawData)), memory.Slice((int)length)),
        _ => (new ASFObject(type, new ASFObjectData(rawData)), memory.Slice((int)length)),
      };
    }

  }

  internal struct MMSDataPacket
  {
    public ReadOnlyMemory<byte> RawData { get; }
    public int LocationId => BinaryPrimitives.ReadInt32LittleEndian(RawData.Span);
    public byte Incarnation => RawData.Span[4];
    public byte AFFlags => RawData.Span[5];
    public ReadOnlyMemory<byte> Payload => RawData.Slice(8, BinaryPrimitives.ReadUInt16LittleEndian(RawData.Span.Slice(6))-8);

    public MMSDataPacket(ReadOnlyMemory<byte> rawData)
    {
      RawData = rawData;
    }
  }

  internal struct WMSPFrame
  {
    public ReadOnlyMemory<byte> RawData { get; }
    public bool B => (RawData.Span[0] & 0x80) != 0;
    public string Type { get; }
    public int Length => BinaryPrimitives.ReadUInt16LittleEndian(RawData.Span.Slice(2));
    public int? Reason {
      get {
        switch (Type) {
        case "$C":
        case "$E":
        case "$P":
          return BinaryPrimitives.ReadInt32LittleEndian(RawData.Slice(4).Span);
        default:
          return null;
        }
      }
    }
    public ReadOnlyMemory<byte> Payload {
      get {
        switch (Type) {
        case "$C":
        case "$E":
        case "$P":
          return RawData.Slice(8, Length-4);
        default:
          return RawData.Slice(4, Length);
        }
      }
    }
    public MMSDataPacket MMSPayload {
      get {
        switch (Type) {
        case "$H":
        case "$D":
        case "$M":
          return new MMSDataPacket(RawData.Slice(4, Length));
        default:
          throw new InvalidOperationException(); 
        }
      }
    }

    public WMSPFrame(ReadOnlyMemory<byte> rawData)
    {
      RawData = rawData;
      Type = new string(new char[] { (char)(RawData.Span[0] & 0x7F), (char)RawData.Span[1] });
    }

    public static (WMSPFrame, ReadOnlyMemory<byte>) ReadFromMemory(ReadOnlyMemory<byte> memory)
    {
      try {
        var frame = new WMSPFrame(memory);
        return (frame, memory.Slice(4+frame.Length));
      }
      catch (ArgumentOutOfRangeException ex) {
        throw new EndOfStreamException(ex.Message, ex);
      }
    }

    public static async Task<WMSPFrame> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
      using var headerBuffer = await stream.ReadBytesAsync(4, cancellationToken).ConfigureAwait(false);
      var length = BinaryPrimitives.ReadUInt16LittleEndian(headerBuffer.Memory.Span.Slice(2));
      var rawData = new Memory<byte>(new byte[4+length]);
      headerBuffer.Memory.CopyTo(rawData);
      await stream.ReadBytesAsync(rawData.Slice(4), length, cancellationToken).ConfigureAwait(false);
      return new WMSPFrame(rawData);
    }

  }

  public class ASFContentReader
    : IContentReader
  {
    public ASFContentReader(Channel channel)
    {
      this.Channel = channel;
    }

    public string Name { get { return "ASF(WMV or WMA)"; } }
    public Channel Channel { get; private set; }

    internal static ChannelInfo ParseChannelInfo(WMSPFrame frame)
    {
      var info = new AtomCollection();
      var (header, next) = ASFObject.ReadFromMemory(frame.MMSPayload.Payload);
      if (header.Data is TopLevelHeaderObjectData topLevel) {
        var streamType = topLevel.Objects
          .Select(obj => obj.Data)
          .OfType<StreamPropertiesObject>().Aggregate("ASF", (type, prop) => {
            if (prop.StreamType == StreamPropertiesObject.StreamTypeVideo) {
              return "WMV";
            }
            else if (prop.StreamType == StreamPropertiesObject.StreamTypeAudio && type!="WMV") {
              return "WMA";
            }
            else if (type!="WMV" && type!="WMA") {
              return "ASF";
            }
            else {
              return type;
            }
          });
        info.SetChanInfoType(streamType);
        switch (streamType) {
        case "WMV":
          info.SetChanInfoStreamType("video/x-ms-wmv");
          info.SetChanInfoStreamExt(".wmv");
          break;
        case "WMA":
          info.SetChanInfoStreamType("audio/x-ms-wma");
          info.SetChanInfoStreamExt(".wma");
          break;
        case "ASF":
          info.SetChanInfoStreamType("video/x-ms-asf");
          info.SetChanInfoStreamExt(".asf");
          break;
        }
        var bitrateProps = topLevel.Objects.Select(obj => obj.Data).OfType<StreamBitratePropertiesObject>().FirstOrDefault();
        if (bitrateProps!=null) {
          var bitrate = bitrateProps.Records.Sum(r => r.AverageBitrate);
          info.SetChanInfoBitrate(bitrate / 1000);
        }
        else {
          var fileProps = topLevel.Objects.Select(obj => obj.Data).OfType<FilePropertiesObjectData>().First();
          var bitrate = fileProps.MaximumBitrate;
          info.SetChanInfoBitrate(bitrate / 1000);
        }
      }
      return new ChannelInfo(info);
    }

    public async Task ReadAsync(IContentSink sink, Stream stream, CancellationToken cancel_token)
    {
      try {
        bool eos = false;
        var streamOrigin = DateTime.Now;
        var streamNumber = -1;
        var contentPosition = 0L;

        while (!eos) {
          var frame = await WMSPFrame.ReadAsync(stream, cancel_token).ConfigureAwait(false);
          if (frame.Type=="$H") {
            streamNumber = Channel.GenerateStreamID();
            contentPosition = 0L;
            sink.OnChannelInfo(ParseChannelInfo(frame));
            sink.OnContentHeader(new Content(streamNumber, DateTime.Now - streamOrigin, contentPosition, frame.RawData, PCPChanPacketContinuation.None));
            contentPosition += frame.RawData.Length;
          }
          else if (streamNumber>=0 && (frame.Type=="$D" || frame.Type=="$C" || frame.Type=="$M")) {
            sink.OnContent(new Content(streamNumber, DateTime.Now - streamOrigin, contentPosition, frame.RawData, PCPChanPacketContinuation.None));
            contentPosition += frame.RawData.Length;
          }
          else if (frame.Type=="$E") {
            eos = true;
          }
        }

      }
      catch (EndOfStreamException) {
      }
    }

  }

  public class ASFContentReaderFactory
    : IContentReaderFactory
  {
    public string Name { get { return "ASF(WMV or WMA)"; } }

    public IContentReader Create(Channel channel)
    {
      return new ASFContentReader(channel);
    }

    private (bool Parsed, string? ContentType, string? MimeType) TryParseContentType(byte[] header_bytes)
    {
      try {
        var mem = new ReadOnlyMemory<byte>(header_bytes);
        for (var chunks=0; chunks<8; chunks++) {
          var (frame, next) = WMSPFrame.ReadFromMemory(mem);
          mem = next;
          if (frame.Type!="$H") continue;
          var info = ASFContentReader.ParseChannelInfo(frame);
          return (true, info.ContentType, info.MIMEType);
        }
      }
      catch (EndOfStreamException) {
      }
      return (false, null, null);
    }

    public bool TryParseContentType(byte[] header_bytes, [NotNullWhen(true)] out string? content_type, [NotNullWhen(true)] out string? mime_type)
    {
      var (parsed, type, mime) = TryParseContentType(header_bytes);
      content_type = type;
      mime_type = mime;
      return parsed;
    }

  }

  [Plugin]
  public class ASFContentReaderPlugin
    : PluginBase
  {
    override public string Name { get { return "ASF Content Reader"; } }

    private readonly ASFContentReaderFactory factory = new ASFContentReaderFactory();
    override protected void OnAttach(PeerCastApplication application)
    {
      application.PeerCast.ContentReaderFactories.Add(factory);
    }

    override protected void OnDetach(PeerCastApplication application)
    {
      application.PeerCast.ContentReaderFactories.Remove(factory);
    }
  }
}
