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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using PeerCastStation.Core;
using PeerCastStation.WPF.ChannelLists.ChannelInfos;
using PeerCastStation.WPF.ChannelLists.ConnectionLists;
using PeerCastStation.WPF.ChannelLists.Dialogs;
using PeerCastStation.WPF.ChannelLists.RelayTrees;
using PeerCastStation.WPF.Commons;

namespace PeerCastStation.WPF.ChannelLists
{
  class ChannelListViewModel : ViewModelBase
  {
    private readonly PeerCast peerCast;

    private readonly ObservableCollection<ChannelViewModel> channels
      = new ObservableCollection<ChannelViewModel>();
    public ObservableCollection<ChannelViewModel> Channels
    {
      get { return channels; }
    }
    private ChannelViewModel? selectedChannel;
    public ChannelViewModel? SelectedChannel
    {
      get { return selectedChannel; }
      set {
        SetProperty("SelectedChannel", ref selectedChannel, value, () => {
          UpdateChannel(selectedChannel);
          UpdateRelayTree(selectedChannel);
        });
      }
    }

    internal BroadcastViewModel Broadcast
    {
      get { return new BroadcastViewModel(peerCast); }
    }

    private readonly ConnectionListViewModel connections;
    public ConnectionListViewModel Connections { get { return connections; } }
    private readonly ChannelInfoViewModel channelInfo = new ChannelInfoViewModel();
    public ChannelInfoViewModel ChannelInfo { get { return channelInfo; } }
    private readonly RelayTreeViewModel relayTree;
    public RelayTreeViewModel RelayTree { get { return relayTree; } }

    internal ChannelListViewModel(PeerCast peerCast)
    {
      this.peerCast = peerCast;
      connections = new ConnectionListViewModel();
      relayTree = new RelayTreeViewModel(peerCast);
    }

    internal void UpdateChannelList()
    {
      var new_list = peerCast.Channels.Select(ch => new ChannelViewModel(peerCast, ch));
      foreach (var item in channels.Where(item => !new_list.Contains(item)).ToArray()) {
        channels.Remove(item);
      }
      foreach (var item in channels) {
        item.Update();
      }
      foreach (var channel in new_list.Except(channels)) {
        channels.Add(channel);
      }
      if (selectedChannel!=null && !channels.Contains(selectedChannel)) {
        this.SelectedChannel = null;
      }
      UpdateChannel(selectedChannel);
    }

    private void UpdateChannel(ChannelViewModel? channel)
    {
      Connections.UpdateConnections(channel);
      ChannelInfo.UpdateChannelInfo(channel);
    }

    private void UpdateRelayTree(ChannelViewModel? channel)
    {
      RelayTree.Update(channel);
    }

    public void UpdateSelectedChannel()
    {
      UpdateChannel(selectedChannel);
    }

    public void UpdateSelectedChannelRelayTree()
    {
      UpdateRelayTree(selectedChannel);
    }
  }
}
