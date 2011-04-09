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

    private class DebugWriter : System.IO.TextWriter
    {
      public DebugWriter()
      {
      }

      public override System.Text.Encoding Encoding
      {
        get { return System.Text.Encoding.Unicode; }
      }

      public override void Write(char[] buffer)
      {
        Write(new String(buffer));
      }

      public override void Write(char[] buffer, int index, int count)
      {
        Write(new String(buffer, index, count));
      }

      public override void Write(char buffer)
      {
        System.Diagnostics.Debug.Write(buffer);
      }

      public override void Write(string buffer)
      {
        System.Diagnostics.Debug.Write(buffer);
      }
    }

    private class TextBoxWriter : System.IO.TextWriter
    {
      private TextBox textBox;
      public TextBoxWriter(TextBox textbox)
      {
        this.textBox = textbox;
      }

      public override System.Text.Encoding Encoding
      {
        get { return System.Text.Encoding.Unicode; }
      }

      public override void Write(char[] buffer)
      {
        Write(new String(buffer));
      }

      public override void Write(char[] buffer, int index, int count)
      {
        Write(new String(buffer, index, count));
      }

      public override void Write(char buffer)
      {
        Write(buffer.ToString());
      }

      public override void Write(string buffer)
      {
        if (textBox.InvokeRequired) {
          textBox.Invoke(new Action(() => {
            textBox.AppendText(buffer);
          }));
        }
        else {
          textBox.AppendText(buffer);
        }
      }
    }

    public MainForm()
    {
      InitializeComponent();
      logLevelList.SelectedIndex = 3;
      Logger.Level = LogLevel.Warn;
      Logger.AddWriter(System.Console.Error);
      Logger.AddWriter(new DebugWriter());
      Logger.AddWriter(new TextBoxWriter(logText));
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
      peerCast.OutputStreamFactories.Add(new PeerCastStation.PCP.PCPPongOutputStreamFactory(peerCast));
      peerCast.OutputStreamFactories.Add(new PeerCastStation.PCP.PCPOutputStreamFactory(peerCast));
      peerCast.OutputStreamFactories.Add(new PeerCastStation.HTTP.HTTPOutputStreamFactory(peerCast));
      peerCast.OutputStreamFactories.Add(new PeerCastStation.HTTP.HTTPDummyOutputStreamFactory(peerCast));
      peerCast.AccessController.MaxPlays = currentMaxDirects;
      peerCast.AccessController.MaxRelays = currentMaxRelays;
      peerCast.AccessController.MaxUpstreamRate = currentMaxUpstreamRate;
      peerCast.ChannelAdded += ChannelAdded;
      peerCast.ChannelRemoved += ChannelRemoved;
      if (peerCast.IsFirewalled.HasValue) {
        portOpenedLabel.Text = peerCast.IsFirewalled.Value ? "未開放" : "開放";
      }
      else {
        portOpenedLabel.Text = "開放状態不明";
      }
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
      if (peerCast.IsFirewalled.HasValue) {
        portOpenedLabel.Text = peerCast.IsFirewalled.Value ? "未開放" : "開放";
      }
      else {
        portOpenedLabel.Text = "開放状態不明";
      }
    }

    private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
    {
      peerCast.Close();
    }

    private void channelList_SelectedIndexChanged(object sender, EventArgs e)
    {
      if (channelList.SelectedIndex<0) return;
      refreshTree(peerCast.Channels[channelList.SelectedIndex]);
    }

    private void channelClose_Click(object sender, EventArgs e)
    {
      if (channelList.SelectedIndex>=0) {
        peerCast.CloseChannel(peerCast.Channels[channelList.SelectedIndex]);
      }
    }

    private void channelPlay_Click(object sender, EventArgs e)
    {
      if (channelList.SelectedIndex>=0) {
        var channel_id = peerCast.Channels[channelList.SelectedIndex].ChannelInfo.ChannelID;
        var endpoint = peerCast.OutputListeners[0].LocalEndPoint;
        string pls;
        if (endpoint.Address.Equals(System.Net.IPAddress.Any)) {
          pls = String.Format("http://localhost:{0}/pls/{1}", endpoint.Port, channel_id.ToString("N"));
        }
        else {
          pls = String.Format("http://{0}/pls/{1}", endpoint.ToString(), channel_id.ToString("N"));
        }
        System.Diagnostics.Process.Start(pls);
      }
    }

    private Node createSelfNodeInfo(Channel channel)
    {
      var node = new Node(new Host());
      node.Host.SessionID      = peerCast.SessionID;
      node.Host.LocalEndPoint  = peerCast.LocalEndPoint;
      node.Host.GlobalEndPoint = peerCast.GlobalEndPoint ?? peerCast.LocalEndPoint;
      node.Host.IsFirewalled   = peerCast.IsFirewalled ?? true;
      node.DirectCount = channel.OutputStreams.CountPlaying;
      node.RelayCount  = channel.OutputStreams.CountRelaying;
      node.IsDirectFull = !peerCast.AccessController.IsChannelPlayable(channel);
      node.IsRelayFull  = !peerCast.AccessController.IsChannelRelayable(channel);
      node.IsReceiving  = true;
      return node;
    }

    private void addRelayTreeNode(TreeNodeCollection tree_nodes, Node node, IList<Node> node_list)
    {
      var endpoint = node.Host.GlobalEndPoint.Port==0 ? node.Host.LocalEndPoint : node.Host.GlobalEndPoint;
      var nodeinfo = String.Format(
        "({0}/{1}) {2}{3}{4}{5}",
        node.DirectCount,
        node.RelayCount,
        node.Host.IsFirewalled ? "F" : " ",
        node.IsDirectFull ? "D" : " ",
        node.IsRelayFull ? "R" : " ",
        node.IsReceiving ? " " : "B");
      var tree_node = tree_nodes.Add(String.Format("{0} {1}", endpoint, nodeinfo));
      foreach (var child in node_list.Where(x => {
        return 
          x.Host.Extra.GetHostUphostIP()!=null &&
          x.Host.Extra.GetHostUphostPort()!=null &&
          (
            (
              node.Host.GlobalEndPoint.Address.Equals(x.Host.Extra.GetHostUphostIP()) &&
              node.Host.GlobalEndPoint.Port==x.Host.Extra.GetHostUphostPort()
            ) ||
            (
              node.Host.LocalEndPoint.Address.Equals(x.Host.Extra.GetHostUphostIP()) &&
              node.Host.LocalEndPoint.Port==x.Host.Extra.GetHostUphostPort()
            )
          );
      })) {
        addRelayTreeNode(tree_node.Nodes, child, node_list);
      }
    }

    private void refreshTree(Channel channel)
    {
      relayTree.BeginUpdate();
      relayTree.Nodes.Clear();
      var root = createSelfNodeInfo(channel);
      addRelayTreeNode(relayTree.Nodes, root, channel.Nodes);
      relayTree.EndUpdate();
    }

    private System.IO.TextWriter logFileWriter = null;
    private void logToFileCheck_CheckedChanged(object sender, EventArgs e)
    {
      if (logFileWriter!=null) {
        Logger.RemoveWriter(logFileWriter);
        if (logToFileCheck.Checked) {
          Logger.AddWriter(logFileWriter);
        }
      }
    }

    private void logFileNameText_Validated(object sender, EventArgs e)
    {
      if (logFileWriter!=null) {
        Logger.RemoveWriter(logFileWriter);
        logFileWriter.Close();
        logFileWriter = null;
      }
      if (logFileNameText.Text.Length>0) {
        try {
          logFileWriter = System.IO.File.AppendText(logFileNameText.Text);
        }
        catch (UnauthorizedAccessException) {
          logFileWriter = null;
        }
        catch (ArgumentException) {
          logFileWriter = null;
        }
        catch (System.IO.PathTooLongException) {
          logFileWriter = null;
        }
        catch (System.IO.DirectoryNotFoundException) {
          logFileWriter = null;
        }
        catch (NotSupportedException) {
          logFileWriter = null;
        }
      }
      if (logFileWriter!=null && logToFileCheck.Checked) {
        Logger.AddWriter(logFileWriter);
      }
    }

    private void selectLogFileName_Click(object sender, EventArgs e)
    {
      if (logSaveFileDialog.ShowDialog(this)==DialogResult.OK) {
        logFileNameText.Text = logSaveFileDialog.FileName;
        logFileNameText_Validated(sender, e);
      }
    }

    private void logClearButton_Click(object sender, EventArgs e)
    {
      logText.ResetText();
    }

    private void logLevelList_SelectedIndexChanged(object sender, EventArgs e)
    {
      switch (logLevelList.SelectedIndex) {
      case 0: Logger.Level = LogLevel.None; break;
      case 1: Logger.Level = LogLevel.Fatal; break;
      case 2: Logger.Level = LogLevel.Error; break;
      case 3: Logger.Level = LogLevel.Warn; break;
      case 4: Logger.Level = LogLevel.Info; break;
      case 5: Logger.Level = LogLevel.Debug; break;
      }
    }
  }
}
