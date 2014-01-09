using System;
using System.Collections.Generic;
using System.IO;

namespace PeerCastStation.FLV
{
  public class AMF0Writer
    : IDisposable
  {
    public Stream BaseStream { get; private set; }
    private bool leaveOpen;
    private Dictionary<object,int> objects = new Dictionary<object,int>();
    public AMF0Writer(Stream output)
      : this(output, false)
    {
    }

    public AMF0Writer(Stream output, bool leave_open)
    {
      this.BaseStream = output;
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
      objects.Clear();
    }

    private int? ObjectIndex(object obj)
    {
      int idx;
      if (objects.TryGetValue(obj, out idx)) {
        return idx;
      }
      else {
        objects.Add(obj, objects.Count);
        return null;
      }
    }

    private void WriteUI32(int value)
    {
      var buf = BitConverter.GetBytes((uint)value);
      if (BitConverter.IsLittleEndian) Array.Reverse(buf);
      BaseStream.Write(buf, 0, 4);
    }

    private void WriteUI16(int value)
    {
      var buf = BitConverter.GetBytes((ushort)value);
      if (BitConverter.IsLittleEndian) Array.Reverse(buf);
      BaseStream.Write(buf, 0, 2);
    }

    private void WriteUI8(int value)
    {
      BaseStream.WriteByte((byte)value);
    }

    private void WriteDouble(double value)
    {
      var buf = BitConverter.GetBytes(value);
      if (BitConverter.IsLittleEndian) Array.Reverse(buf);
      BaseStream.Write(buf, 0, 8);
    }

    private void WriteStringValue(string value)
    {
      var buf = System.Text.Encoding.UTF8.GetBytes(value);
      WriteUI16(buf.Length);
      BaseStream.Write(buf, 0, buf.Length);
    }

    public void WriteMarker(AMF0Marker value)
    {
      WriteUI8((int)value);
    }

    public void WriteNumber(double value)
    {
      WriteMarker(AMF0Marker.Number);
      WriteDouble(value);
    }

    public void WriteNumber(int value)
    {
      WriteNumber((double)value);
    }

    public void WriteString(string value)
    {
      var buf = System.Text.Encoding.UTF8.GetBytes(value);
      if (buf.Length<=0xFFFF) {
        WriteMarker(AMF0Marker.String);
        WriteUI16(buf.Length);
        BaseStream.Write(buf, 0, buf.Length);
      }
      else {
        WriteMarker(AMF0Marker.LongString);
        WriteUI32(buf.Length);
        BaseStream.Write(buf, 0, buf.Length);
      }
    }

    private void WriteProperties(IDictionary<string,AMFValue> properties)
    {
      foreach (var kv in properties) {
        WriteStringValue(kv.Key);
        WriteValue(kv.Value);
      }
      WriteStringValue("");
      WriteMarker(AMF0Marker.ObjectEnd);
    }

    public void WriteReference(int value)
    {
      WriteMarker(AMF0Marker.Reference);
      WriteUI16(value);
    }

    public void WriteObject(AMFObject value)
    {
      var index = ObjectIndex(value);
      if (index.HasValue) {
        WriteReference(index.Value);
        return;
      }
      if (String.IsNullOrEmpty(value.Class.Name)) {
        WriteMarker(AMF0Marker.Object);
        WriteProperties(value.Data);
      }
      else {
        WriteMarker(AMF0Marker.TypedObject);
        WriteStringValue(value.Class.Name);
        WriteProperties(value.Data);
      }
    }

    public void WriteEcmaArray(IDictionary<string,AMFValue> value)
    {
      var index = ObjectIndex(value);
      if (index.HasValue) {
        WriteReference(index.Value);
        return;
      }
      WriteMarker(AMF0Marker.Array);
      WriteUI32(value.Count);
      WriteProperties(value);
    }

    public void WriteStrictArray(ICollection<AMFValue> value)
    {
      var index = ObjectIndex(value);
      if (index.HasValue) {
        WriteReference(index.Value);
        return;
      }
      WriteMarker(AMF0Marker.StrictArray);
      WriteUI32(value.Count);
      foreach (var ent in value) {
        WriteValue(ent);
      }
    }

    public void WriteDate(DateTime value)
    {
      var org = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local);
      var span = new TimeSpan(value.Ticks-org.Ticks);
      WriteMarker(AMF0Marker.Date);
      WriteDouble(span.TotalMilliseconds);
    }

    public void WriteBool(bool value)
    {
      WriteMarker(AMF0Marker.Boolean);
      WriteUI8(value ? 1 : 0);
    }

    public void WriteByteArray(byte[] value)
    {
      if (value.Length<=0xFFFF) {
        WriteMarker(AMF0Marker.String);
        WriteUI16(value.Length);
        BaseStream.Write(value, 0, value.Length);
      }
      else {
        WriteMarker(AMF0Marker.LongString);
        WriteUI32(value.Length);
        BaseStream.Write(value, 0, value.Length);
      }
    }

    public void WriteXML(string value)
    {
      WriteMarker(AMF0Marker.XMLDocument);
      WriteStringValue(value);
    }

    public void WriteValue(AMFValue value)
    {
      if (value==null) {
        WriteMarker(AMF0Marker.Null);
        return;
      }
      switch (value.Type) {
      case AMFValueType.Boolean:
        WriteBool((bool)value);
        break;
      case AMFValueType.ByteArray:
        WriteByteArray((byte[])value.Value);
        break;
      case AMFValueType.Date:
        WriteDate((DateTime)value);
        break;
      case AMFValueType.Double:
        WriteNumber((double)value);
        break;
      case AMFValueType.ECMAArray:
        WriteEcmaArray((IDictionary<string,AMFValue>)value.Value);
        break;
      case AMFValueType.Integer:
        WriteNumber((int)value);
        break;
      case AMFValueType.Null:
        WriteMarker(AMF0Marker.Null);
        break;
      case AMFValueType.Object:
        WriteObject((AMFObject)value);
        break;
      case AMFValueType.ObjectEnd:
        WriteMarker(AMF0Marker.ObjectEnd);
        break;
      case AMFValueType.StrictArray:
        WriteStrictArray((AMFValue[])value);
        break;
      case AMFValueType.String:
        WriteString((string)value);
        break;
      case AMFValueType.Undefined:
        WriteMarker(AMF0Marker.Undefined);
        break;
      case AMFValueType.XML:
      case AMFValueType.XMLDocument:
        WriteXML((string)value);
        break;
      default:
        throw new ArgumentException();
      }
    }

  }

}
