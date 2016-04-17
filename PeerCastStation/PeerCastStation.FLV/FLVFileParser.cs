using System;
using System.Linq;
using System.IO;
using PeerCastStation.FLV.RTMP;
using PeerCastStation.Core;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.FLV
{
	class FLVFileParser
	{
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
				get { return FLVFileParser.GetUInt32(this.Footer); }
			}
			public byte[] Binary {
				get { return Header.Concat(Body).Concat(Footer).ToArray(); }
			}

			private FLVFileParser owner;
			public FLVTag(FLVFileParser owner, byte[] binary)
			{
				this.owner     = owner;
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
						(Type==TagType.Audio || Type==TagType.Video || Type==TagType.Script);
				}
			}

			public bool IsValidFooter {
				get { return this.DataSize+11==this.TagSize; }
			}

			public bool ReadBody(Stream stream)
			{
				bool eos;
				this.Body = owner.ReadBytes(stream, this.DataSize, out eos);
				return !eos;
			}

			public bool ReadFooter(Stream stream)
			{
				bool eos;
				this.Footer = owner.ReadBytes(stream, 4, out eos);
				return !eos;
			}

      public async Task<bool> ReadTagBodyAsync(Stream stream, CancellationToken cancel_token)
      {
        this.Body   = await stream.ReadBytesAsync(this.DataSize, cancel_token);
        this.Footer = await stream.ReadBytesAsync(4, cancel_token);
        return IsValidFooter;
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
							var header = new FileHeader(bin);
							if (header.IsValid) {
								sink.OnFLVHeader();
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
							var body = new FLVTag(this, bin);
							if (body.IsValidHeader) {
								if (!body.ReadBody(stream)) { eos = true; goto error; }
								if (!body.ReadFooter(stream)) {  eos = true; goto error; }
								if (body.IsValidFooter) {
									read_valid = true;
									switch (body.Type) {
									case FLVTag.TagType.Audio:
										sink.OnAudio(body.ToRTMPMessage());
										break;
									case FLVTag.TagType.Video:
										sink.OnVideo(body.ToRTMPMessage());
										break;
									case FLVTag.TagType.Script:
										sink.OnData(new DataAMF0Message(body.ToRTMPMessage()));
										break;
									}
								}
							}
							else {
								stream.Position = start_pos;
								var headerbin = ReadBytes(stream, 13, out eos);
								if (eos) goto error;
								var header = new FileHeader(headerbin);
								if (header.IsValid) {
									read_valid = true;
									sink.OnFLVHeader();
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
      len += await stream.ReadBytesAsync(bin, len, 13-len, cancel_token);
      var header = new FileHeader(bin);
      if (!header.IsValid) throw new BadDataException();
      sink.OnFLVHeader();
      len = 0;

      bool eos = false;
      while (!eos) {
        len += await stream.ReadBytesAsync(bin, len, 11-len, cancel_token);
        var read_valid = false;
        var body = new FLVTag(this, bin);
        if (body.IsValidHeader) {
          if (await body.ReadTagBodyAsync(stream, cancel_token)) {
            len = 0;
            read_valid = true;
            switch (body.Type) {
            case FLVTag.TagType.Audio:
              sink.OnAudio(body.ToRTMPMessage());
              break;
            case FLVTag.TagType.Video:
              sink.OnVideo(body.ToRTMPMessage());
              break;
            case FLVTag.TagType.Script:
              sink.OnData(new DataAMF0Message(body.ToRTMPMessage()));
              break;
            }
          }
        }
        else {
          len += await stream.ReadBytesAsync(bin, len, 13-len, cancel_token);
          var new_header = new FileHeader(bin);
          if (new_header.IsValid) {
            read_valid = true;
            sink.OnFLVHeader();
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

    }

	}

}
