using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace PeerCastStation.FLV
{
  public class AMF3Reader
    : IDisposable
  {
    public Stream BaseStream { get; private set; }
    private bool leaveOpen;
    private List<AMFValue> objects = new List<AMFValue>();
    private List<string> strings = new List<string>();
    private List<AMFClass> classes = new List<AMFClass>();
    public AMF3Reader(Stream input)
      : this(input, false)
    {
    }

    public AMF3Reader(Stream input, bool leave_open)
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

    private AMFValue RegisterObject(AMFValue value)
    {
      objects.Add(value);
      return value;
    }

    private AMFValue GetRegisteredObject(int idx)
    {
      try {
        return objects[idx];
      }
      catch (ArgumentOutOfRangeException e) {
        throw new InvalidDataException("Invalid object reference", e);
      }
    }

    private string RegisterString(string value)
    {
      strings.Add(value);
      return value;
    }

    private string GetRegisteredString(int idx)
    {
      try {
        return strings[idx];
      }
      catch (ArgumentOutOfRangeException e) {
        throw new InvalidDataException("Invalid string reference", e);
      }
    }

    private int ReadUI29()
    {
      int v = 0;
      for (var i=0; i<4; i++) {
        var b = BaseStream.ReadByte();
        if (i==3) {
          v = (v<<8) | b;
        }
        else {
          v = (v<<7) | (b & 0x7F);
          if ((b & 0x80)==0) break;
        }
      }
      return v;
    }

    private string ReadString()
    {
      var len = ReadUI29();
      if (len==1) return "";
      if ((len&0x01)==0) return GetRegisteredString(len>>1);
      len = len>>1;
      var buf = new byte[len];
      BaseStream.Read(buf, 0, len);
      return RegisterString(System.Text.Encoding.UTF8.GetString(buf));
    }

    private AMFValue ReadStringObject(AMFValueType type)
    {
      var len = ReadUI29();
      if (len==1) return new AMFValue("");
      if ((len&0x01)==0) return GetRegisteredObject(len>>1);
      len = len>>1;
      var buf = new byte[len];
      BaseStream.Read(buf, 0, len);
      return RegisterObject(new AMFValue(type, System.Text.Encoding.UTF8.GetString(buf)));
    }

    public AMFValue ReadByteArray()
    {
      var len = ReadUI29();
      if (len==1) return new AMFValue(new byte[0]);
      if ((len&0x01)==0) return GetRegisteredObject(len>>1);
      len = len>>1;
      var buf = new byte[len];
      BaseStream.Read(buf, 0, len);
      return RegisterObject(new AMFValue(buf));
    }

    public AMF3Marker ReadMarker()
    {
      return (AMF3Marker)ReadUI8();
    }

    public AMFValue ReadValue()
    {
      var marker = ReadMarker();
      switch (marker) {
      case AMF3Marker.Undefined:
        return new AMFValue(AMFValueType.Undefined, null);
      case AMF3Marker.Null:
        return new AMFValue(AMFValueType.Null, null);
      case AMF3Marker.False:
        return new AMFValue(false);
      case AMF3Marker.True:
        return new AMFValue(true);
      case AMF3Marker.Integer:
        return new AMFValue(ReadUI29());
      case AMF3Marker.Double:
        return new AMFValue(ReadDouble());
      case AMF3Marker.String:
        return new AMFValue(ReadString());
      case AMF3Marker.XMLDocument:
        return ReadStringObject(AMFValueType.XMLDocument);
      case AMF3Marker.Date:
        return ReadDate();
      case AMF3Marker.Array:
        return ReadArray();
      case AMF3Marker.Object:
        return ReadObject();
      case AMF3Marker.XML:
        return ReadStringObject(AMFValueType.XML);
      case AMF3Marker.ByteArray:
        return ReadByteArray();
      default:
        throw new InvalidDataException();
      }
    }

    public int ReadUI8()
    {
      var b = BaseStream.ReadByte();
      if (b<0) throw new EndOfStreamException();
      return b;
    }

    public double ReadDouble()
    {
      var buf = new byte[8];
      BaseStream.Read(buf, 0, 8);
      if (BitConverter.IsLittleEndian) Array.Reverse(buf);
      return BitConverter.ToDouble(buf, 0);
    }

    public AMFValue ReadObject()
    {
      var idx = ReadUI29();
      if ((idx & 0x01)==0) return GetRegisteredObject(idx>>1);
      AMFClass klass;
      if (((idx>>1) & 0x01)==0) klass = classes[idx>>2];
      else {
        if (((idx>>2) & 0x01)==1) {
          var class_name = ReadString();
          throw new NotImplementedException();
        }
        else {
          var is_dynamic = ((idx>>3) & 0x01)==1;
          var cnt = idx>>4;
          var class_name = ReadString();
          klass = new AMFClass(class_name, is_dynamic, Enumerable.Range(0, cnt).Select(i => ReadString()).ToArray());
          classes.Add(klass);
        }
      }
      var dic = new Dictionary<string,AMFValue>();
      foreach (var trait in klass.Traits) {
        dic[trait] = ReadValue();
      }
      if (klass.IsDynamic) {
        var name = ReadString();
        while (!String.IsNullOrEmpty(name)) {
          dic[name] = ReadValue();
          name = ReadString();
        }
      }
      return RegisterObject(new AMFValue(new AMFObject(klass, dic)));
    }

    public AMFValue ReadDate()
    {
      var idx = ReadUI29();
      if ((idx & 0x01)==0) return GetRegisteredObject(idx>>1);
      var time = ReadDouble();
      return RegisterObject(new AMFValue(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local).AddMilliseconds(time)));
    }

    public AMFValue ReadArray()
    {
      var idx = ReadUI29();
      if ((idx & 0x01)==0) return GetRegisteredObject(idx>>1);
      var cnt = idx>>1;
      var name = ReadString();
      if (String.IsNullOrEmpty(name)) {
        return RegisterObject(new AMFValue(Enumerable.Range(0, cnt).Select(i => ReadValue()).ToArray()));
      }
      else {
        var dic = new Dictionary<string,AMFValue>();
        while (!String.IsNullOrEmpty(name)) {
          var value = ReadValue();
          dic.Add(name, value);
          name  = ReadString();
        }
        for (var i=0; i<cnt; i++) {
          dic.Add(i.ToString(), ReadValue());
        }
        return new AMFValue(dic);
      }
    }

  }

}
