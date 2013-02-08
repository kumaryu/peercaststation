using System;
using System.Windows;
using System.Windows.Forms;
using PeerCastStation.Core;
using PeerCastStation.WPF.Dialogs;
using PeerCastStation.WPF.Properties;

namespace PeerCastStation.WPF
{
  class WindowManager : IDisposable
  {
    private bool disposed;
    private Window window;
    private System.Windows.Forms.NotifyIcon notifyIcon;
    private VersionDescription newVersionInfo;

    private volatile bool isShow;
    public bool IsShow { get { return isShow; } }

    public void ShowMainWindow(PeerCastApplication application, Settings settings)
    {
      window = new MainWindow();
      var viewModel = new MainWindowViewModel(
        application, settings.UpdateURL, settings.CurrentVersion);
      viewModel.NewVersionFound += (sender, e) =>
      {
        newVersionInfo = e.VersionDescription;
        notifyIcon.ShowBalloonTip(
          60000,
          "新しいバージョンがあります",
          e.VersionDescription.Title,
          ToolTipIcon.Info);
      };
      Load(settings, viewModel);
      window.DataContext = viewModel;
      window.Closing += (sender, e) => Save(viewModel, settings);
      window.Closed += (sender, e) =>
        {
          notifyIcon.Dispose();
          application.Stop();
        };
      notifyIcon = new NotifyIconFactory().Create(application.PeerCast, window, viewModel);
      notifyIcon.BalloonTipClicked += (sender1, e1) =>
        {
          if (newVersionInfo != null)
          {
            var dlg = new UpdaterWindow();
            dlg.DataContext = new UpdaterViewModel(newVersionInfo);
            dlg.Show();
          }
        };
      isShow = true;
      new System.Windows.Application().Run(window);
      isShow = false;
    }

    private void Load(Settings settings, MainWindowViewModel mainWindow)
    {
      var log = mainWindow.Log;
      log.LogLevel = settings.LogLevel;
      log.IsOutputToGui = settings.LogToGUI;
      log.IsOutputToConsole = settings.LogToConsole;
      log.IsOutputToFile = settings.LogToFile;
      log.OutputFileName = settings.LogFileName;
      mainWindow.Setting.OtherSetting.IsShowWindowOnStartup = settings.ShowWindowOnStartup;
    }

    private void Save(MainWindowViewModel mainWindow, Settings settings)
    {
      var log = mainWindow.Log;
      settings.LogLevel = log.LogLevel;
      settings.LogToGUI = log.IsOutputToGui;
      settings.LogToConsole = log.IsOutputToConsole;
      settings.LogToFile = log.IsOutputToFile;
      settings.LogFileName = log.OutputFileName;
      settings.ShowWindowOnStartup = mainWindow.Setting.OtherSetting.IsShowWindowOnStartup;
      settings.Save();
    }

    protected void ThrowExceptionIfDisposed()
    {
      if (disposed)
      {
        throw new ObjectDisposedException(GetType().ToString());
      }
    }

    ~WindowManager()
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
        if (window != null && isShow)
        {
          window.Dispatcher.Invoke(new Action(() =>
          {
            if (isShow)
              window.Close();
          }));
        }
      }
      // アンマネージリソースの解放処理
    }
  }
}
