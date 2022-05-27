using System;
using System.Linq;
using System.IO;
using PeerCastStation.Core;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;

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

  internal readonly struct ElementHeader
  {
    public VInt ID   { get; }
    public VInt Size { get; }
    public ElementHeader(VInt id, VInt size)
    {
      ID       = id;
      Size     = size;
    }

    public void Write(Stream s)
    {
      s.Write(ID.Binary, 0, ID.Binary.Length);
      s.Write(Size.Binary, 0, Size.Binary.Length);
    }

    public static ElementHeader Read(Stream s)
    {
      var id  = VInt.ReadUInt(s);
      var sz  = VInt.ReadUInt(s);
      return new ElementHeader(id, sz);
    }

    public static async Task<ElementHeader> ReadAsync(Stream s, CancellationToken cancel_token)
    {
      var id  = await VInt.ReadUIntAsync(s, cancel_token).ConfigureAwait(false);
      var sz  = await VInt.ReadUIntAsync(s, cancel_token).ConfigureAwait(false);
      return new ElementHeader(id, sz);
    }
  }


  internal class Element
  {
    public ElementHeader Header { get; }
    public VInt ID => Header.ID;
    public VInt Size => Header.Size;
    public byte[] Data { get; }
    public Element(in ElementHeader header, byte[] data)
    {
      Header = header;
      Data = data;
    }

    public static Element NoData(in ElementHeader header)
    {
      return new Element(header, Array.Empty<byte>());
    }

    public static Element ReadBody(in ElementHeader header, Stream s)
    {
      if (!header.Size.IsUnknown) {
        var data = new byte[header.Size.Value];
        if (s.Read(data, 0, (int)header.Size.Value)<header.Size.Value) {
          throw new EndOfStreamException();
        }
        return new Element(header, data);
      }
      else {
        return new Element(header, Array.Empty<byte>());
      }
    }

    public static async Task<Element> ReadBodyAsync(ElementHeader header, Stream s, CancellationToken cancel_token)
    {
      if (header.Size.IsUnknown) {
        return new Element(header, Array.Empty<byte>());
      }
      else {
        var data = await s.ReadBytesAsync((int)header.Size.Value, cancel_token).ConfigureAwait(false);
        return new Element(header, data);
      }
    }

    public void Write(Stream s)
    {
      s.Write(ID.Binary, 0, ID.Binary.Length);
      s.Write(Size.Binary, 0, Size.Binary.Length);
      s.Write(Data, 0, Data.Length);
    }

    public long ByteSize {
      get {
        return
          ID.Binary.LongLength +
          Size.Binary.LongLength +
          Data.LongLength;
      }
    }

    public byte[] ToArray()
    {
      var s = new MemoryStream();
      Write(s);
      s.Close();
      return s.ToArray();
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
    public Element? Element            { get; private set; }
    public int      Version            { get; private set; }
    public int      ReadVersion        { get; private set; }
    public int      MaxIDLength        { get; private set; }
    public int      MaxSizeLength      { get; private set; }
    public string   DocType            { get; private set; }
    public int      DocTypeVersion     { get; private set; }
    public int      DocTypeReadVersion { get; private set; }

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
          var header = ElementHeader.Read(s);
          if (header.ID.BinaryEquals(Elements.EBMLVersion)) {
            this.Version = (int)Element.ReadUInt(s, header.Size.Value);
          }
          else if (header.ID.BinaryEquals(Elements.EBMLReadVersion)) {
            this.ReadVersion = (int)Element.ReadUInt(s, header.Size.Value);
          }
          else if (header.ID.BinaryEquals(Elements.EBMLMaxIDLength)) {
            this.MaxIDLength = (int)Element.ReadUInt(s, header.Size.Value);
          }
          else if (header.ID.BinaryEquals(Elements.EBMLMaxSizeLength)) {
            this.MaxSizeLength = (int)Element.ReadUInt(s, header.Size.Value);
          }
          else if (header.ID.BinaryEquals(Elements.DocType)) {
            this.DocType = Element.ReadString(s, header.Size.Value);
          }
          else if (header.ID.BinaryEquals(Elements.DocTypeVersion)) {
            this.DocTypeVersion = (int)Element.ReadUInt(s, header.Size.Value);
          }
          else if (header.ID.BinaryEquals(Elements.DocTypeReadVersion)) {
            this.DocTypeReadVersion = (int)Element.ReadUInt(s, header.Size.Value);
          }
          else {
            Element.ReadBody(header, s);
          }
        }
      }
    }
  }

  internal class Cluster
  {
    public long    BlockSize { get; set; }
    public byte[]? BlockID   { get; set; }
    public double  Timecode  { get; set; }
    public double  Timespan  { get; set; }

    public Cluster()
    {
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
          var header = await ElementHeader.ReadAsync(stream, cancel_token).ConfigureAwait(false);
          if (ebml.MaxIDLength  <header.ID.Length ||
              ebml.MaxSizeLength<header.Size.Length) {
            throw new BadDataException();
          }
        parse_retry:
          switch (state) {
          case ReaderState.EBML:
            if (header.ID.BinaryEquals(Elements.EBML)) {
              var elt = await Element.ReadBodyAsync(header, stream, cancel_token).ConfigureAwait(false);
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
            if (header.ID.BinaryEquals(Elements.Segment)) {
              headers.Add(Element.NoData(header));
              state = ReaderState.EndOfHeader;
            }
            else if (header.ID.BinaryEquals(Elements.EBML)) {
              state = ReaderState.EBML;
              goto parse_retry;
            }
            else if (header.ID.BinaryEquals(Elements.Void) ||
                     header.ID.BinaryEquals(Elements.CRC32)) {
              var elt = await Element.ReadBodyAsync(header, stream, cancel_token).ConfigureAwait(false);
              headers.Add(elt);
            }
            else {
              throw new BadDataException();
            }
            break;
          case ReaderState.EndOfHeader:
            if (header.ID.BinaryEquals(Elements.Segment)) {
              state = ReaderState.Segment;
              goto parse_retry;
            }
            else if (header.ID.BinaryEquals(Elements.EBML)) {
              state = ReaderState.EBML;
              goto parse_retry;
            }
            else if (header.ID.BinaryEquals(Elements.Cluster)) {
              clusters.Clear();
              MemoryStream hbin;
              using (hbin=new MemoryStream()) {
                foreach (var c in headers) {
                  c.Write(hbin);
                }
              }
              headers.Clear();

              stream_index  = Channel.GenerateStreamID();
              stream_origin = DateTime.Now;
              position = 0;
              sink.OnContentHeader(
                new Content(stream_index, TimeSpan.Zero, 0, hbin.ToArray(), PCPChanPacketContinuation.None)
              );
              position += hbin.ToArray().LongLength;
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
            else if (header.ID.BinaryEquals(Elements.Info)) {
              var elt = await Element.ReadBodyAsync(header, stream, cancel_token).ConfigureAwait(false);
              var s = new MemoryStream(elt.Data);
              while (s.Position<s.Length) {
                var child = ElementHeader.Read(s);
                if (child.ID.BinaryEquals(Elements.TimecodeScale)) {
                  timecode_scale = Element.ReadUInt(s, child.Size.Value) * 1.0;
                }
                else {
                  Element.ReadBody(child, s);
                }
              }
              headers.Add(elt);
            }
            else {
              var elt = await Element.ReadBodyAsync(header, stream, cancel_token).ConfigureAwait(false);
              headers.Add(elt);
            }
            break;
          case ReaderState.Cluster:
            if (header.ID.BinaryEquals(Elements.Segment)) {
              state = ReaderState.Segment;
              goto parse_retry;
            }
            else if (header.ID.BinaryEquals(Elements.EBML)) {
              state = ReaderState.EBML;
              goto parse_retry;
            }
            else if (header.ID.BinaryEquals(Elements.Cluster)) {
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
              var elt = Element.NoData(header);
              var cluster = new Cluster();
              clusters.AddLast(cluster);
              sink.OnContent(
                new Content(stream_index, DateTime.Now-stream_origin, position, elt.ToArray(), PCPChanPacketContinuation.None)
              );
              position += elt.ByteSize;
              state = ReaderState.Timecode;
            }
            else if (header.ID.BinaryEquals(Elements.Void) ||
                     header.ID.BinaryEquals(Elements.CRC32)) {
              var elt = await Element.ReadBodyAsync(header, stream, cancel_token).ConfigureAwait(false);
              sink.OnContent(
                new Content(stream_index, DateTime.Now-stream_origin, position, elt.ToArray(), PCPChanPacketContinuation.None)
              );
              position += elt.ByteSize;
            }
            else {
              throw new BadDataException();
            }
            break;
          case ReaderState.Timecode:
            if (header.ID.BinaryEquals(Elements.Segment)) {
              state = ReaderState.Segment;
              goto parse_retry;
            }
            else if (header.ID.BinaryEquals(Elements.EBML)) {
              state = ReaderState.EBML;
              goto parse_retry;
            }
            else if (header.ID.BinaryEquals(Elements.Cluster)) {
              state = ReaderState.Cluster;
              goto parse_retry;
            }
            else if (header.ID.BinaryEquals(Elements.SimpleBlock) ||
                     header.ID.BinaryEquals(Elements.BlockGroup)) {
              state = ReaderState.Block;
              goto parse_retry;
            }
            else if (header.ID.BinaryEquals(Elements.Timecode)) {
              var elt = await Element.ReadBodyAsync(header, stream, cancel_token).ConfigureAwait(false);
              if (clusters.Last!=null) {
                clusters.Last.Value.Timecode =
                  Element.ReadUInt(new MemoryStream(elt.Data), elt.Data.Length)*(timecode_scale/1000000000.0);
                if (clusters.Last.Previous!=null) {
                  clusters.Last.Previous.Value.Timespan = clusters.Last.Value.Timecode - clusters.Last.Previous.Value.Timecode;
                }
              }
              sink.OnContent(
                new Content(stream_index, DateTime.Now-stream_origin, position, elt.ToArray(), PCPChanPacketContinuation.None)
              );
              position += elt.ByteSize;
              state = ReaderState.Block;
            }
            else {
              var elt = await Element.ReadBodyAsync(header, stream, cancel_token).ConfigureAwait(false);
              sink.OnContent(
                new Content(stream_index, DateTime.Now-stream_origin, position, elt.ToArray(), PCPChanPacketContinuation.None)
              );
              position += elt.ByteSize;
            }
            break;
          case ReaderState.Block:
            if (header.ID.BinaryEquals(Elements.Segment)) {
              state = ReaderState.Segment;
              goto parse_retry;
            }
            else if (header.ID.BinaryEquals(Elements.EBML)) {
              state = ReaderState.EBML;
              goto parse_retry;
            }
            else if (header.ID.BinaryEquals(Elements.Cluster)) {
              state = ReaderState.Cluster;
              goto parse_retry;
            }
            else if ((header.ID.BinaryEquals(Elements.SimpleBlock) ||
                      header.ID.BinaryEquals(Elements.BlockGroup)) &&
                     (clusters.Last!=null) &&
                     (clusters.Last.Value.BlockID==null ||
                      header.ID.BinaryEquals(clusters.Last.Value.BlockID))) {
              var elt = await Element.ReadBodyAsync(header, stream, cancel_token).ConfigureAwait(false);
              clusters.Last.Value.BlockSize += elt.Size.Value;
              clusters.Last.Value.BlockID    = elt.ID.Binary;
              sink.OnContent(
                new Content(stream_index, DateTime.Now-stream_origin, position, elt.ToArray(), PCPChanPacketContinuation.None)
              );
              position += elt.ByteSize;
            }
            else if (clusters.Last!=null && clusters.Last.Value.BlockID==null) {
              var elt = await Element.ReadBodyAsync(header, stream, cancel_token).ConfigureAwait(false);
              sink.OnContent(
                new Content(stream_index, DateTime.Now-stream_origin, position, elt.ToArray(), PCPChanPacketContinuation.None)
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

    public bool TryParseContentType(byte[] header, [NotNullWhen(true)] out string? content_type, [NotNullWhen(true)] out string? mime_type)
    {
      try {
        using (var stream=new MemoryStream(header)) {
          var h = ElementHeader.Read(stream);
          if (4<h.ID.Length || 8<h.Size.Length ||
              !h.ID.BinaryEquals(Elements.EBML)) {
            content_type = null;
            mime_type    = null;
            return false;
          }
          var elt = Element.ReadBody(h, stream);
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

    private MKVContentReaderFactory? factory;
    override protected void OnAttach(PeerCastApplication app)
    {
      if (factory==null) factory = new MKVContentReaderFactory();
      app.PeerCast.ContentReaderFactories.Add(factory);
    }

    override protected void OnDetach(PeerCastApplication app)
    {
      var f = Interlocked.Exchange(ref factory, null);
      if (f!=null) {
        app.PeerCast.ContentReaderFactories.Remove(f);
      }
    }
  }
}
