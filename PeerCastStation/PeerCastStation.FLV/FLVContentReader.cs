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

      public bool IsMetaData {
        get {
          return Type == TagType.Script && Body.Length > 12 &&
            Body[0] == 0x02 && Body[1] == 0x00 && Body[2] == 0x0A &&
            System.Text.Encoding.ASCII.GetString(Body, 3, 10) == "onMetaData";
        }
      }

      public bool IsAVCHeader {
        get {
          return Type==TagType.Video && Body.Length>3 &&
            (Body[0] == 0x17 && Body[1] == 0x00 && Body[2] == 0x00 && Body[3] == 0x00);
        }
      }

      public bool IsAACHeader {
        get {
          return Type==TagType.Audio && Body.Length>1 &&
            (Body[0] == 0xAF && Body[1] == 0x00);
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
    private Queue<Content> contentsQueue = new Queue<Content>();

    private IList<Content> FlushContents(IList<Content> prev)
    {
      if (contentsQueue.Count==0) return prev;
      var list = new List<Content>();
      var last = contentsQueue.Dequeue();
      while (contentsQueue.Count>0) {
        var cur = contentsQueue.Dequeue();
        if (last.Stream!=cur.Stream ||
            last.Position+last.Data.Length!=cur.Position ||
            last.Data.Length+cur.Data.Length>12*1024) {
          list.Add(last);
          last = cur;
        }
        else {
          last = new Content(last.Stream, last.Timestamp, last.Position, last.Data.Concat(cur.Data).ToArray());
        }
      }
      list.Add(last);
      return prev!=null ? prev.Concat(list).ToArray() : list.ToArray();
    }

    private bool CheckContentsQueueIsFull()
    {
      return contentsQueue.Sum(c => c.Data.Length)>7500;
    }

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
                  if ((body.IsMetaData || body.IsAVCHeader || body.IsAACHeader) && res.ContentHeader != null) {
                    var conbin = res.ContentHeader.Data.Concat(bin).ToArray();
                    res.ContentHeader = new Content(streamIndex, TimeSpan.Zero, position, conbin);
                    res.Contents = FlushContents(res.Contents);
                  }
                  else {
                    contentsQueue.Enqueue(new Content(streamIndex, DateTime.Now - streamOrigin, position, bin));
                    if (CheckContentsQueueIsFull()) {
                      res.Contents = FlushContents(res.Contents);
                    }
                  }
                  if (body.Type == FLVTag.TagType.Script && OnScriptTag(body, info)) {
                    res.ChannelInfo = new ChannelInfo(info);
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
                  res.Contents = FlushContents(res.Contents);
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

    private bool OnScriptTag(FLVTag tag, AtomCollection channel_info)
    {
      bool modified = false;
      using (var reader=new AMF.AMF0Reader(new MemoryStream(tag.Body))) {
        var name  = reader.ReadValue();
        var value = reader.ReadValue();
        if (name.Type!=AMF.AMFValueType.String) return false;
        double bitrate = 0;
        switch ((string)name) {
        case "onMetaData":
          {
            if (value.ContainsKey("maxBitrate")) {
              string maxBitrateStr = System.Text.RegularExpressions.Regex.Replace(value["maxBitrate"].ToString(), @"([\d]+)k", "$1");
              double maxBitrate;
              if (double.TryParse(maxBitrateStr, out maxBitrate)) {
                bitrate += maxBitrate;
              }
            }
            else {
              if (value.ContainsKey("videodatarate")) {
                bitrate += (double)value["videodatarate"];
              }
            }
            if (value.ContainsKey("audiodatarate")) {
              bitrate += (double)value["audiodatarate"];
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