using System;
using System.Linq;
using System.IO;
using PeerCastStation.Core;
using System.Collections.Generic;

namespace PeerCastStation.FLV
{
  internal class BadDataException : ApplicationException
  {
  }

  public class FLVContentReader
    : IContentReader
  {
    private static readonly Logger Logger = new Logger(typeof(FLVContentReader));
    public FLVContentReader(Channel channel)
    {
      this.Channel = channel;
    }

    public string Name { get { return "Flash Video (FLV)"; } }
    public Channel Channel { get; private set; }

    private class FileHeader
    {
      public byte[] Binary     { get; private set; }
      public byte[] Signature  { get; private set; }
      public int    Version    { get; private set; }
      public bool   HasAudio   { get; private set; }
      public bool   HasVideo   { get; private set; }
      public long   DataOffset { get; private set; }
      public long   Size       { get; private set; }

      public FileHeader(byte[] binary)
      {
        this.Binary     = binary;
        this.Signature  = new byte[] { binary[0], binary[1], binary[2] };
        this.Version    = binary[3];
        this.HasAudio   = (binary[4] & 0x4)!=0;
        this.HasVideo   = (binary[4] & 0x1)!=0;
        this.DataOffset = (binary[5]<<24) | (binary[ 6]<<16) | (binary[ 7]<<8) | (binary[ 8]<<0);
        this.Size       = (binary[9]<<24) | (binary[10]<<16) | (binary[11]<<8) | (binary[12]<<0);
      }

      public bool IsValid {
        get {
          return Signature[0]=='F' && Signature[1]=='L' && Signature[2]=='V' && Version==1 && Size==0;
        }
      }
    }

    private class FLVTag
    {
      public enum TagType {
        Audio  = 8,
        Video  = 9,
        Script = 18,
      }
      public byte[]  Header    { get; private set; }
      public byte[]  Body      { get; private set; }
      public byte[]  Footer    { get; private set; }
      public bool    Filter    { get; private set; }
      public TagType Type      { get; private set; }
      public int     DataSize  { get; private set; }
      public long    Timestamp { get; private set; }
      public int     StreamID  { get; private set; }
      public long    TagSize   {
        get { return FLVContentReader.GetUInt32(this.Footer); }
      }
      public byte[] Binary {
        get { return Header.Concat(Body).Concat(Footer).ToArray(); }
      }

      public FLVTag(byte[] binary)
      {
        this.Header    = binary;
        this.Filter    = (binary[0] & 0x20)!=0;
        this.Type      = (TagType)(binary[0] & 0x1F);
        this.DataSize  =                   (binary[1]<<16) | (binary[2]<<8) | (binary[3]);
        this.Timestamp = (binary[7]<<24) | (binary[4]<<16) | (binary[5]<<8) | (binary[6]);
        this.StreamID  =                   (binary[8]<<16) | (binary[9]<<8) | (binary[10]);
        this.Body      = null;
        this.Footer    = null;
      }

      public bool IsValidHeader {
        get {
          return
            (Header[0] & 0xC0)==0 &&
            (Type==TagType.Audio || Type==TagType.Video || Type==TagType.Script) &&
            StreamID==0;
        }
      }

      public bool IsValidFooter {
        get { return this.DataSize+11==this.TagSize; }
      }

      public bool IsAVCHeader {
        get {
          return (Body[0] == 0x17 && Body[1] == 0x00 && Body[2] == 0x00 && Body[3] == 0x00);
        }
      }

      public bool IsAACHeader {
        get {
          return (Body[0] == 0xAF && Body[1] == 0x00);
        }
      }

      public void ReadBody(Stream stream)
      {
        this.Body = FLVContentReader.ReadBytes(stream, this.DataSize);
      }

      public void ReadFooter(Stream stream)
      {
        this.Footer = FLVContentReader.ReadBytes(stream, 4);
      }
    }

    private class TagDesc
    {
      public double Timestamp { get; set; }
      public int    DataSize  { get; set; }
    }

    private static byte[] ReadBytes(Stream stream, int len)
    {
      var res = new byte[len];
      if (stream.Read(res, 0, len)<len) {
        throw new EndOfStreamException();
      }
      return res;
    }

    private static long GetUInt32(byte[] bin)
    {
      return (bin[0]<<24) | (bin[1]<<16) | (bin[2]<<8) | (bin[3]<<0);
    }

    private enum ReaderState {
      Header,
      Body,
    };
    private ReaderState state = ReaderState.Header;
    private long position = 0;
    private int streamIndex = -1;
    private DateTime streamOrigin;
    private FileHeader fileHeader;

    public ParsedContent Read(Stream stream)
    {
      var res = new ParsedContent();
      var info = new AtomCollection(Channel.ChannelInfo.Extra);
      var processed = false;
      var eos = false;
      while (!eos) {
        var start_pos = stream.Position;
        try {
          switch (state) {
          case ReaderState.Header:
            {
              var bin = ReadBytes(stream, 13);
              var header = new FileHeader(bin);
              if (header.IsValid) {
                Logger.Info("FLV Header found");
                fileHeader = header;
                bin = header.Binary;
                streamIndex = Channel.GenerateStreamID();
                streamOrigin = DateTime.Now;
                res.ContentHeader = new Content(streamIndex, TimeSpan.Zero, position, bin);
                res.Contents = null;
                info.SetChanInfoType("FLV");
                info.SetChanInfoStreamType("video/x-flv");
                info.SetChanInfoStreamExt(".flv");
                res.ChannelInfo = new ChannelInfo(info);
                position = bin.Length;
                state = ReaderState.Body;
              }
              else {
                throw new BadDataException();
              }
            }
            break;
          case ReaderState.Body:
            {
              var bin = ReadBytes(stream, 11);
              var read_valid = false;
              var body = new FLVTag(bin);
              if (body.IsValidHeader) {
                body.ReadBody(stream);
                body.ReadFooter(stream);
                if (body.IsValidFooter) {
                  read_valid = true;
                  bin = body.Binary;
                  if (res.Contents==null) res.Contents = new List<Content>();
                  var isMetaData = false;
                  if (body.Type==FLVTag.TagType.Script && OnScriptTag(body, info)) {
                    isMetaData = true;
                    res.ChannelInfo = new ChannelInfo(info);
                  }
                  if ((isMetaData || body.IsAVCHeader || body.IsAACHeader) && res.ContentHeader != null) {
                    var conbin = res.ContentHeader.Data.Concat(bin).ToArray();
                    res.ContentHeader = new Content(streamIndex, TimeSpan.Zero, position, conbin);
                  }
                  else {
                    res.Contents.Add(new Content(streamIndex, DateTime.Now - streamOrigin, position, bin));
                  }
                  position += bin.Length;
                }
              }
              if (!read_valid) {
                stream.Position = start_pos;
                var header = new FileHeader(ReadBytes(stream, 13));
                if (header.IsValid) {
                  Logger.Info("New FLV Header found");
                  read_valid = true;
                  fileHeader = header;
                  bin = header.Binary;
                  streamIndex = Channel.GenerateStreamID();
                  streamOrigin = DateTime.Now;
                  res.ContentHeader = new Content(streamIndex, TimeSpan.Zero, 0, bin);
                  res.Contents = null;
                  info.SetChanInfoType("FLV");
                  info.SetChanInfoStreamType("video/x-flv");
                  info.SetChanInfoStreamExt(".flv");
                  res.ChannelInfo = new ChannelInfo(info);
                  position = bin.Length;
                }
              }
              if (!read_valid) throw new BadDataException();
            }
            break;
          }
          processed = true;
        }
        catch (EndOfStreamException) {
          if (!processed) throw;
          stream.Position = start_pos;
          eos = true;
        }
        catch (BadDataException) {
          stream.Position = start_pos+1;
        }
      }
      return res;
    }

    private class AMF0Reader
      : IDisposable
    {
      public Stream BaseStream { get; private set; }
      public AMF0Reader(Stream base_stream)
      {
        BaseStream = base_stream;
      }

      private enum DataType {
        Number          = 0,
        Boolean         = 1,
        String          = 2,
        Object          = 3,
        MovieClip       = 4,
        Null            = 5,
        Undefined       = 6,
        Reference       = 7,
        Array           = 8,
        ObjectEndMarker = 9,
        StrictArray     = 10,
        Date            = 11,
        LongString      = 12,
      }

      public class ScriptDataObjectEnd {}
      public class ScriptDataObject : Dictionary<string, object> { }
      public class ScriptDataEcmaArray : Dictionary<string, object> { }

      public object ReadValue()
      {
        switch ((DataType)ReadUI8()) {
        case DataType.Number:
          return ReadDouble();
        case DataType.Boolean:
          return ReadUI8()!=0;
        case DataType.String:
          return ReadString();
        case DataType.Object:
          return ReadObject();
        case DataType.MovieClip:
          return null; //Not supported
        case DataType.Null:
          return null;
        case DataType.Undefined:
          return null;
        case DataType.Reference:
          return ReadUI16(); //???
        case DataType.Array:
          return ReadEcmaArray();
        case DataType.ObjectEndMarker:
          return new ScriptDataObjectEnd();
        case DataType.StrictArray:
          return ReadStrictArray();
        case DataType.Date:
          return ReadDate();
        case DataType.LongString:
          return ReadLongString();
        }
        return null;
      }

      public string ReadString()
      {
        var len = ReadUI16();
        var buf = new byte[len];
        BaseStream.Read(buf, 0, len);
        return System.Text.Encoding.UTF8.GetString(buf);
      }

      public string ReadLongString()
      {
        var len = ReadUI32();
        var buf = new byte[len];
        var pos = 0;
        while (len>0) {
          var read = BaseStream.Read(buf, pos, (int)Math.Min(len, Int32.MaxValue));
          pos += read;
          len -= read;
        }
        return System.Text.Encoding.UTF8.GetString(buf);
      }

      public long ReadUI32()
      {
        var buf = new byte[4];
        BaseStream.Read(buf, 0, 4);
        if (BitConverter.IsLittleEndian) Array.Reverse(buf);
        return BitConverter.ToUInt32(buf, 0);
      }

      public int ReadUI16()
      {
        var buf = new byte[2];
        BaseStream.Read(buf, 0, 2);
        if (BitConverter.IsLittleEndian) Array.Reverse(buf);
        return BitConverter.ToUInt16(buf, 0);
      }

      public int ReadSI16()
      {
        var buf = new byte[2];
        BaseStream.Read(buf, 0, 2);
        if (BitConverter.IsLittleEndian) Array.Reverse(buf);
        return BitConverter.ToInt16(buf, 0);
      }

      public int ReadUI8()
      {
        var b = BaseStream.ReadByte();
        if (b<0) throw new EndOfStreamException();
        return b;
      }

      public double ReadDouble()
      {
        var buf = new byte[8];
        BaseStream.Read(buf, 0, 8);
        if (BitConverter.IsLittleEndian) Array.Reverse(buf);
        return BitConverter.ToDouble(buf, 0);
      }

      private KeyValuePair<string, object>? ReadProperty()
      {
        var name = ReadString();
        var value = ReadValue();
        if (name=="" && value is ScriptDataObjectEnd) {
          return null;
        }
        else {
          return new KeyValuePair<string, object>(name, value);
        }
      }

      public ScriptDataObject ReadObject()
      {
        var obj = new ScriptDataObject();
        var prop = ReadProperty();
        while (prop.HasValue) {
          obj.Add(prop.Value.Key, prop.Value.Value);
          prop = ReadProperty();
        }
        return obj;
      }

      public ScriptDataEcmaArray ReadEcmaArray()
      {
        var len = ReadUI32();
        var obj = new ScriptDataEcmaArray();
        var prop = ReadProperty();
        while (prop.HasValue) {
          obj.Add(prop.Value.Key, prop.Value.Value);
          prop = ReadProperty();
        }
        return obj;
      }

      public object[] ReadStrictArray()
      {
        var ary = new object[ReadUI32()];
        for (long i=0; i<ary.LongLength; i++) {
          ary[i] = ReadValue();
        }
        return ary;
      }

      public DateTimeOffset ReadDate()
      {
        var time = ReadDouble();
        var tz   = ReadSI16();
        var utc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(time);
        return new DateTimeOffset(utc).ToOffset(TimeSpan.FromMinutes(tz));
      }

      public void Dispose()
      {
        BaseStream.Dispose();
      }
    }

    private bool OnScriptTag(FLVTag tag, AtomCollection channel_info)
    {
      bool modified = false;
      using (var reader=new AMF0Reader(new MemoryStream(tag.Body))) {
        var name  = reader.ReadValue();
        var value = reader.ReadValue();
        if (!(name is string)) return false;
        double bitrate = 0;
        switch ((string)name) {
        case "onMetaData":
          {
            var args = value as AMF0Reader.ScriptDataEcmaArray;
            if (args==null) break;
            object val;
            if (args.TryGetValue("videodatarate", out val)) {
              bitrate += (double)val;
            }
            if (args.TryGetValue("audiodatarate", out val)) {
              bitrate += (double)val;
            }
          }
          break;
        }
        if (bitrate!=0) {
          channel_info.SetChanInfoBitrate((int)bitrate);
          modified = true;
        }
      }
      return modified;
    }
  }

  public class FLVContentReaderFactory
    : IContentReaderFactory
  {
    public string Name { get { return "Flash Video (FLV)"; } }

    public IContentReader Create(Channel channel)
    {
      return new FLVContentReader(channel);
    }
  }

  [Plugin]
  public class FLVContentReaderPlugin
    : PluginBase
  {
    override public string Name { get { return "FLV Content Reader"; } }

    private FLVContentReaderFactory factory;
    override protected void OnAttach()
    {
      if (factory==null) factory = new FLVContentReaderFactory();
      Application.PeerCast.ContentReaderFactories.Add(factory);
    }

    override protected void OnDetach()
    {
      Application.PeerCast.ContentReaderFactories.Remove(factory);
    }
  }
}