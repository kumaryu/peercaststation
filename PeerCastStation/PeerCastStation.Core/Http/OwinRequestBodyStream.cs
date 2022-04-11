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

    public bool ContinueOnRead { get; set; } = true;

    public OwinContext Context { get; private set; }
    public OwinEnvironment Environment { get { return Context.Environment; } }
    public Stream BaseStream { get; private set; }

    private Stream? bodyStream = null;
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

    public OwinRequestBodyStream(OwinContext ctx, Stream baseStream)
    {
      Context = ctx;
      BaseStream = baseStream;
      Context.OnSendingHeaders.Add(state => {
        ContinueOnRead = false;
      }, null);
    }

    private async Task SendContinue(CancellationToken cancellationToken)
    {
      if (!ContinueOnRead) return;
      if (StringComparer.OrdinalIgnoreCase.Compare(Environment.GetRequestHeader("Expect", ""), "100-continue")==0) {
        var response_protocol = Environment.Get(OwinEnvironment.Owin.ResponseProtocol, Environment.Get(OwinEnvironment.Owin.RequestProtocol, "HTTP/1.0"));
        if (response_protocol!="HTTP/1.0") {
          var status_code = 100;
          var reason_phrase = HttpReasonPhrase.GetReasonPhrase(100);
          using (var writer=new StreamWriter(BaseStream, System.Text.Encoding.ASCII, 2048, true)) {
            writer.NewLine = "\r\n";
            await writer.WriteAsync(response_protocol).ConfigureAwait(false);
            await writer.WriteAsync(' ').ConfigureAwait(false);
            await writer.WriteAsync(status_code.ToString()).ConfigureAwait(false);
            await writer.WriteAsync(' ').ConfigureAwait(false);
            await writer.WriteLineAsync(reason_phrase).ConfigureAwait(false);
            await writer.WriteLineAsync().ConfigureAwait(false);
          }
        }
      }
      ContinueOnRead = false;
    }

    public override void Flush()
    {
      throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
      return ReadAsync(buffer, offset, count, CancellationToken.None).Result;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
      await SendContinue(cancellationToken);
      return await BodyStream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
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
