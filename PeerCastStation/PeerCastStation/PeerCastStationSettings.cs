using System;
using System.Collections.Generic;
using System.Linq;
using PeerCastStation.Core;

namespace PeerCastStation
{
  public class ChannelCleanerSettings
  {
    public int InactiveLimit { get; set; }

    public ChannelCleanerSettings()
    {
      this.InactiveLimit = 1800000;
    }
  }

  public class PeerCastStationSettings
  {
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
        this.EndPoint = settings.EndPoint;
        this.LocalAccepts = settings.LocalAccepts;
        this.GlobalAccepts = settings.GlobalAccepts;
      }
    }

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
    public ListenerSettings[]       Listeners        { get; set; }
    public AccessControllerSettings AccessController { get; set; }
    public YellowPageSettings[]     YellowPages      { get; set; }

    public PeerCastStationSettings()
    {
      Listeners        = new ListenerSettings[0];
      AccessController = new AccessControllerSettings();
      YellowPages      = new YellowPageSettings[0];
    }

    internal void Import(PeerCastStation.Properties.Settings settings)
    {
      this.BroadcastID = settings.BroadcastID;
      this.AccessController = new AccessControllerSettings(settings.AccessController);
      this.Listeners   = settings.Listeners.Select(s => new ListenerSettings(s)).ToArray();
      this.YellowPages = settings.YellowPages.Select(s => new YellowPageSettings(s)).ToArray();
    }
  }
}
