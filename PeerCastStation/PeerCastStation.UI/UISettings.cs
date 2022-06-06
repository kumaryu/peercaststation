using System;
using System.Collections.Generic;
using System.Linq;

namespace PeerCastStation.UI
{
  [PeerCastStation.Core.PecaSettings]
  public enum PlayProtocol {
    Unknown = 0,
    HTTP,
    MSWMSP,
    HLS,
    RTMP,
  }

  [PeerCastStation.Core.PecaSettings]
  public class UISettings
  {
    private BroadcastInfo[] broadcastHistory = new BroadcastInfo[0];
    public BroadcastInfo[] BroadcastHistory {
      get { return broadcastHistory; }
      set { broadcastHistory = value; }
    }

    public Dictionary<string, PlayProtocol> DefaultPlayProtocols { get; set; } = new Dictionary<string, PlayProtocol>();

    public Dictionary<string, Dictionary<string, string>> UserConfig { get; set; } = new Dictionary<string, Dictionary<string, string>>();

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

    public BroadcastInfo? FindBroadcastHistroryItem(BroadcastInfo info)
    {
      return BroadcastHistory.FirstOrDefault(i =>
          i.NetworkType == info.NetworkType &&
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
    public PeerCastStation.Core.NetworkType NetworkType { get; set; } = Core.NetworkType.IPv4;
    public string StreamType  { get; set; } = "";
    public string StreamUrl   { get; set; } = "";
    public int    Bitrate     { get; set; } = 0;
    public string ContentType { get; set; } = "";
    public string YellowPage  { get; set; } = "";
    public string ChannelName { get; set; } = "";
    public string Genre       { get; set; } = "";
    public string Description { get; set; } = "";
    public string Comment     { get; set; } = "";
    public string ContactUrl  { get; set; } = "";
    public string TrackTitle  { get; set; } = "";
    public string TrackAlbum  { get; set; } = "";
    public string TrackArtist { get; set; } = "";
    public string TrackGenre  { get; set; } = "";
    public string TrackUrl    { get; set; } = "";
    public bool   Favorite    { get; set; } = false;

    public BroadcastInfo()
    {
    }

    public BroadcastInfo(
      PeerCastStation.Core.NetworkType networkType,
      string streamType,
      string streamUrl,
      int    bitrate,
      string contentType,
      string yellowPage,
      string channelName,
      string genre,
      string description,
      string comment,
      string contactUrl,
      string trackTitle,
      string trackAlbum,
      string trackArtist,
      string trackGenre,
      string trackUrl,
      bool   favorite)
    {
      NetworkType = networkType;
      StreamType = streamType;
      StreamUrl = streamUrl;
      Bitrate = bitrate;
      ContentType = contentType;
      YellowPage = yellowPage;
      ChannelName = channelName;
      Genre = genre;
      Description = description;
      Comment = comment;
      ContactUrl = contactUrl;
      TrackTitle = trackTitle;
      TrackAlbum = trackAlbum;
      TrackArtist = trackArtist;
      TrackGenre = trackGenre;
      TrackUrl = trackUrl;
      Favorite = favorite;
    }
  }

}

