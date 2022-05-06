using System;
using Newtonsoft.Json.Linq;

namespace PeerCastStation.UI
{
  public static class JTokenExtensions
  {
    static public int? AsInt(this JToken? self)
    {
      if (self==null) return null;
      try {
        return (int?)self;
      }
      catch (ArgumentException) {
        return null;
      }
    }

    static public bool? AsBool(this JToken? self)
    {
      if (self==null) return null;
      try {
        return (bool?)self;
      }
      catch (ArgumentException) {
        return null;
      }
    }

    static public string? AsString(this JToken? self)
    {
      if (self==null) return null;
      try {
        return (string?)self;
      }
      catch (ArgumentException) {
        return null;
      }
    }

    static public System.Net.IPAddress? AsIPAddress(this JToken? self)
    {
      var s = AsString(self);
      if (s!=null && System.Net.IPAddress.TryParse(s, out var addr)) {
        return addr;
      }
      else {
        return null;
      }
    }

  }
}
