using System;
using System.Linq;
using System.IO;
using PeerCastStation.FLV.RTMP;
using PeerCastStation.Core;

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
						(Type==TagType.Audio || Type==TagType.Video || Type==TagType.Script) &&
						StreamID==0;
				}
			}

			public bool IsValidFooter {
				get { return this.DataSize+11==this.TagSize; }
			}

			public void ReadBody(Stream stream)
			{
				this.Body = owner.ReadBytes(stream, this.DataSize);
			}

			public void ReadFooter(Stream stream)
			{
				this.Footer = owner.ReadBytes(stream, 4);
			}

			public RTMPMessage ToRTMPMessage(long timestamp_offset)
			{
				return new RTMPMessage(
					(RTMPMessageType)this.Type,
					this.Timestamp + timestamp_offset,
					this.StreamID,
					this.Body);
			}
		}

		private byte[] ReadBytes(Stream stream, int len)
		{
			var res = new byte[len];
			var read = stream.Read(res, 0, len);
			if (read<len) throw new EndOfStreamException();
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
		private long timestampOrigin = -1;

		public bool Read(Stream stream, IRTMPContentSink sink)
		{
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
								timestampOrigin = -1;
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
							var bin = ReadBytes(stream, 11);
							var read_valid = false;
							var body = new FLVTag(this, bin);
							if (body.IsValidHeader) {
								body.ReadBody(stream);
								body.ReadFooter(stream);
								if (body.IsValidFooter) {
									read_valid = true;
									if (timestampOrigin<=0) {
										//timestampOrigin = body.Timestamp;
										timestampOrigin = 0;
									}
									switch (body.Type) {
									case FLVTag.TagType.Audio:
										sink.OnAudio(body.ToRTMPMessage(-timestampOrigin));
										break;
									case FLVTag.TagType.Video:
										sink.OnVideo(body.ToRTMPMessage(-timestampOrigin));
										break;
									case FLVTag.TagType.Script:
										sink.OnData(new DataAMF0Message(body.ToRTMPMessage(-timestampOrigin)));
										break;
									}
								}
							}
							else {
								stream.Position = start_pos;
								var header = new FileHeader(ReadBytes(stream, 13));
								if (header.IsValid) {
									timestampOrigin = -1;
									read_valid = true;
									sink.OnFLVHeader();
								}
							}
							if (!read_valid) throw new BadDataException();
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
			}
			return processed;
		}

	}

}
