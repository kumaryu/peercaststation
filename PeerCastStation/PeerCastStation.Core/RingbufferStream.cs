using System;
using System.Linq;

namespace PeerCastStation.Core
{
	public class RingbufferStream
		: System.IO.Stream
	{
		private long length   = 0;
		private long readPos  = 0;
		private long writePos = 0;
		private long position = 0;
		private byte[] buffer;

		public RingbufferStream(int capacity)
		{
			buffer = new byte[capacity];
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			buffer = null;
		}

		public byte[] GetBuffer()
		{
			return buffer.Concat(buffer).Skip((int)readPos).Take((int)length).ToArray();
		}

		public int Capacity {
			get {
				if (buffer==null) throw new ObjectDisposedException("buffer");
				return buffer.Length;
			}
		}
		public override bool CanRead { get { return true; } }
		public override bool CanSeek { get { return true; } }
		public override bool CanWrite { get { return true; } }

		public override void Flush()
		{
		}

		public override long Length { get { return length; } }

		public override long Position
		{
			get { return position; }
			set { Seek(value-position, System.IO.SeekOrigin.Current); }
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			if (buffer==null) throw new ArgumentNullException("buffer");
			if (offset<0)     throw new ArgumentOutOfRangeException("offset");
			if (count<0)      throw new ArgumentOutOfRangeException("count");
			if (offset+count>buffer.Length) throw new ArgumentException();
			if (this.buffer==null) throw new ObjectDisposedException("buffer");
			if (length==0) return 0;
			count = Math.Min(count, (int)length);
			var len_firsthalf = Capacity-readPos;
			Array.Copy(this.buffer, readPos, buffer, offset, Math.Min(count, len_firsthalf));
			if (count>len_firsthalf) {
				Array.Copy(this.buffer, 0, buffer, offset+len_firsthalf, count-len_firsthalf);
			}
			readPos = (readPos+count) % Capacity;
			length -= count;
			position += count;
			return count;
		}

		public override long Seek(long offset, System.IO.SeekOrigin origin)
		{
			switch (origin) {
			case System.IO.SeekOrigin.Begin:
				Seek(-position+offset, System.IO.SeekOrigin.Current);
				break;
			case System.IO.SeekOrigin.End:
				Seek(length-position+offset, System.IO.SeekOrigin.Current);
				break;
			case System.IO.SeekOrigin.Current:
				if (offset>0) {
					offset    = Math.Min(length, offset);
					readPos   = (readPos+(int)offset) % Capacity;
					length   -= offset;
					position += offset;
				}
				if (offset<0) {
					offset = Math.Min(Capacity-length, -offset);
					readPos -= offset;
					if (readPos<0) readPos += Capacity;
					length   += offset;
					position -= offset;
				}
				break;
			}
			return position;
		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException();
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			if (buffer==null) throw new ArgumentNullException("buffer");
			if (offset<0)     throw new ArgumentOutOfRangeException("offset");
			if (count<0)      throw new ArgumentOutOfRangeException("count");
			if (offset+count>buffer.Length) throw new ArgumentException();
			if (this.buffer==null) throw new ObjectDisposedException("buffer");
			while (count>Capacity) {
				Write(buffer, offset, Capacity);
				offset += Capacity;
				count  -= Capacity;
			}
			var len_firsthalf = Capacity-writePos;
			Array.Copy(buffer, offset, this.buffer, writePos, Math.Min(count, len_firsthalf));
			if (count>len_firsthalf) {
				Array.Copy(buffer, offset+len_firsthalf, this.buffer, 0, count-len_firsthalf);
			}
			var len_free = Capacity-length;
			if (count>len_free) {
				readPos = (readPos+(count-len_free)) % Capacity;
			}
			writePos = (writePos+count) % Capacity;
			length = Math.Min(length+count, Capacity);
		}
	}
}
