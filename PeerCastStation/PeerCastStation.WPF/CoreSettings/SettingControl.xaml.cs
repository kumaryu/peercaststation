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
using System.Windows.Controls;

namespace PeerCastStation.WPF.CoreSettings
{
  /// <summary>
  /// Settings.xaml の相互作用ロジック
  /// </summary>
  public partial class SettingControl : UserControl
  {
    public SettingControl()
    {
      InitializeComponent();
    }

    private void BandwidthCheckButton_Click(object sender, RoutedEventArgs args)
    {
      var dialog = new BandwidthCheckDialog();
      dialog.Owner = Window.GetWindow(this);
      dialog.ShowDialog();
      if (dialog.Result.HasValue) {
        ((SettingViewModel)this.DataContext).MaxUpstreamRate = dialog.Result.Value;
      }
    }

    private void PortCheckButton_Click(object sender, RoutedEventArgs args)
    {
      ((SettingViewModel)this.DataContext).PortCheck();
    }

  }
}
