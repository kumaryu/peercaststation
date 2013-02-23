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
