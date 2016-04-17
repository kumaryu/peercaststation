using System;
using System.Linq;
using System.IO;
using PeerCastStation.Core;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.ASF
{
  internal class BinaryWriter
  {
    public static void WriteBytes(Stream s, byte[] bytes)
    {
      s.Write(bytes, 0, bytes.Length);
    }

    public static void WriteBytesLE(Stream s, byte[] bytes)
    {
      if (!BitConverter.IsLittleEndian) {
        Array.Reverse(bytes);
      }
      WriteBytes(s, bytes);
    }

    public static void WriteUInt8(Stream s, byte value)
    {
      s.WriteByte(value);
    }

    public static void WriteInt16LE(Stream s, short value)
    {
      WriteBytesLE(s, BitConverter.GetBytes(value));
    }

    public static void WriteUInt16LE(Stream s, ushort value)
    {
      WriteBytesLE(s, BitConverter.GetBytes(value));
    }

    public static void WriteInt32LE(Stream s, int value)
    {
      WriteBytesLE(s, BitConverter.GetBytes(value));
    }

    public static void WriteUInt32LE(Stream s, uint value)
    {
      WriteBytesLE(s, BitConverter.GetBytes(value));
    }

    public static void WriteInt64LE(Stream s, long value)
    {
      WriteBytesLE(s, BitConverter.GetBytes(value));
    }

    public static void WriteUInt64LE(Stream s, ulong value)
    {
      WriteBytesLE(s, BitConverter.GetBytes(value));
    }
  }

  internal class BinaryReader
  {
    public static byte[] ReadBytes(Stream s, int sz)
    {
      var bytes = new byte[sz];
      var pos = 0;
      while (pos<sz) {
        var read = s.Read(bytes, 0, bytes.Length);
        if (read<=0) throw new EndOfStreamException();
        pos += read;
      }
      return bytes;
    }

    public static async Task<byte[]> ReadBytesAsync(Stream s, int sz, CancellationToken cancel_token)
    {
      var bytes = new byte[sz];
      var pos = 0;
      while (pos<sz) {
        var read = await s.ReadAsync(bytes, 0, bytes.Length, cancel_token);
        if (read<=0) throw new EndOfStreamException();
        pos += read;
      }
      return bytes;
    }

    public static byte[] ReadBytesLE(Stream s, int sz)
    {
      if (BitConverter.IsLittleEndian) {
        return ReadBytes(s, sz);
      }
      else {
        var bytes = ReadBytes(s, sz);
        Array.Reverse(bytes);
        return bytes;
      }
    }

    public static async Task<byte[]> ReadBytesLEAsync(Stream s, int sz, CancellationToken cancel_token)
    {
      if (BitConverter.IsLittleEndian) {
        return await ReadBytesAsync(s, sz, cancel_token);
      }
      else {
        var bytes = ReadBytes(s, sz);
        Array.Reverse(bytes);
        return bytes;
      }
    }

    public static byte ReadUInt8(Stream s)
    {
      int b = s.ReadByte();
      if (b<0) throw new EndOfStreamException();
      return (byte)b;
    }

    public static async Task<byte> ReadUInt8Async(Stream s, CancellationToken cancel_token)
    {
      int b = await s.ReadByteAsync();
      if (b<0) throw new EndOfStreamException();
      return (byte)b;
    }

    public static short ReadInt16LE(Stream s)
    {
      return BitConverter.ToInt16(ReadBytesLE(s, 2), 0);
    }

    public static async Task<short> ReadInt16LEAsync(Stream s, CancellationToken cancel_token)
    {
      return BitConverter.ToInt16(await ReadBytesLEAsync(s, 2, cancel_token), 0);
    }

    public static short GetInt16LE(byte[] bytes, int offset)
    {
      if (BitConverter.IsLittleEndian) {
        return BitConverter.ToInt16(bytes, offset);
      }
      else {
        return BitConverter.ToInt16(new byte[] { bytes[offset+1], bytes[offset+0] }, 0);
      }
    }

    public static ushort ReadUInt16LE(Stream s)
    {
      return BitConverter.ToUInt16(ReadBytesLE(s, 2), 0);
    }

    public static async Task<ushort> ReadUInt16LEAsync(Stream s, CancellationToken cancel_token)
    {
      return BitConverter.ToUInt16(await ReadBytesLEAsync(s, 2, cancel_token), 0);
    }

    public static ushort GetUInt16LE(byte[] bytes, int offset)
    {
      if (BitConverter.IsLittleEndian) {
        return BitConverter.ToUInt16(bytes, offset);
      }
      else {
        return BitConverter.ToUInt16(new byte[] { bytes[offset+1], bytes[offset+0] }, 0);
      }
    }

    public static int ReadInt32LE(Stream s)
    {
      return BitConverter.ToInt32(ReadBytesLE(s, 4), 0);
    }

    public static async Task<int> ReadInt32LEAsync(Stream s, CancellationToken cancel_token)
    {
      return BitConverter.ToInt32(await ReadBytesLEAsync(s, 4, cancel_token), 0);
    }

    public static int GetInt32LE(byte[] bytes, int offset)
    {
      if (BitConverter.IsLittleEndian) {
        return BitConverter.ToInt32(bytes, offset);
      }
      else {
        return BitConverter.ToInt32(new byte[] {
          bytes[offset+3], bytes[offset+2], bytes[offset+1], bytes[offset+0]
        }, 0);
      }
    }

    public static uint ReadUInt32LE(Stream s)
    {
      return BitConverter.ToUInt32(ReadBytesLE(s, 4), 0);
    }

    public static async Task<uint> ReadUInt32LEAsync(Stream s, CancellationToken cancel_token)
    {
      return BitConverter.ToUInt32(await ReadBytesLEAsync(s, 4, cancel_token), 0);
    }

    public static uint GetUInt32LE(byte[] bytes, int offset)
    {
      if (BitConverter.IsLittleEndian) {
        return BitConverter.ToUInt32(bytes, offset);
      }
      else {
        return BitConverter.ToUInt32(new byte[] {
          bytes[offset+3], bytes[offset+2], bytes[offset+1], bytes[offset+0]
        }, 0);
      }
    }

    public static long ReadInt64LE(Stream s)
    {
      return BitConverter.ToInt64(ReadBytesLE(s, 8), 0);
    }

    public static async Task<long> ReadInt64LEAsync(Stream s, CancellationToken cancel_token)
    {
      return BitConverter.ToInt64(await ReadBytesLEAsync(s, 8, cancel_token), 0);
    }

    public static long GetInt64LE(byte[] bytes, int offset)
    {
      if (BitConverter.IsLittleEndian) {
        return BitConverter.ToInt64(bytes, offset);
      }
      else {
        return BitConverter.ToInt64(new byte[] {
          bytes[offset+7], bytes[offset+6], bytes[offset+5], bytes[offset+4],
          bytes[offset+3], bytes[offset+2], bytes[offset+1], bytes[offset+0]
        }, 0);
      }
    }

    public static ulong ReadUInt64LE(Stream s)
    {
      return BitConverter.ToUInt64(ReadBytesLE(s, 8), 0);
    }

    public static async Task<ulong> ReadUInt64LEAsync(Stream s, CancellationToken cancel_token)
    {
      return BitConverter.ToUInt64(await ReadBytesLEAsync(s, 8, cancel_token), 0);
    }

    public static ulong GetUInt64LE(byte[] bytes, int offset)
    {
      if (BitConverter.IsLittleEndian) {
        return BitConverter.ToUInt64(bytes, offset);
      }
      else {
        return BitConverter.ToUInt64(new byte[] {
          bytes[offset+7], bytes[offset+6], bytes[offset+5], bytes[offset+4],
          bytes[offset+3], bytes[offset+2], bytes[offset+1], bytes[offset+0]
        }, 0);
      }
    }

  }

  internal class ASFChunk
  {
    public enum ChunkType {
      Unknown,
      Header,
      Data,
    };
    public ChunkType KnownType {
      get {
        switch (Type) {
        case 0x4824: // asf header
          return ChunkType.Header;
        case 0x4424: // asf data
          return ChunkType.Data;
        default:
          return ChunkType.Unknown;
        }
      }
    }
    public int TotalLength { get { return Length+4; } }
    public ushort Type   { get; private set; }
    public ushort Length { get; private set; }
    public uint   SequenceNumber { get; private set; }
    public ushort V1   { get; private set; }
    public ushort V2   { get; private set; }
    public byte[] Data { get; private set; }

    public ASFChunk(
      ushort type,
      ushort length,
      uint   sequence_number,
      ushort v1,
      ushort v2,
      byte[] data)
    {
      this.Type = type;
      this.Length = length;
      this.SequenceNumber = sequence_number;
      this.V1 = v1;
      this.V2 = v2;
      this.Data = data;
    }

    public byte[] ToByteArray()
    {
      var s = new MemoryStream();
      BinaryWriter.WriteUInt16LE(s, Type);
      BinaryWriter.WriteUInt16LE(s, Length);
      BinaryWriter.WriteUInt32LE(s, SequenceNumber);
      BinaryWriter.WriteUInt16LE(s, V1);
      BinaryWriter.WriteUInt16LE(s, V2);
      BinaryWriter.WriteBytes(s, Data);
      return s.ToArray();
    }

    public static ASFChunk Read(Stream stream)
    {
      var pos = stream.Position;
      try {
        var type    = BinaryReader.ReadUInt16LE(stream);
        var len     = BinaryReader.ReadUInt16LE(stream);
        if (len<8) {
          var data  = BinaryReader.ReadBytes(stream, len);
          return new ASFChunk(type, len, 0, 0, 0, data);
        }
        else {
          var seq_num = BinaryReader.ReadUInt32LE(stream);
          var v1      = BinaryReader.ReadUInt16LE(stream);
          var v2      = BinaryReader.ReadUInt16LE(stream);
          var data    = BinaryReader.ReadBytes(stream, len-8);
          return new ASFChunk(type, len, seq_num, v1, v2, data);
        }
      }
      catch (EndOfStreamException) {
        stream.Position = pos;
        throw;
      }
    }

    public static async Task<ASFChunk> ReadAsync(Stream stream, CancellationToken cancel_token)
    {
      var type = await BinaryReader.ReadUInt16LEAsync(stream, cancel_token);
      var len  = await BinaryReader.ReadUInt16LEAsync(stream, cancel_token);
      if (len<8) {
        var data = await BinaryReader.ReadBytesAsync(stream, len, cancel_token);
        return new ASFChunk(type, len, 0, 0, 0, data);
      }
      else {
        var seq_num = await BinaryReader.ReadUInt32LEAsync(stream, cancel_token);
        var v1      = await BinaryReader.ReadUInt16LEAsync(stream, cancel_token);
        var v2      = await BinaryReader.ReadUInt16LEAsync(stream, cancel_token);
        var data    = await BinaryReader.ReadBytesAsync(stream, len-8, cancel_token);
        return new ASFChunk(type, len, seq_num, v1, v2, data);
      }
    }
  }

  internal class ASFHeader
  {
    public enum StreamType {
      Unknown,
      Audio,
      Video,
    };
    public int Bitrate { get; private set; }
    public StreamType[] Streams { get; private set; }

    public ASFHeader(int bitrate, StreamType[] streams)
    {
      this.Bitrate = bitrate;
      this.Streams = streams;
    }

    public static ASFHeader Read(ASFChunk chunk)
    {
      var streams = new List<StreamType>();
      int bitrate = 0;
      var s = new MemoryStream(chunk.Data, false);
      ASFObject.ReadHeader(s); //root_obj
      var sub_headers = BinaryReader.ReadUInt32LE(s);
      s.Seek(2, SeekOrigin.Current); //2 bytes padding?
      for (var i=0; i<sub_headers; i++) {
        var obj = ASFObject.Read(s);
        switch (obj.Type) {
        case ASFObject.KnownType.FileProperty: {
          var objdata = new MemoryStream(obj.Data, false);
          objdata.Seek(32, SeekOrigin.Current);
          BinaryReader.ReadInt64LE(objdata); //packets
          objdata.Seek(28, SeekOrigin.Current);
          BinaryReader.ReadInt32LE(objdata); //packetsize_min
          BinaryReader.ReadInt32LE(objdata); //packetsize_max
          bitrate = (BinaryReader.ReadInt32LE(objdata)+999) / 1000;
          }
          break;
        case ASFObject.KnownType.StreamProperty: {
          var objdata = new MemoryStream(obj.Data, false);
          var stream_type = new Guid(BinaryReader.ReadBytes(objdata, 16));
          if (stream_type==ASFObject.StreamIDAudio) {
            streams.Add(StreamType.Audio);
          }
          else if (stream_type==ASFObject.StreamIDVideo) {
            streams.Add(StreamType.Video);
          }
          else {
            streams.Add(StreamType.Unknown);
          }
          }
          break;
        default:
          break;
        }
      }
      return new ASFHeader(bitrate, streams.ToArray());
    }
  }

  internal class ASFObject
  {
    public static readonly Guid HeadObjectID           = new Guid(0x75B22630, 0x668E, 0x11CF, 0xA6,0xD9,0x00,0xAA,0x00,0x62,0xCE,0x6C);
    public static readonly Guid DataObjectID           = new Guid(0x75B22636, 0x668E, 0x11CF, 0xA6,0xD9,0x00,0xAA,0x00,0x62,0xCE,0x6C);
    public static readonly Guid FilePropertyObjectID   = new Guid(0x8CABDCA1, 0xA947, 0x11CF, 0x8E,0xE4,0x00,0xC0,0x0C,0x20,0x53,0x65);
    public static readonly Guid StreamPropertyObjectID = new Guid(0xB7DC0791, 0xA9B7, 0x11CF, 0x8E,0xE6,0x00,0xC0,0x0C,0x20,0x53,0x65);
    public static readonly Guid StreamBitrateObjectID  = new Guid(0x7BF875CE, 0x468D, 0x11D1, 0x8D,0x82,0x00,0x60,0x97,0xC9,0xA2,0xB2);
    public static readonly Guid StreamIDAudio    = new Guid(0xF8699E40, 0x5B4D, 0x11CF, 0xA8,0xFD,0x00,0x80,0x5F,0x5C,0x44,0x2B);
    public static readonly Guid StreamIDVideo    = new Guid(0xBC19EFC0, 0x5B4D, 0x11CF, 0xA8,0xFD,0x00,0x80,0x5F,0x5C,0x44,0x2B);

    public enum KnownType
    {
      Unknown,
      HeadObject,
      DataObject,
      FileProperty,
      StreamProperty,
      StreamBitrate,
    };
    public Guid   ID     { get; private set; }
    public ulong  Length { get; private set; }
    public byte[] Data   { get; private set; }
    public KnownType Type
    {
      get
      {
        if (ID==HeadObjectID) return KnownType.HeadObject;
        else if (ID==DataObjectID) return KnownType.DataObject;
        else if (ID==FilePropertyObjectID) return KnownType.FileProperty;
        else if (ID==StreamPropertyObjectID) return KnownType.StreamProperty;
        else if (ID==StreamBitrateObjectID) return KnownType.StreamBitrate;
        else return KnownType.Unknown;
      }
    }

    public ASFObject(
      Guid id,
      ulong length,
      byte[] data)
    {
      this.ID = id;
      this.Length = length;
      this.Data = data;
    }

    public static ASFObject Read(Stream stream)
    {
      var id = new Guid(BinaryReader.ReadBytes(stream, 16));
      var len = BinaryReader.ReadUInt64LE(stream);
      var data = BinaryReader.ReadBytes(stream, (int)len-24);
      return new ASFObject(id, len, data);
    }

    public static ASFObject ReadHeader(Stream stream)
    {
      var id = new Guid(BinaryReader.ReadBytes(stream, 16));
      var len = BinaryReader.ReadUInt64LE(stream);
      return new ASFObject(id, len, null);
    }
  };

  public class ASFContentReader
    : IContentReader
  {
    public ASFContentReader(Channel channel)
    {
      this.Channel = channel;
    }

    public string Name { get { return "ASF(WMV or WMA)"; } }
    public Channel Channel { get; private set; }

    public async Task ReadAsync(IContentSink sink, Stream stream, CancellationToken cancel_token)
    {
      int      streamIndex  = -1;
      DateTime streamOrigin = DateTime.Now;
      bool eof = false;
      do {
        ASFChunk chunk = null;
        try {
          chunk = await ASFChunk.ReadAsync(stream, cancel_token);
        }
        catch (EndOfStreamException) {
          eof = true;
          continue;
        }
        switch (chunk.KnownType) {
        case ASFChunk.ChunkType.Header:
          {
            var header = ASFHeader.Read(chunk);
            var info = new AtomCollection(Channel.ChannelInfo.Extra);
            info.SetChanInfoBitrate(header.Bitrate);
            if (header.Streams.Any(type => type==ASFHeader.StreamType.Video)) {
              info.SetChanInfoType("WMV");
              info.SetChanInfoStreamType("video/x-ms-wmv");
              info.SetChanInfoStreamExt(".wmv");
            }
            else if (header.Streams.Any(type => type==ASFHeader.StreamType.Audio)) {
              info.SetChanInfoType("WMA");
              info.SetChanInfoStreamType("audio/x-ms-wma");
              info.SetChanInfoStreamExt(".wma");
            }
            else {
              info.SetChanInfoType("ASF");
              info.SetChanInfoStreamType("video/x-ms-asf");
              info.SetChanInfoStreamExt(".asf");
            }
            sink.OnChannelInfo(new ChannelInfo(info));
            streamIndex = Channel.GenerateStreamID();
            streamOrigin = DateTime.Now;
            sink.OnContentHeader(new Content(streamIndex, TimeSpan.Zero, Channel.ContentPosition, chunk.ToByteArray()));
            break;
          }
        case ASFChunk.ChunkType.Data:
          sink.OnContent(
            new Content(streamIndex, DateTime.Now-streamOrigin, Channel.ContentPosition, chunk.ToByteArray())
          );
          break;
        case ASFChunk.ChunkType.Unknown:
          break;
        }
      } while (!eof);

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

    public bool TryParseContentType(byte[] header_bytes, out string content_type, out string mime_type)
    {
      using (var stream=new MemoryStream(header_bytes)) {
        try {
          for (var chunks=0; chunks<8; chunks++) {
            var chunk = ASFChunk.Read(stream);
            if (chunk.KnownType!=ASFChunk.ChunkType.Header) continue;
            var header = ASFHeader.Read(chunk);
            if (header.Streams.Any(type => type==ASFHeader.StreamType.Video)) {
              content_type = "WMV";
              mime_type = "video/x-ms-wmv";
            }
            else if (header.Streams.Any(type => type==ASFHeader.StreamType.Audio)) {
              content_type = "WMA";
              mime_type = "audio/x-ms-wma";
            }
            else {
              content_type = "ASF";
              mime_type = "video/x-ms-asf";
            }
            return true;
          }
        }
        catch (EndOfStreamException) {
        }
      }
      content_type = null;
      mime_type    = null;
      return false;
    }

  }

  [Plugin]
  public class ASFContentReaderPlugin
    : PluginBase
  {
    override public string Name { get { return "ASF Content Reader"; } }

    private ASFContentReaderFactory factory;
    override protected void OnAttach()
    {
      if (factory==null) factory = new ASFContentReaderFactory();
      Application.PeerCast.ContentReaderFactories.Add(factory);
    }

    override protected void OnDetach()
    {
      Application.PeerCast.ContentReaderFactories.Remove(factory);
    }
  }
}
