﻿using System;
using PeerCastStation.Core;

namespace PeerCastStation.WPF
{
  [PeerCastStation.Core.PecaSettings]
  public enum RemoteNodeName {
    Uri,
    EndPoint,
    SessionID,
  }

  [PecaSettings]
  public enum WindowTitleMode {
    Simple,
    Version,
    ChannelStats,
  }

  [PeerCastStation.Core.PecaSettings]
  public class WPFSettings
  {
    private bool showWindowOnStartup = true;
    private bool showNotifications = true;

    public bool ShowWindowOnStartup {
      get { return showWindowOnStartup; }
      set { showWindowOnStartup = value; }
    }
    public bool ShowNotifications {
      get { return showNotifications; }
      set { showNotifications = value; }
    }

    public RemoteNodeName RemoteNodeName { get; set; } = RemoteNodeName.SessionID;
    public double WindowLeft   { get; set; }
    public double WindowTop    { get; set; }
    public double WindowWidth  { get; set; }
    public double WindowHeight { get; set; }

    private BroadcastInfo[] broadcastHistory = new BroadcastInfo[0];
    public BroadcastInfo[] BroadcastHistory {
      get { return broadcastHistory; }
      set { broadcastHistory = value; }
    }

    public WindowTitleMode WindowTitleMode { get; set; } = WindowTitleMode.Version;

    public WPFSettings()
    {
      WindowLeft   = Double.NaN;
      WindowTop    = Double.NaN;
      WindowWidth  = Double.NaN;
      WindowHeight = Double.NaN;
    }
  }

  [PeerCastStation.Core.PecaSettings]
  public class BroadcastInfo
  {
    public NetworkType NetworkType { get; set; }
    public string StreamType  { get; set; }
    public string StreamUrl   { get; set; }
    public int    Bitrate     { get; set; }
    public string ContentType { get; set; }
    public string YellowPage  { get; set; }
    public string ChannelName { get; set; }
    public string Genre       { get; set; }
    public string Description { get; set; }
    public string Comment     { get; set; }
    public string ContactUrl  { get; set; }
    public string TrackTitle  { get; set; }
    public string TrackAlbum  { get; set; }
    public string TrackArtist { get; set; }
    public string TrackGenre  { get; set; }
    public string TrackUrl    { get; set; }
  }
}
