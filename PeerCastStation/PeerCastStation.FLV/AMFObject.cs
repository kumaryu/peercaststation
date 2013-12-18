using System;
using System.Collections.Generic;
using System.Linq;

namespace PeerCastStation.FLV
{
  public class AMFClass
  {
    public string Name      { get; private set; }
    public bool   IsDynamic { get; private set; }
    public IEnumerable<string> Traits { get; private set; }

    public AMFClass(string name, bool is_dynamic, IEnumerable<string> traits)
    {
      this.Name = name;
      this.IsDynamic = is_dynamic;
      this.Traits = traits;
    }
  }

  public class AMFObject
  {
    public AMFClass Class { get; private set; }
    public IDictionary<string,AMFValue> Data { get; private set; }
    public AMFObject(AMFClass amfclass, IDictionary<string,AMFValue> data)
    {
      this.Class = amfclass;
      this.Data  = data;
    }

    public AMFObject(IDictionary<string,AMFValue> data)
      : this(new AMFClass(null, true, data.Keys), data)
    {
    }

    public AMFValue this[string key] {
      get {
        AMFValue res;
        if (Data.TryGetValue(key, out res)) return res;
        else                                return AMFValue.Null;
      }
    }

  }

}
