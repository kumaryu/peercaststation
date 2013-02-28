using System;
using System.Collections.Generic;
using System.Linq;
using PeerCastStation.Core;

namespace PeerCastStation
{
  [PecaSettings]
  public class ChannelCleanerSettings
  {
    public int InactiveLimit { get; set; }

    public ChannelCleanerSettings()
    {
      this.InactiveLimit = 1800000;
    }
  }

  [PecaSettings]
  public class PeerCastStationSettings
  {
    [PecaSettings]
    public class LoggerSettings
    {
      public LogLevel           Level        { get; set; }
      public LoggerOutputTarget OutputTarget { get; set; }
      public string             LogFileName  { get; set; }

      public LoggerSettings()
      {
        this.Level = LogLevel.Warn;
        this.OutputTarget =
          LoggerOutputTarget.Debug |
          LoggerOutputTarget.Console |
          LoggerOutputTarget.UserInterface;
      }
    }

    [PecaSettings]
    public class ListenerSettings
    {
      public System.Net.IPEndPoint EndPoint { get; set; }
      public OutputStreamType LocalAccepts  { get; set; }
      public OutputStreamType GlobalAccepts { get; set; }

      public ListenerSettings()
      {
      }

      internal ListenerSettings(PeerCastStation.Properties.ListenerSettings settings)
      {
        this.EndPoint      = settings.EndPoint;
        this.LocalAccepts  = settings.LocalAccepts;
        this.GlobalAccepts = settings.GlobalAccepts;
      }
    }

    [PecaSettings]
    public class AccessControllerSettings
    {
      public int MaxRelays            { get; set; }
      public int MaxDirects           { get; set; }
      public int MaxRelaysPerChannel  { get; set; }
      public int MaxDirectsPerChannel { get; set; }
      public int MaxUpstreamRate      { get; set; }

      public AccessControllerSettings()
      {
      }

      internal AccessControllerSettings(PeerCastStation.Properties.AccessControllerSettings settings)
      {
        this.MaxRelays            = settings.MaxRelays;
        this.MaxDirects           = settings.MaxDirects;
        this.MaxRelaysPerChannel  = settings.MaxRelaysPerChannel;
        this.MaxDirectsPerChannel = settings.MaxDirectsPerChannel;
        this.MaxUpstreamRate      = settings.MaxUpstreamRate;
      }
    }

    [PecaSettings]
    public class YellowPageSettings
    {
      public string Protocol { get; set; }
      public string Name     { get; set; }
      public Uri    Uri      { get; set; }

      public YellowPageSettings()
      {
      }

      internal YellowPageSettings(PeerCastStation.Properties.YellowPageSettings settings)
      {
        this.Protocol = settings.Protocol;
        this.Name     = settings.Name;
        this.Uri      = settings.Uri;
      }
    }

    public Guid BroadcastID { get; set; }
    public LoggerSettings           Logger           { get; set; }
    public ListenerSettings[]       Listeners        { get; set; }
    public AccessControllerSettings AccessController { get; set; }
    public YellowPageSettings[]     YellowPages      { get; set; }

    public PeerCastStationSettings()
    {
      Logger           = new LoggerSettings();
      Listeners        = new ListenerSettings[0];
      AccessController = new AccessControllerSettings();
      YellowPages      = new YellowPageSettings[0];
    }

    internal void Import(PeerCastStation.Properties.Settings settings)
    {
      if (settings==null) return;
      try {
        if (settings.BroadcastID!=Guid.Empty) {
          this.BroadcastID = settings.BroadcastID;
        }
        settings.BroadcastID = Guid.NewGuid();
        if (settings.AccessController!=null) {
          this.AccessController = new AccessControllerSettings(settings.AccessController);
        }
        if (settings.Listeners!=null) {
          this.Listeners = settings.Listeners.Where(s => s!=null).Select(s => new ListenerSettings(s)).ToArray();
        }
        if (settings.YellowPages!=null) {
          this.YellowPages = settings.YellowPages.Where(s => s!=null).Select(s => new YellowPageSettings(s)).ToArray();
        }
      }
      catch (System.Configuration.ConfigurationErrorsException) {
      }
    }
  }
}
