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

    private bool IsOSX()
    {
      if (PlatformID.Unix  ==Environment.OSVersion.Platform ||
          PlatformID.MacOSX==Environment.OSVersion.Platform) {
        var start_info = new System.Diagnostics.ProcessStartInfo("uname");
        start_info.RedirectStandardOutput = true;
        start_info.UseShellExecute = false;
        start_info.ErrorDialog = false;
        var process = System.Diagnostics.Process.Start(start_info);
        if (process!=null) {
          return System.Text.RegularExpressions.Regex.IsMatch(
              process.StandardOutput.ReadToEnd(), @"Darwin");
        }
        else {
          return false;
        }
      }
      else {
        return false;
      }
    }

    public MainForm()
    {
      InitializeComponent();
      if (IsOSX()) {
        this.Font = new System.Drawing.Font("Osaka", this.Font.SizeInPoints);
      }
      port.Value = currentPort;
      maxRelays.Value = currentMaxRelays;
      maxDirects.Value = currentMaxDirects;
      maxUpstreamRate.Value = currentMaxUpstreamRate;
      peerCast = new PeerCastStation.Core.PeerCast();
      peerCast.StartListen(new System.Net.IPEndPoint(System.Net.IPAddress.Any, currentPort));
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
      channelList.Items.Clear();
      foreach (var c in peerCast.Channels) {
        channelList.Items.Add(CreateChannelListItem(c));
      }
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
      var status = "UNKNOWN";
      switch (c.Status) {
      case SourceStreamStatus.Idle:       status = "IDLE";    break;
      case SourceStreamStatus.Connecting: status = "CONNECT"; break;
      case SourceStreamStatus.Searching:  status = "SEARCH";  break;
      case SourceStreamStatus.Recieving:  status = "RECIEVE"; break;
      case SourceStreamStatus.Error:      status = "ERROR";   break;
      }
      return String.Format(
        "{0} {1}kbps ({2}/{3}) [{4}/{5}] {6}",
        c.ChannelInfo.Name,
        bitrate,
        total_plays,
        total_relays,
        c.OutputStreams.CountPlaying,
        c.OutputStreams.CountRelaying,
        status);
    }

    private void applySettings_Click(object sender, EventArgs e)
    {
      if (port.Value!=currentPort) {
        var listener = peerCast.OutputListeners.FirstOrDefault(x => x.LocalEndPoint.Port==currentPort);
        if (listener!=null) peerCast.StopListen(listener);
        currentPort = (int)port.Value;
        peerCast.StartListen(new System.Net.IPEndPoint(System.Net.IPAddress.Any, currentPort));
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

    private void channelClose_Click(object sender, EventArgs e)
    {
      if (channelList.SelectedIndex>=0) {
        peerCast.CloseChannel(peerCast.Channels[channelList.SelectedIndex]);
        channelGrid.SelectedObject = null;
      }
    }
  }
}
