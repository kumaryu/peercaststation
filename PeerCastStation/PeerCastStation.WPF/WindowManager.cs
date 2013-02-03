using System;
using System.Windows;
using PeerCastStation.Core;
using PeerCastStation.WPF.Properties;

namespace PeerCastStation.WPF
{
  class WindowManager : IDisposable
  {
    private bool disposed;
    private Window window;
    private volatile bool isShow;
    public bool IsShow { get { return isShow; } }

    public void ShowMainWindow(PeerCastApplication application, Settings settings)
    {
      window = CreateWindow(application, settings);

      isShow = true;
      window.ShowDialog();
      isShow = false;
    }

    private MainWindow CreateWindow(PeerCastApplication application, Settings settings)
    {
      var window = new MainWindow();
      var viewModel = new MainWindowViewModel(application);
      Load(settings, viewModel);
      window.DataContext = viewModel;
      window.Closing += (sender, e) => Save(viewModel, settings);
      window.Closed += (sender, e) => application.Stop();
      return window;
    }

    private void Load(Settings settings, MainWindowViewModel mainWindow)
    {
      var log = mainWindow.Log;
      log.LogLevel = settings.LogLevel;
      log.IsOutputToGui = settings.LogToGUI;
      log.IsOutputToConsole = settings.LogToConsole;
      log.IsOutputToFile = settings.LogToFile;
      log.OutputFileName = settings.LogFileName;
    }

    private void Save(MainWindowViewModel mainWindow, Settings settings)
    {
      var log = mainWindow.Log;
      settings.LogLevel = log.LogLevel;
      settings.LogToGUI = log.IsOutputToGui;
      settings.LogToConsole = log.IsOutputToConsole;
      settings.LogToFile = log.IsOutputToFile;
      settings.LogFileName = log.OutputFileName;
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
