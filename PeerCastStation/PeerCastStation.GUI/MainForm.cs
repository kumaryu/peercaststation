using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using PeerCastStation.Core;
using System.Linq;

namespace PeerCastStation.GUI
{
  public partial class MainForm : Form
  {
    private int currentPort = 7148;
    private int currentMaxRelays = 0;
    private int currentMaxDirects = 0;
    private int currentMaxUpstreamRate = 0;
    private PeerCastStation.Core.PeerCast peerCast;
    public MainForm()
    {
      InitializeComponent();
      port.Value = currentPort;
      maxRelays.Value = currentMaxRelays;
      maxDirects.Value = currentMaxDirects;
      maxUpstreamRate.Value = currentMaxUpstreamRate;
      peerCast = new PeerCastStation.Core.PeerCast(
        new System.Net.IPEndPoint(System.Net.IPAddress.Any, currentPort)
      );
      peerCast.SourceStreamFactories["pcp"] = new PeerCastStation.PCP.PCPSourceStreamFactory(peerCast);
      peerCast.OutputStreamFactories.Add(new PeerCastStation.PCP.PCPOutputStreamFactory(peerCast));
      peerCast.OutputStreamFactories.Add(new PeerCastStation.HTTP.HTTPOutputStreamFactory(peerCast));
      peerCast.AccessController.MaxPlays = currentMaxDirects;
      peerCast.AccessController.MaxRelays = currentMaxRelays;
      peerCast.AccessController.MaxUpstreamRate = currentMaxUpstreamRate;
      peerCast.ChannelAdded += ChannelAdded;
      peerCast.ChannelRemoved += ChannelRemoved;
    }

    private void ChannelAdded(object sender, PeerCastStation.Core.ChannelChangedEventArgs e)
    {
      channelList.Items.Add(CreateChannelListItem(e.Channel));
      e.Channel.PropertyChanged += ChannelInfoChanged;
    }

    private void ChannelRemoved(object sender, PeerCastStation.Core.ChannelChangedEventArgs e)
    {
      e.Channel.PropertyChanged -= ChannelInfoChanged;
    }

    private void ChannelInfoChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
      channelList.Items.Clear();
      foreach (var c in peerCast.Channels) {
        channelList.Items.Add(CreateChannelListItem(c));
      }
    }

    private string CreateChannelListItem(PeerCastStation.Core.Channel c)
    {
      var total_plays  = c.Nodes.Sum(n => n.DirectCount) + c.OutputStreams.CountPlaying;
      var total_relays = c.Nodes.Sum(n => n.RelayCount)  + c.OutputStreams.CountRelaying;
      var chaninfo = c.ChannelInfo.Extra.GetChanInfo();
      var bitrate = chaninfo!=null ? (chaninfo.GetChanInfoBitrate() ?? 0) : 0; 
      return String.Format(
        "{0} {1}kbps ({2}/{3}) [{4}/{5}]",
        c.ChannelInfo.Name,
        bitrate,
        total_plays,
        total_relays,
        c.OutputStreams.CountPlaying,
        c.OutputStreams.CountRelaying);
    }

    private void applySettings_Click(object sender, EventArgs e)
    {
      if (port.Value!=currentPort) {
        peerCast.Close();
        currentPort = (int)port.Value;
        peerCast = new PeerCastStation.Core.PeerCast(
          new System.Net.IPEndPoint(System.Net.IPAddress.Any, currentPort)
        );
        peerCast.SourceStreamFactories["pcp"] = new PeerCastStation.PCP.PCPSourceStreamFactory(peerCast);
        peerCast.OutputStreamFactories.Add(new PeerCastStation.PCP.PCPOutputStreamFactory(peerCast));
        peerCast.OutputStreamFactories.Add(new PeerCastStation.HTTP.HTTPOutputStreamFactory(peerCast));
        channelGrid.SelectedObject = peerCast.Channels;
      }
      currentMaxRelays      =  (int)maxRelays.Value;
      currentMaxDirects     =  (int)maxDirects.Value;
      currentMaxUpstreamRate = (int)maxUpstreamRate.Value;
      peerCast.AccessController.MaxPlays = currentMaxDirects;
      peerCast.AccessController.MaxRelays = currentMaxRelays;
      peerCast.AccessController.MaxUpstreamRate = currentMaxUpstreamRate;
    }

    private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
    {
      peerCast.Close();
    }

    private void channelList_SelectedIndexChanged(object sender, EventArgs e)
    {
      if (channelList.SelectedIndex>=0) {
        channelGrid.SelectedObject = peerCast.Channels[channelList.SelectedIndex].ChannelInfo;
      }
      else {
        channelGrid.SelectedObject = null;
      }
    }
  }
}
