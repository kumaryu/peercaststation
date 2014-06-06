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
using System.Windows.Input;
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
      CommandBindings.Add(new CommandBinding(PeerCastCommands.Play, OnPlayExecuted, CanExecuteChannelCommand));
      CommandBindings.Add(new CommandBinding(PeerCastCommands.Disconnect, OnDisconnectExecuted, CanExecuteChannelCommand));
      CommandBindings.Add(new CommandBinding(PeerCastCommands.Reconnect, OnReconnectExecuted, CanExecuteChannelCommand));
      CommandBindings.Add(new CommandBinding(PeerCastCommands.OpenContactUrl, OnOpenContactUrlExecuted, CanExecuteChannelCommand));
      CommandBindings.Add(new CommandBinding(PeerCastCommands.CopyContactUrl, OnCopyContactUrlExecuted, CanExecuteChannelCommand));
      CommandBindings.Add(new CommandBinding(PeerCastCommands.CopyStreamUrl, OnCopyStreamUrlExecuted, CanExecuteChannelCommand));
      CommandBindings.Add(new CommandBinding(PeerCastCommands.Broadcast, OnBroadcastExecuted));
    }

    private void CanExecuteChannelCommand(object sender, CanExecuteRoutedEventArgs e)
    {
      e.CanExecute = ((ChannelListViewModel)DataContext).SelectedChannel!=null;
    }

    private void OnPlayExecuted(object sender, ExecutedRoutedEventArgs e)
    {
      var channel = ((ChannelListViewModel)DataContext).SelectedChannel;
      if (channel==null) return;
      var pls = channel.PlayListUri;
      if (pls!=null) System.Diagnostics.Process.Start(pls.ToString());
    }

    private void OnDisconnectExecuted(object sender, ExecutedRoutedEventArgs e)
    {
      var channel = ((ChannelListViewModel)DataContext).SelectedChannel;
      if (channel==null) return;
      channel.Disconnect();
    }

    private void OnReconnectExecuted(object sender, ExecutedRoutedEventArgs e)
    {
      var channel = ((ChannelListViewModel)DataContext).SelectedChannel;
      if (channel==null) return;
      channel.Reconnect();
    }

    private void OnOpenContactUrlExecuted(object sender, ExecutedRoutedEventArgs e)
    {
      var channel = ((ChannelListViewModel)DataContext).SelectedChannel;
      if (channel==null) return;
      var uri = channel.ContactUri;
      if (uri!=null) System.Diagnostics.Process.Start(uri.ToString());
    }

    private void OnCopyContactUrlExecuted(object sender, ExecutedRoutedEventArgs e)
    {
      var channel = ((ChannelListViewModel)DataContext).SelectedChannel;
      if (channel==null) return;
      var uri = channel.ContactUri;
      if (uri!=null) {
        try {
          Clipboard.SetText(uri.ToString());
        }
        catch (System.Runtime.InteropServices.COMException) {}
      }
    }

    private void OnCopyStreamUrlExecuted(object sender, ExecutedRoutedEventArgs e)
    {
      var channel = ((ChannelListViewModel)DataContext).SelectedChannel;
      if (channel==null) return;
      var uri = channel.StreamUri;
      if (uri!=null) {
        try {
          Clipboard.SetText(uri.ToString());
        }
        catch (System.Runtime.InteropServices.COMException) {}
      }
    }

    private void OnBroadcastExecuted(object sender, ExecutedRoutedEventArgs e)
    {
      var dialog = new BroadcastWindow {
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
