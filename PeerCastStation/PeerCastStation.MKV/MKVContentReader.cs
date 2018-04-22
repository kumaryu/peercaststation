using System;
using System.Linq;
using System.IO;
using PeerCastStation.Core;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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

    public static async Task<VInt> ReadUIntAsync(Stream s, CancellationToken cancel_token)
    {
      int first = await s.ReadByteAsync().ConfigureAwait(false);
      if (first<0) throw new EndOfStreamException();
      int len = CheckLength(first);
      var bin = new byte[len];
      bin[0] = (byte)first;
      await s.ReadBytesAsync(bin, 1, len-1, cancel_token).ConfigureAwait(false);
      long res = bin.Aggregate(0L, (r, b) => (r<<8) | b);
      res &= (1<<(7*len))-1;
      return new VInt(res, bin);
    }

    public static VInt ReadUInt(Stream s)
    {
      int b = s.ReadByte();
      if (b<0) throw new EndOfStreamException();
      int len = CheckLength(b);
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

    private static int CheckLength(int v)
    {
      try {
        return Enumerable.Range(0, 8).First(i => ((1<<(7-i)) & v)!=0)+1;
      }
      catch (InvalidOperationException) {
        throw new BadDataException();
      }
    }
  }

  internal class Element
  {
    public VInt   ID       { get; set; }
    public VInt   Size     { get; set; }
    public byte[] Data     { get; set; }
    public Element(VInt id, VInt size)
    {
      this.ID       = id;
      this.Size     = size;
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

    public async Task ReadBodyAsync(Stream s, CancellationToken cancel_token)
    {
      if (this.Size.IsUnknown) return;
      this.Data = await s.ReadBytesAsync((int)this.Size.Value, cancel_token).ConfigureAwait(false);
    }

    public void Write(Stream s)
    {
      s.Write(this.ID.Binary, 0, this.ID.Binary.Length);
      s.Write(this.Size.Binary, 0, this.Size.Binary.Length);
      if (this.Data!=null) {
        s.Write(this.Data, 0, this.Data.Length);
      }
    }

    public long ByteSize {
      get {
        return
          this.ID.Binary.LongLength +
          this.Size.Binary.LongLength +
          (this.Data?.LongLength ?? 0L);
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
      var id  = VInt.ReadUInt(s);
      var sz  = VInt.ReadUInt(s);
      return new Element(id, sz);
    }

    public static async Task<Element> ReadHeaderAsync(Stream s, CancellationToken cancel_token)
    {
      var id  = await VInt.ReadUIntAsync(s, cancel_token).ConfigureAwait(false);
      var sz  = await VInt.ReadUIntAsync(s, cancel_token).ConfigureAwait(false);
      return new Element(id, sz);
    }

    public static long ReadUInt(Stream s, long len)
    {
      return s.ReadBytes((int)len).Aggregate(0L, (r,v) => (r<<8) | v);
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

    private enum ReaderState {
      EBML,
      Segment,
      EndOfHeader,
      Cluster,
      Timecode,
      Block,
    };

    public async Task ReadAsync(IContentSink sink, Stream stream, CancellationToken cancel_token)
    {
      var state = ReaderState.EBML;
      var position = 0L;
      var stream_index = -1;
      var stream_origin = DateTime.Now;
      var timecode_scale = 1000000.0;
      var ebml     = new EBML();
      var clusters = new LinkedList<Cluster>();
      var headers  = new List<Element>();

      var eos = false;
      while (!eos) {
        try {
          var elt = await Element.ReadHeaderAsync(stream, cancel_token).ConfigureAwait(false);
          if (ebml.MaxIDLength  <elt.ID.Length ||
              ebml.MaxSizeLength<elt.Size.Length) {
            throw new BadDataException();
          }
        parse_retry:
          switch (state) {
          case ReaderState.EBML:
            if (elt.ID.BinaryEquals(Elements.EBML)) {
              await elt.ReadBodyAsync(stream, cancel_token).ConfigureAwait(false);
              headers.Clear();
              headers.Add(elt);
              ebml = new EBML(elt);
              state = ReaderState.Segment;
            }
            else {
              throw new BadDataException();
            }
            break;
          case ReaderState.Segment:
            if (elt.ID.BinaryEquals(Elements.Segment)) {
              headers.Add(elt);
              state = ReaderState.EndOfHeader;
            }
            else if (elt.ID.BinaryEquals(Elements.EBML)) {
              state = ReaderState.EBML;
              goto parse_retry;
            }
            else if (elt.ID.BinaryEquals(Elements.Void) ||
                     elt.ID.BinaryEquals(Elements.CRC32)) {
              await elt.ReadBodyAsync(stream, cancel_token).ConfigureAwait(false);
              headers.Add(elt);
            }
            else {
              throw new BadDataException();
            }
            break;
          case ReaderState.EndOfHeader:
            if (elt.ID.BinaryEquals(Elements.Segment)) {
              state = ReaderState.Segment;
              goto parse_retry;
            }
            else if (elt.ID.BinaryEquals(Elements.EBML)) {
              state = ReaderState.EBML;
              goto parse_retry;
            }
            else if (elt.ID.BinaryEquals(Elements.Cluster)) {
              clusters.Clear();
              MemoryStream header;
              using (header=new MemoryStream()) {
                foreach (var c in headers) {
                  c.Write(header);
                }
              }
              headers.Clear();

              stream_index  = Channel.GenerateStreamID();
              stream_origin = DateTime.Now;
              position = 0;
              sink.OnContentHeader(
                new Content(stream_index, TimeSpan.Zero, 0, header.ToArray())
              );
              position += header.ToArray().LongLength;
              var info = new AtomCollection();
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
              sink.OnChannelInfo(new ChannelInfo(info));

              state = ReaderState.Cluster;
              goto parse_retry;
            }
            else if (elt.ID.BinaryEquals(Elements.Info)) {
              await elt.ReadBodyAsync(stream, cancel_token).ConfigureAwait(false);
              var s = new MemoryStream(elt.Data);
              while (s.Position<s.Length) {
                var child = Element.ReadHeader(s);
                if (child.ID.BinaryEquals(Elements.TimecodeScale)) {
                  timecode_scale = Element.ReadUInt(s, child.Size.Value) * 1.0;
                }
                else {
                  child.ReadBody(s);
                }
              }
              headers.Add(elt);
            }
            else {
              await elt.ReadBodyAsync(stream, cancel_token).ConfigureAwait(false);
              headers.Add(elt);
            }
            break;
          case ReaderState.Cluster:
            if (elt.ID.BinaryEquals(Elements.Segment)) {
              state = ReaderState.Segment;
              goto parse_retry;
            }
            else if (elt.ID.BinaryEquals(Elements.EBML)) {
              state = ReaderState.EBML;
              goto parse_retry;
            }
            else if (elt.ID.BinaryEquals(Elements.Cluster)) {
              if (clusters.Count>0) {
                var timespan = clusters.Sum(c => c.Timespan);
                if (timespan>=30.0) {
                  var sz = clusters.Sum(c => c.Timespan>0 ? c.BlockSize : 0);
                  var kbps = (int)((sz*8/timespan+900) / 1000.0);
                  var info = new AtomCollection();
                  info.SetChanInfoBitrate(kbps);
                  sink.OnChannelInfo(new ChannelInfo(info));
                  while (clusters.Count>1) clusters.RemoveFirst();
                }
              }
              var cluster = new Cluster(elt);
              clusters.AddLast(cluster);
              sink.OnContent(
                new Content(stream_index, DateTime.Now-stream_origin, position, elt.ToArray())
              );
              position += elt.ByteSize;
              state = ReaderState.Timecode;
            }
            else if (elt.ID.BinaryEquals(Elements.Void) ||
                     elt.ID.BinaryEquals(Elements.CRC32)) {
              await elt.ReadBodyAsync(stream, cancel_token).ConfigureAwait(false);
              sink.OnContent(
                new Content(stream_index, DateTime.Now-stream_origin, position, elt.ToArray())
              );
              position += elt.ByteSize;
            }
            else {
              throw new BadDataException();
            }
            break;
          case ReaderState.Timecode:
            if (elt.ID.BinaryEquals(Elements.Segment)) {
              state = ReaderState.Segment;
              goto parse_retry;
            }
            else if (elt.ID.BinaryEquals(Elements.EBML)) {
              state = ReaderState.EBML;
              goto parse_retry;
            }
            else if (elt.ID.BinaryEquals(Elements.Cluster)) {
              state = ReaderState.Cluster;
              goto parse_retry;
            }
            else if (elt.ID.BinaryEquals(Elements.SimpleBlock) ||
                     elt.ID.BinaryEquals(Elements.BlockGroup)) {
              state = ReaderState.Block;
              goto parse_retry;
            }
            else if (elt.ID.BinaryEquals(Elements.Timecode)) {
              await elt.ReadBodyAsync(stream, cancel_token).ConfigureAwait(false);
              if (clusters.Last!=null) {
                clusters.Last.Value.Timecode =
                  Element.ReadUInt(new MemoryStream(elt.Data), elt.Data.Length)*(timecode_scale/1000000000.0);
                if (clusters.Count>1) {
                  clusters.Last.Previous.Value.Timespan = clusters.Last.Value.Timecode - clusters.Last.Previous.Value.Timecode;
                }
              }
              sink.OnContent(
                new Content(stream_index, DateTime.Now-stream_origin, position, elt.ToArray())
              );
              position += elt.ByteSize;
              state = ReaderState.Block;
            }
            else {
              await elt.ReadBodyAsync(stream, cancel_token).ConfigureAwait(false);
              sink.OnContent(
                new Content(stream_index, DateTime.Now-stream_origin, position, elt.ToArray())
              );
              position += elt.ByteSize;
            }
            break;
          case ReaderState.Block:
            if (elt.ID.BinaryEquals(Elements.Segment)) {
              state = ReaderState.Segment;
              goto parse_retry;
            }
            else if (elt.ID.BinaryEquals(Elements.EBML)) {
              state = ReaderState.EBML;
              goto parse_retry;
            }
            else if (elt.ID.BinaryEquals(Elements.Cluster)) {
              state = ReaderState.Cluster;
              goto parse_retry;
            }
            else if ((elt.ID.BinaryEquals(Elements.SimpleBlock) ||
                      elt.ID.BinaryEquals(Elements.BlockGroup)) &&
                     (clusters.Last.Value.BlockID==null ||
                      elt.ID.BinaryEquals(clusters.Last.Value.BlockID))) {
              await elt.ReadBodyAsync(stream, cancel_token).ConfigureAwait(false);
              clusters.Last.Value.BlockSize += elt.Size.Value;
              clusters.Last.Value.BlockID    = elt.ID.Binary;
              sink.OnContent(
                new Content(stream_index, DateTime.Now-stream_origin, position, elt.ToArray())
              );
              position += elt.ByteSize;
            }
            else if (clusters.Last.Value.BlockID==null) {
              await elt.ReadBodyAsync(stream, cancel_token).ConfigureAwait(false);
              sink.OnContent(
                new Content(stream_index, DateTime.Now-stream_origin, position, elt.ToArray())
              );
              position += elt.ByteSize;
            }
            else {
              state = ReaderState.Cluster;
              goto parse_retry;
            }
            break;
          }
        }
        catch (EndOfStreamException) {
          eos = true;
        }
        catch (BadDataException) {
        }
      }

    }

  }

  public class MKVContentReaderFactory
    : IContentReaderFactory
  {
    public string Name { get { return "Matroska (MKV or WebM)"; } }

    public IContentReader Create(Channel channel)
    {
      return new MKVContentReader(channel);
    }

    public bool TryParseContentType(byte[] header, out string content_type, out string mime_type)
    {
      try {
        using (var stream=new MemoryStream(header)) {
          var elt = Element.ReadHeader(stream);
          if (4<elt.ID.Length || 8<elt.Size.Length ||
              !elt.ID.BinaryEquals(Elements.EBML)) {
            content_type = null;
            mime_type    = null;
            return false;
          }
          elt.ReadBody(stream);
          var ebml = new EBML(elt);
          if (ebml.DocType=="webm") {
            content_type = "WEBM";
            mime_type = "video/webm";
          }
          else {
            content_type = "MKV";
            mime_type = "video/x-matroska";
          }
          return true;
        }
      }
      catch (EndOfStreamException) {
        content_type = null;
        mime_type    = null;
        return false;
      }
    }

  }

  [Plugin]
  public class MKVContentReaderPlugin
    : PluginBase
  {
    override public string Name { get { return "Matroska Content Reader"; } }

    private MKVContentReaderFactory factory;
    override protected void OnAttach()
    {
      if (factory==null) factory = new MKVContentReaderFactory();
      Application.PeerCast.ContentReaderFactories.Add(factory);
    }

    override protected void OnDetach()
    {
      Application.PeerCast.ContentReaderFactories.Remove(factory);
    }
  }
}
