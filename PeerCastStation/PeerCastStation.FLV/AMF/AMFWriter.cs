using System;
using System.Collections.Generic;
using System.IO;

namespace PeerCastStation.FLV.AMF
{
  public abstract class AMFWriter
    : IDisposable
  {
    public Stream BaseStream { get; private set; }
    private bool leaveOpen;
    public AMFWriter(Stream output)
      : this(output, false)
    {
    }

    public AMFWriter(Stream output, bool leave_open)
    {
      this.BaseStream = output;
      this.leaveOpen = leave_open;
    }

    public void Dispose()
    {
      Close();
    }

    public virtual void Close()
    {
      if (!leaveOpen) {
        BaseStream.Close();
      }
    }

    public abstract void WriteNull();
    public abstract void WriteNumber(double value);
    public abstract void WriteNumber(int value);
    public abstract void WriteString(string value);
    public abstract void WriteObject(AMFObject value);
    public abstract void WriteEcmaArray(IDictionary<string,AMFValue> value);
    public abstract void WriteStrictArray(ICollection<AMFValue> value);
    public abstract void WriteDate(DateTime value);
    public abstract void WriteBool(bool value);
    public abstract void WriteByteArray(byte[] value);
    public abstract void WriteXML(string value);
    public abstract void WriteXMLDocument(string value);
    public abstract void WriteValue(AMFValue value);

  }
}
