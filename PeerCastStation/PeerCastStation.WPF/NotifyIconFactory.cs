using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Wpf = System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using PeerCastStation.Core;
using PeerCastStation.WPF.Properties;

namespace PeerCastStation.WPF
{
  class NotifyIconFactory
  {
    public NotifyIcon Create(
      PeerCast peerCast, Wpf.Window window, MainWindowViewModel viewModel)
    {
      var notifyIcon = new NotifyIcon();
      notifyIcon.Icon = Resources.peercaststation_small;
      notifyIcon.ContextMenuStrip = GetNotifyIconMenu(peerCast, window, viewModel);
      notifyIcon.Visible = true;
      notifyIcon.DoubleClick += (sender1, e1) =>
      {
        window.Show();
        window.Activate();
      };
      return notifyIcon;
    }

    private ContextMenuStrip GetNotifyIconMenu(
      PeerCast peerCast, Wpf.Window window, MainWindowViewModel viewModel)
    {
      var notifyIconMenu = new System.Windows.Forms.ContextMenuStrip();
      notifyIconMenu.SuspendLayout();
      notifyIconMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
          GetVersionCheck(viewModel),
          ToolStripSeparator,
          GetShowHTMLUI(peerCast),
          GetShowGUI(window),
          ToolStripSeparator,
          Quit});
      notifyIconMenu.Name = "notifyIconMenu";
      notifyIconMenu.ShowImageMargin = false;
      notifyIconMenu.Size = new System.Drawing.Size(163, 104);
      notifyIconMenu.ResumeLayout(false);
      return notifyIconMenu;
    }

    private ToolStripItem GetVersionCheck(MainWindowViewModel viewModel)
    {
      var versionCheckMenuItem = new System.Windows.Forms.ToolStripMenuItem();
      versionCheckMenuItem.Name = "versionCheckMenuItem";
      versionCheckMenuItem.Size = new System.Drawing.Size(162, 22);
      versionCheckMenuItem.Text = "アップデートのチェック(&U)";
      versionCheckMenuItem.Click += (sender, e) => viewModel.CheckVersion();
      return versionCheckMenuItem;
    }

    private ToolStripSeparator ToolStripSeparator
    {
      get
      {
        var toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
        toolStripSeparator1.Name = "toolStripSeparator1";
        toolStripSeparator1.Size = new System.Drawing.Size(159, 6);
        return toolStripSeparator1;
      }
    }

    private ToolStripItem GetShowHTMLUI(PeerCast peerCast)
    {
      var showHTMLUIMenuItem = new System.Windows.Forms.ToolStripMenuItem();
      showHTMLUIMenuItem.Name = "showHTMLUIMenuItem";
      showHTMLUIMenuItem.Size = new System.Drawing.Size(162, 22);
      showHTMLUIMenuItem.Text = "HTML UIを表示(&H)";
      showHTMLUIMenuItem.Click += (sender, e) =>
        {
          var listener = peerCast.FindListener(System.Net.IPAddress.Loopback, OutputStreamType.Interface);
          if (listener != null)
          {
            var endpoint = listener.LocalEndPoint;
            var host = endpoint.Address.Equals(System.Net.IPAddress.Any) ?
              String.Format("localhost:{0}", endpoint.Port) :
              endpoint.ToString();
            System.Diagnostics.Process.Start(String.Format("http://{0}/html/index.html", host));
          }
        };
      return showHTMLUIMenuItem;
    }

    private ToolStripItem GetShowGUI(Wpf.Window window)
    {
      var showGUIMenuItem = new System.Windows.Forms.ToolStripMenuItem();
      showGUIMenuItem.AutoToolTip = true;
      showGUIMenuItem.Name = "showGUIMenuItem";
      showGUIMenuItem.Size = new System.Drawing.Size(162, 22);
      showGUIMenuItem.Text = "GUIを表示(&G)";
      showGUIMenuItem.ToolTipText = "PeerCastStationのGUIを表示します";
      showGUIMenuItem.Click += (sender, e) =>
        {
          window.Show();
          window.Activate();
        };
      return showGUIMenuItem;
    }

    private ToolStripItem Quit
    {
      get
      {
        var quitMenuItem = new System.Windows.Forms.ToolStripMenuItem();
        quitMenuItem.AutoToolTip = true;
        quitMenuItem.Name = "quitMenuItem";
        quitMenuItem.Size = new System.Drawing.Size(162, 22);
        quitMenuItem.Text = "終了(&Q)";
        quitMenuItem.ToolTipText = "PeerCastStationを終了します";
        quitMenuItem.Click += (sender, e) => System.Windows.Application.Current.Shutdown();
        return quitMenuItem;
      }
    }
  }
}
