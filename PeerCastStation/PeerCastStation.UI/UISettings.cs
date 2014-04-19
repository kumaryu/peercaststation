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

    public void AddBroadcastHistory(BroadcastInfo info)
    {
      if (FindBroadcastHistroryItem(info)!=null) return;
      var fav    = BroadcastHistory.Where(i =>  i.Favorite);
      var others = BroadcastHistory.Where(i => !i.Favorite);
      BroadcastHistory = fav.Concat(Enumerable.Repeat(info, 1).Concat(others.Take(19))).ToArray();
    }

    public BroadcastInfo FindBroadcastHistroryItem(BroadcastInfo info)
    {
      return BroadcastHistory.FirstOrDefault(i =>
          i.StreamType  == info.StreamType  &&
          i.StreamUrl   == info.StreamUrl   &&
          i.Bitrate     == info.Bitrate     &&
          i.ContentType == info.ContentType &&
          i.YellowPage  == info.YellowPage  &&
          i.ChannelName == info.ChannelName &&
          i.Genre       == info.Genre       &&
          i.Description == info.Description &&
          i.Comment     == info.Comment     &&
          i.ContactUrl  == info.ContactUrl  &&
          i.TrackTitle  == info.TrackTitle  &&
          i.TrackAlbum  == info.TrackAlbum  &&
          i.TrackArtist == info.TrackArtist &&
          i.TrackGenre  == info.TrackGenre  &&
          i.TrackUrl    == info.TrackUrl);
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
