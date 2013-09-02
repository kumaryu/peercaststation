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
    private readonly PeerCast peerCast;

    private readonly ObservableCollection<IChannelConnectionItem> connections
      = new ObservableCollection<IChannelConnectionItem>();
    public ObservableCollection<IChannelConnectionItem> Connections
    {
      get { return connections; }
    }

    private IChannelConnectionItem connection;
    public IChannelConnectionItem Connection
    {
      get { return connection; }
      set
      {
        SetProperty("Connection", ref connection, value, () =>
          {
            close.OnCanExecuteChanged();
            reconnect.OnCanExecuteChanged();
          });
      }
    }

    private readonly Command close;
    public Command Close { get { return close; } }
    private readonly Command reconnect;
    public Command Reconnect { get { return reconnect; } }

    public ConnectionListViewModel(PeerCast peerCast)
    {
      this.peerCast = peerCast;

      close = new Command(
        () => connection.Disconnect(),
        () => connection != null && connection.IsDisconnectable);
      reconnect = new Command(
        () => connection.Reconnect(),
        () => connection != null && connection.IsReconnectable);
    }

    internal void Update(Channel channel)
    {
      if (channel==null) {
        connections.Clear();
        return;
      }
      var new_list = new List<IChannelConnectionItem>();
      new_list.Add(new ChannelConnectionSourceItem(channel.SourceStream));
      var announcings = peerCast.YellowPages
        .Select(yp => yp.AnnouncingChannels.FirstOrDefault(c => c.Channel.ChannelID==channel.ChannelID))
        .Where(c => c != null);
      foreach (var announcing in announcings) {
        new_list.Add(new ChannelConnectionAnnouncingItem(announcing));
      }
      foreach (var os in channel.OutputStreams) {
        new_list.Add(new ChannelConnectionOutputItem(os));
      }
      foreach (var item in connections.Except(new_list).ToArray()) {
        connections.Remove(item);
      }
      foreach (var item in connections) {
        item.Update();
      }
      foreach (var item in new_list.Except(connections).ToArray()) {
        connections.Add(item);
      }
    }

  }
}
