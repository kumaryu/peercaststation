using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Text.RegularExpressions;

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
      channelGrid.SelectedObject = peerCast.Channels;
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

    private void startRelay_Click(object sender, EventArgs e)
    {
      var relay_url = new Uri(relayURL.Text);
      var match = Regex.Match(relay_url.PathAndQuery, @"/[^/]+/([A-Za-z0-9]+)(\.[^?]*)?(\?tip=(.*))?");
      if (match.Success) {
        var channel_id = Guid.Parse(match.Groups[1].Value);
        if (match.Groups[4].Success) {
          var tracker = new Uri(String.Format("pcp://{0}/", match.Groups[4].Value));
          peerCast.RelayChannel(channel_id, tracker);
        }
        else {
          peerCast.RelayChannel(channel_id);
        }
      }
    }

    private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
    {
      peerCast.Close();
    }
  }
}
