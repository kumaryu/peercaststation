using System;
using System.Collections.Generic;
using System.IO;

namespace PeerCastStation.FLV
{
  public class AMF0Reader
    : IDisposable
  {
    public Stream BaseStream { get; private set; }
    private bool leaveOpen;
    private List<AMFValue> objects = new List<AMFValue>();
    public AMF0Reader(Stream input)
      : this(input, false)
    {
    }

    public AMF0Reader(Stream input, bool leave_open)
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

    public AMFValue ReadValue()
    {
      switch (ReadMarker()) {
      case AMF0Marker.Number:
        return new AMFValue(ReadDouble());
      case AMF0Marker.Boolean:
        return new AMFValue(ReadUI8()!=0);
      case AMF0Marker.String:
        return new AMFValue(ReadString());
      case AMF0Marker.Object:
        return RegisterObject(new AMFValue(ReadObject()));
      case AMF0Marker.MovieClip:
        return null; //Not supported
      case AMF0Marker.Null:
        return new AMFValue(AMFValueType.Null, null);
      case AMF0Marker.Undefined:
        return new AMFValue(AMFValueType.Undefined, null);
      case AMF0Marker.Reference:
        return new AMFValue(ReadReference());
      case AMF0Marker.Array:
        return RegisterObject(new AMFValue(ReadEcmaArray()));
      case AMF0Marker.ObjectEnd:
        return new AMFValue(AMFValueType.ObjectEnd, null);
      case AMF0Marker.StrictArray:
        return RegisterObject(new AMFValue(ReadStrictArray()));
      case AMF0Marker.Date:
        return new AMFValue(ReadDate());
      case AMF0Marker.LongString:
        return new AMFValue(ReadLongString());
      case AMF0Marker.Unsupported:
        return null; //Not supported
      case AMF0Marker.RecordSet:
        return null; //Not supported
      case AMF0Marker.XMLDocument:
        return new AMFValue(AMFValueType.XMLDocument, ReadLongString());
      case AMF0Marker.TypedObject:
        return RegisterObject(new AMFValue(ReadTypedObject()));
      case AMF0Marker.AVMPlusObject:
        return null; //Not supported
      default:
        throw new InvalidDataException();
      }
    }

    public AMF0Marker ReadMarker()
    {
      return (AMF0Marker)ReadUI8();
    }

    public AMFValue ReadReference()
    {
      try {
        var idx = ReadUI16();
        if (idx<=objects.Count) return objects[idx];
        else                    return AMFValue.Null;
      }
      catch (ArgumentOutOfRangeException e) {
        throw new InvalidDataException("Invalid reference", e);
      }
    }

    public string ReadString()
    {
      var len = ReadUI16();
      var buf = new byte[len];
      BaseStream.Read(buf, 0, len);
      return System.Text.Encoding.UTF8.GetString(buf);
    }

    public string ReadLongString()
    {
      var len = ReadUI32();
      var buf = new byte[len];
      var pos = 0;
      while (len>0) {
        var read = BaseStream.Read(buf, pos, (int)Math.Min(len, Int32.MaxValue));
        pos += read;
        len -= read;
      }
      return System.Text.Encoding.UTF8.GetString(buf);
    }

    public long ReadUI32()
    {
      var buf = new byte[4];
      BaseStream.Read(buf, 0, 4);
      if (BitConverter.IsLittleEndian) Array.Reverse(buf);
      return BitConverter.ToUInt32(buf, 0);
    }

    public int ReadUI16()
    {
      var buf = new byte[2];
      BaseStream.Read(buf, 0, 2);
      if (BitConverter.IsLittleEndian) Array.Reverse(buf);
      return BitConverter.ToUInt16(buf, 0);
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

    private IDictionary<string,AMFValue> ReadProperties()
    {
      var dic = new Dictionary<string,AMFValue>();
      while (true) {
        var name  = ReadString();
        var value = ReadValue();
        if (name=="" && value.Type==AMFValueType.ObjectEnd) {
          break;
        }
        else {
          dic.Add(name, value);
        }
      }
      return dic;
    }

    public AMFObject ReadObject()
    {
      return new AMFObject(ReadProperties());
    }

    public IDictionary<string,AMFValue> ReadEcmaArray()
    {
      var len = ReadUI32();
      return ReadProperties();
    }

    public AMFValue[] ReadStrictArray()
    {
      var ary = new AMFValue[ReadUI32()];
      for (int i=0; i<ary.Length; i++) {
        ary[i] = ReadValue();
      }
      return ary;
    }

    public DateTime ReadDate()
    {
      var time = ReadDouble();
      return (new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local)).AddMilliseconds(time);
    }

    public AMFObject ReadTypedObject()
    {
      var name = ReadString();
      var properties = ReadProperties();
      return new AMFObject(new AMFClass(name, false, properties.Keys), properties);
    }

  }

}
