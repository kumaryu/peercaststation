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

    private Stream bodyStream = null;
    private Stream BodyStream {
      get {
        if (bodyStream==null) {
          bodyStream = BaseStream;
          var enc = Environment.GetRequestTransferEncoding();
          if (enc.HasFlag(OwinEnvironment.TransferEncoding.Chunked)) {
            bodyStream = new ChunkedRequestStream(bodyStream, true);
          }
          if (enc.HasFlag(OwinEnvironment.TransferEncoding.Deflate)) {
            bodyStream = new System.IO.Compression.DeflateStream(bodyStream, System.IO.Compression.CompressionMode.Decompress, true);
          }
          if (enc.HasFlag(OwinEnvironment.TransferEncoding.GZip)) {
            bodyStream = new System.IO.Compression.GZipStream(bodyStream, System.IO.Compression.CompressionMode.Decompress, true);
          }
          if (enc.HasFlag(OwinEnvironment.TransferEncoding.Compress) ||
              enc.HasFlag(OwinEnvironment.TransferEncoding.Brotli) ||
              enc.HasFlag(OwinEnvironment.TransferEncoding.Exi) ||
              enc.HasFlag(OwinEnvironment.TransferEncoding.Unsupported)) {
            throw new HttpErrorException(HttpStatusCode.NotImplemented);
          }
        }
        return bodyStream;
      }
    }

    public OwinRequestBodyStream(IDictionary<string,object> env, Stream baseStream)
    {
      Environment = new OwinEnvironment(env);
      BaseStream = baseStream;
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
      return BodyStream.ReadAsync(buffer, offset, count, cancellationToken);
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
