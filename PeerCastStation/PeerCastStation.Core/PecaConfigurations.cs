using System;
using System.Collections.Generic;
using System.Configuration;

namespace PeerCastStation.Core
{
  public class PecaConfigurations
    : IAppConfigurations
  {
    private Dictionary<string,string> values = new Dictionary<string, string>();

    public IEnumerable<string> Keys {
      get { return values.Keys; }
    }

    public PecaConfigurations()
    {
      foreach (var key in ConfigurationManager.AppSettings.AllKeys) {
        values.Add(key, ConfigurationManager.AppSettings[key]);
      }
    }

    public bool TryGetString(string key, out string value)
    {
      return values.TryGetValue(key, out value);
    }

    public void SetValue(string key, string value)
    {
      values[key] = value;
    }
  }

}
