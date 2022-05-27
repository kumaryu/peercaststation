using System;
using System.Linq;
using System.IO;
using PeerCastStation.FLV.RTMP;
using PeerCastStation.Core;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;

namespace PeerCastStation.FLV
{
  public class FLVFileHeader
  {
    public byte[] Binary     { get; private set; }
    public byte[] Signature  { get; private set; }
    public int    Version    { get; private set; }
    public bool   HasAudio   { get; private set; }
    public bool   HasVideo   { get; private set; }
    public long   DataOffset { get; private set; }
    public long   Size       { get; private set; }

    public FLVFileHeader(byte[] binary)
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

  public class FLVFileParser
  {
    private enum TagType {
      Audio  = 8,
      Video  = 9,
      Script = 18,
    }

    private struct FLVTagHeader
    {
      public byte[] Binary { get; }

      public bool Filter => (Binary[0] & 0x20)!=0;
      public TagType Type => (TagType)(Binary[0] & 0x1F);
      public int DataSize => (Binary[1]<<16) | (Binary[2]<<8) | (Binary[3]);
      public long Timestamp => (Binary[7]<<24) | (Binary[4]<<16) | (Binary[5]<<8) | (Binary[6]);
      public int StreamID => (Binary[8]<<16) | (Binary[9]<<8) | (Binary[10]);

      private FLVTagHeader(byte[] binary)
      {
        Binary = binary;
      }

      public static bool TryCreate(byte[] binary, [NotNullWhen(true)] out FLVTagHeader? header)
      {
        if (binary.Length<11) {
          header = null;
          return false;
        }
        if ((binary[0] & 0xC0)!=0) {
          header = null;
          return false;
        }
        var type = (TagType)(binary[0] & 0x1F);
        if (type==TagType.Audio || type==TagType.Video || type==TagType.Script) {
          header = new FLVTagHeader(binary);
          return true;
        }
        else {
          header = null;
          return false;
        }
      }
    }

    private class FLVTag
    {
      public FLVTagHeader Header { get; private set; }
      public byte[] Body   { get; private set; }
      public byte[] Footer { get; private set; }
      public bool Filter => Header.Filter;
      public TagType Type => Header.Type;
      public int DataSize => Header.DataSize;
      public long Timestamp => Header.Timestamp;
      public int StreamID => Header.StreamID;
      public long TagSize {
        get { return FLVFileParser.GetUInt32(this.Footer); }
      }

      private FLVTag(FLVTagHeader header, byte[] body, byte[] footer)
      {
        this.Header    = header;
        this.Body      = body;
        this.Footer    = footer;
      }

      public static bool IsValidHeader(byte[] binary)
      {
        if (binary.Length<11) {
          return false;
        }
        if ((binary[0] & 0xC0)!=0) {
          return false;
        }
        var type = (TagType)(binary[0] & 0x1F);
        return type==TagType.Audio || type==TagType.Video || type==TagType.Script;
      }

      public static bool TryReadTag(FLVFileParser owner, FLVTagHeader header, Stream stream, [NotNullWhen(true)] out FLVTag? tag)
      {
        bool eos;
        var body = owner.ReadBytes(stream, header.DataSize, out eos);
        if (eos) {
          tag = null;
          return false;
        }
        var footer = owner.ReadBytes(stream, 4, out eos);
        if (eos) {
          tag = null;
          return false;
        }
        tag = new FLVTag(header, body, footer);
        return true;
      }

      public static async Task<FLVTag> TryReadTagAsync(FLVTagHeader header, Stream stream, CancellationToken cancel_token)
      {
        var body = await stream.ReadBytesAsync(header.DataSize, cancel_token).ConfigureAwait(false);
        var footer = await stream.ReadBytesAsync(4, cancel_token).ConfigureAwait(false);
        var tagsize = FLVFileParser.GetUInt32(footer);
        return new FLVTag(header, body, footer);
      }

      public bool IsValidFooter {
        get { return this.DataSize+11==this.TagSize; }
      }

      public RTMPMessage ToRTMPMessage()
      {
        return new RTMPMessage(
          (RTMPMessageType)this.Type,
          this.Timestamp,
          this.StreamID,
          this.Body);
      }
    }

    private byte[] ReadBytes(Stream stream, int len, out bool eos)
    {
      var res = new byte[len];
      var read = stream.Read(res, 0, len);
      eos = read<len;
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

    public bool Read(Stream stream, IRTMPContentSink sink)
    {
      var processed = false;
      var eos = false;
      while (!eos) {
        retry:
        var start_pos = stream.Position;
        try {
          switch (state) {
          case ReaderState.Header:
            {
              var bin = ReadBytes(stream, 13, out eos);
              if (eos) goto error;
              var header = new FLVFileHeader(bin);
              if (header.IsValid) {
                sink.OnFLVHeader(header);
                state = ReaderState.Body;
              }
              else {
                throw new BadDataException();
              }
            }
            break;
          case ReaderState.Body:
            {
              var bin = ReadBytes(stream, 11, out eos);
              if (eos) goto error;
              var read_valid = false;
              if (FLVTagHeader.TryCreate(bin, out var header)) {
                if (FLVTag.TryReadTag(this, header.Value, stream, out var tag)) {
                  if (tag.IsValidFooter) {
                    read_valid = true;
                    switch (tag.Type) {
                    case TagType.Audio:
                      sink.OnAudio(tag.ToRTMPMessage());
                      break;
                    case TagType.Video:
                      sink.OnVideo(tag.ToRTMPMessage());
                      break;
                    case TagType.Script:
                      sink.OnData(new DataAMF0Message(tag.ToRTMPMessage()));
                      break;
                    }
                  }
                }
                else {
                  eos = true;
                  goto error;
                }
              }
              else {
                stream.Position = start_pos;
                var headerbin = ReadBytes(stream, 13, out eos);
                if (eos) goto error;
                var fileheader = new FLVFileHeader(headerbin);
                if (fileheader.IsValid) {
                  read_valid = true;
                  sink.OnFLVHeader(fileheader);
                }
              }
              if (!read_valid) {
                stream.Position = start_pos+1;
                var b = stream.ReadByte();
                while (true) {
                  if (b<0) {
                    eos = true;
                    goto error;
                  }
                  if ((b & 0xC0)==0 && ((b & 0x1F)==8 || (b & 0x1F)==9 || (b & 0x1F)==18)) {
                    break;
                  }
                  b = stream.ReadByte();
                }
                stream.Position = stream.Position-1;
                goto retry;
              }
            }
            break;
          }
          processed = true;
        }
        catch (EndOfStreamException) {
          stream.Position = start_pos;
          eos = true;
        }
        catch (BadDataException) {
          stream.Position = start_pos+1;
        }
      error:
        if (eos) {
          stream.Position = start_pos;
          eos = true;
        }
      }
      return processed;
    }

    public async Task ReadAsync(
      Stream stream,
      IRTMPContentSink sink,
      CancellationToken cancel_token)
    {
      int len = 0;
      var bin = new byte[13];
      try {
        len += await stream.ReadBytesAsync(bin, len, 13-len, cancel_token).ConfigureAwait(false);
      }
      catch (EndOfStreamException) {
        return;
      }
      var header = new FLVFileHeader(bin);
      if (!header.IsValid) throw new BadDataException();
      sink.OnFLVHeader(header);
      len = 0;

      bool eos = false;
      while (!eos) {
        try {
          len += await stream.ReadBytesAsync(bin, len, 11-len, cancel_token).ConfigureAwait(false);
          var read_valid = false;
          if (FLVTagHeader.TryCreate(bin, out var tagheader)) {
            var tag = await FLVTag.TryReadTagAsync(tagheader.Value, stream, cancel_token).ConfigureAwait(false);
            if (tag.IsValidFooter) {
              len = 0;
              read_valid = true;
              switch (tag.Type) {
              case TagType.Audio:
                sink.OnAudio(tag.ToRTMPMessage());
                break;
              case TagType.Video:
                sink.OnVideo(tag.ToRTMPMessage());
                break;
              case TagType.Script:
                sink.OnData(new DataAMF0Message(tag.ToRTMPMessage()));
                break;
              }
            }
          }
          else {
            len += await stream.ReadBytesAsync(bin, len, 13-len, cancel_token).ConfigureAwait(false);
            var fileheader = new FLVFileHeader(bin);
            if (fileheader.IsValid) {
              read_valid = true;
              sink.OnFLVHeader(fileheader);
            }
          }
          if (!read_valid) {
            int pos = 1;
            for (; pos<len; pos++) {
              var b = bin[pos];
              if ((b & 0xC0)==0 && ((b & 0x1F)==8 || (b & 0x1F)==9 || (b & 0x1F)==18)) {
                break;
              }
            }
            if (pos==len) {
              len = 0;
            }
            else {
              Array.Copy(bin, pos, bin, 0, len-pos);
              len -= pos;
            }
          }
        }
        catch (EndOfStreamException) {
          eos = true;
        }
      }

    }

  }

}
