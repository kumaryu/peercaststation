using System;
using System.Diagnostics;
using System.Windows.Forms;
using PeerCastStation.Core;
using PeerCastStation.WPF.Dialogs;
using PeerCastStation.WPF.Properties;

namespace PeerCastStation.WPF
{
  class NotifyIconManager
  {
    private readonly NotifyIcon notifyIcon;
    private readonly ToolStripItem versionCheck;
    private readonly ToolStripItem showGUI;
    private readonly ToolStripItem quit;
    private bool disposed;
    private VersionDescription newVersionInfo;

    public VersionDescription NewVersionInfo
    {
      set
      {
        newVersionInfo = value;
        notifyIcon.ShowBalloonTip(
          60000,
          "新しいバージョンがあります",
          newVersionInfo.Title,
          ToolTipIcon.Info);
      }
    }

    public event EventHandler CheckVersionClicked
    {
      add { versionCheck.Click += value; }
      remove { versionCheck.Click -= value; }
    }
    public event EventHandler ShowWindowClicked
    {
      add
      {
        showGUI.Click += value;
        notifyIcon.DoubleClick += value;
      }
      remove
      {
        showGUI.Click -= value;
        notifyIcon.DoubleClick -= value;
      }
    }
    public event EventHandler QuitClicked
    {
      add { quit.Click += value; }
      remove { quit.Click -= value; }
    }

    public NotifyIconManager(PeerCast peerCast)
    {
      versionCheck = VersionCheck;
      showGUI = ShowGUI;
      quit = Quit;
      notifyIcon = CreateNotifyIcon(peerCast);
      notifyIcon.BalloonTipClicked += (sender, e) =>
      {
        if (newVersionInfo == null)
          return;

        new UpdaterWindow()
        {
          DataContext = new UpdaterViewModel(newVersionInfo)
        }.Show();
      };
    }

    public void Run()
    {
      Application.Run();
    }

    ~NotifyIconManager()
    {
      Dispose(false);
    }

    public void Dispose()
    {
      GC.SuppressFinalize(this);
      Dispose(true);
    }

    protected virtual void Dispose(bool disposing)
    {
      if (disposed)
      {
        return;
      }
      disposed = true;
      if (disposing)
      {
        // マネージリソースの解放処理
        Application.Exit();
        notifyIcon.Dispose();
      }
      // アンマネージリソースの解放処理
    }

    private NotifyIcon CreateNotifyIcon(
      PeerCast peerCast)
    {
      var notifyIcon = new NotifyIcon();
      notifyIcon.Icon = Resources.peercaststation_small;
      notifyIcon.ContextMenuStrip = GetNotifyIconMenu(peerCast);
      notifyIcon.Visible = true;
      return notifyIcon;
    }

    private ContextMenuStrip GetNotifyIconMenu(
      PeerCast peerCast)
    {
      var notifyIconMenu = new System.Windows.Forms.ContextMenuStrip();
      notifyIconMenu.SuspendLayout();
      notifyIconMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
          versionCheck,
          ToolStripSeparator,
          GetShowHTMLUI(peerCast),
          showGUI,
          ToolStripSeparator,
          quit});
      notifyIconMenu.Name = "notifyIconMenu";
      notifyIconMenu.ShowImageMargin = false;
      notifyIconMenu.Size = new System.Drawing.Size(163, 104);
      notifyIconMenu.ResumeLayout(false);
      return notifyIconMenu;
    }

    private ToolStripItem VersionCheck
    {
      get
      {
        var versionCheckMenuItem = new System.Windows.Forms.ToolStripMenuItem();
        versionCheckMenuItem.Name = "versionCheckMenuItem";
        versionCheckMenuItem.Size = new System.Drawing.Size(162, 22);
        versionCheckMenuItem.Text = "アップデートのチェック(&U)";
        return versionCheckMenuItem;
      }
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
          Process.Start(String.Format("http://{0}/html/index.html", host));
        }
      };
      return showHTMLUIMenuItem;
    }

    private ToolStripItem ShowGUI
    {
      get
      {
        var showGUIMenuItem = new System.Windows.Forms.ToolStripMenuItem();
        showGUIMenuItem.AutoToolTip = true;
        showGUIMenuItem.Name = "showGUIMenuItem";
        showGUIMenuItem.Size = new System.Drawing.Size(162, 22);
        showGUIMenuItem.Text = "GUIを表示(&G)";
        showGUIMenuItem.ToolTipText = "PeerCastStationのGUIを表示します";
        return showGUIMenuItem;
      }
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
        return quitMenuItem;
      }
    }
  }
}
