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
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using PeerCastStation.WPF.Dialogs;

namespace PeerCastStation.WPF
{
  /// <summary>
  /// MainWindow.xaml の相互作用ロジック
  /// </summary>
  internal partial class MainWindow : Window
  {
    private bool IsFinite(double value)
    {
      return !Double.IsNaN(value) && !Double.IsInfinity(value);
    }

    private DispatcherTimer timer;
    private System.Windows.Interop.WindowInteropHelper? hwnd;
    private System.Windows.Interop.HwndSource? nativeSource;
    public MainWindow(PeerCastAppViewModel viewmodel)
    {
      InitializeComponent();
      this.Loaded += MainWindow_Loaded;
      this.CommandBindings.Add(new System.Windows.Input.CommandBinding(PeerCastCommands.OpenSettings, OnOpenSettings));
      this.CommandBindings.Add(new System.Windows.Input.CommandBinding(PeerCastCommands.ShowLogs, OnShowLogs));
      this.CommandBindings.Add(new System.Windows.Input.CommandBinding(PeerCastCommands.About, OnAbout));
      this.CommandBindings.Add(new System.Windows.Input.CommandBinding(PeerCastCommands.OpenBrowserUI, OnOpenBrowserUI));
      this.CommandBindings.Add(new System.Windows.Input.CommandBinding(PeerCastCommands.OpenHelp, OnOpenHelp));
      this.CommandBindings.Add(new System.Windows.Input.CommandBinding(PeerCastCommands.Quit, OnQuit));
      timer = new DispatcherTimer(
        TimeSpan.FromSeconds(1),
        DispatcherPriority.Normal,
        (sender, e) => viewmodel.UpdateStatus(),
        Application.Current.Dispatcher);
      this.DataContext = viewmodel;

    }

    private bool InitWindow()
    {
      if (hwnd?.Handle!=null && hwnd?.Handle!=IntPtr.Zero) {
        return true;
      }
      else {
        hwnd = new System.Windows.Interop.WindowInteropHelper(this);
        if (hwnd.Handle!=IntPtr.Zero) {
          nativeSource = System.Windows.Interop.HwndSource.FromHwnd(hwnd.Handle);
          nativeSource.AddHook(OnWindowMessage);
          var dpi = Screen.GetDpiForWindow(hwnd);
          var settings = PeerCastStation.Core.PeerCastApplication.Current!.Settings.Get<WPFSettings>();
          var rect = new Rect(
            IsFinite(settings.WindowLeft)   ? settings.WindowLeft   : (this.Left*dpi/96.0),
            IsFinite(settings.WindowTop)    ? settings.WindowTop    : (this.Top*dpi/96.0),
            IsFinite(settings.WindowWidth)  ? settings.WindowWidth  : (this.Width*dpi/96.0),
            IsFinite(settings.WindowHeight) ? settings.WindowHeight : (this.Height*dpi/96.0)
          );
          var screens = Screen.GetAllScreen();
          if (!screens.Any(s => s.PhysicalWorkingArea.Contains(rect))) {
            var targetScreen =
              screens.OrderByDescending(s => {
                var r = Rect.Intersect(rect, s.PhysicalWorkingArea);
                return r.IsEmpty ? 0.0 : r.Width * r.Height;
              }).First();
            var targetArea = targetScreen.PhysicalWorkingArea;
            if (rect.Width>targetArea.Width) {
              rect = new Rect(rect.Left, rect.Top, targetArea.Width, rect.Height);
            }
            if (rect.Height>targetArea.Height) {
              rect = new Rect(rect.Left, rect.Top, rect.Width, targetArea.Height);
            }
            if (rect.Top<targetArea.Top) {
              rect = new Rect(rect.Left, targetArea.Top, rect.Width, rect.Height);
            }
            if (rect.Top>=targetArea.Bottom) {
              rect = new Rect(rect.Left, targetArea.Bottom-rect.Height/2, rect.Width, rect.Height);
            }
            if (rect.Left<=targetArea.Left-rect.Right) {
              rect = new Rect(targetArea.Left-rect.Width/2, rect.Top, rect.Width, rect.Height);
            }
            if (rect.Left>=targetArea.Right) {
              rect = new Rect(targetArea.Right-rect.Width/2, rect.Top, rect.Width, rect.Height);
            }
          }
          dpi = Screen.GetDpiForWindow(this);
          this.Top = rect.Top * 96.0 / dpi;
          dpi = Screen.GetDpiForWindow(this);
          this.Left = rect.Left * 96.0 / dpi;
          dpi = Screen.GetDpiForWindow(this);
          this.Width = rect.Width * 96.0 / dpi;
          dpi = Screen.GetDpiForWindow(this);
          this.Height = rect.Height * 96.0 / dpi;
          return true;
        }
        else {
          return false;
        }
      }
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
      InitWindow();
    }

    private void OnOpenBrowserUI(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
    {
      ((PeerCastAppViewModel)this.DataContext).OpenBrowserUI();
    }

    private void OnOpenHelp(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
    {
      ((PeerCastAppViewModel)this.DataContext).OpenHelp();
    }

    private void OnQuit(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
    {
      ((PeerCastAppViewModel)this.DataContext).Quit();
    }

    private void OnAbout(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
    {
      var dialog = new VersionInfoWindow {
        Owner = this,
        DataContext = ((PeerCastAppViewModel)DataContext).VersionInfo
      };
      dialog.ShowDialog();
    }

		private void UpdateCheck_Click(object sender, RoutedEventArgs e)
		{
			var dialog = new UpdaterWindow();
			dialog.Owner = this;
			dialog.ShowDialog();
		}

    private LogWindow? logWindow;
    private void OnShowLogs(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
    {
      if (logWindow==null) {
        logWindow = new LogWindow { DataContext=((PeerCastAppViewModel)this.DataContext).Log };
      }
      logWindow.Show();
    }

    private void OnOpenSettings(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
    {
      SettingsDialog.ShowDialog(this, ((PeerCastAppViewModel)this.DataContext).Model);
    }

    protected override void OnActivated(EventArgs e)
    {
      base.OnActivated(e);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
      base.OnClosing(e);
      e.Cancel = true;
      Visibility = Visibility.Hidden;
    }

    protected override void OnLocationChanged(EventArgs e)
    {
      if (!InitWindow()) return;
      var settings = PeerCastStation.Core.PeerCastApplication.Current!.Settings.Get<WPFSettings>();
      var bounds = RestoreBounds;
      if (!bounds.IsEmpty) {
        var dpi = Screen.GetDpiForWindow(this);
        bounds.Scale(dpi / 96.0, dpi / 96.0);
        settings.WindowLeft   = bounds.Left;
        settings.WindowTop    = bounds.Top;
        settings.WindowWidth  = bounds.Width;
        settings.WindowHeight = bounds.Height;
      }
      PeerCastStation.Core.PeerCastApplication.Current.SaveSettings();
      base.OnLocationChanged(e);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
      if (!InitWindow()) return;
      var settings = PeerCastStation.Core.PeerCastApplication.Current!.Settings.Get<WPFSettings>();
      var bounds = RestoreBounds;
      if (!bounds.IsEmpty) {
        var dpi = Screen.GetDpiForWindow(this);
        bounds.Scale(dpi / 96.0, dpi / 96.0);
        settings.WindowLeft   = bounds.Left;
        settings.WindowTop    = bounds.Top;
        settings.WindowWidth  = bounds.Width;
        settings.WindowHeight = bounds.Height;
      }
      PeerCastStation.Core.PeerCastApplication.Current.SaveSettings();
      base.OnRenderSizeChanged(sizeInfo);
    }

    private IntPtr OnWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
      handled = false;
      if (msg==0x0018) { //WM_SHOWWINDOW
        if (wParam.ToInt64()!=0) {
          this.Show();
          this.Activate();
          handled = true;
        }
      }
      return new IntPtr(0);
    }

  }
}
