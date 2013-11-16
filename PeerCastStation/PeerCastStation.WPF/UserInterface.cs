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
  [Plugin]
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
    MainViewModel viewModel;
    Thread notifyIconThread;
    NotifyIconManager notifyIconManager;
    Thread mainThread;
    private AppCastReader versionChecker;
    override protected void OnStart()
    {
      notifyIconThread = new Thread(() => {
        notifyIconManager = new NotifyIconManager(Application.PeerCast);
        notifyIconManager.CheckVersionClicked += (sender, e) => versionChecker.CheckVersion();
        notifyIconManager.QuitClicked         += (sender, e) => Application.Stop();
        notifyIconManager.ShowWindowClicked   += (sender, e) => {
          if (mainWindow!=null) {
            mainWindow.Dispatcher.Invoke(new Action(() => {
              mainWindow.Show();
              if (mainWindow.WindowState==WindowState.Minimized) {
                mainWindow.WindowState = WindowState.Normal;
              }
              mainWindow.Activate();
            }));
          }
        };
        versionChecker = new AppCastReader(
          new Uri(Settings.Default.UpdateURL, UriKind.Absolute),
          Settings.Default.CurrentVersion);
        versionChecker.NewVersionFound += (sender, e) => {
          notifyIconManager.NewVersionInfo = e.VersionDescription;
        };
        versionChecker.CheckVersion();
        notifyIconManager.Run();
      });
      notifyIconThread.SetApartmentState(ApartmentState.STA);
      notifyIconThread.Start();

      mainThread = new Thread(() => {
        var app = new Application();
        viewModel = new MainViewModel(Application);
        var settings = Application.Settings.Get<WPFSettings>();
        mainWindow = new MainWindow(viewModel);
        if (settings.ShowWindowOnStartup) mainWindow.Show();
        app.Run();
        viewModel.Dispose();
      });
      mainThread.Name = "WPF UI Thread";
      mainThread.SetApartmentState(ApartmentState.STA);
      mainThread.Start();
    }

    override protected void OnStop()
    {
      if (mainWindow!=null) {
        mainWindow.Dispatcher.Invoke(new Action(() => {
          System.Windows.Application.Current.Shutdown();
        }));
      }
      notifyIconManager.Dispose();
      mainThread.Join();
      notifyIconThread.Join();
    }

    public void ShowNotificationMessage(NotificationMessage msg)
    {
      if (notifyIconManager==null) return;
      notifyIconManager.ShowNotificationMessage(msg);
    }
  }
}
