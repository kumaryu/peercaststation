using System;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.Core.Http
{
  internal class OwinRequestBodyStream
    : Stream
  {
    public override bool CanRead {
      get { return true; }
    }

    public override bool CanSeek {
      get { return false; }
    }

    public override bool CanWrite {
      get { return false; }
    }

    public override long Length {
      get { throw new NotSupportedException(); }
    }

    public override long Position {
      get { throw new NotSupportedException(); }
      set { throw new NotSupportedException(); }
    }

    public OwinEnvironment Environment { get; private set; }
    public Stream BaseStream { get; private set; }
    public OwinEnvironment.TransferEncoding Encoding { get; private set; }

    public OwinRequestBodyStream(IDictionary<string,object> env, Stream baseStream)
    {
      Environment = new OwinEnvironment(env);
      BaseStream = baseStream;
      Encoding = Environment.GetRequestTransferEncoding();
      if (Encoding.HasFlag(OwinEnvironment.TransferEncoding.Chunked)) {
        BaseStream = new ChunkedContentStream(BaseStream, true);
      }
      if (Encoding.HasFlag(OwinEnvironment.TransferEncoding.Deflate)) {
        BaseStream = new System.IO.Compression.DeflateStream(BaseStream, System.IO.Compression.CompressionMode.Decompress, true);
      }
      if (Encoding.HasFlag(OwinEnvironment.TransferEncoding.GZip)) {
        BaseStream = new System.IO.Compression.GZipStream(BaseStream, System.IO.Compression.CompressionMode.Decompress, true);
      }
      if (Encoding.HasFlag(OwinEnvironment.TransferEncoding.Compress) ||
          Encoding.HasFlag(OwinEnvironment.TransferEncoding.Brotli) ||
          Encoding.HasFlag(OwinEnvironment.TransferEncoding.Exi) ||
          Encoding.HasFlag(OwinEnvironment.TransferEncoding.Unsupported)) {
        throw new HttpErrorException(HttpStatusCode.NotImplemented);
      }
    }

    public override void Flush()
    {
      throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
      return ReadAsync(buffer, offset, count, CancellationToken.None).Result;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
      return base.ReadAsync(buffer, offset, count, cancellationToken);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
      throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
      throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
      throw new NotSupportedException();
    }
  }
}
