using System;
using System.Collections.Generic;
using System.Linq;

namespace PeerCastStation.FLV.AMF
{
  public class AMFClass
  {
    public string        Name      { get; private set; }
    public bool          IsDynamic { get; private set; }
    public IList<string> Traits    { get; private set; }

    public AMFClass(string name, bool is_dynamic, IEnumerable<string> traits)
    {
      this.Name      = name;
      this.IsDynamic = is_dynamic;
      this.Traits    = new List<string>(traits);
    }

    public AMFClass(string name, bool is_dynamic)
    {
      this.Name      = name;
      this.IsDynamic = is_dynamic;
      this.Traits    = new List<string>();
    }
  }

  public class AMFObject
    : IEnumerable<KeyValuePair<string,AMFValue>>
  {
    public AMFClass Class { get; private set; }
    public IDictionary<string,AMFValue> Data { get; private set; }
    public AMFObject(AMFClass amfclass, IDictionary<string,AMFValue> data)
    {
      this.Class = amfclass;
      this.Data  = data;
    }

    public AMFObject(IDictionary<string,AMFValue> data)
      : this(new AMFClass("", true, data.Keys), data)
    {
    }

    public AMFObject()
    {
      this.Class = new AMFClass("", true);
      this.Data  = new Dictionary<string,AMFValue>();
    }

    public AMFValue this[string key] {
      get {
        if (Data.TryGetValue(key, out var res)) return res;
        else                                    return AMFValue.Null;
      }
    }

    public bool ContainsKey(string key)
    {
      return Data.ContainsKey(key);
    }

    public void Add(string key, AMFValue value)
    {
      if (!this.Class.IsDynamic)      throw new NotSupportedException("Class is not dynamic");
      if (this.Data.ContainsKey(key)) throw new ArgumentException("Same key is already exists", "key");
      this.Data.Add(key, value);
      this.Class.Traits.Add(key);
    }

    public void Add(string key, string value)
    {
      this.Add(key, new AMFValue(value));
    }

    public void Add(string key, int value)
    {
      this.Add(key, new AMFValue(value));
    }

    public void Add(string key, long value)
    {
      this.Add(key, new AMFValue(value));
    }

    public void Add(string key, double value)
    {
      this.Add(key, new AMFValue(value));
    }

    public void Add(string key, AMFObject value)
    {
      this.Add(key, new AMFValue(value));
    }

    public IEnumerator<KeyValuePair<string, AMFValue>> GetEnumerator()
    {
      return this.Data.GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
      return this.Data.GetEnumerator();
    }
  }

}
