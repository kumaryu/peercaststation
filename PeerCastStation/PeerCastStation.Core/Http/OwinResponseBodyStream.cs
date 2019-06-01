using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.Core.Http
{
  public abstract class ResponseStream
    : Stream
  {
    public abstract Task CompleteAsync(CancellationToken cancellationToken);
  }

  public class ResponseStreamWrapper
    : ResponseStream
  {
    public Stream BaseStream { get; private set; }
    public override bool CanRead { get { return false; } }
    public override bool CanSeek { get { return false; } }
    public override bool CanWrite { get { return true; } }
    public override bool CanTimeout {
      get { return BaseStream.CanTimeout; }
    }

    public override int ReadTimeout {
      get { throw new NotSupportedException(); }
      set { throw new NotSupportedException(); }
    }

    public override int WriteTimeout {
      get { return BaseStream.WriteTimeout; }
      set { BaseStream.WriteTimeout = value; }
    }

    public override long Length {
      get { throw new NotSupportedException(); }
    }

    public override long Position {
      get { throw new NotSupportedException(); }
      set { throw new NotSupportedException(); }
    }

    public ResponseStreamWrapper(Stream baseStream)
    {
      BaseStream = baseStream;
    }

    public override Task CompleteAsync(CancellationToken cancellationToken)
    {
      return BaseStream.FlushAsync(cancellationToken);
    }

    public override void Flush()
    {
      BaseStream.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
      return BaseStream.FlushAsync(cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
      if (disposing) {
        BaseStream.Dispose();
      }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
      throw new NotSupportedException();
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
      BaseStream.Write(buffer, offset, count);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
      return BaseStream.WriteAsync(buffer, offset, count, cancellationToken);
    }
  }

  internal class OwinResponseBodyStream
    : ResponseStream
  {
    public OwinContext Context { get; private set; }
    public OwinEnvironment Environment {
      get { return Context.Environment; }
    }
    public Stream BaseStream { get; private set; }
    public ResponseStream BodyStream { get; private set; }
    public bool Submitted { get; private set; } = false;

    public override bool CanRead {
      get { return false; }
    }

    public override bool CanSeek {
      get { return false; }
    }

    public override bool CanWrite {
      get { return true; }
    }

    public override bool CanTimeout {
      get { return BaseStream.CanTimeout; }
    }

    public override long Length {
      get { throw new NotSupportedException(); }
    }

    public override long Position {
      get { throw new NotSupportedException(); }
      set { throw new NotSupportedException(); }
    }

    public OwinResponseBodyStream(OwinContext ctx, Stream baseStream)
    {
      Context = ctx;
      BaseStream = baseStream;
      BodyStream = null;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
      throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
      throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
      throw new NotSupportedException();
    }

    private class DeferredResponseStream
      : ResponseStream
    {
      public override bool CanRead { get { return false; } }
      public override bool CanSeek { get { return false; } }

      public override bool CanWrite { get { return false; } }
      public override long Length {
        get { throw new NotSupportedException(); }
      }

      public override long Position {
        get { throw new NotSupportedException(); }
        set { throw new NotSupportedException(); }
      }

      public OwinResponseBodyStream Owner { get; private set; }
      public ResponseStream BaseStream { get; private set; }
      private MemoryStream bufferStream = new MemoryStream();

      public DeferredResponseStream(OwinResponseBodyStream owner, ResponseStream baseStream)
      {
        Owner = owner;
        BaseStream = baseStream;
      }

      public override int Read(byte[] buffer, int offset, int count)
      {
        throw new NotSupportedException();
      }

      public override long Seek(long offset, SeekOrigin origin)
      {
        throw new NotSupportedException();
      }

      public override void SetLength(long value)
      {
        throw new NotSupportedException();
      }

      public override void Flush()
      {
        FlushAsync(CancellationToken.None).Wait();
      }

      public override async Task FlushAsync(CancellationToken cancellationToken)
      {
        await bufferStream.FlushAsync(cancellationToken).ConfigureAwait(false);
      }

      public override void Write(byte[] buffer, int offset, int count)
      {
        bufferStream.Write(buffer, offset, count);
      }

      public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
      {
        return bufferStream.WriteAsync(buffer, offset, count, cancellationToken);
      }

      public override async Task CompleteAsync(CancellationToken cancellationToken)
      {
        await bufferStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        Owner.Environment.SetResponseHeader("Content-Length", bufferStream.Length.ToString());
        await Owner.SendResponseHeaderAsync(cancellationToken).ConfigureAwait(false);
        bufferStream.Seek(0, SeekOrigin.Begin);
        await bufferStream.CopyToAsync(BaseStream, 2048, cancellationToken).ConfigureAwait(false);
        await BaseStream.CompleteAsync(cancellationToken).ConfigureAwait(false);
      }
    }

    private async Task SendResponseHeaderAsync(CancellationToken cancellationToken)
    {
      Context.OnSendingHeaders.Invoke();
      var response_protocol = Environment.Get(OwinEnvironment.Owin.ResponseProtocol, Environment.Get(OwinEnvironment.Owin.RequestProtocol, "HTTP/1.0"));
      var status_code = Environment.Get(OwinEnvironment.Owin.ResponseStatusCode, 200);
      var reason_phrase = Environment.Get(OwinEnvironment.Owin.ResponseReasonPhrase, HttpReasonPhrase.GetReasonPhrase(status_code));
      Environment.SetResponseHeaderOptional("Date", () => DateTimeOffset.Now.ToString("R"));
      using (var writer=new StreamWriter(BaseStream, System.Text.Encoding.ASCII, 2048, true)) {
        writer.NewLine = "\r\n";
        await writer.WriteAsync(response_protocol).ConfigureAwait(false);
        await writer.WriteAsync(' ').ConfigureAwait(false);
        await writer.WriteAsync(status_code.ToString()).ConfigureAwait(false);
        await writer.WriteAsync(' ').ConfigureAwait(false);
        await writer.WriteLineAsync(reason_phrase).ConfigureAwait(false);
        if (Environment.TryGetValue<IDictionary<string,string[]>>(OwinEnvironment.Owin.ResponseHeaders, out var headers)) {
          foreach (var ent in headers) {
            await writer.WriteAsync(ent.Key).ConfigureAwait(false);
            await writer.WriteAsync(':').ConfigureAwait(false);
            if (ent.Value.Length>0) {
              await writer.WriteAsync(ent.Value[0]);
              for (var i=1; i<ent.Value.Length; i++) {
                await writer.WriteAsync(',').ConfigureAwait(false);
                await writer.WriteAsync(ent.Value[i]).ConfigureAwait(false);
              }
            }
            await writer.WriteLineAsync().ConfigureAwait(false);
          }
        }
        await writer.WriteLineAsync().ConfigureAwait(false);
      }
      Submitted = true;
    }

    private async Task<ResponseStream> GetBodyStreamAsync(CancellationToken cancellationToken)
    {
      if (BodyStream!=null) return BodyStream;
      var basestrm = Environment.GetRequestMethod()=="HEAD" ? Stream.Null : BaseStream;
      var encoding = Environment.GetResponseTransferEncoding();
      if (encoding.HasFlag(OwinEnvironment.TransferEncoding.Deflate)) {
        basestrm = new System.IO.Compression.DeflateStream(basestrm, System.IO.Compression.CompressionMode.Compress, true);
      }
      if (encoding.HasFlag(OwinEnvironment.TransferEncoding.GZip)) {
        basestrm = new System.IO.Compression.GZipStream(basestrm, System.IO.Compression.CompressionMode.Compress, true);
      }
      if (encoding.HasFlag(OwinEnvironment.TransferEncoding.Compress) ||
          encoding.HasFlag(OwinEnvironment.TransferEncoding.Brotli) ||
          encoding.HasFlag(OwinEnvironment.TransferEncoding.Exi) ||
          encoding.HasFlag(OwinEnvironment.TransferEncoding.Unsupported)) {
        throw new HttpErrorException(HttpStatusCode.NotImplemented);
      }

      ResponseStream strm = new ResponseStreamWrapper(basestrm);
      if (encoding.HasFlag(OwinEnvironment.TransferEncoding.Chunked)) {
        await SendResponseHeaderAsync(cancellationToken).ConfigureAwait(false);
        strm = new ChunkedResponseStream(strm);
      }
      else if (!Environment.IsKeepAlive() || Environment.ResponseHeaderContainsKey("Content-Length")) {
        await SendResponseHeaderAsync(cancellationToken).ConfigureAwait(false);
      }
      else {
        strm = new DeferredResponseStream(this, strm);
      }
      BodyStream = strm;
      return BodyStream;
    }

    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
      var strm = await GetBodyStreamAsync(cancellationToken).ConfigureAwait(false);
      await strm.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public override void Flush()
    {
      FlushAsync(CancellationToken.None).Wait();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
      var strm = GetBodyStreamAsync(CancellationToken.None).Result;
      strm.Write(buffer, offset, count);
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
      var strm = await GetBodyStreamAsync(cancellationToken).ConfigureAwait(false);
      await strm.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
    }

    public override async Task CompleteAsync(CancellationToken cancellationToken)
    {
      var strm = await GetBodyStreamAsync(cancellationToken).ConfigureAwait(false);
      await strm.CompleteAsync(cancellationToken).ConfigureAwait(false);
    }

  }

}

