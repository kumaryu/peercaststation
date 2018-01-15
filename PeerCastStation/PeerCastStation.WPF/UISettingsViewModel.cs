using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using PeerCastStation.Core;

namespace PeerCastStation.WPF
{
  public class UISettingsViewModel
    : INotifyPropertyChanged
  {
    public ObservableCollection<BroadcastInfoViewModel> BroadcastHistory { get; private set; }

    private PeerCastStation.Core.PecaSettings settings;
    public UISettingsViewModel(PeerCastStation.Core.PecaSettings settings)
    {
      this.settings = settings;
      var wpf = settings.Get<WPFSettings>();
      var ui = settings.Get<PeerCastStation.UI.UISettings>();
      if (ui.BroadcastHistory.Length>0) {
        BroadcastHistory = new ObservableCollection<BroadcastInfoViewModel>(
          ui.BroadcastHistory.Select(info => new BroadcastInfoViewModel(info)));
      }
      else {
        BroadcastHistory = new ObservableCollection<BroadcastInfoViewModel>(
          wpf.BroadcastHistory.Select(info => new BroadcastInfoViewModel(info)));
      }
    }

    public void Save()
    {
      var wpf = settings.Get<WPFSettings>();
      var ui = settings.Get<PeerCastStation.UI.UISettings>();
      ui.BroadcastHistory = BroadcastHistory.Select(info => info.Save()).ToArray();
      wpf.BroadcastHistory = new BroadcastInfo[0];
      PeerCastStation.Core.PeerCastApplication.Current.SaveSettings();
    }

    public void AddBroadcastHistory(BroadcastInfoViewModel info)
    {
      if (BroadcastHistory.Any(i => i.Equals(info))) return;
      var fav    = BroadcastHistory.Where(i =>  i.Favorite);
      var others = BroadcastHistory.Where(i => !i.Favorite);
      BroadcastHistory = new ObservableCollection<BroadcastInfoViewModel>(
        fav.Concat(Enumerable.Repeat(info, 1))
           .Concat(others.Take(19))
      );
    }

    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged(string name)
    {
      if (PropertyChanged!=null) {
        PropertyChanged(this, new PropertyChangedEventArgs(name));
      }
    }
  }

  public class BroadcastInfoViewModel
    : INotifyPropertyChanged
  {
    public BroadcastInfoViewModel()
    {
    }

    public BroadcastInfoViewModel(BroadcastInfo model)
    {
      networkType = model.NetworkType;
      streamType  = model.StreamType;
      streamUrl   = model.StreamUrl;
      bitrate     = model.Bitrate;
      contentType = model.ContentType;
      yellowPage  = model.YellowPage;
      channelName = model.ChannelName;
      genre       = model.Genre;
      description = model.Description;
      comment     = model.Comment;
      contactUrl  = model.ContactUrl;
      trackTitle  = model.TrackTitle;
      trackAlbum  = model.TrackAlbum;
      trackArtist = model.TrackArtist;
      trackGenre  = model.TrackGenre;
      trackUrl    = model.TrackUrl;
    }

    public BroadcastInfoViewModel(PeerCastStation.UI.BroadcastInfo model)
    {
      networkType = model.NetworkType;
      streamType  = model.StreamType;
      streamUrl   = model.StreamUrl;
      bitrate     = model.Bitrate;
      contentType = model.ContentType;
      yellowPage  = model.YellowPage;
      channelName = model.ChannelName;
      genre       = model.Genre;
      description = model.Description;
      comment     = model.Comment;
      contactUrl  = model.ContactUrl;
      trackTitle  = model.TrackTitle;
      trackAlbum  = model.TrackAlbum;
      trackArtist = model.TrackArtist;
      trackGenre  = model.TrackGenre;
      trackUrl    = model.TrackUrl;
      favorite    = model.Favorite;
    }

    public PeerCastStation.UI.BroadcastInfo Save()
    {
      return new PeerCastStation.UI.BroadcastInfo() {
        NetworkType = this.NetworkType,
        StreamType  = this.StreamType,
        StreamUrl   = this.StreamUrl,
        Bitrate     = this.Bitrate,
        ContentType = this.ContentType,
        YellowPage  = this.YellowPage,
        ChannelName = this.ChannelName,
        Genre       = this.Genre,
        Description = this.Description,
        Comment     = this.Comment,
        ContactUrl  = this.ContactUrl,
        TrackTitle  = this.TrackTitle,
        TrackAlbum  = this.TrackAlbum,
        TrackArtist = this.TrackArtist,
        TrackGenre  = this.TrackGenre,
        TrackUrl    = this.TrackUrl,
        Favorite    = this.Favorite,
      };
    }

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

    private NetworkType networkType;
    private string streamType;
    private string streamUrl;
    private int    bitrate;
    private string contentType;
    private string yellowPage;
    private string channelName;
    private string genre;
    private string description;
    private string comment;
    private string contactUrl;
    private string trackTitle;
    private string trackAlbum;
    private string trackArtist;
    private string trackGenre;
    private string trackUrl;
    private bool   favorite;

    public NetworkType NetworkType {
      get { return networkType; }
      set { if (networkType!=value) { networkType = value; OnPropertyChanged(nameof(NetworkType)); } }
    }
    public string StreamType {
      get { return streamType; }
      set { if (streamType!=value) { streamType = value; OnPropertyChanged("StreamType"); } }
    }
    public string StreamUrl {
      get { return streamUrl; }
      set { if (streamUrl!=value) { streamUrl = value; OnPropertyChanged("StreamUrl"); } }
    }
    public int    Bitrate {
      get { return bitrate; }
      set { if (bitrate!=value) { bitrate = value; OnPropertyChanged("Bitrate"); } }
    }
    public string ContentType {
      get { return contentType; }
      set { if (contentType!=value) { contentType = value; OnPropertyChanged("ContentType"); } }
    }
    public string YellowPage {
      get { return yellowPage; }
      set { if (yellowPage!=value) { yellowPage = value; OnPropertyChanged("YellowPage"); } }
    }
    public string ChannelName {
      get { return channelName; }
      set { if (channelName!=value) { channelName = value; OnPropertyChanged("ChannelName"); } }
    }
    public string Genre {
      get { return genre; }
      set { if (genre!=value) { genre = value; OnPropertyChanged("Genre"); } }
    }
    public string Description {
      get { return description; }
      set { if (description!=value) { description = value; OnPropertyChanged("Description"); } }
    }
    public string Comment {
      get { return comment; }
      set { if (comment!=value) { comment = value; OnPropertyChanged("Comment"); } }
    }
    public string ContactUrl {
      get { return contactUrl; }
      set { if (contactUrl!=value) { contactUrl = value; OnPropertyChanged("ContactUrl"); } }
    }
    public string TrackTitle {
      get { return trackTitle; }
      set { if (trackTitle!=value) { trackTitle = value; OnPropertyChanged("TrackTitle"); } }
    }
    public string TrackAlbum {
      get { return trackAlbum; }
      set { if (trackAlbum!=value) { trackAlbum = value; OnPropertyChanged("TrackAlbum"); } }
    }
    public string TrackArtist {
      get { return trackArtist; }
      set { if (trackArtist!=value) { trackArtist = value; OnPropertyChanged("TrackArtist"); } }
    }
    public string TrackGenre {
      get { return trackGenre; }
      set { if (trackGenre!=value) { trackGenre = value; OnPropertyChanged("TrackGenre"); } }
    }
    public string TrackUrl {
      get { return trackUrl; }
      set { if (trackUrl!=value) { trackUrl = value; OnPropertyChanged("TrackUrl"); } }
    }
    public bool   Favorite {
      get { return favorite; }
      set { if (favorite!=value) { favorite = value; OnPropertyChanged("Favorite"); } }
    }

    public override bool Equals(object obj)
    {
      if (obj==null || GetType()!=obj.GetType()) return false;
      var x = (BroadcastInfoViewModel)obj;
      return NetworkType == x.NetworkType &&
             StreamUrl   == x.StreamUrl   &&
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

    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged(string name)
    {
      if (PropertyChanged!=null) PropertyChanged(this, new PropertyChangedEventArgs(name));
    }
  }

}
