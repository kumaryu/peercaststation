using System;
using System.Linq;
using System.Windows.Forms;
using PeerCastStation.Core;

namespace PeerCastStation.GUI
{
  public partial class BroadcastDialog : Form
  {
    public Uri  StreamSource { get; set; }
    public IContentReaderFactory ContentReaderFactory { get; set; }
    public IYellowPageClient YellowPage { get; set; }
    public ChannelInfo  ChannelInfo { get; set; }
    public ChannelTrack ChannelTrack { get; set; }

    private class ContentReaderItem
    {
      public IContentReaderFactory ContentReaderFactory { get; private set; }
      public ContentReaderItem(IContentReaderFactory reader)
      {
        ContentReaderFactory = reader;
      }

      public override string ToString()
      {
        return ContentReaderFactory.Name;
      }
    }

    private class YellowPageItem
    {
      public IYellowPageClient YellowPage { get; private set; }
      public YellowPageItem(IYellowPageClient yp)
      {
        YellowPage = yp;
      }

      public override string ToString()
      {
        return YellowPage.Name;
      }
    }

    private PeerCast peerCast;
    public BroadcastDialog(PeerCast peercast)
    {
      peerCast = peercast;
      InitializeComponent();
      bcContentType.Items.AddRange(peerCast.ContentReaderFactories.Select(reader => new ContentReaderItem(reader)).ToArray());
      bcYP.Items.AddRange(peerCast.YellowPages.Select(yp => new YellowPageItem(yp)).ToArray());
      if (bcContentType.Items.Count>0) bcContentType.SelectedIndex = 0;
      if (bcYP.Items.Count>0) bcYP.SelectedIndex = 0;
    }

    private void BroadcastStart_Click(object sender, EventArgs args)
    {
      Uri source;
      if (Uri.TryCreate(bcStreamUrl.Text, UriKind.Absolute, out source)) {
        StreamSource = source;
      }
      else {
        StreamSource = null;
      }
      var reader = bcContentType.SelectedItem as ContentReaderItem;
      if (reader!=null) ContentReaderFactory = reader.ContentReaderFactory;
      var yp = bcYP.SelectedItem as YellowPageItem;
      if (yp!=null) YellowPage = yp.YellowPage;
      var info = new AtomCollection();
      int bitrate;
      if (Int32.TryParse(bcBitrate.Text, out bitrate)) {
        info.SetChanInfoBitrate(bitrate);
      }
      info.SetChanInfoName(bcChannelName.Text);
      info.SetChanInfoGenre(bcGenre.Text);
      info.SetChanInfoDesc(bcDescription.Text);
      info.SetChanInfoComment(bcComment.Text);
      info.SetChanInfoURL(bcContactUrl.Text);
      ChannelInfo = new ChannelInfo(info);
      var track = new AtomCollection();
      track.SetChanTrackTitle(bcTrackTitle.Text);
      track.SetChanTrackGenre(bcTrackGenre.Text);
      track.SetChanTrackAlbum(bcAlbum.Text);
      track.SetChanTrackCreator(bcCreator.Text);
      track.SetChanTrackURL(bcTrackURL.Text);
      ChannelTrack = new ChannelTrack(track);
      if (StreamSource!=null && ContentReaderFactory!=null && !String.IsNullOrEmpty(ChannelInfo.Name)) {
        DialogResult = DialogResult.OK;
      }
    }
  }
}
