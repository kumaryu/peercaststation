using System;
using System.Linq;
using System.Windows.Input;
using PeerCastStation.Core;
using PeerCastStation.WPF.Commons;
using PeerCastStation.WPF.CoreSettings;

namespace PeerCastStation.WPF.Channels
{
  class BroadcastViewModel : ViewModelBase
  {
    private readonly ContentReaderItem[] contentTypes;
    public ContentReaderItem[] ContentTypes { get { return contentTypes; } }

    private readonly YellowPageItem[] yellowPages;
    public YellowPageItem[] YellowPages { get { return yellowPages; } }

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
    public int? Bitrate
    {
      get { return bitrate; }
      set { SetProperty("Bitrate", ref bitrate, value); }
    }

    private ContentReaderItem contentType;
    public ContentReaderItem ContentType
    {
      get { return contentType; }
      set
      {
        SetProperty("ContentType", ref contentType, value,
          () => start.OnCanExecuteChanged());
      }
    }

    private YellowPageItem yellowPage;
    public YellowPageItem YellowPage
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

    private readonly TrackViewModel track = new TrackViewModel();
    public TrackViewModel Track { get { return track; } }

    private readonly Command start;
    public ICommand Start { get { return start; } }

    private IContentReaderFactory ContentReaderFactory
    {
      get { return contentType == null ? null : contentType.ContentReaderFactory; }
    }

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

    public BroadcastViewModel(PeerCast peerCast)
    {
      contentTypes = peerCast.ContentReaderFactories
        .Select(reader => new ContentReaderItem(reader)).ToArray();

      yellowPages = peerCast.YellowPages
        .Select(yp => new YellowPageItem(yp)).ToArray();
      if (contentTypes.Length > 0) contentType = contentTypes[0];

      start = new Command(() =>
        {
          var source = StreamSource;
          var contentReaderFactory = ContentReaderFactory;
          if (!CanBroadcast(source, contentReaderFactory, channelName))
          {
            return;
          }
          IYellowPageClient yellowPage = null;
          if (this.yellowPage != null) yellowPage = this.yellowPage.YellowPageClient;
          var channelInfo = CreateChannelInfo(this);
          var channelTrack = CreateChannelTrack(track);

          var channel_id = Utils.CreateChannelID(
            peerCast.BroadcastID,
            channelName,
            genre,
            source.ToString());
          var channel = peerCast.BroadcastChannel(
            yellowPage,
            channel_id,
            channelInfo,
            source,
            contentReaderFactory);
          if (channel != null)
          {
            channel.ChannelTrack = channelTrack;
          }
        },
        () => CanBroadcast(StreamSource, ContentReaderFactory, channelName));
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

    private ChannelTrack CreateChannelTrack(TrackViewModel track)
    {
      var collection = new AtomCollection();
      collection.SetChanTrackTitle(track.Title);
      collection.SetChanTrackGenre(track.Genre);
      collection.SetChanTrackAlbum(track.Album);
      collection.SetChanTrackCreator(track.Artist);
      collection.SetChanTrackURL(track.Url);
      return new ChannelTrack(collection);
    }
  }
}
