using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using PeerCastStation.Core;

namespace PeerCastStation.WPF
{
  class WindowManager : IDisposable
  {
    private readonly AutoResetEvent windowEvent = new AutoResetEvent(false);
    private readonly Application application;
    private readonly Window window;

    private bool disposed;
    private volatile bool isShow;

    public event EventHandler Closed
    {
      add { window.Closed += value; }
      remove { window.Closed -= value; }
    }

    public WindowManager(MainViewModel viewModel)
    {
      application = new Application();
      window = new MainWindow() { DataContext = viewModel };
      viewModel.SynchronizationContext = new DispatcherSynchronizationContext();
    }

    public void Run(bool isShowWindow)
    {
      if (!isShowWindow)
      {
        windowEvent.WaitOne();
        if (disposed)
          return;
      }

      isShow = true;
      application.Run(window);
      isShow = false;
    }

    public void ShowMainWindow()
    {
      if (!isShow)
      {
        windowEvent.Set();
        window.Dispatcher.BeginInvoke(new Action(
          () => window.Activate()
        ), null);
        return;
      }
      window.Dispatcher.BeginInvoke(new Action(() =>
      {
        window.Show();
        window.Activate();
      }), null);
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
        windowEvent.Set();
        if (isShow)
        {
          window.Dispatcher.Invoke(new Action(() =>
          {
            if (isShow)
              application.Shutdown();
          }));
        }
      }
      // アンマネージリソースの解放処理
    }
  }
}
