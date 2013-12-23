using System;
using System.Configuration;

namespace PeerCastStation.Core
{
  public class AppSettingsReader
  {
    static public bool TryGetDate(string key, out DateTime value)
    {
      try {
        var v = System.Configuration.ConfigurationManager.AppSettings[key];
        return DateTime.TryParse(v, out value);
      }
      catch (System.Configuration.ConfigurationException) {
        value = DateTime.Today;
        return false;
      }
    }

    static public DateTime GetDate(string key, DateTime default_value)
    {
      DateTime res;
      if (TryGetDate(key, out res)) return res;
      else                          return default_value;
    }

    static public bool TryGetString(string key, out string value)
    {
      try {
        value = System.Configuration.ConfigurationManager.AppSettings[key];
        return value!=null;
      }
      catch (System.Configuration.ConfigurationException) {
        value = null;
        return false;
      }
    }

    static public string GetString(string key, string default_value)
    {
      string res;
      if (TryGetString(key, out res)) return res;
      else                            return default_value;
    }

    static public bool TryGetUri(string key, out Uri value)
    {
      try {
        var v = System.Configuration.ConfigurationManager.AppSettings[key];
        return Uri.TryCreate(v, UriKind.Absolute, out value);
      }
      catch (System.Configuration.ConfigurationException) {
        value = null;
        return false;
      }
    }

    static public Uri GetUri(string key, Uri default_value)
    {
      Uri res;
      if (TryGetUri(key, out res)) return res;
      else                         return default_value;
    }

    static public bool TryGetIPAddress(string key, out System.Net.IPAddress value)
    {
      try {
        var v = System.Configuration.ConfigurationManager.AppSettings[key];
        return System.Net.IPAddress.TryParse(v, out value);
      }
      catch (System.Configuration.ConfigurationException) {
        value = null;
        return false;
      }
    }

    static public System.Net.IPAddress GetIPAddress(string key, System.Net.IPAddress default_value)
    {
      System.Net.IPAddress res;
      if (TryGetIPAddress(key, out res)) return res;
      else                               return default_value;
    }


    static public bool TryGetInt(string key, out int value)
    {
      try {
        var v = System.Configuration.ConfigurationManager.AppSettings[key];
        return Int32.TryParse(v, out value);
      }
      catch (System.Configuration.ConfigurationException) {
        value = 0;
        return false;
      }
    }

    static public int GetInt(string key, int default_value)
    {
      int res;
      if (TryGetInt(key, out res)) return res;
      else                         return default_value;
    }

  }

}
