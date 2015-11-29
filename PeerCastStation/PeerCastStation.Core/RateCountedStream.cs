using System;
using System.IO;

namespace PeerCastStation.Core
{
	public class RateCountedStream
		: Stream
	{
		public RateCountedStream(Stream base_stream, TimeSpan duration)
		{
			this.BaseStream = base_stream;
			this.readCounter = new RateCounter((int)duration.TotalMilliseconds);
			this.writeCounter = new RateCounter((int)duration.TotalMilliseconds);
		}

    protected override void Dispose(bool disposing)
    {
      base.Dispose(disposing);
      if (disposing) {
        this.BaseStream.Dispose();
      }
    }

		private RateCounter readCounter;
		private RateCounter writeCounter;

		public double ReadRate { get { return readCounter.Rate; } }
		public double WriteRate { get { return writeCounter.Rate; } }
		public Stream BaseStream { get; private set; }

		public override bool CanRead {
			get { return BaseStream.CanRead; }
		}

		public override bool CanSeek {
			get { return BaseStream.CanSeek; }
		}

		public override bool CanWrite {
			get { return BaseStream.CanWrite; }
		}

		public override void Flush()
		{
			BaseStream.Flush();
		}

		public override long Length {
			get { return BaseStream.Length; }
		}

		public override long Position {
			get { return BaseStream.Position; }
			set { BaseStream.Position = value; }
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			var result = BaseStream.Read(buffer, offset, count);
			if (result>0) readCounter.Add(result);
			return result;
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			return BaseStream.Seek(offset, origin);
		}

		public override void SetLength(long value)
		{
			BaseStream.SetLength(value);
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			BaseStream.Write(buffer, offset, count);
			writeCounter.Add(count);
		}
	}
}
