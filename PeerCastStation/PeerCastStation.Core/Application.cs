using System;
using System.Collections.Generic;

namespace PeerCastStation.Core
{
  public interface IAppConfigurations
  {
    bool TryGetString(string key, out string value);
  }

  public static class AppConfigurationsExtension
  {
    public static bool TryGetDate(this IAppConfigurations config, string key, out DateTime value)
    {
      if (config.TryGetString(key, out string v) &&
          DateTime.TryParse(v, System.Globalization.DateTimeFormatInfo.InvariantInfo, System.Globalization.DateTimeStyles.None, out value)) {
        return true;
      }
      else {
        value = DateTime.Today;
        return false;
      }
    }

    public static DateTime GetDate(this IAppConfigurations config, string key, DateTime default_value)
    {
      if (TryGetDate(config, key, out var res)) return res;
      else return default_value;
    }

    public static string GetString(this IAppConfigurations config, string key, string default_value)
    {
      if (config.TryGetString(key, out var res)) return res;
      else return default_value;
    }

    public static bool TryGetUri(this IAppConfigurations config, string key, out Uri value)
    {
      if (config. TryGetString(key, out var v) &&
          Uri.TryCreate(v, UriKind.Absolute, out value)) {
        return true;
      }
      else {
        value = null;
        return false;
      }
    }

    public static Uri GetUri(this IAppConfigurations config, string key, Uri default_value)
    {
      if (TryGetUri(config, key, out var res)) return res;
      else return default_value;
    }

    public static bool TryGetIPAddress(this IAppConfigurations config, string key, out System.Net.IPAddress value)
    {
      if (config.TryGetString(key, out var v) &&
          System.Net.IPAddress.TryParse(v, out value)) {
        return true;
      }
      else {
        value = null;
        return false;
      }
    }

    public static System.Net.IPAddress GetIPAddress(this IAppConfigurations config, string key, System.Net.IPAddress default_value)
    {
      if (config.TryGetIPAddress(key, out var res)) return res;
      else return default_value;
    }

    public static bool TryGetInt(this IAppConfigurations config, string key, out int value)
    {
      if (config.TryGetString(key, out var v) &&
          Int32.TryParse(v, out value)) {
        return true;
      }
      else {
        value = 0;
        return false;
      }
    }

    public static int GetInt(this IAppConfigurations config, string key, int default_value)
    {
      if (TryGetInt(config, key, out var res)) return res;
      else return default_value;
    }
  }

  public abstract class PeerCastApplication
  {
    public static PeerCastApplication Current { get; set; }
    public abstract IAppConfigurations Configurations { get; }
    public abstract PecaSettings Settings { get; }
    public abstract IEnumerable<IPlugin> Plugins { get; }
    public abstract PeerCast PeerCast { get; }
    public abstract string BasePath { get; }
    public abstract void Stop(int exit_code);
    public void Stop()
    {
      Stop(0);
    }

    public abstract void SaveSettings();
    public PeerCastApplication()
    {
      if (Current==null) Current = this;
    }
  }
}
