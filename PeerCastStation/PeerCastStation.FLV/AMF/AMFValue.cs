using System;
using System.Collections.Generic;
using System.Linq;

namespace PeerCastStation.FLV.AMF
{
  public enum AMFValueType
  {
    Undefined,
    Null,
    Boolean,
    Integer,
    Double,
    String,
    XMLDocument,
    Date,
    ECMAArray,
    StrictArray,
    Object,
    XML,
    ByteArray,
    ObjectEnd,
  }

  public class AMFValue
  {
    public static readonly AMFValue Null = new AMFValue();
    public AMFValueType Type  { get; private set; }
    public object       Value { get; private set; }
    public AMFValue()
    {
      Type = AMFValueType.Null;
    }

    public AMFValue(AMFValueType type, object value)
    {
      Type = type;
      Value = value;
    }

    public AMFValue(AMFValue x)
    {
      this.Type  = x.Type;
      this.Value = x.Value;
    }

    public AMFValue(int value)
    {
      if (value<0 || 0x3FFFFFFF<value) {
        Type  = AMFValueType.Double;
        Value = (double)value;
      }
      else {
        Type = AMFValueType.Integer;
        Value = value;
      }
    }

    public AMFValue(long value)
    {
      if (value<0 || 0x3FFFFFFF<value) {
        Type  = AMFValueType.Double;
        Value = (double)value;
      }
      else {
        Type = AMFValueType.Integer;
        Value = (int)value;
      }
    }

    public AMFValue(double value)
    {
      Type  = AMFValueType.Double;
      Value = value;
    }

    public AMFValue(bool value)
    {
      Type = AMFValueType.Boolean;
      Value = value;
    }

    public AMFValue(string value)
    {
      if (value==null) {
        Type  = AMFValueType.Null;
        Value = null;
      }
      else {
        Type  = AMFValueType.String;
        Value = value;
      }
    }

    public AMFValue(DateTime value)
    {
      Type = AMFValueType.Date;
      Value = value;
    }

    public AMFValue(AMFObject value)
    {
      Type = AMFValueType.Object;
      Value = value;
    }

    public AMFValue(IEnumerable<AMFValue> value)
    {
      Type = AMFValueType.StrictArray;
      Value = value.ToArray();
    }

    public AMFValue(IDictionary<string,AMFValue> value)
    {
      Type = AMFValueType.ECMAArray;
      Value = value;
    }

    public AMFValue(byte[] value)
    {
      Type = AMFValueType.ByteArray;
      Value = value;
    }

    public AMFValue this[int idx] {
      get {
        if (Type==AMFValueType.StrictArray) {
          var ary = ((AMFValue[])Value);
          if (idx<0 || ary.Length<=idx) return AMFValue.Null;
          else                          return ary[idx];
        }
        else {
          return this[idx.ToString()];
        }
      }
    }

    public AMFValue this[string key] {
      get {
        switch (Type) {
        case AMFValueType.StrictArray:
          return this[Int32.Parse(key)];
        case AMFValueType.ECMAArray:
          {
            var dic = (IDictionary<string,AMFValue>)Value;
            AMFValue res;
            if (dic.TryGetValue(key, out res)) return res;
            else                               return AMFValue.Null;
          }
        case AMFValueType.Object:
          return ((AMFObject)Value)[key];
        default:
          throw new InvalidOperationException();
        }
      }
    }

    public bool ContainsKey(string key)
    {
      switch (Type) {
      case AMFValueType.ECMAArray:
        return ((IDictionary<string,AMFValue>)Value).ContainsKey(key);
      case AMFValueType.Object:
        return ((AMFObject)Value).ContainsKey(key);
      default:
        return false;
      }
    }

    public static bool IsNull(AMFValue value)
    {
      return value==null || value.Type==AMFValueType.Null;
    }

    public static explicit operator int(AMFValue value)
    {
      switch (value.Type) {
      case AMFValueType.Undefined:
      case AMFValueType.Null:
        return 0;
      case AMFValueType.Boolean:
        return ((bool)value.Value) ? 1 : 0;
      case AMFValueType.Double:
        return (int)((double)value.Value);
      case AMFValueType.Integer:
        return (int)value.Value;
      case AMFValueType.String:
        return Int32.Parse((string)value.Value);
      default:
        throw new InvalidCastException();
      }
    }

    public static explicit operator long(AMFValue value)
    {
      switch (value.Type) {
      case AMFValueType.Undefined:
      case AMFValueType.Null:
        return 0;
      case AMFValueType.Boolean:
        return ((bool)value.Value) ? 1 : 0;
      case AMFValueType.Double:
        return (long)((double)value.Value);
      case AMFValueType.Integer:
        return (long)((int)value.Value);
      case AMFValueType.String:
        return Int64.Parse((string)value.Value);
      default:
        throw new InvalidCastException();
      }
    }

    public static explicit operator bool(AMFValue value)
    {
      switch (value.Type) {
      case AMFValueType.Undefined:
      case AMFValueType.Null:
        return false;
      case AMFValueType.Boolean:
        return (bool)value.Value;
      case AMFValueType.Double:
        return ((double)value.Value)!=0;
      case AMFValueType.Integer:
        return ((int)value.Value)!=0;
      case AMFValueType.String:
        if (String.IsNullOrEmpty((string)value.Value) ||
            ((string)value.Value)=="false") {
          return false;
        }
        else {
          return true;
        }
      default:
        throw new InvalidCastException();
      }
    }

    public static explicit operator double(AMFValue value)
    {
      switch (value.Type) {
      case AMFValueType.Undefined:
      case AMFValueType.Null:
        return 0;
      case AMFValueType.Boolean:
        return ((bool)value.Value) ? 1 : 0;
      case AMFValueType.Double:
        return (double)value.Value;
      case AMFValueType.Integer:
        return (double)((int)value.Value);
      case AMFValueType.String:
        return Double.Parse((string)value.Value);
      default:
        throw new InvalidCastException();
      }
    }

    public static explicit operator string(AMFValue value)
    {
      switch (value.Type) {
      case AMFValueType.Undefined:
      case AMFValueType.Null:
        return null;
      default:
        return value.Value.ToString();
      }
    }

    public static explicit operator DateTime(AMFValue value)
    {
      switch (value.Type) {
      case AMFValueType.String:
        return DateTime.Parse((string)value.Value);
      case AMFValueType.Date:
        return (DateTime)value.Value;
      default:
        throw new InvalidCastException();
      }
    }

    public static explicit operator AMFValue[](AMFValue value)
    {
      switch (value.Type) {
      case AMFValueType.StrictArray:
        return (AMFValue[])value.Value;
      default:
        throw new InvalidCastException();
      }
    }

    public static explicit operator AMFObject(AMFValue value)
    {
      switch (value.Type) {
      case AMFValueType.Object:
        return ((AMFObject)value.Value);
      default:
        throw new InvalidCastException();
      }
    }

    public bool Equals(AMFValue obj)
    {
      if (obj.Type!=obj.Type) return false;
      switch (obj.Type) {
      case AMFValueType.Null:
      case AMFValueType.Undefined:
      case AMFValueType.ObjectEnd:
        return true;
      default:
        return this.Value.Equals(obj.Value);
      }
    }

    public override bool Equals(object obj)
    {
      if (obj.GetType()!=this.GetType()) return false;
      return Equals((AMFValue)obj);
    }

    public override int GetHashCode()
    {
      return new int[] {
        (int)this.Type,
        this.GetHashCode(),
      }.GetHashCode();
    }

  }

}
