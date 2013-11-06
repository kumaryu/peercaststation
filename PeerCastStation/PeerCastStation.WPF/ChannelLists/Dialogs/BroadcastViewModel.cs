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
using System.Linq;
using System.Windows.Input;
using PeerCastStation.Core;
using PeerCastStation.WPF.ChannelLists.ChannelInfos;
using PeerCastStation.WPF.Commons;
using PeerCastStation.WPF.CoreSettings;

namespace PeerCastStation.WPF.ChannelLists.Dialogs
{
  class BroadcastViewModel : ViewModelBase
  {
    private readonly IContentReaderFactory[] contentTypes;
    public IContentReaderFactory[] ContentTypes { get { return contentTypes; } }

    private readonly YellowPageItem[] yellowPages;
    public YellowPageItem[] YellowPages { get { return yellowPages; } }

    public BroadcastInfo[] BroadcastHistory {
      get {
        var settings = PeerCastApplication.Current.Settings.Get<WPFSettings>();
        return settings.BroadcastHistory;
      }
    }

    private BroadcastInfo selectedBroadcastHistory;
    public BroadcastInfo SelectedBroadcastHistory {
      get { return selectedBroadcastHistory; }
      set {
        if (value!=null) {
          StreamUrl   = value.StreamUrl;
          Bitrate     = value.Bitrate==0 ? null : value.Bitrate.ToString();
          ContentType = contentTypes.FirstOrDefault(t => t.Name==value.ContentType);
          if (value.YellowPage!=null) {
            var yp = yellowPages.Where(y => y.YellowPageClient!=null).FirstOrDefault(y => y.YellowPageClient.Name==value.YellowPage);
            YellowPage  = yp!=null ? yp.YellowPageClient : null;
          }
          else {
            YellowPage = null;
          }
          ChannelName = value.ChannelName;
          Genre       = value.Genre;
          Description = value.Description;
          Comment     = value.Comment;
          ContactUrl  = value.ContactUrl;
          TrackTitle  = value.TrackTitle;
          TrackAlbum  = value.TrackAlbum;
          TrackArtist = value.TrackArtist;
          TrackGenre  = value.TrackGenre;
          TrackUrl    = value.TrackUrl;
          selectedBroadcastHistory = value;
        }
      }
    }

    private string streamUrl = "";
    public string StreamUrl
    {
      get { return streamUrl; }
      set
      {
        SetProperty("StreamUrl", ref streamUrl, value,
          () => start.OnCanExecuteChanged());
      }
    }

    private int? bitrate;
    public string Bitrate
    {
      get { return bitrate.HasValue ? bitrate.ToString() : "自動"; }
      set {
        int result;
        if (!String.IsNullOrEmpty(value) && Int32.TryParse(value, out result)) {
          SetProperty("Bitrate", ref bitrate, result);
        }
        else if (bitrate.HasValue) {
          SetProperty("Bitrate", ref bitrate, null);
        }
      }
    }

    private IContentReaderFactory contentType;
    public IContentReaderFactory ContentType
    {
      get { return contentType; }
      set
      {
        SetProperty("ContentType", ref contentType, value,
          () => start.OnCanExecuteChanged());
      }
    }

    private IYellowPageClient yellowPage;
    public IYellowPageClient YellowPage
    {
      get { return yellowPage; }
      set { SetProperty("YellowPage", ref yellowPage, value); }
    }

    private string channelName = "";
    public string ChannelName
    {
      get { return channelName; }
      set
      {
        SetProperty("ChannelName", ref channelName, value,
          () => start.OnCanExecuteChanged());
      }
    }

    private string genre = "";
    public string Genre
    {
      get { return genre; }
      set { SetProperty("Genre", ref genre, value); }
    }

    private string description = "";
    public string Description
    {
      get { return description; }
      set { SetProperty("Description", ref description, value); }
    }

    private string comment = "";
    public string Comment
    {
      get { return comment; }
      set { SetProperty("Comment", ref comment, value); }
    }

    private string contactUrl = "";
    public string ContactUrl
    {
      get { return contactUrl; }
      set { SetProperty("ContactUrl", ref contactUrl, value); }
    }

    private string trackTitle = "";
    public string TrackTitle
    {
      get { return trackTitle; }
      set { SetProperty("TrackTitle", ref trackTitle, value); }
    }

    private string trackAlbum = "";
    public string TrackAlbum
    {
      get { return trackAlbum; }
      set { SetProperty("TrackAlbum", ref trackAlbum, value); }
    }

    private string trackArtist = "";
    public string TrackArtist
    {
      get { return trackArtist; }
      set { SetProperty("TrackArtist", ref trackArtist, value); }
    }

    private string trackGenre = "";
    public string TrackGenre
    {
      get { return trackGenre; }
      set { SetProperty("TrackGenre", ref trackGenre, value); }
    }

    private string trackUrl = "";
    public string TrackUrl
    {
      get { return trackUrl; }
      set { SetProperty("TrackUrl", ref trackUrl, value); }
    }

    private readonly Command start;
    public ICommand Start { get { return start; } }

    private Uri StreamSource
    {
      get
      {
        Uri source;
        if (!Uri.TryCreate(streamUrl, UriKind.Absolute, out source))
        {
          return null;
        }
        return source;
      }
    }

    private PeerCast peerCast;
    public BroadcastViewModel(PeerCast peerCast)
    {
      this.peerCast = peerCast;
      contentTypes = peerCast.ContentReaderFactories.ToArray();

      yellowPages = new YellowPageItem[] { new YellowPageItem("掲載なし", null) }
        .Concat(peerCast.YellowPages.Select(yp => new YellowPageItem(yp))).ToArray();
      if (contentTypes.Length > 0) contentType = contentTypes[0];

      start = new Command(OnBroadcast,
        () => CanBroadcast(StreamSource, ContentType, channelName));
    }

    private void OnBroadcast()
    {
      var source = StreamSource;
      var contentReaderFactory = ContentType;
      if (!CanBroadcast(source, contentReaderFactory, channelName)) return;
      IYellowPageClient yellowPage = this.yellowPage;
      var channelInfo = CreateChannelInfo(this);
      var channelTrack = CreateChannelTrack(this);

      var channel_id = Utils.CreateChannelID(
        peerCast.BroadcastID,
        channelName,
        genre,
        source.ToString());
      var source_stream = peerCast.SourceStreamFactories
        .Where(sstream => (sstream.Type & SourceStreamType.Broadcast)!=0)
        .FirstOrDefault(sstream => sstream.Scheme==source.Scheme);
      var channel = peerCast.BroadcastChannel(
        yellowPage,
        channel_id,
        channelInfo,
        source,
        source_stream,
        contentReaderFactory);
      if (channel!=null) {
        channel.ChannelTrack = channelTrack;
      }

      var info = new BroadcastInfo {
        StreamUrl   = this.StreamUrl,
        Bitrate     = this.bitrate.HasValue ? this.bitrate.Value : 0,
        ContentType = this.ContentType.Name,
        YellowPage  = this.YellowPage!=null ? this.YellowPage.Name : null,
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
      };
      var settings = PeerCastApplication.Current.Settings.Get<WPFSettings>();
      if (!settings.BroadcastHistory.Any(i => i.Equals(info))) {
        settings.BroadcastHistory =
          Enumerable.Repeat(info, 1)
                    .Concat(settings.BroadcastHistory)
                    .Take(20)
                    .ToArray();
        PeerCastApplication.Current.SaveSettings();
      }
    }

    private bool CanBroadcast(Uri streamSource, IContentReaderFactory contentReaderFactory, string channelName)
    {
      return streamSource != null
        && contentReaderFactory != null
        && !String.IsNullOrEmpty(channelName);
    }

    private ChannelInfo CreateChannelInfo(BroadcastViewModel viewModel)
    {
      var info = new AtomCollection();
      if (viewModel.bitrate.HasValue) info.SetChanInfoBitrate(viewModel.bitrate.Value);
      info.SetChanInfoName(viewModel.channelName);
      info.SetChanInfoGenre(viewModel.genre);
      info.SetChanInfoDesc(viewModel.description);
      info.SetChanInfoComment(viewModel.comment);
      info.SetChanInfoURL(viewModel.contactUrl);
      return new ChannelInfo(info);
    }

    private ChannelTrack CreateChannelTrack(BroadcastViewModel viewModel)
    {
      var collection = new AtomCollection();
      collection.SetChanTrackTitle(viewModel.TrackTitle);
      collection.SetChanTrackGenre(viewModel.TrackGenre);
      collection.SetChanTrackAlbum(viewModel.TrackAlbum);
      collection.SetChanTrackCreator(viewModel.TrackArtist);
      collection.SetChanTrackURL(viewModel.TrackUrl);
      return new ChannelTrack(collection);
    }
  }
}
