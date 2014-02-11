using System;
using System.IO;

namespace PeerCastStation.FLV.RTMP
{
  public class RTMPBinaryWriter
    : IDisposable
  {
    private bool LeaveOpen { get; set; }
    public Stream BaseStream { get; private set; }
    public RTMPBinaryWriter(Stream stream, bool leave_open)
    {
      this.BaseStream = stream;
      this.LeaveOpen = leave_open;
    }

    public RTMPBinaryWriter(Stream stream)
      : this(stream, false)
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

    public void Write(byte value)
    {
      BaseStream.WriteByte(value);
    }

    public void Write(int value)
    {
      var bytes = BitConverter.GetBytes(value);
      if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
      BaseStream.Write(bytes, 0, bytes.Length);
    }

    public void WriteUInt16(int value)
    {
      var bytes = BitConverter.GetBytes((ushort)value);
      if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
      BaseStream.Write(bytes, 0, bytes.Length);
    }

    public void WriteUInt24(int value)
    {
      BaseStream.WriteByte((byte)((value>>16)&0xFF));
      BaseStream.WriteByte((byte)((value>>8)&0xFF));
      BaseStream.WriteByte((byte)((value>>0)&0xFF));
    }

    public void WriteUInt32(long value)
    {
      var bytes = BitConverter.GetBytes((uint)value);
      if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
      BaseStream.Write(bytes, 0, bytes.Length);
    }

    public void WriteUInt32LE(long value)
    {
      var bytes = BitConverter.GetBytes((uint)value);
      if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
      BaseStream.Write(bytes, 0, bytes.Length);
    }

    public void Write(byte[] value)
    {
      BaseStream.Write(value, 0, value.Length);
    }

    public void Write(byte[] value, int offset, int length)
    {
      BaseStream.Write(value, offset, length);
    }
  }

}
