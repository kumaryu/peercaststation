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
    private TextBoxWriter guiWriter = null;
    private BindingList<ChannelListItem> channelListItems = new BindingList<ChannelListItem>();
    public class ContentReaderWrapper
    {
      public IContentReader Reader { get; private set; }
      public ContentReaderWrapper(IContentReader reader)
      {
        this.Reader = reader;
      }

      public override string ToString()
      {
        return this.Reader.Name;
      }
    }

    static bool isOSX;
    static public bool IsOSX { get { return isOSX; } }
    static MainForm()
    {
      if (PlatformID.Unix  ==Environment.OSVersion.Platform ||
          PlatformID.MacOSX==Environment.OSVersion.Platform) {
        var start_info = new System.Diagnostics.ProcessStartInfo("uname");
        start_info.RedirectStandardOutput = true;
        start_info.UseShellExecute = false;
        start_info.ErrorDialog = false;
        var process = System.Diagnostics.Process.Start(start_info);
        if (process!=null) {
          isOSX = System.Text.RegularExpressions.Regex.IsMatch(
              process.StandardOutput.ReadToEnd(), @"Darwin");
        }
        else {
          isOSX = false;
        }
      }
      else {
        isOSX = false;
      }
    }

    private NotifyIcon notifyIcon;

    public MainForm(PeerCast peercast)
    {
      InitializeComponent();
      channelList.DataSource = channelListItems; 
      Settings.Default.PropertyChanged += SettingsPropertyChanged;
      Logger.Level = LogLevel.Warn;
      Logger.AddWriter(new DebugWriter());
      guiWriter = new TextBoxWriter(logText);
      if (IsOSX) {
        this.Font = new System.Drawing.Font("Osaka", this.Font.SizeInPoints);
        statusBar.Font = new System.Drawing.Font("Osaka", statusBar.Font.SizeInPoints);
      }
      if (PlatformID.Win32NT==Environment.OSVersion.Platform) {
        notifyIcon = new NotifyIcon(this.components);
        notifyIcon.Icon = this.Icon;
        notifyIcon.ContextMenuStrip = notifyIconMenu;
        notifyIcon.Visible = true;
        notifyIcon.DoubleClick += showGUIMenuItem_Click;
      }
      peerCast = peercast;
      peerCast.ChannelAdded          += ChannelAdded;
      peerCast.ChannelRemoved        += ChannelRemoved;
      logLevelList.SelectedIndex = Settings.Default.LogLevel;
      logToFileCheck.Checked     = Settings.Default.LogToFile;
      logFileNameText.Text       = Settings.Default.LogFileName;
      logToConsoleCheck.Checked  = Settings.Default.LogToConsole;
      logToGUICheck.Checked      = Settings.Default.LogToGUI;
      OnUpdateSettings(null);
      timer.Interval = 1000;
      timer.Enabled = true;
      timer.Tick += (sender, args) => {
        if (peerCast.IsFirewalled.HasValue) {
          portOpenedLabel.Text = peerCast.IsFirewalled.Value ? "未開放" : "開放";
        }
        else {
          portOpenedLabel.Text = "開放状態不明";
        }
        portLabel.Text = "リレー可能ポート:" + String.Join(", ",
          peerCast.OutputListeners.Where(listener =>
            (listener.GlobalOutputAccepts & OutputStreamType.Relay)!=0
          ).Select(
            listener => listener.LocalEndPoint.Port
          ).Distinct().Select(
            port => port.ToString()
          ).ToArray());
      };
    }

    private void OnUpdateSettings(string property_name)
    {
      if (property_name==null || property_name=="LogLevel") {
        switch (Settings.Default.LogLevel) {
        case 0: Logger.Level = LogLevel.None;  break;
        case 1: Logger.Level = LogLevel.Fatal; break;
        case 2: Logger.Level = LogLevel.Error; break;
        case 3: Logger.Level = LogLevel.Warn;  break;
        case 4: Logger.Level = LogLevel.Info;  break;
        case 5: Logger.Level = LogLevel.Debug; break;
        }
      }
      if (property_name==null || property_name=="LogToFile") {
        if (logFileWriter!=null) {
          Logger.RemoveWriter(logFileWriter);
          if (Settings.Default.LogToFile) {
            Logger.AddWriter(logFileWriter);
          }
        }
      }
      if (property_name==null || property_name=="LogFileName") {
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
          catch (System.IO.IOException)                { logFileWriter = null; }
        }
        if (logFileWriter!=null && Settings.Default.LogToFile) {
          Logger.AddWriter(logFileWriter);
        }
      }
      if (property_name==null || property_name=="LogToConsole") {
        Logger.RemoveWriter(System.Console.Error);
        if (Settings.Default.LogToConsole) {
          Logger.AddWriter(System.Console.Error);
        }
      }
      if (property_name==null || property_name=="LogToGUI") {
        Logger.RemoveWriter(guiWriter);
        if (Settings.Default.LogToGUI) {
          Logger.AddWriter(guiWriter);
        }
      }
    }

    private void SettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
      OnUpdateSettings(e.PropertyName);
    }

    private class ChannelListItem
      : INotifyPropertyChanged
    {
      
      public Channel Channel { get; private set; }
      public ChannelListItem(Channel channel)
      {
        this.Channel = channel;
      }

      public void Update()
      {
        if (PropertyChanged!=null) {
          PropertyChanged(this, new PropertyChangedEventArgs("Name"));
        }
      }

      public string Name
      {
        get
        {
          var status = "UNKNOWN";
          switch (Channel.Status) {
          case SourceStreamStatus.Idle:       status = "IDLE";    break;
          case SourceStreamStatus.Connecting: status = "CONNECT"; break;
          case SourceStreamStatus.Searching:  status = "SEARCH";  break;
          case SourceStreamStatus.Receiving:  status = "RECEIVE"; break;
          case SourceStreamStatus.Error:      status = "ERROR";   break;
          }
          return String.Format(
            "{0} {1}kbps ({2}/{3}) [{4}/{5}] {6}",
            Channel.ChannelInfo.Name,
            Channel.ChannelInfo.Bitrate,
            Channel.TotalDirects,
            Channel.TotalRelays,
            Channel.LocalDirects,
            Channel.LocalRelays,
            status);
        }
      }

      public event PropertyChangedEventHandler PropertyChanged;
    }

    private void ChannelAdded(object sender, PeerCastStation.Core.ChannelChangedEventArgs e)
    {
      this.BeginInvoke(new Action(() => {
        channelListItems.Add(new ChannelListItem(e.Channel));
        e.Channel.ChannelInfoChanged += ChannelInfoChanged;
      }));
    }

    private void ChannelRemoved(object sender, PeerCastStation.Core.ChannelChangedEventArgs e)
    {
      this.BeginInvoke(new Action(() => {
        e.Channel.ChannelInfoChanged -= ChannelInfoChanged;
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
      }));
    }

    private class YellowPageItem
    {
      public string Name { get; private set; }
      public IYellowPageClient YellowPageClient { get; private set; }
      public YellowPageItem(string name, IYellowPageClient yellowpage)
      {
        this.Name = name;
        this.YellowPageClient = yellowpage;
      }

      public YellowPageItem(IYellowPageClient yellowpage)
        : this(String.Format("{0} ({1})", yellowpage.Name, yellowpage.Uri), yellowpage)
      {
      }

      public override string ToString()
      {
        return this.Name;
      }
    }

    private void ChannelInfoChanged(object sender, EventArgs e)
    {
      this.BeginInvoke(new Action(() => {
        foreach (var i in channelListItems) {
          if (i.Channel==sender) {
            i.Update();
            break;
          }
        }
        var item = channelList.SelectedItem as ChannelListItem;
        if (item!=null && item.Channel==sender) {
          refreshTree(item.Channel);
          refreshChannelInfo(item.Channel);
          refreshOutputList(item.Channel);
        }
        if (peerCast.IsFirewalled.HasValue) {
          portOpenedLabel.Text = peerCast.IsFirewalled.Value ? "未開放" : "開放";
        }
        else {
          portOpenedLabel.Text = "開放状態不明";
        }
      }));
    }

    private void applySettings_Click(object sender, EventArgs e)
    {
      peerCast.AccessController.MaxRelays           = (int)maxRelays.Value;
      peerCast.AccessController.MaxPlays            = (int)maxDirects.Value;
      peerCast.AccessController.MaxRelaysPerChannel = (int)maxRelaysPerChannel.Value;
      peerCast.AccessController.MaxPlaysPerChannel  = (int)maxDirectsPerChannel.Value;
      peerCast.AccessController.MaxUpstreamRate     = (int)maxUpstreamRate.Value;
      if (peerCast.IsFirewalled.HasValue) {
        portOpenedLabel.Text = peerCast.IsFirewalled.Value ? "未開放" : "開放";
      }
      else {
        portOpenedLabel.Text = "開放状態不明";
      }
    }

    private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
    {
      if (e.CloseReason==CloseReason.UserClosing &&
          PlatformID.Win32NT==Environment.OSVersion.Platform) {
        e.Cancel = true;
        this.Hide();
      }
    }

    private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
    {
      Settings.Default.Save();
      Logger.RemoveWriter(guiWriter);
    }

    private void channelList_SelectedIndexChanged(object sender, EventArgs e)
    {
      var item = channelList.SelectedItem as ChannelListItem;
      if (item!=null) {
        refreshTree(item.Channel);
        refreshChannelInfo(item.Channel);
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

    public void OpenPeerCastUri(string peercast_uri)
    {
      var match = Regex.Match(peercast_uri, @"peercast://(pls/)?(.+)$");
      if (match.Success && match.Groups[2].Success && peerCast.OutputListeners.Count>0) {
        var channel = match.Groups[2].Value;
        var endpoint = peerCast.OutputListeners[0].LocalEndPoint;
        string pls;
        if (endpoint.Address.Equals(System.Net.IPAddress.Any)) {
          pls = String.Format("http://localhost:{0}/pls/{1}", endpoint.Port, channel);
        }
        else {
          pls = String.Format("http://{0}/pls/{1}", endpoint.ToString(), channel);
        }
        System.Diagnostics.Process.Start(pls);
      }
    }

    private void channelPlay_Click(object sender, EventArgs e)
    {
      var item = channelList.SelectedItem as ChannelListItem;
      if (item!=null && peerCast.OutputListeners.Count>0) {
        var channel_id = item.Channel.ChannelID;
        var ext = (item.Channel.ChannelInfo.ContentType=="WMV" ||
                   item.Channel.ChannelInfo.ContentType=="WMA" ||
                   item.Channel.ChannelInfo.ContentType=="ASX") ? ".asx" : ".m3u";
        var endpoint = peerCast.OutputListeners[0].LocalEndPoint;
        string pls;
        if (endpoint.Address.Equals(System.Net.IPAddress.Any)) {
          pls = String.Format("http://localhost:{0}/pls/{1}{2}", endpoint.Port, channel_id.ToString("N"), ext);
        }
        else {
          pls = String.Format("http://{0}/pls/{1}{2}", endpoint.ToString(), channel_id.ToString("N"), ext);
        }
        System.Diagnostics.Process.Start(pls);
      }
    }

    private Host createSelfNodeInfo(Channel channel)
    {
      var host = new HostBuilder();
      host.SessionID      = peerCast.SessionID;
      host.LocalEndPoint  = peerCast.GetLocalEndPoint(channel.SourceHost.GlobalEndPoint.AddressFamily, OutputStreamType.Relay);
      host.GlobalEndPoint = peerCast.GetGlobalEndPoint(channel.SourceHost.GlobalEndPoint.AddressFamily, OutputStreamType.Relay);
      host.IsFirewalled   = peerCast.IsFirewalled ?? true;
      host.DirectCount    = channel.LocalDirects;
      host.RelayCount     = channel.LocalRelays;
      host.IsDirectFull   = !peerCast.AccessController.IsChannelPlayable(channel);
      host.IsRelayFull    = !peerCast.AccessController.IsChannelRelayable(channel);
      host.IsReceiving    = true;
      return host.ToHost();
    }

    private void addRelayTreeNode(
      TreeNodeCollection tree_nodes,
      Host node,
      IList<Host> node_list,
      IList<Guid> added_list)
    {
      var endpoint = (node.GlobalEndPoint==null || node.GlobalEndPoint.Port==0) ? node.LocalEndPoint : node.GlobalEndPoint;
      if (endpoint==null) return;
      var nodeinfo = String.Format(
        "({0}/{1}) {2}{3}{4}",
        node.DirectCount,
        node.RelayCount,
        node.IsFirewalled ? "0" : "",
        node.IsRelayFull  ? "-" : "",
        node.IsReceiving  ? "" : "B");
      var tree_node = tree_nodes.Add(String.Format("{0} {1}", endpoint, nodeinfo));
      added_list.Add(node.SessionID);
      tree_node.Tag = node;
      foreach (var child in node_list) {
        if (added_list.Contains(child.SessionID)) continue;
        var uphost = child.Extra.GetHostUphostEndPoint();
        if (uphost!=null && endpoint.Equals(uphost)) {
          addRelayTreeNode(tree_node.Nodes, child, node_list, added_list);
        }
      }
    }

    private void addRelayTreeNode(TreeNodeCollection tree_nodes, Host node, IList<Host> node_list)
    {
      addRelayTreeNode(tree_nodes, node, node_list, new List<Guid>());
    }

    private void refreshTree(Channel channel)
    {
      relayTree.BeginUpdate();
      relayTree.Nodes.Clear();
      var root = createSelfNodeInfo(channel);
      addRelayTreeNode(relayTree.Nodes, root, channel.Nodes);
      relayTree.ExpandAll();
      relayTree.EndUpdate();
    }

    private class ChannelInfoContainer
    {
      public string InfoChannelName { get; private set; }
      public string InfoGenre { get; private set; }
      public string InfoDesc { get; private set; }
      public string InfoContactURL { get; private set; }
      public string InfoComment { get; private set; }
      public string InfoContentType { get; private set; }
      public string InfoBitrate { get; private set; }
      public string TrackAlbum { get; private set; }
      public string TrackArtist { get; private set; }
      public string TrackTitle { get; private set; }
      public string TrackContactURL { get; private set; }

      public ChannelInfoContainer(ChannelInfo info, ChannelTrack track)
      {
        if (info!=null) {
          InfoChannelName = info.Name;
          InfoGenre       = info.Genre;
          InfoDesc        = info.Desc;
          InfoContactURL  = info.URL;
          InfoComment     = info.Comment;
          InfoContentType = info.ContentType;
          InfoBitrate     = String.Format("{0} kbps", info.Bitrate);
        }
        else {
          InfoChannelName = "";
          InfoGenre       = "";
          InfoDesc        = "";
          InfoContactURL  = "";
          InfoComment     = "";
          InfoContentType = "";
          InfoBitrate     = "";
        }
        if (track!=null) {
          TrackAlbum      = track.Album;
          TrackArtist     = track.Creator;
          TrackTitle      = track.Name;
          TrackContactURL = track.URL;
        }
        else {
          TrackAlbum      = "";
          TrackArtist     = "";
          TrackTitle      = "";
          TrackContactURL = "";
        }
      }
    }

    private ChannelInfoContainer channelInfo = new ChannelInfoContainer(null, null);
    private void refreshChannelInfo(Channel channel)
    {
      var is_tracker = peerCast.BroadcastID==channel.BroadcastID;
      var info = new ChannelInfoContainer(channel.ChannelInfo, channel.ChannelTrack);
      chanInfoChannelID.Text = channel.ChannelID.ToString("N").ToUpper();
      if (info.InfoChannelName!=channelInfo.InfoChannelName) chanInfoChannelName.Text = info.InfoChannelName;
      if (info.InfoGenre      !=channelInfo.InfoGenre)       chanInfoGenre.Text       = info.InfoGenre;
      if (info.InfoDesc       !=channelInfo.InfoDesc)        chanInfoDesc.Text        = info.InfoDesc;
      if (info.InfoContactURL !=channelInfo.InfoContactURL)  chanInfoContactURL.Text  = info.InfoContactURL;
      if (info.InfoComment    !=channelInfo.InfoComment)     chanInfoComment.Text     = info.InfoComment;
      if (info.InfoContentType!=channelInfo.InfoContentType) chanInfoContentType.Text = info.InfoContentType;
      if (info.InfoBitrate    !=channelInfo.InfoBitrate)     chanInfoBitrate.Text     = info.InfoBitrate;
      if (info.TrackAlbum     !=channelInfo.TrackAlbum)      chanTrackAlbum.Text      = info.TrackAlbum;
      if (info.TrackArtist    !=channelInfo.TrackArtist)     chanTrackArtist.Text     = info.TrackArtist;
      if (info.TrackTitle     !=channelInfo.TrackTitle)      chanTrackTitle.Text      = info.TrackTitle;
      if (info.TrackContactURL!=channelInfo.TrackContactURL) chanTrackContactURL.Text = info.TrackContactURL;
      chanInfoGenre.ReadOnly       = !is_tracker;
      chanInfoDesc.ReadOnly        = !is_tracker;
      chanInfoContactURL.ReadOnly  = !is_tracker;
      chanInfoComment.ReadOnly     = !is_tracker;
      chanTrackAlbum.ReadOnly      = !is_tracker;
      chanTrackArtist.ReadOnly     = !is_tracker;
      chanTrackTitle.ReadOnly      = !is_tracker;
      chanTrackContactURL.ReadOnly = !is_tracker;
      chanInfoUpdateButton.Enabled = is_tracker;
      channelInfo = info;
    }

    private void chanInfoUpdateButton_Click(object sender, EventArgs e)
    {
      var item = channelList.SelectedItem as ChannelListItem;
      if (item!=null) {
        var channel = item.Channel;
        var is_tracker = peerCast.BroadcastID==channel.BroadcastID;
        if (!is_tracker) return;
        var info = new AtomCollection(channel.ChannelInfo.Extra);
        if (info!=null) {
          info.SetChanInfoComment(chanInfoComment.Text);
          info.SetChanInfoGenre(chanInfoGenre.Text);
          info.SetChanInfoDesc(chanInfoDesc.Text);
          info.SetChanInfoURL(chanInfoContactURL.Text);
          info.SetChanInfoComment(chanInfoComment.Text);
          channel.ChannelInfo = new ChannelInfo(info);
        }
        var track = new AtomCollection(channel.ChannelTrack.Extra);
        if (track!=null) {
          track.SetChanTrackAlbum(chanTrackAlbum.Text);
          track.SetChanTrackCreator(chanTrackArtist.Text);
          track.SetChanTrackTitle(chanTrackTitle.Text);
          track.SetChanTrackURL(chanTrackContactURL.Text);
          channel.ChannelTrack = new ChannelTrack(track);
        }
      }
    }

    private void refreshOutputList(Channel channel)
    {
      var new_list = channel.OutputStreams;
      var old_list = outputList.Items.OfType<IOutputStream>();
      foreach (var os in old_list.Intersect(new_list)) {
        outputList.Items[outputList.Items.IndexOf(os)] = os;
      }
      foreach (var os in new_list.Except(old_list)) {
        outputList.Items.Add(os);
      }
      foreach (var os in old_list.Except(new_list)) {
        outputList.Items.Remove(os);
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
        connection.Stop();
      }
    }

    private void channelStart_Click(object sender, EventArgs args)
    {
      var dlg = new BroadcastDialog(peerCast);
      if (dlg.ShowDialog(this)==DialogResult.OK) {
        var channel_id = Utils.CreateChannelID(
          peerCast.BroadcastID,
          dlg.ChannelInfo.Name,
          dlg.ChannelInfo.Genre,
          dlg.StreamSource.ToString());
        var channel = peerCast.BroadcastChannel(
          dlg.YellowPage,
          channel_id,
          dlg.ChannelInfo,
          dlg.StreamSource,
          dlg.ContentReader);
        if (channel!=null) {
          channel.ChannelTrack = dlg.ChannelTrack;
        }
      }
    }

    private void showGUIMenuItem_Click(object sender, EventArgs e)
    {
      this.Show();
    }

    private void quitMenuItem_Click(object sender, EventArgs e)
    {
      Application.Exit();
    }

    private void versionInfoButton_Click(object sender, EventArgs e)
    {
      var dlg = new VersionInfoDialog(new string[] { "PeerCastStation" });
      dlg.ShowDialog();
    }

    class PortListItem
    {
      public OutputListener Listener { get; set; }

      public PortListItem(OutputListener listener)
      {
        Listener = listener;
      }

      public override string  ToString()
      {
        var addr = Listener.LocalEndPoint.Address.ToString();
        if (Listener.LocalEndPoint.Address.Equals(System.Net.IPAddress.Any))     addr = "IPv4 Any";
        if (Listener.LocalEndPoint.Address.Equals(System.Net.IPAddress.IPv6Any)) addr = "IPv6 Any";
        var local_accepts = "無し";
        if ((Listener.LocalOutputAccepts & ~OutputStreamType.Metadata)!=OutputStreamType.None) {
          var accepts = new List<string>();
          if ((Listener.LocalOutputAccepts & OutputStreamType.Interface)!=0) accepts.Add("操作");
          if ((Listener.LocalOutputAccepts & OutputStreamType.Relay)!=0) accepts.Add("リレー");
          if ((Listener.LocalOutputAccepts & OutputStreamType.Play)!=0) accepts.Add("視聴");
          local_accepts = String.Join(",", accepts.ToArray());
        }
        var global_accepts = "無し";
        if ((Listener.GlobalOutputAccepts & ~OutputStreamType.Metadata)!=OutputStreamType.None) {
          var accepts = new List<string>();
          if ((Listener.GlobalOutputAccepts & OutputStreamType.Interface)!=0) accepts.Add("操作");
          if ((Listener.GlobalOutputAccepts & OutputStreamType.Relay)!=0) accepts.Add("リレー");
          if ((Listener.GlobalOutputAccepts & OutputStreamType.Play)!=0) accepts.Add("視聴");
          global_accepts = String.Join(",", accepts.ToArray());
        }
        return String.Format(
          "{0}:{1} LAN:{2} WAN:{3}",
          addr,
          Listener.LocalEndPoint.Port,
          local_accepts,
          global_accepts);
      }
    }

    private void mainTab_SelectedIndexChanged(object sender, EventArgs e)
    {
      if (mainTab.SelectedTab==tabSettings) {
        maxRelays.Value            = peerCast.AccessController.MaxRelays;
        maxDirects.Value           = peerCast.AccessController.MaxPlays;
        maxRelaysPerChannel.Value  = peerCast.AccessController.MaxRelaysPerChannel;
        maxDirectsPerChannel.Value = peerCast.AccessController.MaxPlaysPerChannel;
        maxUpstreamRate.Value      = peerCast.AccessController.MaxUpstreamRate;
        portsList.Items.Clear();
        portsList.Items.AddRange(peerCast.OutputListeners.Select(listener => new PortListItem(listener)).ToArray());
        portGlobalRelay.Enabled     = false;
        portGlobalDirect.Enabled    = false;
        portGlobalInterface.Enabled = false;
        portLocalRelay.Enabled      = false;
        portLocalDirect.Enabled     = false;
        portLocalInterface.Enabled  = false;
        yellowPagesList.Items.Clear();
        yellowPagesList.Items.AddRange(peerCast.YellowPages.Select(yp => new YellowPageItem(yp)).ToArray());
      }
    }

    private void portsList_SelectedIndexChanged(object sender, EventArgs e)
    {
      var item = portsList.SelectedItem as PortListItem;
      if (item!=null) {
        var listener = item.Listener;
        portGlobalRelay.Checked     = (listener.GlobalOutputAccepts & OutputStreamType.Relay)!=0;
        portGlobalDirect.Checked    = (listener.GlobalOutputAccepts & OutputStreamType.Play)!=0;
        portGlobalInterface.Checked = (listener.GlobalOutputAccepts & OutputStreamType.Interface)!=0;
        portLocalRelay.Checked      = (listener.LocalOutputAccepts & OutputStreamType.Relay)!=0;
        portLocalDirect.Checked     = (listener.LocalOutputAccepts & OutputStreamType.Play)!=0;
        portLocalInterface.Checked  = (listener.LocalOutputAccepts & OutputStreamType.Interface)!=0;
        portGlobalRelay.Enabled     = true;
        portGlobalDirect.Enabled    = true;
        portGlobalInterface.Enabled = true;
        portLocalRelay.Enabled      = true;
        portLocalDirect.Enabled     = true;
        portLocalInterface.Enabled  = true;
      }
      else {
        portGlobalRelay.Enabled     = false;
        portGlobalDirect.Enabled    = false;
        portGlobalInterface.Enabled = false;
        portLocalRelay.Enabled      = false;
        portLocalDirect.Enabled     = false;
        portLocalInterface.Enabled  = false;
      }
    }

    private void portLocalRelay_CheckedChanged(object sender, EventArgs e)
    {
      var item = portsList.SelectedItem as PortListItem;
      if (item!=null) {
        var listener = item.Listener;
        if (portLocalRelay.Checked) {
          listener.LocalOutputAccepts |= OutputStreamType.Relay;
        }
        else {
          listener.LocalOutputAccepts &= ~OutputStreamType.Relay;
        }
        portsList.Items[portsList.Items.IndexOf(item)] = item;
      }
    }

    private void portGlobalRelay_CheckedChanged(object sender, EventArgs e)
    {
      var item = portsList.SelectedItem as PortListItem;
      if (item!=null) {
        var listener = item.Listener;
        if (portGlobalRelay.Checked) {
          listener.GlobalOutputAccepts |= OutputStreamType.Relay;
        }
        else {
          listener.GlobalOutputAccepts &= ~OutputStreamType.Relay;
        }
        portsList.Items[portsList.Items.IndexOf(item)] = item;
      }
    }

    private void portLocalDirect_CheckedChanged(object sender, EventArgs e)
    {
      var item = portsList.SelectedItem as PortListItem;
      if (item!=null) {
        var listener = item.Listener;
        if (portLocalDirect.Checked) {
          listener.LocalOutputAccepts |= OutputStreamType.Play;
        }
        else {
          listener.LocalOutputAccepts &= ~OutputStreamType.Play;
        }
        portsList.Items[portsList.Items.IndexOf(item)] = item;
      }
    }

    private void portGlobalDirect_CheckedChanged(object sender, EventArgs e)
    {
      var item = portsList.SelectedItem as PortListItem;
      if (item!=null) {
        var listener = item.Listener;
        if (portGlobalDirect.Checked) {
          listener.GlobalOutputAccepts |= OutputStreamType.Play;
        }
        else {
          listener.GlobalOutputAccepts &= ~OutputStreamType.Play;
        }
        portsList.Items[portsList.Items.IndexOf(item)] = item;
      }
    }

    private void portLocalInterface_CheckedChanged(object sender, EventArgs e)
    {
      var item = portsList.SelectedItem as PortListItem;
      if (item!=null) {
        var listener = item.Listener;
        if (portLocalInterface.Checked) {
          listener.LocalOutputAccepts |= OutputStreamType.Interface;
        }
        else {
          listener.LocalOutputAccepts &= ~OutputStreamType.Interface;
        }
        portsList.Items[portsList.Items.IndexOf(item)] = item;
      }
    }

    private void portGlobalInterface_CheckedChanged(object sender, EventArgs e)
    {
      var item = portsList.SelectedItem as PortListItem;
      if (item!=null) {
        var listener = item.Listener;
        if (portGlobalInterface.Checked) {
          listener.GlobalOutputAccepts |= OutputStreamType.Interface;
        }
        else {
          listener.GlobalOutputAccepts &= ~OutputStreamType.Interface;
        }
        portsList.Items[portsList.Items.IndexOf(item)] = item;
      }
    }

    private void portAddButton_Click(object sender, EventArgs args)
    {
      var dlg = new ListenerEditDialog();
      if (dlg.ShowDialog(this)==System.Windows.Forms.DialogResult.OK) {
        try {
          var listener = peerCast.StartListen(new System.Net.IPEndPoint(dlg.Address, dlg.Port), dlg.LocalAccepts, dlg.GlobalAccepts);
          portsList.Items.Add(new PortListItem(listener));
        }
        catch (System.Net.Sockets.SocketException) {
        }
      }
    }

    private void portRemoveButton_Click(object sender, EventArgs e)
    {
      var item = portsList.SelectedItem as PortListItem;
      if (item!=null) {
        peerCast.StopListen(item.Listener);
        portsList.Items.Clear();
        portsList.Items.AddRange(peerCast.OutputListeners.Select(listener => new PortListItem(listener)).ToArray());
      }
    }

    private void yellowPageAddButton_Click(object sender, EventArgs args)
    {
      var dlg = new YellowPagesEditDialog(peerCast);
      if (dlg.ShowDialog(this)==System.Windows.Forms.DialogResult.OK) {
        peerCast.AddYellowPage(dlg.Protocol, dlg.YPName, dlg.Uri);
        yellowPagesList.Items.Clear();
        yellowPagesList.Items.AddRange(peerCast.YellowPages.Select(yp => new YellowPageItem(yp)).ToArray());
      }
    }

    private void yellowPageRemoveButton_Click(object sender, EventArgs e)
    {
      var item = yellowPagesList.SelectedItem as YellowPageItem;
      if (item!=null) {
        peerCast.RemoveYellowPage(item.YellowPageClient);
        yellowPagesList.Items.Clear();
        yellowPagesList.Items.AddRange(peerCast.YellowPages.Select(yp => new YellowPageItem(yp)).ToArray());
      }
    }
  }
}
