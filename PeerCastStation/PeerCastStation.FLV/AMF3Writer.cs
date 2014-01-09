using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace PeerCastStation.FLV
{
  public class AMF3Writer
    : IDisposable
  {
    public Stream BaseStream { get; private set; }
    private bool leaveOpen;
    private Dictionary<string,int> strings = new Dictionary<string,int>();
    private Dictionary<object,int> objects = new Dictionary<object,int>();
    private Dictionary<AMFClass,int> classes = new Dictionary<AMFClass,int>();
    public AMF3Writer(Stream output)
      : this(output, false)
    {
    }

    public AMF3Writer(Stream output, bool leave_open)
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
      strings.Clear();
      objects.Clear();
      classes.Clear();
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

    private int? ClassIndex(AMFClass obj)
    {
      int idx;
      if (classes.TryGetValue(obj, out idx)) {
        return idx;
      }
      else {
        classes.Add(obj, classes.Count);
        return null;
      }
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

    private void WriteUI29(int value)
    {
      if (0x00<=value && value<=0x7F) {
        BaseStream.WriteByte((byte)value);
      }
      else if (0x80<=value && value<=0x3FFF) {
        BaseStream.WriteByte((byte)(0x80 | ((value>>7) & 0x7F)));
        BaseStream.WriteByte((byte)(value & 0x7F));
      }
      else if (0x4000<=value && value<=0x1FFFFF) {
        BaseStream.WriteByte((byte)(0x80 | ((value>>14) & 0x7F)));
        BaseStream.WriteByte((byte)(0x80 | ((value>>7) & 0x7F)));
        BaseStream.WriteByte((byte)(value & 0x7F));
      }
      else if (0x20000<=value && value<=0x1FFFFFFF) {
        BaseStream.WriteByte((byte)(0x80 | ((value>>22) & 0x7F)));
        BaseStream.WriteByte((byte)(0x80 | ((value>>15) & 0x7F)));
        BaseStream.WriteByte((byte)(0x80 | ((value>>8) & 0x7F)));
        BaseStream.WriteByte((byte)(value & 0xFF));
      }
      else {
        throw new ArgumentOutOfRangeException("value");
      }
    }

    public void WriteString(string value)
    {
      WriteMarker(AMF3Marker.String);
      WriteStringValue(value);
    }

    private void WriteStringValue(string value)
    {
      if (String.IsNullOrEmpty(value)) {
        WriteUI29(1);
        return;
      }
      int idx;
      if (strings.TryGetValue(value, out idx)) {
        WriteUI29((idx << 1) | 0);
      }
      else {
        var buf = System.Text.Encoding.UTF8.GetBytes(value);
        WriteUI29((buf.Length << 1) | 1);
        BaseStream.Write(buf, 0, buf.Length);
        strings.Add(value, strings.Count);
      }
    }

    private void WriteStringObject(string value)
    {
      if (String.IsNullOrEmpty(value)) {
        WriteUI29(1);
        return;
      }
      var idx = ObjectIndex(value);
      if (idx.HasValue) {
        WriteUI29((idx.Value << 1) | 0);
      }
      else {
        var buf = System.Text.Encoding.UTF8.GetBytes(value);
        WriteUI29((buf.Length << 1) | 1);
        BaseStream.Write(buf, 0, buf.Length);
      }
    }

    public void WriteMarker(AMF3Marker value)
    {
      WriteUI8((int)value);
    }

    public void WriteNumber(double value)
    {
      WriteMarker(AMF3Marker.Double);
      WriteDouble(value);
    }

    public void WriteNumber(int value)
    {
      if (value<0 || 0x1FFFFFFF<value) {
        WriteNumber((double)value);
      }
      else {
        WriteMarker(AMF3Marker.Integer);
        WriteUI29(value);
      }
    }

    public void WriteDate(DateTime value)
    {
      var org = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local);
      var span = new TimeSpan(value.Ticks-org.Ticks);
      WriteMarker(AMF3Marker.Date);
      var idx = ObjectIndex(span);
      if (idx.HasValue) {
        WriteUI29((idx.Value << 1) | 0);
      }
      else {
        WriteUI29(1);
        WriteDouble(span.TotalMilliseconds);
      }
    }

    public void WriteBool(bool value)
    {
      if (value) WriteMarker(AMF3Marker.True);
      else       WriteMarker(AMF3Marker.False);
    }

    public void WriteObject(AMFObject value)
    {
      WriteMarker(AMF3Marker.Object);
      var idx = ObjectIndex(value);
      if (idx.HasValue) {
        WriteUI29((idx.Value<<1) | 0);
        return;
      }
      var klass = value.Class;
      idx = ClassIndex(klass);
      if (idx.HasValue) {
        WriteUI29((idx.Value << 2) | 0x01);
      }
      else {
        WriteUI29(
          ((klass.IsDynamic ? 1 : 0) << 3) |
          (klass.Traits.Count() << 4) |
          0x03);
        WriteStringValue(klass.Name);
        foreach (var name in klass.Traits) {
          WriteStringValue(name);
        }
      }
      foreach (var name in klass.Traits) {
        WriteValue(value[name]);
      }
      if (klass.IsDynamic) {
        foreach (var name in value.Data.Keys.Where(name => !klass.Traits.Contains(name))) {
          WriteStringValue(name);
          WriteValue(value[name]);
        }
        WriteStringValue("");
      }
    }

    public void WriteEcmaArray(IDictionary<string,AMFValue> dic)
    {
      WriteMarker(AMF3Marker.Array);
      var idx = ObjectIndex(dic);
      if (idx.HasValue) {
        WriteUI29((idx.Value << 1) | 0);
        return;
      }
      WriteUI29((0<<1) | 1);
      foreach (var kv in dic) {
        WriteStringValue(kv.Key);
        WriteValue(kv.Value);
      }
      WriteStringValue("");
    }

    public void WriteStrictArray(ICollection<AMFValue> ary)
    {
      WriteMarker(AMF3Marker.Array);
      var idx = ObjectIndex(ary);
      if (idx.HasValue) {
        WriteUI29((idx.Value << 1) | 0);
        return;
      }
      WriteUI29((ary.Count<<1) | 1);
      WriteStringValue("");
      foreach (var value in ary) {
        WriteValue(value);
      }
    }

    public void WriteByteArray(byte[] value)
    {
      if (value.Length==0) {
        WriteUI29(1);
        return;
      }
      var idx = ObjectIndex(value);
      if (idx.HasValue) {
        WriteUI29((idx.Value << 1) | 0);
      }
      else {
        WriteUI29((value.Length << 1) | 1);
        BaseStream.Write(value, 0, value.Length);
      }
    }

    public void WriteXML(string value)
    {
      WriteMarker(AMF3Marker.XML);
      WriteStringValue(value);
    }

    public void WriteXMLDocument(string value)
    {
      WriteMarker(AMF3Marker.XMLDocument);
      WriteStringValue(value);
    }

    public void WriteValue(AMFValue value)
    {
      if (value==null) {
        WriteMarker(AMF3Marker.Null);
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
        WriteMarker(AMF3Marker.Null);
        break;
      case AMFValueType.Object:
        WriteObject((AMFObject)value);
        break;
      case AMFValueType.ObjectEnd:
        //ignored
        break;
      case AMFValueType.StrictArray:
        WriteStrictArray((AMFValue[])value);
        break;
      case AMFValueType.String:
        WriteString((string)value);
        break;
      case AMFValueType.Undefined:
        WriteMarker(AMF3Marker.Undefined);
        break;
      case AMFValueType.XML:
        WriteXML((string)value);
        break;
      case AMFValueType.XMLDocument:
        WriteXMLDocument((string)value);
        break;
      default:
        throw new ArgumentException();
      }
    }

  }
}
