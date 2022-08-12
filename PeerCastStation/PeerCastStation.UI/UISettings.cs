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

    public BroadcastInfo[] AddBroadcastHistory(BroadcastInfo info)
    {
      var fav = FindBroadcastHistroryItem(info)?.Favorite ?? false;
      info.Favorite = fav;
      var history = BroadcastHistory.Where(i => !i.Equals(info));
      var favorites = history.Where(i =>  i.Favorite);
      var others    = history.Where(i => !i.Favorite);
      if (fav) {
        favorites = Enumerable.Concat(Enumerable.Repeat(info, 1), favorites);
      }
      else {
        others = Enumerable.Concat(Enumerable.Repeat(info, 1), others.Take(19));
      }
      BroadcastHistory = Enumerable.Concat(favorites, others).ToArray();
      return BroadcastHistory;
    }

    public void SetFavorite(BroadcastInfo info, bool value)
    {
      var item = FindBroadcastHistroryItem(info);
      if (item!=null && item.Favorite!=value) {
        item.Favorite = value;
      }
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

    public override bool Equals(object? obj)
    {
      if (obj==null || GetType()!=obj.GetType()) return false;
      return Equals((BroadcastInfo)obj);
    }

    public bool Equals(BroadcastInfo obj)
    {
      return NetworkType == obj.NetworkType &&
             StreamUrl   == obj.StreamUrl   &&
             StreamType  == obj.StreamType  &&
             Bitrate     == obj.Bitrate     &&
             ContentType == obj.ContentType &&
             YellowPage  == obj.YellowPage  &&
             ChannelName == obj.ChannelName &&
             Genre       == obj.Genre       &&
             Description == obj.Description &&
             Comment     == obj.Comment     &&
             ContactUrl  == obj.ContactUrl  &&
             TrackTitle  == obj.TrackTitle  &&
             TrackAlbum  == obj.TrackAlbum  &&
             TrackArtist == obj.TrackArtist &&
             TrackGenre  == obj.TrackGenre  &&
             TrackUrl    == obj.TrackUrl;
    }

    public override int GetHashCode()
    {
      return (int)(new object[] {
        NetworkType,
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

