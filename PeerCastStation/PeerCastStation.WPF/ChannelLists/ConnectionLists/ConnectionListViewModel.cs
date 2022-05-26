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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using PeerCastStation.Core;
using PeerCastStation.WPF.Commons;

namespace PeerCastStation.WPF.ChannelLists.ConnectionLists
{
  class ConnectionListViewModel : ViewModelBase
  {
    public ObservableCollection<ChannelConnectionViewModel> Connections { get; private set; }

    private ChannelConnectionViewModel? selectedConnection;
    public ChannelConnectionViewModel? SelectedConnection {
      get { return selectedConnection; }
      set {
        SetProperty("SelectedConnection", ref selectedConnection, value, () => {
          Close.OnCanExecuteChanged();
          Reconnect.OnCanExecuteChanged();
        });
      }
    }

    public Command Close     { get; private set; }
    public Command Reconnect { get; private set; }

    public ConnectionListViewModel()
    {
      this.Connections = new ObservableCollection<ChannelConnectionViewModel>();
      this.Close = new Command(
        () => selectedConnection?.Disconnect(),
        () => selectedConnection!=null && selectedConnection.IsDisconnectable);
      this.Reconnect = new Command(
        () => selectedConnection?.Reconnect(),
        () => selectedConnection != null && selectedConnection.IsReconnectable);
    }

    public void UpdateConnections(ChannelViewModel? channel)
    {
      if (channel==null) {
        Connections.Clear();
        return;
      }
      var new_list = channel.Connections.ToArray();
      foreach (var item in Connections.Except(new_list).ToArray()) {
        Connections.Remove(item);
      }
      foreach (var item in Connections) {
        item.Update();
      }
      foreach (var item in new_list.Except(Connections).ToArray()) {
        Connections.Add(item);
      }
    }

  }
}
