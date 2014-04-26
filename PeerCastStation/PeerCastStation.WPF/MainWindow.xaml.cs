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
using System.Windows;
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

    private System.Windows.Interop.WindowInteropHelper hwnd;
    private System.Windows.Interop.HwndSource nativeSource;
    public MainWindow(MainViewModel viewmodel)
    {
      InitializeComponent();
      var settings = PeerCastStation.Core.PeerCastApplication.Current.Settings.Get<WPFSettings>();
      if (IsFinite(settings.WindowLeft))   this.Left   = settings.WindowLeft;
      if (IsFinite(settings.WindowTop))    this.Top    = settings.WindowTop;
      if (IsFinite(settings.WindowWidth))  this.Width  = settings.WindowWidth;
      if (IsFinite(settings.WindowHeight)) this.Height = settings.WindowHeight;
      if (IsFinite(this.Left) && IsFinite(this.Width)) {
        if (this.Width>SystemParameters.VirtualScreenWidth) {
          this.Width = SystemParameters.VirtualScreenWidth;
        }
        if (this.Left+this.Width/2<SystemParameters.VirtualScreenLeft) {
          this.Left = SystemParameters.VirtualScreenLeft;
        }
        if (this.Left+this.Width/2>SystemParameters.VirtualScreenWidth+SystemParameters.VirtualScreenLeft) {
          this.Left = SystemParameters.VirtualScreenWidth+SystemParameters.VirtualScreenLeft - this.Width;
        }
      }
      if (IsFinite(this.Top) && IsFinite(this.Height)) {
        if (this.Height>SystemParameters.VirtualScreenHeight) {
          this.Height = SystemParameters.VirtualScreenHeight;
        }
        if (this.Top<SystemParameters.VirtualScreenTop) {
          this.Top = SystemParameters.VirtualScreenTop;
        }
        if (this.Top+this.Height/2>SystemParameters.VirtualScreenHeight+SystemParameters.VirtualScreenTop) {
          this.Top = SystemParameters.VirtualScreenHeight+SystemParameters.VirtualScreenTop - this.Height;
        }
      }
      this.CommandBindings.Add(new System.Windows.Input.CommandBinding(PeerCastCommands.OpenSettings, OnOpenSettings));
      this.CommandBindings.Add(new System.Windows.Input.CommandBinding(PeerCastCommands.ShowLogs, OnShowLogs));
      this.DataContext = viewmodel;
    }

    private LogWindow logWindow;
    private void OnShowLogs(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
    {
      if (logWindow==null) {
        logWindow = new LogWindow { DataContext=((MainViewModel)this.DataContext).Log };
      }
      logWindow.Show();
    }

    private void OnOpenSettings(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
    {
      SettingsDialog.ShowDialog(this, (MainViewModel)this.DataContext);
    }

    protected override void OnActivated(EventArgs e)
    {
      base.OnActivated(e);
      hwnd = new System.Windows.Interop.WindowInteropHelper(this);
      if (hwnd.Handle!=null && hwnd.Handle!=IntPtr.Zero) {
        nativeSource = System.Windows.Interop.HwndSource.FromHwnd(hwnd.Handle);
        nativeSource.AddHook(OnWindowMessage);
      }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
      base.OnClosing(e);
      e.Cancel = true;
      Visibility = Visibility.Hidden;
    }

    protected override void OnLocationChanged(EventArgs e)
    {
      var settings = PeerCastStation.Core.PeerCastApplication.Current.Settings.Get<WPFSettings>();
      var bounds = RestoreBounds;
      if (!bounds.IsEmpty) {
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
      var settings = PeerCastStation.Core.PeerCastApplication.Current.Settings.Get<WPFSettings>();
      var bounds = RestoreBounds;
      if (!bounds.IsEmpty) {
        settings.WindowLeft   = bounds.Left;
        settings.WindowTop    = bounds.Top;
        settings.WindowWidth  = bounds.Width;
        settings.WindowHeight = bounds.Height;
      }
      PeerCastStation.Core.PeerCastApplication.Current.SaveSettings();
      base.OnRenderSizeChanged(sizeInfo);
    }

    private void VersionInfoButton_Click(object sender, RoutedEventArgs e)
    {
      var dialog = new VersionInfoWindow
      {
        Owner = Window.GetWindow(this),
        DataContext = ((MainViewModel)DataContext).VersionInfo
      };
      dialog.ShowDialog();
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
