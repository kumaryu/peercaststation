﻿using System;
using PeerCastStation.Core;

namespace PeerCastStation.App
{
  [PecaSettings("PeerCastStation.ChannelCleanerSettings")]
  public class ChannelCleanerSettings
  {
    public int InactiveLimit  { get; set; }
    public ChannelCleaner.CleanupMode Mode { get; set; }

    public ChannelCleanerSettings()
    {
      this.InactiveLimit = 1800000;
      this.Mode          = ChannelCleaner.CleanupMode.Disconnected;
    }
  }

  [PecaSettings("PeerCastStation.PeerCastStationSettings")]
  public class PeerCastStationSettings
  {
    public class LoggerSettings
    {
      public LogLevel           Level { get; set; } = LogLevel.Warn;
      public LoggerOutputTarget OutputTarget { get; set; } = LoggerOutputTarget.Debug | LoggerOutputTarget.Console | LoggerOutputTarget.UserInterface;
      public string             LogFileName { get; set; } = "";
    }

    public class ListenerSettings
    {
      public System.Net.IPEndPoint? EndPoint     { get; set; }
      public OutputStreamType LocalAccepts       { get; set; }
      public bool             LocalAuthRequired  { get; set; }
      public OutputStreamType GlobalAccepts      { get; set; }
      public bool             GlobalAuthRequired { get; set; }
      public string           AuthId             { get; set; }
      public string           AuthPassword       { get; set; }

      public ListenerSettings()
      {
        var newkey = AuthenticationKey.Generate();
        this.AuthId       = newkey.Id;
        this.AuthPassword = newkey.Password;
      }
    }

    public class AccessControllerSettings
    {
      public int MaxRelays                          { get; set; }
      public int MaxDirects                         { get; set; }
      public int MaxRelaysPerChannel                { get; set; }
      public int MaxRelaysPerBroadcastChannel       { get; set; }
      public int MaxDirectsPerChannel               { get; set; }
      public int MaxDirectsPerBroadcastChannel      { get; set; }
      public int MaxUpstreamRate                    { get; set; }
      public int MaxUpstreamRateIPv6                { get; set; }
      public int MaxUpstreamRatePerChannel          { get; set; }
      public int MaxUpstreamRatePerBroadcastChannel { get; set; }

      public AccessControllerSettings()
      {
        MaxRelaysPerBroadcastChannel       = 0;
        MaxDirectsPerBroadcastChannel      = 0;
        MaxUpstreamRatePerBroadcastChannel = 0;
      }
    }

    public class YellowPageSettings
    {
      public string? Protocol { get; set; }
      public string? Name     { get; set; }
      public Uri?    Uri      { get; set; }
      public Uri?    ChannelsUri { get; set; }

      public YellowPageSettings()
      {
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
  }
}
