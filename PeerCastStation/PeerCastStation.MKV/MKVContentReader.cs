using System;
using System.Linq;
using System.IO;
using PeerCastStation.Core;
using System.Collections.Generic;

namespace PeerCastStation.MKV
{
  internal class BadDataException : ApplicationException
  {
  }

  internal class VInt
  {
    public long   Value  { get; private set; }
    public byte[] Binary { get; private set; }
    public int    Length { get { return Binary.Length; } }
    public bool IsUnknown {
      get {
        return Enumerable.Range(0, this.Length*7).All(i => (Value & (1<<i))!=0);
      }
    }

    public VInt(long value, byte[] binary)
    {
      this.Value  = value;
      this.Binary = binary;
    }

    public bool BinaryEquals(byte[] bin)
    {
      return this.Binary.SequenceEqual(bin);
    }

    public static VInt ReadUInt(Stream s)
    {
      int b = s.ReadByte();
      if (b<0) throw new EndOfStreamException();
      int len = CheckLength(b);
      if (len<0) throw new BadDataException();
      var bin = new byte[len];
      bin[0] = (byte)b;
      long res = b;
      for (var i=1; i<len; i++) {
        b = s.ReadByte();
        if (b<0) throw new EndOfStreamException();
        bin[i] = (byte)b;
        res = (res<<8) | (byte)b;
      }
      res &= (1<<(7*len))-1;
      return new VInt(res, bin);
    }

    public static VInt ReadSInt(Stream s)
    {
      var uv = ReadUInt(s);
      return new VInt(uv.Value - (1<<(uv.Length*7-1))-1, uv.Binary);
    }

    public static int CheckLength(int v)
    {
      try {
        return Enumerable.Range(0, 8).First(i => ((1<<(7-i)) & v)!=0)+1;
      }
      catch (InvalidOperationException) {
        return -1;
      }
    }
  }

  internal class Element
  {
    public VInt   ID       { get; set; }
    public VInt   Size     { get; set; }
    public byte[] Data     { get; set; }
    public long   Position { get; set; }

    public Element(VInt id, VInt size, long position)
    {
      this.ID       = id;
      this.Size     = size;
      this.Position = position;
    }

    public void ReadBody(Stream s)
    {
      if (!this.Size.IsUnknown) {
        var data = new byte[this.Size.Value];
        if (s.Read(data, 0, (int)this.Size.Value)<this.Size.Value) {
          throw new EndOfStreamException();
        }
        this.Data = data;
      }
    }

    public void Write(Stream s)
    {
      s.Write(this.ID.Binary, 0, this.ID.Binary.Length);
      s.Write(this.Size.Binary, 0, this.Size.Binary.Length);
      if (this.Data!=null) {
        s.Write(this.Data, 0, this.Data.Length);
      }
    }

    public byte[] ToArray()
    {
      var s = new MemoryStream();
      Write(s);
      s.Close();
      return s.ToArray();
    }

    public static Element ReadHeader(Stream s)
    {
      var pos = s.Position;
      var id  = VInt.ReadUInt(s);
      var sz  = VInt.ReadUInt(s);
      return new Element(id, sz, pos);
    }

    public static long ReadUInt(Stream s, long len)
    {
      long res = 0;
      for (var i=0; i<len; i++) {
        var v = s.ReadByte();
        if (v<0) throw new EndOfStreamException();
        res = (res<<8) | (byte)v;
      }
      return res;
    }

    public static long ReadSInt(Stream s, long len)
    {
      long res = 0;
      for (var i=0; i<len; i++) {
        var v = s.ReadByte();
        if (v<0) throw new EndOfStreamException();
        res = (res<<8) | (byte)v;
      }
      if ((res & (1<<(int)(len*8-1)))!=0) {
        res -= 1<<(int)(len*8);
      }
      return res;
    }

    public static string ReadString(Stream s, long len)
    {
      var buf = new byte[len];
      if (s.Read(buf, 0, (int)len)<len) {
        throw new EndOfStreamException();
      }
      return System.Text.Encoding.UTF8.GetString(buf);
    }
  }

  public static class Elements
  {
    public static readonly byte[] EBML        = { 0x1A,0x45,0xDF,0xA3 };
    public static readonly byte[] EBMLVersion        = { 0x42,0x86 };
    public static readonly byte[] EBMLReadVersion    = { 0x42,0xF7 };
    public static readonly byte[] EBMLMaxIDLength    = { 0x42,0xF2 };
    public static readonly byte[] EBMLMaxSizeLength  = { 0x42,0xF3 };
    public static readonly byte[] DocType            = { 0x42,0x82 };
    public static readonly byte[] DocTypeVersion     = { 0x42,0x87 };
    public static readonly byte[] DocTypeReadVersion = { 0x42,0x85 };
    public static readonly byte[] Segment       = { 0x18,0x53,0x80,0x67 };
    public static readonly byte[] SeekHead      = { 0x11,0x4D,0x9B,0x74 };
    public static readonly byte[] Info          = { 0x15,0x49,0xA9,0x66 };
    public static readonly byte[] Cluster       = { 0x1F,0x43,0xB6,0x75 };
    public static readonly byte[] Tracks        = { 0x16,0x54,0xAE,0x6B };
    public static readonly byte[] Cues          = { 0x1C,0x53,0xBB,0x6B };
    public static readonly byte[] Attachments   = { 0x19,0x41,0xA4,0x69 };
    public static readonly byte[] Chapters      = { 0x10,0x43,0xA7,0x70 };
    public static readonly byte[] Tags          = { 0x12,0x54,0xC3,0x67 };
    public static readonly byte[] TimecodeScale = { 0x2A,0xD1,0xB1 };
    public static readonly byte[] BlockGroup    = { 0xA0 };
    public static readonly byte[] SimpleBlock   = { 0xA3 };
    public static readonly byte[] Timecode      = { 0xE7 };
    public static readonly byte[] PrevSize      = { 0xAB };
    public static readonly byte[] Void          = { 0xEC };
    public static readonly byte[] CRC32         = { 0xBF };
    public static readonly byte[] Position      = { 0xA7 };
  }

  internal class EBML
  {
    public Element Element            { get; private set; }
    public int     Version            { get; private set; }
    public int     ReadVersion        { get; private set; }
    public int     MaxIDLength        { get; private set; }
    public int     MaxSizeLength      { get; private set; }
    public string  DocType            { get; private set; }
    public int     DocTypeVersion     { get; private set; }
    public int     DocTypeReadVersion { get; private set; }

    public EBML()
    {
      this.Version            = 1;
      this.ReadVersion        = 1;
      this.MaxIDLength        = 4;
      this.MaxSizeLength      = 8;
      this.DocType            = "matroska";
      this.DocTypeVersion     = 1;
      this.DocTypeReadVersion = 1;
      this.Element = null;
    }

    public EBML(Element element)
      : this()
    {
      this.Element = element;
      using (var s=new MemoryStream(this.Element.Data)) {
        while (s.Position<s.Length) {
          var elt = Element.ReadHeader(s);
          if (elt.ID.BinaryEquals(Elements.EBMLVersion)) {
            this.Version = (int)Element.ReadUInt(s, elt.Size.Value);
          }
          else if (elt.ID.BinaryEquals(Elements.EBMLReadVersion)) {
            this.ReadVersion = (int)Element.ReadUInt(s, elt.Size.Value);
          }
          else if (elt.ID.BinaryEquals(Elements.EBMLMaxIDLength)) {
            this.MaxIDLength = (int)Element.ReadUInt(s, elt.Size.Value);
          }
          else if (elt.ID.BinaryEquals(Elements.EBMLMaxSizeLength)) {
            this.MaxSizeLength = (int)Element.ReadUInt(s, elt.Size.Value);
          }
          else if (elt.ID.BinaryEquals(Elements.DocType)) {
            this.DocType = Element.ReadString(s, elt.Size.Value);
          }
          else if (elt.ID.BinaryEquals(Elements.DocTypeVersion)) {
            this.DocTypeVersion = (int)Element.ReadUInt(s, elt.Size.Value);
          }
          else if (elt.ID.BinaryEquals(Elements.DocTypeReadVersion)) {
            this.DocTypeReadVersion = (int)Element.ReadUInt(s, elt.Size.Value);
          }
          else {
            elt.ReadBody(s);
          }
        }
      }
    }
  }

  internal class Segment
  {
    public Element       Element        { get; private set; }
    public List<Element> HeaderElements { get; private set; }
    public double        TimecodeScale  { get; private set; }

    public Segment(Element element)
    {
      this.Element        = element;
      this.HeaderElements = new List<Element>();
      this.TimecodeScale  = 1000000.0;
    }

    public void AddHeader(Element elt)
    {
      if (elt.ID.BinaryEquals(Elements.Info)) {
        var s = new MemoryStream(elt.Data);
        while (s.Position<s.Length) {
          var child = Element.ReadHeader(s);
          if (child.ID.BinaryEquals(Elements.TimecodeScale)) {
            this.TimecodeScale = Element.ReadUInt(s, child.Size.Value) * 1.0;
          }
          else {
            child.ReadBody(s);
          }
        }
      }
      HeaderElements.Add(elt);
    }
  }

  internal class Cluster
  {
    public Element Element   { get; private set; }
    public long    BlockSize { get; set; }
    public byte[]  BlockID   { get; set; }
    public double  Timecode  { get; set; }
    public double  Timespan  { get; set; }

    public Cluster(Element element)
    {
      this.Element   = element;
      this.BlockSize = 0;
      this.BlockID   = null;
      this.Timecode  = 0.0;
      this.Timespan  = 0.0;
    }
  }

  public class MKVContentReader
    : IContentReader
  {
    public MKVContentReader(Channel channel)
    {
      this.Channel = channel;
    }

    public string Name { get { return "Matroska (MKV or WebM)"; } }
    public Channel Channel { get; private set; }

    private EBML    ebml    = new EBML();
    private Segment segment;
    private LinkedList<Cluster> clusters = new LinkedList<Cluster>();

    private enum ReaderState {
      EBML,
      Segment,
      EndOfHeader,
      Cluster,
      Timecode,
      Block,
    };
    private ReaderState state = ReaderState.EBML;
    private long position = 0;

    public ParsedContent Read(Stream stream)
    {
      var res = new ParsedContent();
      var bodies = new List<Element>();
      var info = new AtomCollection(Channel.ChannelInfo.Extra);
      var processed = false;
      var eos = false;
      while (!eos) {
        var start_pos = stream.Position;
        try {
          var elt = Element.ReadHeader(stream);
          if (ebml.MaxIDLength  <elt.ID.Length ||
              ebml.MaxSizeLength<elt.Size.Length) {
            throw new BadDataException();
          }
          switch (state) {
          case ReaderState.EBML:
            if (elt.ID.BinaryEquals(Elements.EBML)) {
              elt.ReadBody(stream);
              ebml = new EBML(elt);
              state = ReaderState.Segment;
            }
            else {
              throw new BadDataException();
            }
            break;
          case ReaderState.Segment:
            if (elt.ID.BinaryEquals(Elements.Segment)) {
              segment = new Segment(elt);
              state = ReaderState.EndOfHeader;
            }
            else if (elt.ID.BinaryEquals(Elements.EBML)) {
              stream.Position = elt.Position;
              state = ReaderState.EBML;
              continue;
            }
            else if (elt.ID.BinaryEquals(Elements.Void) ||
                     elt.ID.BinaryEquals(Elements.CRC32)) {
              elt.ReadBody(stream);
            }
            else {
              throw new BadDataException();
            }
            break;
          case ReaderState.EndOfHeader:
            if (elt.ID.BinaryEquals(Elements.Segment)) {
              stream.Position = elt.Position;
              state = ReaderState.Segment;
              continue;
            }
            else if (elt.ID.BinaryEquals(Elements.EBML)) {
              stream.Position = elt.Position;
              state = ReaderState.EBML;
              continue;
            }
            else if (elt.ID.BinaryEquals(Elements.Cluster)) {
              stream.Position = elt.Position;
              state = ReaderState.Cluster;
              clusters.Clear();
              MemoryStream header;
              using (header=new MemoryStream()) {
                ebml.Element.Write(header);
                segment.Element.Write(header);
                foreach (var c in segment.HeaderElements) {
                  c.Write(header);
                }
              }
              res.ContentHeader = new Content(0, header.ToArray());
              if (ebml.DocType=="webm") {
                info.SetChanInfoType("WEBM");
                info.SetChanInfoStreamType("video/webm");
                info.SetChanInfoStreamExt(".webm");
              }
              else {
                info.SetChanInfoType("MKV");
                info.SetChanInfoStreamType("video/x-matroska");
                info.SetChanInfoStreamExt(".mkv");
              }
              res.ChannelInfo = new ChannelInfo(info);
            }
            else {
              elt.ReadBody(stream);
              segment.AddHeader(elt);
            }
            break;
          case ReaderState.Cluster:
            if (elt.ID.BinaryEquals(Elements.Segment)) {
              stream.Position = elt.Position;
              state = ReaderState.Segment;
              continue;
            }
            else if (elt.ID.BinaryEquals(Elements.EBML)) {
              stream.Position = elt.Position;
              state = ReaderState.EBML;
              continue;
            }
            else if (elt.ID.BinaryEquals(Elements.Cluster)) {
              if (clusters.Count>0) {
                var timespan = clusters.Sum(c => c.Timespan);
                if (timespan>=30.0) {
                  var sz = clusters.Sum(c => c.Timespan>0 ? c.BlockSize : 0);
                  var kbps = (int)((sz*8/timespan+900) / 1000.0);
                  info.SetChanInfoBitrate(kbps);
                  res.ChannelInfo = new ChannelInfo(info);
                  while (clusters.Count>1) clusters.RemoveFirst();
                }
              }
              var cluster = new Cluster(elt);
              clusters.AddLast(cluster);
              bodies.Add(elt);
              state = ReaderState.Timecode;
            }
            else if (elt.ID.BinaryEquals(Elements.Void) ||
                     elt.ID.BinaryEquals(Elements.CRC32)) {
              elt.ReadBody(stream);
              bodies.Add(elt);
            }
            else {
              throw new BadDataException();
            }
            break;
          case ReaderState.Timecode:
            if (elt.ID.BinaryEquals(Elements.Segment)) {
              stream.Position = elt.Position;
              state = ReaderState.Segment;
              continue;
            }
            else if (elt.ID.BinaryEquals(Elements.EBML)) {
              stream.Position = elt.Position;
              state = ReaderState.EBML;
              continue;
            }
            else if (elt.ID.BinaryEquals(Elements.Cluster)) {
              stream.Position = elt.Position;
              state = ReaderState.Cluster;
              continue;
            }
            else if (elt.ID.BinaryEquals(Elements.Timecode)) {
              elt.ReadBody(stream);
              if (clusters.Last!=null) {
                clusters.Last.Value.Timecode =
                  Element.ReadUInt(new MemoryStream(elt.Data), elt.Data.Length)*(segment.TimecodeScale/1000000000.0);
                if (clusters.Count>1) {
                  clusters.Last.Previous.Value.Timespan = clusters.Last.Value.Timecode - clusters.Last.Previous.Value.Timecode;
                }
              }
              bodies.Add(elt);
              state = ReaderState.Block;
            }
            else if (elt.ID.BinaryEquals(Elements.SimpleBlock) ||
                     elt.ID.BinaryEquals(Elements.BlockGroup)) {
              stream.Position = elt.Position;
              state = ReaderState.Block;
              continue;
            }
            else {
              elt.ReadBody(stream);
              bodies.Add(elt);
            }
            break;
          case ReaderState.Block:
            if (elt.ID.BinaryEquals(Elements.Segment)) {
              stream.Position = elt.Position;
              state = ReaderState.Segment;
              continue;
            }
            else if (elt.ID.BinaryEquals(Elements.EBML)) {
              stream.Position = elt.Position;
              state = ReaderState.EBML;
              continue;
            }
            else if (elt.ID.BinaryEquals(Elements.Cluster)) {
              stream.Position = elt.Position;
              state = ReaderState.Cluster;
              continue;
            }
            else if ((elt.ID.BinaryEquals(Elements.SimpleBlock) ||
                      elt.ID.BinaryEquals(Elements.BlockGroup)) &&
                     (clusters.Last.Value.BlockID==null ||
                      elt.ID.BinaryEquals(clusters.Last.Value.BlockID))) {
              elt.ReadBody(stream);
              clusters.Last.Value.BlockSize += elt.Size.Value;
              clusters.Last.Value.BlockID    = elt.ID.Binary;
              bodies.Add(elt);
            }
            else if (clusters.Last.Value.BlockID==null) {
              elt.ReadBody(stream);
              bodies.Add(elt);
            }
            else {
              stream.Position = elt.Position;
              state = ReaderState.Cluster;
              continue;
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
      if (res.ContentHeader!=null) {
        position =
          res.ContentHeader.Position +
          res.ContentHeader.Data.Length;
      }
      if (bodies.Count>0) {
        res.Contents = new List<Content>();
        foreach (var body in bodies) {
          MemoryStream s;
          using (s=new MemoryStream()) {
            body.Write(s);
          }
          var data = s.ToArray();
          res.Contents.Add(new Content(position, data));
          position += data.Length;
        }
      }
      return res;
    }
  }

  [Plugin]
  public class MKVContentReaderFactory
    : IContentReaderFactory
  {
    public string Name { get { return "Matroska (MKV or WebM)"; } }

    public IContentReader Create(Channel channel)
    {
      return new MKVContentReader(channel);
    }
  }
}
