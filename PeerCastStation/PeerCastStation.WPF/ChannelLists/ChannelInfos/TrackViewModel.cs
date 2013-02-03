using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PeerCastStation.WPF.Commons;

namespace PeerCastStation.WPF.ChannelLists.ChannelInfos
{
  class TrackViewModel : ViewModelBase
  {
    private bool isReadOnly;
    public bool IsReadOnly
    {
      get { return isReadOnly; }
      set { SetProperty("IsReadOnly", ref isReadOnly, value); }
    }

    private string title = "";
    public string Title
    {
      get { return title; }
      set { SetProperty("Title", ref title, value); }
    }

    private string album = "";
    public string Album
    {
      get { return album; }
      set { SetProperty("Album", ref album, value); }
    }

    private string artist = "";
    public string Artist
    {
      get { return artist; }
      set { SetProperty("Artist", ref artist, value); }
    }

    private string genre = "";
    public string Genre
    {
      get { return genre; }
      set { SetProperty("Genre", ref genre, value); }
    }

    private string url = "";
    public string Url
    {
      get { return url; }
      set { SetProperty("Url", ref url, value); }
    }
  }
}
