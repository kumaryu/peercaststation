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
    public MainWindow(MainViewModel viewmodel)
    {
      InitializeComponent();
      var settings = PeerCastStation.Core.PeerCastApplication.Current.Settings.Get<WPFSettings>();
      if (!Double.IsNaN(settings.WindowLeft))   this.Left   = settings.WindowLeft;
      if (!Double.IsNaN(settings.WindowTop))    this.Top    = settings.WindowTop;
      if (!Double.IsNaN(settings.WindowWidth))  this.Width  = settings.WindowWidth;
      if (!Double.IsNaN(settings.WindowHeight)) this.Height = settings.WindowHeight;
      if (!Double.IsNaN(this.Left) && !Double.IsNaN(this.Width)) {
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
      if (!Double.IsNaN(this.Top) && !Double.IsNaN(this.Height)) {
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
      this.DataContext = viewmodel;
    }

    protected override void OnLocationChanged(System.EventArgs e)
    {
      base.OnLocationChanged(e);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
      var settings = PeerCastStation.Core.PeerCastApplication.Current.Settings.Get<WPFSettings>();
      var bounds = RestoreBounds;
      settings.WindowLeft   = bounds.Left;
      settings.WindowTop    = bounds.Top;
      settings.WindowWidth  = bounds.Width;
      settings.WindowHeight = bounds.Height;
      base.OnClosing(e);
      e.Cancel = true;
      Visibility = Visibility.Hidden;
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
  }
}
