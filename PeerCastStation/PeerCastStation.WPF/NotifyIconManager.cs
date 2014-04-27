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
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using PeerCastStation.Core;
using PeerCastStation.UI;
using PeerCastStation.WPF.Dialogs;
using PeerCastStation.WPF.Properties;

namespace PeerCastStation.WPF
{
  class NotifyIconManager
  {
    private readonly NotifyIcon notifyIcon;
    private bool disposed;
    private IEnumerable<VersionDescription> newVersionInfo;

    public void NotifyNewVersions(IEnumerable<VersionDescription> new_versions)
    {
      newVersionInfo = new_versions;
      notifyIcon.ShowBalloonTip(
        60000,
        "新しいバージョンがあります",
        newVersionInfo.First().Title,
        ToolTipIcon.Info);
    }

    private PeerCastAppViewModel appViewModel;
    private UserInterface owner;
    public NotifyIconManager(PeerCastAppViewModel app_viewmodel, UserInterface owner)
    {
      this.appViewModel = app_viewmodel;
      this.owner = owner;
      notifyIcon = new NotifyIcon();
      notifyIcon.Icon = Resources.peercaststation_small;
      notifyIcon.ContextMenuStrip = CreateNotifyIconMenu();
      notifyIcon.Text = appViewModel.Version;
      notifyIcon.Visible = true;
      notifyIcon.DoubleClick += (sender, args) => this.owner.ShowWindow();
      notifyIcon.BalloonTipClicked += (sender, e) => {
        if (newVersionInfo==null) return;
        new UpdaterWindow() { DataContext=new UpdaterViewModel(newVersionInfo) }.Show();
      };
    }

    public void Run()
    {
      Application.Run();
    }

    public void Dispose()
    {
      if (disposed) return;
      disposed = true;
      Application.Exit();
      notifyIcon.Dispose();
    }

    public void ShowNotificationMessage(NotificationMessage msg)
    {
      if (notifyIcon==null) return;
      var timeout = 5000;
      var icon = ToolTipIcon.Info;
      switch (msg.Type) {
      case NotificationMessageType.Normal:  icon = ToolTipIcon.None; break;
      case NotificationMessageType.Info:    icon = ToolTipIcon.Info; break;
      case NotificationMessageType.Warning: icon = ToolTipIcon.Warning; break;
      case NotificationMessageType.Error:   icon = ToolTipIcon.Error; break;
      }
      newVersionInfo = null;
      notifyIcon.ShowBalloonTip(
        timeout,
        msg.Title,
        msg.Message,
        icon);
    }

    private ContextMenuStrip CreateNotifyIconMenu()
    {
      var notifyIconMenu = new System.Windows.Forms.ContextMenuStrip();
      notifyIconMenu.SuspendLayout();
      notifyIconMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
          CreateShowHelp(),
          CreateVersionCheck(),
          CreateSeparator(),
          CreateShowHTMLUI(),
          CreateShowGUI(),
          CreateSeparator(),
          CreateQuit(),
      });
      notifyIconMenu.ShowImageMargin = false;
      notifyIconMenu.ResumeLayout(false);
      return notifyIconMenu;
    }

    private ToolStripItem CreateVersionCheck()
    {
      var item = new System.Windows.Forms.ToolStripMenuItem();
      item.Text = "アップデートのチェック(&U)";
      item.Click += (sender, args) => this.owner.CheckVersion();
      return item;
    }

    private ToolStripSeparator CreateSeparator()
    {
      return new System.Windows.Forms.ToolStripSeparator();
    }

    private ToolStripItem CreateShowHelp()
    {
      var item = new System.Windows.Forms.ToolStripMenuItem();
      item.Text = "ヘルプ(&H)";
      item.ToolTipText = "PeerCastStationのヘルプを表示します";
      item.Click += (sender, e) => appViewModel.OpenHelp();
      return item;
    }

    private ToolStripItem CreateShowHTMLUI()
    {
      var item = new System.Windows.Forms.ToolStripMenuItem();
      item.Text = "HTML UIを表示(&U)";
      item.ToolTipText = "PeerCastStationのブラウザインターフェースを表示します";
      item.Click += (sender, e) => appViewModel.OpenBrowserUI();
      return item;
    }

    private ToolStripItem CreateShowGUI()
    {
      var item = new System.Windows.Forms.ToolStripMenuItem();
      item.AutoToolTip = true;
      item.Text = "GUIを表示(&G)";
      item.ToolTipText = "PeerCastStationのGUIを表示します";
      item.Click += (sender, args) => {
        this.owner.ShowWindow();
      };
      return item;
    }

    private ToolStripItem CreateQuit()
    {
      var item = new System.Windows.Forms.ToolStripMenuItem();
      item.AutoToolTip = true;
      item.Text        = "終了(&Q)";
      item.ToolTipText = "PeerCastStationを終了します";
      item.Click += (sender, args) => appViewModel.Quit();
      return item;
    }

  }

}
