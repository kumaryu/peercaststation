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

    private readonly ObservableCollection<ChannelListItem> channels
      = new ObservableCollection<ChannelListItem>();
    public ObservableCollection<ChannelListItem> Channels
    {
      get { return channels; }
    }
    private ChannelListItem channel;
    public ChannelListItem Channel
    {
      get { return channel; }
      set {
        SetProperty("Channel", ref channel, value, () => {
          if (channel!=null) {
            UpdateChannel(channel.Channel);
            UpdateRelayTree(channel.Channel);
          }
          else {
            UpdateChannel(null);
            UpdateRelayTree(null);
          }
          OnButtonsCanExecuteChanged();
        });
      }
    }
    private bool IsChannelSelected
    {
      get { return channel != null; }
    }

    private readonly Command play;
    public Command Play { get { return play; } }
    private readonly Command close;
    public Command Close { get { return close; } }
    private readonly Command bump;
    public Command Bump { get { return bump; } }
    internal BroadcastViewModel Broadcast
    {
      get { return new BroadcastViewModel(peerCast); }
    }

    private readonly Command openContactUrl;
    public Command OpenContactUrl { get { return openContactUrl; } }
    private readonly Command copyStreamUrl;
    public Command CopyStreamUrl { get { return copyStreamUrl; } }
    private readonly Command copyContactUrl;
    public Command CopyContactUrl { get { return copyContactUrl; } }

    private readonly ConnectionListViewModel connections;
    public ConnectionListViewModel Connections { get { return connections; } }
    private readonly ChannelInfoViewModel channelInfo = new ChannelInfoViewModel();
    public ChannelInfoViewModel ChannelInfo { get { return channelInfo; } }
    private readonly RelayTreeViewModel relayTree;
    public RelayTreeViewModel RelayTree { get { return relayTree; } }

    internal ChannelListViewModel(PeerCast peerCast)
    {
      this.peerCast = peerCast;
      connections = new ConnectionListViewModel(peerCast);
      relayTree = new RelayTreeViewModel(peerCast);

      play = new Command(() =>
      {
        if (peerCast.OutputListeners.Count <= 0)
          return;

        var channel = Channel.Channel;
        var channel_id = channel.ChannelID;
        var ext = (channel.ChannelInfo.ContentType == "WMV" ||
                    channel.ChannelInfo.ContentType == "WMA" ||
                    channel.ChannelInfo.ContentType == "ASX") ? ".asx" : ".m3u";
        var endpoint = peerCast.OutputListeners[0].LocalEndPoint;
        string pls;
        if (endpoint.Address.Equals(System.Net.IPAddress.Any))
        {
          pls = String.Format("http://localhost:{0}/pls/{1}{2}", endpoint.Port, channel_id.ToString("N"), ext);
        }
        else
        {
          pls = String.Format("http://{0}/pls/{1}{2}", endpoint.ToString(), channel_id.ToString("N"), ext);
        }
        System.Diagnostics.Process.Start(pls);
      },
        () => IsChannelSelected);
      close = new Command(
        () => peerCast.CloseChannel(Channel.Channel),
        () => IsChannelSelected);
      bump = new Command(
        () => Channel.Channel.Reconnect(),
        () => IsChannelSelected);
      openContactUrl = new Command(() =>
      {
        var url = Channel.Channel.ChannelInfo.URL;
        Uri uri;
        if (!String.IsNullOrEmpty(url) && Uri.TryCreate(url, UriKind.Absolute, out uri))
        {
          Process.Start(uri.ToString());
        }
      },
        () => IsChannelSelected);
      copyStreamUrl = new Command(() =>
      {
        var channel_id = Channel.Channel.ChannelID;
        var endpoint = peerCast.OutputListeners[0].LocalEndPoint;
        var ext = Channel.Channel.ChannelInfo.ContentExtension;
        string url;
        if (endpoint.Address.Equals(System.Net.IPAddress.Any))
        {
          url = String.Format("http://localhost:{0}/stream/{1}{2}", endpoint.Port, channel_id.ToString("N"), ext);
        }
        else
        {
          url = String.Format("http://{0}/pls/{1}{2}", endpoint.ToString(), channel_id.ToString("N"), ext);
        }
        Clipboard.SetText(url);
      },
        () => IsChannelSelected && peerCast.OutputListeners.Count > 0);
      copyContactUrl = new Command(() =>
      {
        var url = Channel.Channel.ChannelInfo.URL;
        Uri uri;
        if (!String.IsNullOrEmpty(url) && Uri.TryCreate(url, UriKind.Absolute, out uri))
        {
          Clipboard.SetText(uri.ToString());
        }
      },
        () => IsChannelSelected);
    }

    internal void UpdateChannelList()
    {
      var new_list = peerCast.Channels;
      foreach (var item in channels.Where(item => !new_list.Contains(item.Channel)).ToArray()) {
        channels.Remove(item);
      }
      foreach (var item in channels) {
        item.Update();
      }
      foreach (var channel in new_list.Except(channels.Select(item => item.Channel))) {
        channels.Add(new ChannelListItem(channel));
      }
      if (!channels.Contains(this.channel)) {
        this.Channel = null;
      }
      if (this.Channel!=null) {
        UpdateChannel(this.Channel.Channel);
      }
      else {
        UpdateChannel(null);
      }
    }

    private void UpdateChannel(Channel channel)
    {
      Connections.Update(channel);
      ChannelInfo.UpdateChannelInfo(channel);
    }

    private void UpdateRelayTree(Channel channel)
    {
      RelayTree.Update(channel);
    }

    public void UpdateSelectedChannel()
    {
      if (this.Channel!=null) {
        UpdateChannel(this.Channel.Channel);
      }
      else {
        UpdateChannel(null);
      }
    }

    public void UpdateSelectedChannelRelayTree()
    {
      if (this.Channel!=null) {
        RelayTree.Update(this.Channel.Channel);
      }
      else {
        RelayTree.Update(null);
      }
    }

    private void OnButtonsCanExecuteChanged()
    {
      play.OnCanExecuteChanged();
      close.OnCanExecuteChanged();
      bump.OnCanExecuteChanged();
    }
  }
}
