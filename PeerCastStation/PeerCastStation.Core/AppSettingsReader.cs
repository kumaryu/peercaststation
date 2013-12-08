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
  }

}
