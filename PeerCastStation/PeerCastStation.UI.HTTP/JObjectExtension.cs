using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace PeerCastStation.UI.HTTP
{
	static internal class JObjectExtension
	{
    static public void TryGetThen(this JObject self, string property, Action<int> action)
    {
      var v = self[property];
      if (v==null) return;
      switch (v.Type) {
      case JTokenType.Null:
      case JTokenType.Undefined:
        break;
      case JTokenType.String:
        if (String.IsNullOrEmpty((string)v)) {
          return;
        }
        else {
          int result;
          if (Int32.TryParse((string)v, out result)) {
            action(result);
          }
          break;
        }
      default:
        action((int)v);
        break;
      }
    }

    static public void TryGetThen(this JObject self, string property, Action<bool> action)
    {
      var v = self[property];
      if (v==null) return;
      switch (v.Type) {
      case JTokenType.Null:
      case JTokenType.Undefined:
        break;
      case JTokenType.String:
        if (String.IsNullOrEmpty((string)v)) {
          return;
        }
        else {
          bool result;
          if (Boolean.TryParse((string)v, out result)) {
            action(result);
          }
          break;
        }
      case JTokenType.Boolean:
      default:
        action((bool)v);
        break;
      }
    }

		static public void TryGetThen(this JObject self, string property, Action<string> action)
		{
			var v = self[property];
			if (v==null) return;
			switch (v.Type) {
			case JTokenType.Null:
			case JTokenType.Undefined:
				break;
			default:
				action((string)v);
				break;
			}
		}

		static public void TryGetThen(this JObject self, string property, Action<JObject> action)
		{
			var v = self[property];
			if (v!=null && v.Type==JTokenType.Object) action((JObject)v);
		}

	}
}
