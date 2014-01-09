using System;
using System.IO;

namespace PeerCastStation.FLV.AMF
{
  public abstract class AMFReader
    : IDisposable
  {
    public Stream BaseStream { get; private set; }
    private bool leaveOpen;
    public AMFReader(Stream input)
      : this(input, false)
    {
    }

    public AMFReader(Stream input, bool leave_open)
    {
      this.BaseStream = input;
      this.leaveOpen = leave_open;
    }

    public void Dispose()
    {
      Close();
    }

    public void Close()
    {
      if (!leaveOpen) {
        BaseStream.Close();
      }
    }

    public abstract AMFValue ReadValue();
  }
}
