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
    private ChannelViewModel channel;

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
      set {
        if (SetProperty("Genre", ref genre, value)) {
          IsModified = true;
        }
      }
    }

    private string description;
    public string Description
    {
      get { return description; }
      set {
        if (SetProperty("Description", ref description, value)) {
          IsModified = true;
        }
      }
    }

    private string contactUrl;
    public string ContactUrl
    {
      get { return contactUrl; }
      set {
        if (SetProperty("ContactUrl", ref contactUrl, value)) {
          IsModified = true;
        }
      }
    }

    private string comment;
    public string Comment
    {
      get { return comment; }
      set {
        if (SetProperty("Comment", ref comment, value)) {
          IsModified = true;
        }
      }
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

    private string buffer;
    public string Buffer
    {
      get { return buffer; }
      private set { SetProperty(nameof(Buffer), ref buffer, value); }
    }

    private string uptime;
    public string Uptime
    {
      get { return uptime; }
      private set { SetProperty("Uptime", ref uptime, value); }
    }

    private string trackTitle = "";
    public string TrackTitle
    {
      get { return trackTitle; }
      set {
        if (SetProperty("TrackTitle", ref trackTitle, value)) {
          IsModified = true;
        }
      }
    }

    private string trackAlbum = "";
    public string TrackAlbum
    {
      get { return trackAlbum; }
      set {
        if (SetProperty("TrackAlbum", ref trackAlbum, value)) {
          IsModified = true;
        }
      }
    }

    private string trackArtist = "";
    public string TrackArtist
    {
      get { return trackArtist; }
      set {
        if (SetProperty("TrackArtist", ref trackArtist, value)) {
          IsModified = true;
        }
      }
    }

    private string trackGenre = "";
    public string TrackGenre
    {
      get { return trackGenre; }
      set {
        if (SetProperty("TrackGenre", ref trackGenre, value)) {
          IsModified = true;
        }
      }
    }

    private string trackUrl = "";
    public string TrackUrl
    {
      get { return trackUrl; }
      set {
        if (SetProperty("TrackUrl", ref trackUrl, value)) {
          IsModified = true;
        }
      }
    }

    private bool isTracker;
    public bool IsTracker
    {
      get { return isTracker; }
      set {
        SetProperty("IsTracker", ref isTracker, value, () => {
          isModified = false;
        });
      }
    }

    private bool isModified = false;
    public bool IsModified {
      get {
        return isModified;
      }
      private set {
        if (isModified==value) return;
        isModified = value;
        update.OnCanExecuteChanged();
      }
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
          track.SetChanTrackAlbum(trackAlbum);
          track.SetChanTrackCreator(trackArtist);
          track.SetChanTrackTitle(trackTitle);
          track.SetChanTrackGenre(trackGenre);
          track.SetChanTrackURL(trackUrl);
          channel.ChannelTrack = new ChannelTrack(track);
          IsModified = false;
        },
        () => channel!=null && IsTracker && IsModified);
    }

    public void UpdateChannelInfo(ChannelViewModel channel)
    {
      this.channel = channel;
      if (channel==null) {
        ChannelName = "";
        ContentType = "";
        Bitrate     = "";
        Buffer      = "";
        Uptime      = "";
        Genre       = "";
        Description = "";
        ContactUrl  = "";
        Comment     = "";
        TrackAlbum  = "";
        TrackArtist = "";
        TrackTitle  = "";
        TrackGenre  = "";
        TrackUrl    = "";
        ChannelId   = "";
        IsModified  = false;
        IsTracker = false;
        update.OnCanExecuteChanged();
        return;
      }

      IsTracker = channel.IsBroadcasting;
      ChannelId = channel.ChannelID.ToString("N").ToUpper();
      var info = channel.ChannelInfo;
      if (info != null) {
        ChannelName = info.Name;
        ContentType = info.ContentType;
        Bitrate = String.Format("{0} kbps", info.Bitrate);
        var contents = channel.GetContents();
        var buffersBytes = contents.Sum(cc => cc.Data.Length);
        var minBufferBytes = contents.Count>0 ? contents.Min(cc => cc.Data.Length) : 0;
        var maxBufferBytes = contents.Count>0 ? contents.Max(cc => cc.Data.Length) : 0;
        var avgBufferBytes = contents.Count>0 ? buffersBytes / contents.Count : 0;
        var buffersDuration = ((contents.LastOrDefault()?.Timestamp ?? TimeSpan.Zero) - (contents.FirstOrDefault()?.Timestamp ?? TimeSpan.Zero)).TotalSeconds;
        Buffer  = $"Duration: {buffersDuration:F1}s, Count: {contents.Count} ,Total {buffersBytes/1024} KiB, min. {minBufferBytes} B, max. {maxBufferBytes} B, avg. {avgBufferBytes} B)";
        Uptime  = String.Format(
          "{0:D}:{1:D2}:{2:D2}",
          (int)channel.Uptime.TotalHours,
          channel.Uptime.Minutes,
          channel.Uptime.Seconds);
      }
      else {
        ChannelName = "";
        ContentType = "";
        Bitrate     = "";
        Buffer      = "";
        Uptime      = "";
      }
      if (IsTracker && IsModified) return;

      if (info != null) {
        Genre = info.Genre;
        Description = info.Desc;
        ContactUrl = info.URL;
        Comment = info.Comment;
      }
      else {
        Genre = "";
        Description = "";
        ContactUrl = "";
        Comment = "";
      }
      var track = channel.ChannelTrack;
      if (track != null) {
        TrackAlbum = track.Album;
        TrackArtist = track.Creator;
        TrackTitle = track.Name;
        TrackGenre = track.Genre;
        TrackUrl = track.URL;
      }
      else {
        TrackAlbum = "";
        TrackArtist = "";
        TrackTitle = "";
        TrackGenre = "";
        TrackUrl = "";
      }
      IsModified = false;

      update.OnCanExecuteChanged();
    }

  }
}
