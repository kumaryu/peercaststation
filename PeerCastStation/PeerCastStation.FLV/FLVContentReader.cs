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
      public byte[] Header    { get; private set; }
      public byte[] Body      { get; private set; }
      public byte[] Footer    { get; private set; }
      public bool   Filter    { get; private set; }
      public int    Type      { get; private set; }
      public int    DataSize  { get; private set; }
      public long   Timestamp { get; private set; }
      public int    StreamID  { get; private set; }
      public long   TagSize   {
        get { return FLVContentReader.GetUInt32(this.Footer); }
      }
      public byte[] Binary {
        get { return Header.Concat(Body).Concat(Footer).ToArray(); }
      }

      public FLVTag(byte[] binary)
      {
        this.Header    = binary;
        this.Filter    = (binary[0] & 0x20)!=0;
        this.Type      = binary[0] & 0x1F;
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
            (Type==8 || Type==9 || Type==19) &&
            StreamID==0;
        }
      }

      public bool IsValidFooter {
        get { return this.DataSize+11==this.TagSize; }
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
    private FileHeader fileHeader;
    private LinkedList<TagDesc> tags = new LinkedList<TagDesc>();

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
                res.ContentHeader = new Content(position, bin);
                res.Contents = null;
                info.SetChanInfoType("FLV");
                info.SetChanInfoStreamType("video/x-flv");
                info.SetChanInfoStreamExt(".flv");
                res.ChannelInfo = new ChannelInfo(info);
                position = bin.Length;
                tags.Clear();
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
                  res.Contents.Add(new Content(position, bin));
                  tags.AddLast(new TagDesc { Timestamp=body.Timestamp/1000.0, DataSize=body.DataSize });
                  var timespan = tags.Last.Value.Timestamp-tags.First.Value.Timestamp;
                  if (timespan>=30.0) {
                    var sz = tags.Take(tags.Count-1).Sum(t => t.DataSize);
                    info.SetChanInfoBitrate((int)(sz*8/timespan+900)/1000);
                    res.ChannelInfo = new ChannelInfo(info);
                    while (tags.Count>1) tags.RemoveFirst();
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
                  res.ContentHeader = new Content(0, bin);
                  res.Contents = null;
                  info.SetChanInfoType("FLV");
                  info.SetChanInfoStreamType("video/x-flv");
                  info.SetChanInfoStreamExt(".flv");
                  res.ChannelInfo = new ChannelInfo(info);
                  tags.Clear();
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
  }

  [Plugin]
  public class FLVContentReaderFactory
    : IContentReaderFactory
  {
    public string Name { get { return "Flash Video (FLV)"; } }

    public IContentReader Create(Channel channel)
    {
      return new FLVContentReader(channel);
    }
  }
}