﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using PeerCastStation.Core;

namespace PeerCastStation.HTTP
{
  public class HTTPChunkedContentStream
    : Stream
  {
    public Stream BaseStream { get; private set; }
    public override bool CanRead {
      get { return BaseStream.CanRead; }
    }

    public override bool CanSeek {
      get { return false; }
    }

    public override bool CanWrite {
      get { return BaseStream.CanWrite; }
    }

    public override long Length {
      get { throw new NotSupportedException(); }
    }

    public override long Position {
      get { throw new NotSupportedException(); }
      set { throw new NotSupportedException(); }
    }

    public override void Flush()
    {
      this.BaseStream.Flush();
    }

    private bool leaveOpen;

    public HTTPChunkedContentStream(Stream base_stream, bool leave_open)
    {
      this.BaseStream = base_stream;
      this.leaveOpen = leave_open;
    }

    public HTTPChunkedContentStream(Stream base_stream)
      : this(base_stream, false)
    {
    }

    protected override void Dispose(bool disposing)
    {
      base.Dispose(disposing);
      if (!disposing || leaveOpen) return;
      this.BaseStream.Dispose();
    }

    private int currentChunkSize = 0;
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
      var bytes = new List<byte>();
      while (currentChunkSize==0) {
        var b = await BaseStream.ReadByteAsync(cancellationToken);
        if (b<0) throw new IOException();
        if (b=='\r') {
          b = await BaseStream.ReadByteAsync(cancellationToken);
          if (b<0) throw new IOException();
          if (b=='\n') {
            var line = System.Text.Encoding.ASCII.GetString(bytes.ToArray());
            bytes.Clear();
            if (line!="") {
              int len;
              if (Int32.TryParse(
                  line.Split(';')[0],
                  System.Globalization.NumberStyles.AllowHexSpecifier,
                  System.Globalization.CultureInfo.InvariantCulture.NumberFormat,
                  out len)) {
                if (len==0) {
                  return 0;
                }
                else {
                  currentChunkSize = len;
                }
              }
              else {
                throw new HTTPError(HttpStatusCode.BadRequest);
              }
            }
          }
          else {
            bytes.Add((byte)'\r');
            bytes.Add((byte)b);
          }
        }
        else {
          bytes.Add((byte)b);
        }
      }
      if (currentChunkSize>0) {
        int len = await BaseStream.ReadAsync(buffer, offset, Math.Min(count, currentChunkSize), cancellationToken);
        if (len>=0) {
          offset += len;
          count  -= len;
          currentChunkSize -= len;
        }
        if (currentChunkSize==0) {
          await BaseStream.ReadByteAsync(cancellationToken); //\r
          await BaseStream.ReadByteAsync(cancellationToken); //\n
        }
        return len;
      }
      else {
        return 0;
      }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
      var bytes = new List<byte>();
      while (currentChunkSize==0) {
        var b = BaseStream.ReadByte();
        if (b<0) return 0;
        if (b=='\r') {
          b = BaseStream.ReadByte();
          if (b<0) return 0;
          if (b=='\n') {
            var line = System.Text.Encoding.ASCII.GetString(bytes.ToArray());
            bytes.Clear();
            if (line!="") {
              int len;
              if (Int32.TryParse(
                  line.Split(';')[0],
                  System.Globalization.NumberStyles.AllowHexSpecifier,
                  System.Globalization.CultureInfo.InvariantCulture.NumberFormat,
                  out len)) {
                if (len==0) {
                  return 0;
                }
                else {
                  currentChunkSize = len;
                }
              }
              else {
                throw new HTTPError(HttpStatusCode.BadRequest);
              }
            }
          }
          else {
            bytes.Add((byte)'\r');
            bytes.Add((byte)b);
          }
        }
        else {
          bytes.Add((byte)b);
        }
      }
      if (currentChunkSize>0) {
        int len = BaseStream.Read(buffer, offset, Math.Min(count, currentChunkSize));
        if (len>=0) {
          offset += len;
          count  -= len;
          currentChunkSize -= len;
        }
        if (currentChunkSize==0) {
          BaseStream.ReadByte(); //\r
          BaseStream.ReadByte(); //\n
        }
        return len;
      }
      else {
        return 0;
      }
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
      throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
      throw new NotSupportedException();
    }

    private static readonly byte[] CRLF = { (byte)'\r', (byte)'\n' };
    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
      MemoryStream buf;
      using (buf=new MemoryStream()) {
        var header = System.Text.Encoding.ASCII.GetBytes(count.ToString("X"));
        buf.Write(header, 0, header.Length);
        buf.Write(CRLF,   0, CRLF.Length);
        buf.Write(buffer, offset, count);
        buf.Write(CRLF,   0, CRLF.Length);
      }
      var ary = buf.ToArray();
      await BaseStream.WriteAsync(ary, 0, ary.Length, cancellationToken);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
      MemoryStream buf;
      using (buf=new MemoryStream()) {
        var header = System.Text.Encoding.ASCII.GetBytes(count.ToString("X"));
        buf.Write(header, 0, header.Length);
        buf.Write(CRLF,   0, CRLF.Length);
        buf.Write(buffer, offset, count);
        buf.Write(CRLF,   0, CRLF.Length);
      }
      var ary = buf.ToArray();
      BaseStream.Write(ary, 0, ary.Length);
    }
  }

}
