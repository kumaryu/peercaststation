using System;
using System.Linq;

namespace PeerCastStation.UI
{
  [PeerCastStation.Core.PecaSettings]
  public class UISettings
  {
    private BroadcastInfo[] broadcastHistory = new BroadcastInfo[0];
    public BroadcastInfo[] BroadcastHistory {
      get { return broadcastHistory; }
      set { broadcastHistory = value; }
    }

    public UISettings()
    {
    }
  }

  [PeerCastStation.Core.PecaSettings]
  public class BroadcastInfo
  {
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
    public bool   Favorite    { get; set; }
  }
}
