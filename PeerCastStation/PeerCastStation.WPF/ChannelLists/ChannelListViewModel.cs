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
      set
      {
        SetProperty("Channel", ref channel, value, () =>
        {
          if (IsChannelSelected)
          {
            Connections.Channel = value.Channel;
            ChannelInfo.From(
              value.Channel,
              peerCast.BroadcastID == value.Channel.BroadcastID);
            RelayTree.Channel = value.Channel;
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
      var old_list = channels.Select(item => item.Channel);
      foreach (var channel in old_list.Intersect(new_list).ToArray())
      {
        for (var i = 0; i < channels.Count; i++)
        {
          if (channels[i].Channel == channel)
          {
            var selected = this.channel == null ? false : this.channel.Channel == channel;
            channels[i] = new ChannelListItem(channel);
            if (selected)
            {
              Channel = channels[i];
            }
            break;
          }
        }
      }
      foreach (var channel in new_list.Except(old_list).ToArray())
      {
        channels.Add(new ChannelListItem(channel));
      }
      foreach (var channel in old_list.Except(new_list).ToArray())
      {
        for (var i = 0; i < channels.Count; i++)
        {
          if ((channels[i] as ChannelListItem).Channel == channel)
          {
            channels.RemoveAt(i);
            break;
          }
        }
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
