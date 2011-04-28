// PeerCastStation, a P2P streaming servent.
// Copyright (C) 2011 Ryuichi Sakamoto (kumaryu@kumaryu.net)
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
using System.Drawing;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using PeerCastStation.Core;
using PeerCastStation.GUI.Properties;
using System.Linq;
using System.ComponentModel;

namespace PeerCastStation.GUI
{
  public partial class MainForm : Form
  {
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
          textBox.BeginInvoke(new Action(() => {
            textBox.AppendText(buffer);
          }));
        }
        else {
          textBox.AppendText(buffer);
        }
      }
    }

    private Timer timer = new Timer();
    private int currentPort;
    private TextBoxWriter guiWriter = null;
    private BindingList<ChannelListItem> channelListItems = new BindingList<ChannelListItem>();
    public MainForm()
    {
      InitializeComponent();
      channelList.DataSource = channelListItems; 
      Settings.Default.PropertyChanged += SettingsPropertyChanged;
      Logger.Level = LogLevel.Warn;
      Logger.AddWriter(new DebugWriter());
      guiWriter = new TextBoxWriter(logText);
      if (IsOSX()) {
        this.Font = new System.Drawing.Font("Osaka", this.Font.SizeInPoints);
        statusBar.Font = new System.Drawing.Font("Osaka", statusBar.Font.SizeInPoints);
      }
      port.Value                 = Settings.Default.Port;
      maxRelays.Value            = Settings.Default.MaxRelays;
      maxDirects.Value           = Settings.Default.MaxPlays;
      maxUpstreamRate.Value      = Settings.Default.MaxUpstreamRate;
      peerCast = new PeerCastStation.Core.PeerCast();
      peerCast.SourceStreamFactories["pcp"] = new PeerCastStation.PCP.PCPSourceStreamFactory(peerCast);
      peerCast.OutputStreamFactories.Add(new PeerCastStation.PCP.PCPPongOutputStreamFactory(peerCast));
      peerCast.OutputStreamFactories.Add(new PeerCastStation.PCP.PCPOutputStreamFactory(peerCast));
      peerCast.OutputStreamFactories.Add(new PeerCastStation.HTTP.HTTPOutputStreamFactory(peerCast));
      peerCast.OutputStreamFactories.Add(new PeerCastStation.Ohaoha.OhaohaCheckOutputStreamFactory(peerCast));
      peerCast.OutputStreamFactories.Add(new PeerCastStation.HTTP.HTTPDummyOutputStreamFactory(peerCast));
      currentPort = Settings.Default.Port;
      try {
        peerCast.StartListen(new System.Net.IPEndPoint(System.Net.IPAddress.Any, currentPort));
        portLabel.Text = String.Format("ポート:{0}", currentPort);
      }
      catch (System.Net.Sockets.SocketException) {
        portLabel.Text = String.Format("ポート{0}を開けません", currentPort);
      }
      peerCast.AccessController.MaxPlays        = Settings.Default.MaxPlays;
      peerCast.AccessController.MaxRelays       = Settings.Default.MaxRelays;
      peerCast.AccessController.MaxUpstreamRate = Settings.Default.MaxUpstreamRate;
      peerCast.ChannelAdded   += ChannelAdded;
      peerCast.ChannelRemoved += ChannelRemoved;
      logLevelList.SelectedIndex = Settings.Default.LogLevel;
      logToFileCheck.Checked = Settings.Default.LogToFile;
      logFileNameText.Text = Settings.Default.LogFileName;
      logToConsoleCheck.Checked = Settings.Default.LogToConsole;
      logToGUICheck.Checked = Settings.Default.LogToGUI;
      if (peerCast.IsFirewalled.HasValue) {
        portOpenedLabel.Text = peerCast.IsFirewalled.Value ? "未開放" : "開放";
      }
      else {
        portOpenedLabel.Text = "開放状態不明";
      }
      timer.Interval = 1;
      timer.Enabled = true;
    }

    private void SettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
      switch (e.PropertyName) {
      case "Port":
        var listener = peerCast.OutputListeners.FirstOrDefault(x => x.LocalEndPoint.Port==currentPort);
        if (listener!=null) peerCast.StopListen(listener);
        currentPort = Settings.Default.Port;
        try {
          peerCast.StartListen(new System.Net.IPEndPoint(System.Net.IPAddress.Any, currentPort));
          portLabel.Text = String.Format("ポート:{0}", currentPort);
        }
        catch (System.Net.Sockets.SocketException) {
          portLabel.Text = String.Format("ポート{0}を開けません", currentPort);
        }
        break;
      case "MaxPlays":
        peerCast.AccessController.MaxPlays        = Settings.Default.MaxPlays;
        break;
      case "MaxRelays":
        peerCast.AccessController.MaxRelays       = Settings.Default.MaxRelays;
        break;
      case "MaxUpStreamRate":
        peerCast.AccessController.MaxUpstreamRate = Settings.Default.MaxUpstreamRate;
        break;
      case "LogLevel":
        switch (Settings.Default.LogLevel) {
        case 0: Logger.Level = LogLevel.None;  break;
        case 1: Logger.Level = LogLevel.Fatal; break;
        case 2: Logger.Level = LogLevel.Error; break;
        case 3: Logger.Level = LogLevel.Warn;  break;
        case 4: Logger.Level = LogLevel.Info;  break;
        case 5: Logger.Level = LogLevel.Debug; break;
        }
        break;
      case "LogToFile":
        if (logFileWriter!=null) {
          Logger.RemoveWriter(logFileWriter);
          if (Settings.Default.LogToFile) {
            Logger.AddWriter(logFileWriter);
          }
        }
        break;
      case "LogFile":
        if (logFileWriter!=null) {
          Logger.RemoveWriter(logFileWriter);
          logFileWriter.Close();
          logFileWriter = null;
        }
        if (Settings.Default.LogFileName!=null && Settings.Default.LogFileName!="") {
          try {
            logFileWriter = System.IO.File.AppendText(Settings.Default.LogFileName);
          }
          catch (UnauthorizedAccessException)          { logFileWriter = null; }
          catch (ArgumentException)                    { logFileWriter = null; }
          catch (System.IO.PathTooLongException)       { logFileWriter = null; }
          catch (System.IO.DirectoryNotFoundException) { logFileWriter = null; }
          catch (NotSupportedException)                { logFileWriter = null; }
        }
        if (logFileWriter!=null && Settings.Default.LogToFile) {
          Logger.AddWriter(logFileWriter);
        }
        break;
      case "LogToConsole":
        Logger.RemoveWriter(System.Console.Error);
        if (Settings.Default.LogToConsole) {
          Logger.AddWriter(System.Console.Error);
        }
        break;
      case "LogToGUI":
        Logger.RemoveWriter(guiWriter);
        if (Settings.Default.LogToGUI) {
          Logger.AddWriter(guiWriter);
        }
        break;
      }
    }

    private class ChannelListItem
      : INotifyPropertyChanged
    {
      
      public Channel Channel { get; private set; }
      public ChannelListItem(Channel channel)
      {
        this.Channel = channel;
        this.Channel.PropertyChanged += ChannelInfoChanged;
      }

      private void ChannelInfoChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
      {
        if (PropertyChanged!=null) PropertyChanged(this, new PropertyChangedEventArgs("Name"));
      }

      public string Name
      {
        get
        {
          var total_plays  = Channel.Nodes.Sum(n => n.DirectCount) + Channel.OutputStreams.CountPlaying;
          var total_relays = Channel.Nodes.Sum(n => n.RelayCount)  + Channel.OutputStreams.CountRelaying;
          var chaninfo = Channel.ChannelInfo.Extra.GetChanInfo();
          var bitrate = chaninfo!=null ? (chaninfo.GetChanInfoBitrate() ?? 0) : 0;
          var status = "UNKNOWN";
          switch (Channel.Status) {
          case SourceStreamStatus.Idle:       status = "IDLE";    break;
          case SourceStreamStatus.Connecting: status = "CONNECT"; break;
          case SourceStreamStatus.Searching:  status = "SEARCH";  break;
          case SourceStreamStatus.Recieving:  status = "RECIEVE"; break;
          case SourceStreamStatus.Error:      status = "ERROR";   break;
          }
          return String.Format(
            "{0} {1}kbps ({2}/{3}) [{4}/{5}] {6}",
            Channel.ChannelInfo.Name,
            bitrate,
            total_plays,
            total_relays,
            Channel.OutputStreams.CountPlaying,
            Channel.OutputStreams.CountRelaying,
            status);
        }
      }

      /*
      public override string ToString()
      {
        var total_plays  = Channel.Nodes.Sum(n => n.DirectCount) + Channel.OutputStreams.CountPlaying;
        var total_relays = Channel.Nodes.Sum(n => n.RelayCount)  + Channel.OutputStreams.CountRelaying;
        var chaninfo = Channel.ChannelInfo.Extra.GetChanInfo();
        var bitrate = chaninfo!=null ? (chaninfo.GetChanInfoBitrate() ?? 0) : 0;
        var status = "UNKNOWN";
        switch (Channel.Status) {
        case SourceStreamStatus.Idle:       status = "IDLE";    break;
        case SourceStreamStatus.Connecting: status = "CONNECT"; break;
        case SourceStreamStatus.Searching:  status = "SEARCH";  break;
        case SourceStreamStatus.Recieving:  status = "RECIEVE"; break;
        case SourceStreamStatus.Error:      status = "ERROR";   break;
        }
        return String.Format(
          "{0} {1}kbps ({2}/{3}) [{4}/{5}] {6}",
          Channel.ChannelInfo.Name,
          bitrate,
          total_plays,
          total_relays,
          Channel.OutputStreams.CountPlaying,
          Channel.OutputStreams.CountRelaying,
          status);
      }
       */

      public event PropertyChangedEventHandler PropertyChanged;
    }

    private void ChannelAdded(object sender, PeerCastStation.Core.ChannelChangedEventArgs e)
    {
      channelListItems.Add(new ChannelListItem(e.Channel));
      e.Channel.PropertyChanged += ChannelInfoChanged;
    }

    private void ChannelRemoved(object sender, PeerCastStation.Core.ChannelChangedEventArgs e)
    {
      e.Channel.PropertyChanged -= ChannelInfoChanged;
      ChannelListItem item = null;  
      foreach (var i in channelListItems) {
        if (i.Channel==e.Channel) {
          item = i;
          break;
        }
      }
      if (item!=null) {
        channelListItems.Remove(item);
      }
    }

    private void ChannelInfoChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
    }

    private void applySettings_Click(object sender, EventArgs e)
    {
      Settings.Default.Port            = (int)port.Value;
      Settings.Default.MaxRelays       = (int)maxRelays.Value;
      Settings.Default.MaxPlays        = (int)maxDirects.Value;
      Settings.Default.MaxUpstreamRate = (int)maxUpstreamRate.Value;
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
      Settings.Default.Save();
    }

    private void channelList_SelectedIndexChanged(object sender, EventArgs e)
    {
      var item = channelList.SelectedItem as ChannelListItem;
      if (item!=null) {
        refreshTree(item.Channel);
        refreshOutputList(item.Channel);
      }
      else {
        relayTree.Nodes.Clear();
      }
    }

    private void channelClose_Click(object sender, EventArgs e)
    {
      var item = channelList.SelectedItem as ChannelListItem;
      if (item!=null) {
        peerCast.CloseChannel(item.Channel);
      }
    }

    private void channelPlay_Click(object sender, EventArgs e)
    {
      var item = channelList.SelectedItem as ChannelListItem;
      if (item!=null) {
        var channel_id = item.Channel.ChannelInfo.ChannelID;
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
        node.IsDirectFull      ? "D" : " ",
        node.IsRelayFull       ? "R" : " ",
        node.IsReceiving       ? " " : "B");
      var tree_node = tree_nodes.Add(String.Format("{0} {1}", endpoint, nodeinfo));
      tree_node.Tag = node;
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

    private void refreshOutputList(Channel channel)
    {
      outputList.Items.Clear();
      foreach (var os in channel.OutputStreams) {
        outputList.Items.Add(os);
      }
    }

    private System.IO.TextWriter logFileWriter = null;
    private void logToFileCheck_CheckedChanged(object sender, EventArgs e)
    {
      Settings.Default.LogToFile = logToFileCheck.Checked;
    }

    private void logToConsoleCheck_CheckedChanged(object sender, EventArgs e)
    {
      Settings.Default.LogToConsole = logToConsoleCheck.Checked;
    }

    private void logToGUICheck_CheckedChanged(object sender, EventArgs e)
    {
      Settings.Default.LogToGUI = logToGUICheck.Checked;
    }

    private void logFileNameText_Validated(object sender, EventArgs e)
    {
      Settings.Default.LogFileName = logFileNameText.Text;
    }

    private void selectLogFileName_Click(object sender, EventArgs e)
    {
      if (logSaveFileDialog.ShowDialog(this)==DialogResult.OK) {
        logFileNameText.Text = logSaveFileDialog.FileName;
        Settings.Default.LogFileName = logSaveFileDialog.FileName;
      }
    }

    private void logClearButton_Click(object sender, EventArgs e)
    {
      logText.ResetText();
    }

    private void logLevelList_SelectedIndexChanged(object sender, EventArgs e)
    {
      Settings.Default.LogLevel = logLevelList.SelectedIndex;
    }

    private void channelBump_Click(object sender, EventArgs e)
    {
      var item = channelList.SelectedItem as ChannelListItem;
      if (item!=null) {
        item.Channel.Reconnect();
      }
    }

    private void downStreamClose_Click(object sender, EventArgs e)
    {
      var connection = outputList.SelectedItem as IOutputStream;
      if (connection!=null) {
        connection.Close();
      }
    }
  }
}
