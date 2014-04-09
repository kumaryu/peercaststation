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
    public string Name {
      get {
        return String.Format(
          "{0} {1} {2} - {3} Playing: {4}",
          ChannelName,
          Genre,
          Description,
          Comment,
          TrackTitle);
      }
    }

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

    public override bool Equals(object obj)
    {
      if (obj==null || GetType()!=obj.GetType()) return false;
      var x = (BroadcastInfo)obj;
      return StreamUrl   == x.StreamUrl   &&
             StreamType  == x.StreamType  &&
             Bitrate     == x.Bitrate     &&
             ContentType == x.ContentType &&
             YellowPage  == x.YellowPage  &&
             ChannelName == x.ChannelName &&
             Genre       == x.Genre       &&
             Description == x.Description &&
             Comment     == x.Comment     &&
             ContactUrl  == x.ContactUrl  &&
             TrackTitle  == x.TrackTitle  &&
             TrackAlbum  == x.TrackAlbum  &&
             TrackArtist == x.TrackArtist &&
             TrackGenre  == x.TrackGenre  &&
             TrackUrl    == x.TrackUrl;
    }

    public override int GetHashCode()
    {
      return (int)(new object[] {
        StreamType,
        StreamUrl,
        Bitrate,
        ContentType,
        YellowPage,
        ChannelName,
        Genre,
        Description,
        Comment,
        ContactUrl,
        TrackTitle,
        TrackAlbum,
        TrackArtist,
        TrackGenre,
        TrackUrl,
      }.Select(o => (long)(o==null ? 0 : o.GetHashCode()))
       .Sum() % Int32.MaxValue);
    }
  }
}
