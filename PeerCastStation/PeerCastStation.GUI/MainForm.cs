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
    private PeerCastApplication application;
    private PeerCast peerCast;
    private LogWriter  guiWriter = new LogWriter(1000);
    private Timer      timer = new Timer();
    private VersionDescription newVersionInfo = null;
    private NotifyIcon notifyIcon;
    private AppCastReader versionChecker;

    public MainForm(PeerCastApplication app)
    {
      InitializeComponent();
      application = app;
      peerCast = app.PeerCast;
      if (PlatformID.Win32NT==Environment.OSVersion.Platform) {
        notifyIcon = new NotifyIcon(this.components);
        notifyIcon.Icon = this.Icon;
        notifyIcon.ContextMenuStrip = notifyIconMenu;
        notifyIcon.Visible = true;
        notifyIcon.DoubleClick += showGUIMenuItem_Click;
        notifyIcon.BalloonTipClicked += notifyIcon_BalloonTipClicked;
        versionChecker = new AppCastReader(
          new Uri(Settings.Default.UpdateURL, UriKind.Absolute),
          Settings.Default.CurrentVersion);
        versionChecker.NewVersionFound += versionChecker_Found;
        versionChecker.CheckVersion();
      }
      this.Visible = application.Settings.Get<GUISettings>().ShowWindowOnStartup;
    }

    public class LogLevelItem
    {
      public string Text { get; set; }
      public LogLevel Level { get; set; }
    }

    private void MainForm_Load(object sender, EventArgs e)
    {
      Logger.AddWriter(guiWriter);
      peerCast.ChannelAdded      += ChannelAdded;
      peerCast.ChannelRemoved    += ChannelRemoved;
      channelCleanerLimit.Value = ChannelCleaner.InactiveLimit / 60000;
      showWindowOnStartup.Checked = application.Settings.Get<GUISettings>().ShowWindowOnStartup;
      logLevelList.DataSource = new LogLevelItem[] {
        new LogLevelItem { Level=LogLevel.None,  Text="なし" },
        new LogLevelItem { Level=LogLevel.Error, Text="エラー" },
        new LogLevelItem { Level=LogLevel.Warn,  Text="エラーと警告" },
        new LogLevelItem { Level=LogLevel.Info,  Text="通知メッセージも含む" },
        new LogLevelItem { Level=LogLevel.Debug, Text="デバッグメッセージも含む" },
      };
      logLevelList.SelectedValueChanged += logLevelList_SelectedValueChanged;
      timer.Interval = 1000;
      timer.Enabled = true;
      timer.Tick += (s, args) => {
        UpdateStatus();
      };
      UpdateStatus();
    }

    private void notifyIcon_BalloonTipClicked(object sender, EventArgs args)
    {
      if (newVersionInfo!=null) {
        var dlg = new UpdaterDialog(newVersionInfo);
        dlg.Show();
      }
    }

    private void versionCheckMenuItem_Click(object sender, EventArgs e)
    {
      versionChecker.CheckVersion();
    }

    private void versionChecker_Found(object sender, NewVersionFoundEventArgs args)
    {
      newVersionInfo = args.VersionDescription;
      notifyIcon.ShowBalloonTip(
        60000,
        "新しいバージョンがあります",
        args.VersionDescription.Title,
        ToolTipIcon.Info);
    }

    private bool updating = false;
    private void UpdateStatus()
    {
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
      UpdateChannelList();
      UpdateLogText();
    }

    void UpdateLogText()
    {
      logText.Text = guiWriter.ToString();
    }

    private class ChannelListItem
    {
      public Channel Channel { get; private set; }
      public ChannelListItem(Channel channel)
      {
        this.Channel = channel;
      }

      public override string ToString()
      {
        var status = "UNKNOWN";
        switch (Channel.Status) {
        case SourceStreamStatus.Idle:       status = "IDLE";    break;
        case SourceStreamStatus.Connecting: status = "CONNECT"; break;
        case SourceStreamStatus.Searching:  status = "SEARCH";  break;
        case SourceStreamStatus.Receiving:  status = "RECEIVE"; break;
        case SourceStreamStatus.Error:      status = "ERROR";   break;
        }
        var relay_status = "　";
        if (Channel.Status==SourceStreamStatus.Idle || 
            Channel.Status==SourceStreamStatus.Error) {
          relay_status = "　";
        }
        else if (Channel.IsRelayFull) {
          if (Channel.LocalRelays>0) {
            relay_status = "○";
          }
          else if (!Channel.PeerCast.IsFirewalled.HasValue || Channel.PeerCast.IsFirewalled.Value) {
            relay_status = "×";
          }
          else {
            relay_status = "△";
          }
        }
        else {
          relay_status = "◎";
        }
        return String.Format(
          "{0} {1} {2}kbps ({3}/{4}) [{5}/{6}] {7}",
          relay_status,
          Channel.ChannelInfo.Name,
          Channel.ChannelInfo.Bitrate,
          Channel.TotalDirects,
          Channel.TotalRelays,
          Channel.LocalDirects,
          Channel.LocalRelays,
          status);
      }
    }

    private void UpdateChannelList()
    {
      var new_list = peerCast.Channels;
      var old_list = channelList.Items.OfType<ChannelListItem>().Select(item => item.Channel);
      updating = true;
      foreach (var channel in old_list.Intersect(new_list).ToArray()) {
        for (var i=0; i<channelList.Items.Count; i++) {
          if ((channelList.Items[i] as ChannelListItem).Channel==channel) {
            channelList.Items[i] = new ChannelListItem(channel);
            if (channelList.SelectedIndex==i) {
              UpdateChannelInfo(channel);
              UpdateOutputList(channel);
            }
            break;
          }
        }
      }
      foreach (var channel in new_list.Except(old_list).ToArray()) {
        channelList.Items.Add(new ChannelListItem(channel));
      }
      foreach (var channel in old_list.Except(new_list).ToArray()) {
        for (var i=0; i<channelList.Items.Count; i++) {
          if ((channelList.Items[i] as ChannelListItem).Channel==channel) {
            channelList.Items.RemoveAt(i);
            break;
          }
        }
      }
      updating = false;
    }

    private void ChannelAdded(object sender, PeerCastStation.Core.ChannelChangedEventArgs e)
    {
      this.BeginInvoke(new Action(() => {
        UpdateChannelList();
      }));
    }

    private void ChannelRemoved(object sender, PeerCastStation.Core.ChannelChangedEventArgs e)
    {
      this.BeginInvoke(new Action(() => {
        UpdateChannelList();
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

    private void applySettings_Click(object sender, EventArgs e)
    {
      peerCast.AccessController.MaxRelays           = (int)maxRelays.Value;
      peerCast.AccessController.MaxPlays            = (int)maxDirects.Value;
      peerCast.AccessController.MaxRelaysPerChannel = (int)maxRelaysPerChannel.Value;
      peerCast.AccessController.MaxPlaysPerChannel  = (int)maxDirectsPerChannel.Value;
      peerCast.AccessController.MaxUpstreamRate     = (int)maxUpstreamRate.Value;
      ChannelCleaner.InactiveLimit = (int)(channelCleanerLimit.Value * 60000);
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
      application.Settings.Get<GUISettings>().ShowWindowOnStartup = showWindowOnStartup.Checked;
      Logger.RemoveWriter(guiWriter);
      peerCast.ChannelAdded   -= ChannelAdded;
      peerCast.ChannelRemoved -= ChannelRemoved;
      Application.ExitThread();
    }

    private void channelList_SelectedIndexChanged(object sender, EventArgs e)
    {
      if (updating) return;
      var item = channelList.SelectedItem as ChannelListItem;
      if (item!=null) {
        UpdateTree(item.Channel);
        UpdateChannelInfo(item.Channel);
        UpdateOutputList(item.Channel);
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

    private void AddRelayTreeNode(
      TreeNodeCollection tree_nodes,
      IEnumerable<Utils.HostTreeNode> nodes,
      HashSet<Guid> added)
    {
      foreach (var node in nodes) {
        if (added.Contains(node.Host.SessionID)) continue;
        added.Add(node.Host.SessionID);
        var endpoint = (node.Host.GlobalEndPoint!=null && node.Host.GlobalEndPoint.Port!=0) ? node.Host.GlobalEndPoint : node.Host.LocalEndPoint;
        string version = "";
        var pcp = node.Host.Extra.GetHostVersion();
        if (pcp.HasValue) {
          version += pcp.Value.ToString();
        }
        var vp = node.Host.Extra.GetHostVersionVP();
        if (vp.HasValue) {
          version += " VP" + vp.Value.ToString();
        }
        var ex    = node.Host.Extra.GetHostVersionEXPrefix();
        var exnum = node.Host.Extra.GetHostVersionEXNumber();
        if (ex!=null && exnum.HasValue) {
          try {
            version += " " + System.Text.Encoding.UTF8.GetString(ex) + exnum.ToString();
          }
          catch (ArgumentException) {
            //ignore
          }
        }
        var nodeinfo = String.Format(
          "{0} ({1}/{2}) {3}{4}{5} {6}",
          endpoint,
          node.Host.DirectCount,
          node.Host.RelayCount,
          node.Host.IsFirewalled ? "0" : "",
          node.Host.IsRelayFull  ? "-" : "",
          node.Host.IsReceiving  ? "" : "B",
          version);
        var tree_node = tree_nodes.Add(nodeinfo);
        AddRelayTreeNode(tree_node.Nodes, node.Children, added);
      }
    }

    private void UpdateTree(Channel channel)
    {
      relayTree.BeginUpdate();
      relayTree.Nodes.Clear();
      var roots = channel.CreateHostTree().Where(node => node.Host.SessionID==peerCast.SessionID);
      AddRelayTreeNode(relayTree.Nodes, roots, new HashSet<Guid>());
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
      public string TrackGenre { get; private set; }
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
          TrackGenre      = track.Genre;
          TrackContactURL = track.URL;
        }
        else {
          TrackAlbum      = "";
          TrackArtist     = "";
          TrackTitle      = "";
          TrackGenre      = "";
          TrackContactURL = "";
        }
      }
    }

    private ChannelInfoContainer channelInfo = new ChannelInfoContainer(null, null);
    private void UpdateChannelInfo(Channel channel)
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
      if (info.TrackGenre     !=channelInfo.TrackGenre)      chanTrackGenre.Text      = info.TrackGenre;
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
          track.SetChanTrackGenre(chanTrackGenre.Text);
          track.SetChanTrackURL(chanTrackContactURL.Text);
          channel.ChannelTrack = new ChannelTrack(track);
        }
      }
    }

    public interface IChannelConnectionItem
    {
      void Disconnect();
      void Reconnect();
      bool IsDisconnectable { get; }
      bool IsReconnectable { get; }
    }

    public class ChannelConnectionSourceItem : IChannelConnectionItem
    {
      private ISourceStream sourceStream;
      public ChannelConnectionSourceItem(ISourceStream ss)
      {
        sourceStream = ss;
      }

      public void Disconnect()
      {
        throw new InvalidOperationException();
      }

      public void Reconnect()
      {
        throw new InvalidOperationException();
      }

      public bool IsDisconnectable
      {
        get { return false; }
      }

      public bool IsReconnectable
      {
        get { return false; }
      }

      public override string ToString()
      {
        return sourceStream.ToString();
      }
    }

    public class ChannelConnectionOutputItem : IChannelConnectionItem
    {
      private IOutputStream outputStream;
      public ChannelConnectionOutputItem(IOutputStream os)
      {
        outputStream = os;
      }

      public void Disconnect()
      {
        outputStream.Stop();
      }

      public void Reconnect()
      {
        throw new InvalidOperationException();
      }

      public bool IsDisconnectable
      {
        get { return true; }
      }

      public bool IsReconnectable
      {
        get { return false; }
      }

      public override string ToString()
      {
        return outputStream.ToString();
      }
    }

    public class ChannelConnectionAnnouncingItem : IChannelConnectionItem
    {
      private IAnnouncingChannel announcingChannel;
      public ChannelConnectionAnnouncingItem(IAnnouncingChannel ac)
      {
        announcingChannel = ac;
      }

      public void Disconnect()
      {
        throw new InvalidOperationException();
      }

      public void Reconnect()
      {
        announcingChannel.YellowPage.RestartAnnounce(announcingChannel);
      }

      public bool IsDisconnectable
      {
        get { return false; }
      }

      public bool IsReconnectable
      {
        get { return true; }
      }

      public override string ToString()
      {
        var yp = announcingChannel.YellowPage;
        return String.Format("COUT {0}({1}) {2}", yp.Name, yp.Protocol, announcingChannel.Status);
      }
    }

    private void UpdateOutputList(Channel channel)
    {
      var idx = connectionList.SelectedIndex;
      connectionList.BeginUpdate();
      connectionList.Items.Clear();
      connectionList.Items.Add(new ChannelConnectionSourceItem(channel.SourceStream));
      var announcings = peerCast.YellowPages
        .Select(yp => yp.AnnouncingChannels.FirstOrDefault(c => c.Channel.ChannelID==channel.ChannelID))
        .Where(c => c!=null);
      foreach (var announcing in announcings.ToArray()) {
        connectionList.Items.Add(new ChannelConnectionAnnouncingItem(announcing));
      }
      foreach (var os in channel.OutputStreams.ToArray()) {
        connectionList.Items.Add(new ChannelConnectionOutputItem(os));
      }
      connectionList.SelectedIndex = Math.Min(idx, connectionList.Items.Count-1);
      connectionList.EndUpdate();
    }

    private void logLevelList_SelectedValueChanged(object sender, EventArgs e)
    {

    }

    private void logToFileCheck_CheckedChanged(object sender, EventArgs e)
    {
      if (logToFileCheck.Checked) {
        Logger.OutputTarget |= LoggerOutputTarget.File;
      }
      else {
        Logger.OutputTarget &= ~LoggerOutputTarget.File;
      }
    }

    private void logToConsoleCheck_CheckedChanged(object sender, EventArgs e)
    {
      if (logToConsoleCheck.Checked) {
        Logger.OutputTarget |= LoggerOutputTarget.Console;
      }
      else {
        Logger.OutputTarget &= ~LoggerOutputTarget.Console;
      }
    }

    private void logToGUICheck_CheckedChanged(object sender, EventArgs e)
    {
      if (logToGUICheck.Checked) {
        Logger.OutputTarget |= LoggerOutputTarget.UserInterface;
      }
      else {
        Logger.OutputTarget &= ~LoggerOutputTarget.UserInterface;
      }
    }

    private void logFileNameText_Validated(object sender, EventArgs e)
    {
      Logger.LogFileName = logFileNameText.Text;
    }

    private void selectLogFileName_Click(object sender, EventArgs e)
    {
      if (logSaveFileDialog.ShowDialog(this)==DialogResult.OK) {
        logFileNameText.Text = logSaveFileDialog.FileName;
        Logger.LogFileName = logFileNameText.Text;
      }
    }

    private void logClearButton_Click(object sender, EventArgs e)
    {
      guiWriter.Clear();
      logText.ResetText();
    }

    private void channelBump_Click(object sender, EventArgs e)
    {
      var item = channelList.SelectedItem as ChannelListItem;
      if (item!=null) {
        item.Channel.Reconnect();
      }
    }

    private void connectionClose_Click(object sender, EventArgs e)
    {
      var connection = connectionList.SelectedItem as IChannelConnectionItem;
      if (connection!=null && connection.IsDisconnectable) {
        connection.Disconnect();
      }
    }

    private void connectionReconnect_Click(object sender, EventArgs e)
    {
      var connection = connectionList.SelectedItem as IChannelConnectionItem;
      if (connection!=null && connection.IsReconnectable) {
        connection.Reconnect();
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
          dlg.ContentReaderFactory);
        if (channel!=null) {
          channel.ChannelTrack = dlg.ChannelTrack;
        }
      }
    }

    private void showGUIMenuItem_Click(object sender, EventArgs e)
    {
      this.Show();
      this.Activate();
    }

    private void quitMenuItem_Click(object sender, EventArgs e)
    {
      Application.Exit();
    }

    private void versionInfoButton_Click(object sender, EventArgs e)
    {
      var dlg = new VersionInfoDialog(PeerCastApplication.Current);
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
      if (mainTab.SelectedTab==tabLog) {
        logFileNameText.Text = Logger.LogFileName;
        logLevelList.SelectedValue = Logger.Level;
        logToConsoleCheck.Checked = (Logger.OutputTarget & LoggerOutputTarget.Console)!=0;
        logToGUICheck.Checked     = (Logger.OutputTarget & LoggerOutputTarget.UserInterface)!=0;
        logToFileCheck.Checked    = (Logger.OutputTarget & LoggerOutputTarget.File)!=0;
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

    private void relayTreeUpdate_Click(object sender, EventArgs e)
    {
      var item = channelList.SelectedItem as ChannelListItem;
      if (item!=null) {
        UpdateTree(item.Channel);
      }
      else {
        relayTree.Nodes.Clear();
      }
    }

    private void showHTMLUIMenuItem_Click(object sender, EventArgs e)
    {
      var listener = peerCast.FindListener(System.Net.IPAddress.Loopback, OutputStreamType.Interface);
      if (listener!=null) {
        var endpoint = listener.LocalEndPoint;
        var host = endpoint.Address.Equals(System.Net.IPAddress.Any) ?
          String.Format("localhost:{0}", endpoint.Port) :
          endpoint.ToString();
        System.Diagnostics.Process.Start(String.Format("http://{0}/html/index.html", host));
      }
    }

    private void HelpMenuItem_Click(object sender, EventArgs e)
    {
      var listener = peerCast.FindListener(System.Net.IPAddress.Loopback, OutputStreamType.Interface);
      if (listener!=null) {
        var endpoint = listener.LocalEndPoint;
        var host = endpoint.Address.Equals(System.Net.IPAddress.Any) ?
          String.Format("localhost:{0}", endpoint.Port) :
          endpoint.ToString();
        System.Diagnostics.Process.Start(String.Format("http://{0}/help/index.html", host));
      }
    }

    private void openContactURLMenu_Click(object sender, EventArgs e)
    {
      var item = channelList.SelectedItem as ChannelListItem;
      if (item!=null) {
        var url = item.Channel.ChannelInfo.URL;
        Uri uri;
        if (!String.IsNullOrEmpty(url) && Uri.TryCreate(url, UriKind.Absolute, out uri)) {
          System.Diagnostics.Process.Start(uri.ToString());
        }
      }
    }

    private void copyStreamURLMenu_Click(object sender, EventArgs e)
    {
      var item = channelList.SelectedItem as ChannelListItem;
      if (item!=null && peerCast.OutputListeners.Count>0) {
        var channel_id = item.Channel.ChannelID;
        var endpoint = peerCast.OutputListeners[0].LocalEndPoint;
        var ext = item.Channel.ChannelInfo.ContentExtension;
        string url;
        if (endpoint.Address.Equals(System.Net.IPAddress.Any)) {
          url = String.Format("http://localhost:{0}/stream/{1}{2}", endpoint.Port, channel_id.ToString("N"), ext);
        }
        else {
          url = String.Format("http://{0}/pls/{1}{2}", endpoint.ToString(), channel_id.ToString("N"), ext);
        }
        Clipboard.SetText(url);
      }
    }

    private void copyContactURLMenu_Click(object sender, EventArgs e)
    {
      var item = channelList.SelectedItem as ChannelListItem;
      if (item!=null) {
        var url = item.Channel.ChannelInfo.URL;
        Uri uri;
        if (!String.IsNullOrEmpty(url) && Uri.TryCreate(url, UriKind.Absolute, out uri)) {
          Clipboard.SetText(uri.ToString());
        }
      }
    }

    private void connectionList_SelectedIndexChanged(object sender, EventArgs e)
    {
      var item = connectionList.SelectedItem as IChannelConnectionItem;
      connectionClose.Enabled     = item!=null && item.IsDisconnectable;
      connectionReconnect.Enabled = item!=null && item.IsReconnectable;
    }
  }
}
