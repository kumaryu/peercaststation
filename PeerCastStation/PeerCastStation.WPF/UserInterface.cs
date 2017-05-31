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
using System;
using System.Threading;
using System.Windows;
using PeerCastStation.Core;
using PeerCastStation.WPF.Properties;

namespace PeerCastStation.WPF
{
  [Plugin(PluginType.GUI)]
  public class UserInterface
    : PluginBase,
      IUserInterfacePlugin
  {
    override public string Name { get { return "GUI by WPF"; } }
    public override bool IsUsable {
      get {
        switch (Environment.OSVersion.Platform) {
        case PlatformID.Win32NT:
          return true;
        default:
          return false;
        }
      }
    }

    Logger logger = new Logger(typeof(UserInterface));
    MainWindow mainWindow;
    PeerCastAppViewModel appViewModel;
    Thread notifyIconThread;
    NotifyIconManager notifyIconManager;
    Thread mainThread;
    private PeerCastStation.UI.Updater versionChecker;
    private Timer versionCheckTimer;
    override protected void OnStart()
    {
      appViewModel = new PeerCastAppViewModel(Application);
      versionChecker = new PeerCastStation.UI.Updater();
      notifyIconThread = new Thread(() => {
        notifyIconManager = new NotifyIconManager(appViewModel, this);
        versionChecker.NewVersionFound += (sender, e) => {
          notifyIconManager.NotifyNewVersions(e.VersionDescriptions);
        };
        notifyIconManager.Run();
      });
      notifyIconThread.SetApartmentState(ApartmentState.STA);
      notifyIconThread.Start();
      versionCheckTimer = new Timer(OnVersionCheckTimer, null, 1000, 1000*3600*24);

      mainThread = new Thread(() => {
        var app = new Application();
        var settings = Application.Settings.Get<WPFSettings>();
        mainWindow = new MainWindow(appViewModel);
        if (settings.ShowWindowOnStartup) mainWindow.Show();
        app.Run();
      });
      mainThread.Name = "WPF UI Thread";
      mainThread.SetApartmentState(ApartmentState.STA);
      mainThread.Start();
    }

    public void ShowWindow()
    {
      if (mainWindow!=null) {
        mainWindow.Dispatcher.Invoke(new Action(() => {
          mainWindow.Show();
          if (mainWindow.WindowState==WindowState.Minimized) {
            mainWindow.WindowState = WindowState.Normal;
          }
          mainWindow.Activate();
        }));
      }
    }

    public void CheckVersion()
    {
      versionChecker.CheckVersion();
    }

    private void OnVersionCheckTimer(object state)
    {
      versionChecker.CheckVersion();
    }

    override protected void OnStop()
    {
      var timer_wait = new AutoResetEvent(false);
      versionCheckTimer.Dispose(timer_wait);
      timer_wait.WaitOne();
      if (mainWindow!=null) {
        mainWindow.Dispatcher.Invoke(new Action(() => {
          System.Windows.Application.Current.Shutdown();
        }));
      }
      notifyIconManager.Dispose();
      mainThread.Join();
      notifyIconThread.Join();
      appViewModel.Dispose();
    }

    public void ShowNotificationMessage(NotificationMessage msg)
    {
      if (notifyIconManager==null) return;
      notifyIconManager.ShowNotificationMessage(msg);
    }
  }
}
