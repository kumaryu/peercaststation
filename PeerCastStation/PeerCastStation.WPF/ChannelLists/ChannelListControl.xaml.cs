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
using PeerCastStation.WPF.ChannelLists.Dialogs;

namespace PeerCastStation.WPF.ChannelLists
{
  /// <summary>
  /// AllChannels.xaml の相互作用ロジック
  /// </summary>
  public partial class ChannelListControl : UserControl
  {
    public ChannelListControl()
    {
      InitializeComponent();
    }

    private void BroadcastButton_Click(object sender, RoutedEventArgs e)
    {
      var dialog = new BroadcastWindow
      {
        Owner = Window.GetWindow(this),
        DataContext = ((ChannelListViewModel)DataContext).Broadcast
      };
      dialog.ShowDialog();
    }

    private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      var list = DataContext as ChannelListViewModel;
      if (list!=null) {
        list.UpdateSelectedChannel();
        list.UpdateSelectedChannelRelayTree();
      }
    }
  }
}
