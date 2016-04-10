using System;
using System.IO;

namespace PeerCastStation.FLV.RTMP
{
  public class RTMPBinaryReader
    : IDisposable
  {
    private bool LeaveOpen { get; set; }
    public Stream BaseStream { get; private set; }
    public RTMPBinaryReader(Stream stream, bool leave_open)
    {
      this.BaseStream = stream;
      this.LeaveOpen = leave_open;
    }

    public RTMPBinaryReader(Stream stream)
      : this(stream, false)
    {
    }

    public RTMPBinaryReader(byte[] bytes)
      : this(new MemoryStream(bytes, false), false)
    {
    }

    public void Dispose()
    {
      if (!LeaveOpen) {
        this.BaseStream.Dispose();
      }
    }

    public void Close()
    {
      Dispose();
    }

    public byte ReadByte()
    {
      var v = BaseStream.ReadByte();
      if (v>=0) return (byte)v;
      else      throw new EndOfStreamException();
    }

    public int ReadUInt16()
    {
      var bytes = new byte[2];
      BaseStream.Read(bytes, 0, 2);
      if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
      return BitConverter.ToUInt16(bytes, 0);
    }

    public int ReadInt32()
    {
      var bytes = new byte[4];
      BaseStream.Read(bytes, 0, 4);
      if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
      return BitConverter.ToInt32(bytes, 0);
    }

    public int ReadUInt24()
    {
      var bytes = new byte[3];
      BaseStream.Read(bytes, 0, 3);
      return (bytes[0]<<16) | (bytes[1]<<8) | bytes[2];
    }

    public long ReadUInt32()
    {
      var bytes = new byte[4];
      BaseStream.Read(bytes, 0, 4);
      if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
      return BitConverter.ToUInt32(bytes, 0);
    }

    public long ReadUInt32LE()
    {
      var bytes = new byte[4];
      BaseStream.Read(bytes, 0, 4);
      if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
      return BitConverter.ToUInt32(bytes, 0);
    }

    public byte[] ReadBytes(int len)
    {
      var bytes = new byte[len];
      BaseStream.Read(bytes, 0, len);
      return bytes;
    }
  }

}
