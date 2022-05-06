using System;
using Newtonsoft.Json.Linq;

namespace PeerCastStation.UI
{
  static public class JObjectExtensions
  {
    static public int? GetValueAsInt(this JObject self, string property)
    {
      return JTokenExtensions.AsInt(self[property]);
    }

    static public void TryGetThen(this JObject self, string property, Action<int> action)
    {
      var v = GetValueAsInt(self, property);
      if (v.HasValue) {
        action(v.Value);
      }
    }

    static public bool? GetValueAsBool(this JObject self, string property)
    {
      return JTokenExtensions.AsBool(self[property]);
    }
    static public void TryGetThen(this JObject self, string property, Action<bool> action)
    {
      var v = GetValueAsBool(self, property);
      if (v.HasValue) {
        action(v.Value);
      }
    }

    static public string? GetValueAsString(this JObject self, string property)
    {
      return JTokenExtensions.AsString(self[property]);
    }

    static public void TryGetThen(this JObject self, string property, Action<string> action)
    {
      var v = GetValueAsString(self, property);
      if (v!=null) {
        action(v);
      }
    }

    static public JObject? GetValueAsObject(this JObject self, string property)
    {
      return self[property] as JObject;
    }

    static public void TryGetThen(this JObject self, string property, Action<JObject> action)
    {
      var v = GetValueAsObject(self, property);
      if (v!=null) {
        action(v);
      }
    }

    static public System.Net.IPAddress? GetValueAsIPAddess(this JObject self, string property)
    {
      return JTokenExtensions.AsIPAddress(self[property]);
    }

    static public JArray? GetValueAsArray(this JObject self, string property)
    {
      return self[property] as JArray;
    }


  }
}
