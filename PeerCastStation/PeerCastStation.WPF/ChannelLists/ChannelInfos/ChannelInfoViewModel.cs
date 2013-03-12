// PeerCastStation, a P2P streaming servent.
// Copyright (C) 2013 PROGRE (djyayutto@gmail.com)
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PeerCastStation.Core;
using PeerCastStation.WPF.Commons;

namespace PeerCastStation.WPF.ChannelLists.ChannelInfos
{
  class ChannelInfoViewModel : ViewModelBase
  {
    private Channel channel;

    private string channelName;
    public string ChannelName
    {
      get { return channelName; }
      private set { SetProperty("ChannelName", ref channelName, value); }
    }

    private string genre;
    public string Genre
    {
      get { return genre; }
      set { SetProperty("Genre", ref genre, value); }
    }

    private string description;
    public string Description
    {
      get { return description; }
      set { SetProperty("Description", ref description, value); }
    }

    private string contactUrl;
    public string ContactUrl
    {
      get { return contactUrl; }
      set { SetProperty("ContactUrl", ref contactUrl, value); }
    }

    private string comment;
    public string Comment
    {
      get { return comment; }
      set { SetProperty("Comment", ref comment, value); }
    }

    private string channelId;
    public string ChannelId
    {
      get { return channelId; }
      private set { SetProperty("ChannelId", ref channelId, value); }
    }

    private string contentType;
    public string ContentType
    {
      get { return contentType; }
      private set { SetProperty("ContentType", ref contentType, value); }
    }

    private string bitrate;
    public string Bitrate
    {
      get { return bitrate; }
      private set { SetProperty("Bitrate", ref bitrate, value); }
    }

    private readonly TrackViewModel track = new TrackViewModel();
    public TrackViewModel Track
    {
      get { return track; }
    }

    private bool isTracker;
    public bool IsTracker
    {
      get { return isTracker; }
      set { SetProperty("IsTracker", ref isTracker, value); }
    }

    private readonly Command update;
    public Command Update { get { return update; } }

    public ChannelInfoViewModel()
    {
      update = new Command(() =>
        {
          var info = new AtomCollection(channel.ChannelInfo.Extra);
          info.SetChanInfoGenre(genre);
          info.SetChanInfoDesc(description);
          info.SetChanInfoURL(contactUrl);
          info.SetChanInfoComment(comment);
          channel.ChannelInfo = new ChannelInfo(info);

          var track = new AtomCollection(channel.ChannelTrack.Extra);
          track.SetChanTrackAlbum(this.track.Album);
          track.SetChanTrackCreator(this.track.Artist);
          track.SetChanTrackTitle(this.track.Title);
          track.SetChanTrackGenre(this.track.Genre);
          track.SetChanTrackURL(this.track.Url);
          channel.ChannelTrack = new ChannelTrack(track);
        },
        () => channel != null && IsTracker);
    }

    public void From(Channel channel, bool isTracker)
    {
      this.channel = channel;
      IsTracker = isTracker;
      this.track.IsReadOnly = !isTracker;

      ChannelId = channel.ChannelID.ToString("N").ToUpper();
      var info = channel.ChannelInfo;
      if (info != null)
      {
        ChannelName = info.Name;
        Genre = info.Genre;
        Description = info.Desc;
        ContactUrl = info.URL;
        Comment = info.Comment;
        ContentType = info.ContentType;
        Bitrate = String.Format("{0} kbps", info.Bitrate);
      }
      else
      {
        ChannelName = "";
        Genre = "";
        Description = "";
        ContactUrl = "";
        Comment = "";
        ContentType = "";
        Bitrate = "";
      }
      var track = channel.ChannelTrack;
      if (track != null)
      {
        this.track.Album = track.Album;
        this.track.Artist = track.Creator;
        this.track.Title = track.Name;
        this.track.Genre = track.Genre;
        this.track.Url = track.URL;
      }
      else
      {
        this.track.Album = "";
        this.track.Artist = "";
        this.track.Title = "";
        this.track.Genre = "";
        this.track.Url = "";
      }

      update.OnCanExecuteChanged();
    }
  }
}
