using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;

namespace PeerCastStation.Core
{
  public static class StreamExtension
  {
    public static async Task<int> ReadByteAsync(this Stream stream, CancellationToken cancel_token)
    {
      var buf = new byte[1];
      var len = await stream.ReadAsync(buf, 0, 1, cancel_token);
      if (len==0) return -1;
      else        return buf[0];
    }

    public static Task<int> ReadByteAsync(this Stream stream)
    {
      return stream.ReadByteAsync(CancellationToken.None);
    }

    public static byte[] ReadBytes(this Stream stream, int length)
    {
      var bytes = new byte[length];
      int pos = 0;
      while (pos<length) {
        var r = stream.Read(bytes, pos, length-pos);
        if (r<=0) throw new EndOfStreamException();
        pos += r;
      }
      return bytes;
    }

    public static async Task<byte[]> ReadBytesAsync(this Stream stream, int length, CancellationToken cancel_token)
    {
      var bytes = new byte[length];
      int pos = 0;
      while (pos<length) {
        cancel_token.ThrowIfCancellationRequested();
        var r = await stream.ReadAsync(bytes, pos, length-pos, cancel_token);
        if (r<=0) throw new EndOfStreamException();
        pos += r;
      }
      return bytes;
    }

    public static Task WriteByteAsync(this Stream stream, byte value, CancellationToken cancel_token)
    {
      return stream.WriteAsync(new byte[] { value }, 0, 1, cancel_token);
    }

    static public void Write(this Stream stream, Atom atom)
    {
      var name = atom.Name.GetBytes();
      stream.Write(name, 0, name.Length);
      if (atom.HasValue) {
        var value = atom.GetBytes();
        var len = BitConverter.GetBytes(value.Length);
        if (!BitConverter.IsLittleEndian) Array.Reverse(len);
        stream.Write(len, 0, len.Length);
        stream.Write(value, 0, value.Length);
      }
      else {
        var cnt = BitConverter.GetBytes(0x80000000U | (uint)atom.Children.Count);
        if (!BitConverter.IsLittleEndian) Array.Reverse(cnt);
        stream.Write(cnt, 0, cnt.Length);
        foreach (var child in atom.Children) {
          Write(stream, child);
        }
      }
    }

    static public async Task WriteAsync(this Stream stream, Atom atom, CancellationToken cancel_token)
    {
      var bufstream = new MemoryStream();
      bufstream.Write(atom);
      var buf = bufstream.ToArray();
      await stream.WriteAsync(buf, 0, buf.Length, cancel_token);
    }

    static public Task WriteAsync(this Stream stream, Atom atom)
    {
      return WriteAsync(stream, atom, CancellationToken.None);
    }

    static public Atom ReadAtom(this Stream stream)
    {
      var header = stream.ReadBytes(8);
      var name = new ID4(header, 0);
      if (!BitConverter.IsLittleEndian) Array.Reverse(header, 4, 4);
      uint len = BitConverter.ToUInt32(header, 4);
      if ((len & 0x80000000U)!=0) {
        if ((len&0x7FFFFFFF)>1024) {
          throw new InvalidDataException("Atom has too many children");
        }
        var children = new AtomCollection();
        for (var i=0; i<(len&0x7FFFFFFF); i++) {
          children.Add(stream.ReadAtom());
        }
        return new Atom(name, children);
      }
      else {
        if (len>1024*1024) {
          throw new InvalidDataException("Atom length too long");
        }
        var value = stream.ReadBytes((int)len);
        return new Atom(name, value);
      }
    }

    static public async Task<Atom> ReadAtomAsync(this Stream stream, CancellationToken cancel_token)
    {
      var header = await stream.ReadBytesAsync(8, cancel_token);
      var name = new ID4(header, 0);
      if (!BitConverter.IsLittleEndian) Array.Reverse(header, 4, 4);
      uint len = BitConverter.ToUInt32(header, 4);
      if ((len & 0x80000000U)!=0) {
        if ((len&0x7FFFFFFF)>1024) {
          throw new InvalidDataException("Atom has too many children");
        }
        var children = new AtomCollection();
        for (var i=0; i<(len&0x7FFFFFFF); i++) {
          children.Add(await stream.ReadAtomAsync(cancel_token));
        }
        return new Atom(name, children);
      }
      else {
        if (len>1024*1024) {
          throw new InvalidDataException("Atom length too long");
        }
        var value = await stream.ReadBytesAsync((int)len, cancel_token);
        return new Atom(name, value);
      }
    }

    static public Task<Atom> ReadAtomAsync(this Stream stream)
    {
      return ReadAtomAsync(stream, CancellationToken.None);
    }

    static public void WriteUTF8(this Stream stream, string value)
    {
      var bytes = System.Text.Encoding.UTF8.GetBytes(value);
      stream.Write(bytes, 0, bytes.Length);
    }

    static public async Task WriteUTF8Async(this Stream stream, string value, CancellationToken cancel_token)
    {
      var bytes = System.Text.Encoding.UTF8.GetBytes(value);
      await stream.WriteAsync(bytes, 0, bytes.Length, cancel_token);
    }

    static public Task WriteUTF8Async(this Stream stream, string value)
    {
      return WriteUTF8Async(stream, value, CancellationToken.None);
    }

  }

}
