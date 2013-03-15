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
      this.DataContext = viewmodel;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
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
