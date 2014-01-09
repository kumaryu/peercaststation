using System;
using System.Collections.Generic;
using System.IO;

namespace PeerCastStation.FLV.AMF
{
  public class AMF0Writer
    : AMFWriter
  {
    private Dictionary<object,int> objects = new Dictionary<object,int>();
    public AMF0Writer(Stream output)
      : this(output, false)
    {
    }

    public AMF0Writer(Stream output, bool leave_open)
      : base(output, leave_open)
    {
    }

    public override void Close()
    {
      base.Close();
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

    private void WriteMarker(AMF0Marker value)
    {
      WriteUI8((int)value);
    }

    public override void WriteNull()
    {
      WriteMarker(AMF0Marker.Null);
    }

    public override void WriteNumber(double value)
    {
      WriteMarker(AMF0Marker.Number);
      WriteDouble(value);
    }

    public override void WriteNumber(int value)
    {
      WriteNumber((double)value);
    }

    public override void WriteString(string value)
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

    private void WriteReference(int value)
    {
      WriteMarker(AMF0Marker.Reference);
      WriteUI16(value);
    }

    public override void WriteObject(AMFObject value)
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

    public override void WriteEcmaArray(IDictionary<string,AMFValue> value)
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

    public override void WriteStrictArray(ICollection<AMFValue> value)
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

    public override void WriteDate(DateTime value)
    {
      var org = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local);
      var span = new TimeSpan(value.Ticks-org.Ticks);
      WriteMarker(AMF0Marker.Date);
      WriteDouble(span.TotalMilliseconds);
    }

    public override void WriteBool(bool value)
    {
      WriteMarker(AMF0Marker.Boolean);
      WriteUI8(value ? 1 : 0);
    }

    public override void WriteByteArray(byte[] value)
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

    public override void WriteXML(string value)
    {
      WriteXMLDocument(value);
    }

    public override void WriteXMLDocument(string value)
    {
      WriteMarker(AMF0Marker.XMLDocument);
      WriteStringValue(value);
    }

    public override void WriteValue(AMFValue value)
    {
      if (value==null) {
        WriteNull();
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
        WriteNull();
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
        WriteXML((string)value);
        break;
      case AMFValueType.XMLDocument:
        WriteXML((string)value);
        break;
      default:
        throw new ArgumentException();
      }
    }

  }

}
