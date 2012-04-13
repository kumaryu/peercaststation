using System;
using System.Linq;
using System.IO;
using PeerCastStation.Core;
using System.Collections.Generic;

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

    public static byte ReadUInt8(Stream s)
    {
      int b = s.ReadByte();
      if (b<0) throw new EndOfStreamException();
      return (byte)b;
    }

    public static short ReadInt16LE(Stream s)
    {
      return BitConverter.ToInt16(ReadBytesLE(s, 2), 0);
    }

    public static ushort ReadUInt16LE(Stream s)
    {
      return BitConverter.ToUInt16(ReadBytesLE(s, 2), 0);
    }

    public static int ReadInt32LE(Stream s)
    {
      return BitConverter.ToInt32(ReadBytesLE(s, 4), 0);
    }

    public static uint ReadUInt32LE(Stream s)
    {
      return BitConverter.ToUInt32(ReadBytesLE(s, 4), 0);
    }

    public static long ReadInt64LE(Stream s)
    {
      return BitConverter.ToInt64(ReadBytesLE(s, 8), 0);
    }

    public static ulong ReadUInt64LE(Stream s)
    {
      return BitConverter.ToUInt64(ReadBytesLE(s, 8), 0);
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

  [Plugin]
  public class ASFContentReader
    : MarshalByRefObject,
      IContentReader
  {
    public ParsedContent Read(Channel channel, Stream stream)
    {
      var chunks = 0;
      var res = new ParsedContent();
      var pos = channel.ContentPosition;
      try {
        while (chunks<8) {
          var chunk = ASFChunk.Read(stream);
          chunks++;
          switch (chunk.KnownType) {
          case ASFChunk.ChunkType.Header: {
              var header = ASFHeader.Read(chunk);
              var info = new AtomCollection(channel.ChannelInfo.Extra);
              info.SetChanInfoBitrate(header.Bitrate);
              if (header.Streams.Any(type => type==ASFHeader.StreamType.Video)) {
                info.SetChanInfoType("WMV");
              }
              else if (header.Streams.Any(type => type==ASFHeader.StreamType.Audio)) {
                info.SetChanInfoType("WMA");
              }
              else {
                info.SetChanInfoType("ASF");
              }
              res.ChannelInfo = new ChannelInfo(info);
              res.ContentHeader = new Content(pos, chunk.ToByteArray());
              pos += chunk.TotalLength;
            }
            break;
          case ASFChunk.ChunkType.Data:
            if (res.Contents==null) res.Contents = new System.Collections.Generic.List<Content>();
            res.Contents.Add(new Content(pos, chunk.ToByteArray()));
            pos += chunk.TotalLength;
            break;
          case ASFChunk.ChunkType.Unknown:
            break;
          }
        }
      }
      catch (EndOfStreamException) {
        if (chunks==0) throw;
      }
      return res;
    }

    public string Name
    {
      get { return "ASF(WMV or WMA)"; }
    }
  }
}
