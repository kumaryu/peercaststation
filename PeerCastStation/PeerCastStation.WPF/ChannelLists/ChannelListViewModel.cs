using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using PeerCastStation.Core;
using PeerCastStation.WPF.ChannelLists.Channels;
using PeerCastStation.WPF.ChannelLists.Dialogs;
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
    private int selectedChannelsIndex = -1;
    public int SelectedChannelsIndex
    {
      get { return selectedChannelsIndex; }
      set
      {
        SetProperty("SelectedChannelsIndex", ref selectedChannelsIndex, value, () =>
          {
            if (IsChannelSelected)
            {
              ChannelInfo.From(
                SelectedChannel.Channel,
                peerCast.BroadcastID == SelectedChannel.Channel.BroadcastID);
              RelayTree.Channel = SelectedChannel.Channel;
            }
            OnButtonsCanExecuteChanged();
          });
      }
    }
    private ChannelListItem SelectedChannel
    {
      get { return channels[selectedChannelsIndex]; }
    }
    private bool IsChannelSelected
    {
      get { return selectedChannelsIndex >= 0; }
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

    private readonly ChannelInfoViewModel channelInfo = new ChannelInfoViewModel();
    public ChannelInfoViewModel ChannelInfo
    {
      get { return channelInfo; }
    }
    private readonly RelayTreeViewModel relayTree;
    public RelayTreeViewModel RelayTree { get { return relayTree; } }

    internal ChannelListViewModel(PeerCast peerCast)
    {
      this.peerCast = peerCast;
      relayTree = new RelayTreeViewModel(peerCast);

      play = new Command(() =>
        {
          if (peerCast.OutputListeners.Count <= 0)
            return;

          var channel = SelectedChannel.Channel;
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
        () => peerCast.CloseChannel(SelectedChannel.Channel),
        () => IsChannelSelected);
      bump = new Command(
        () => SelectedChannel.Channel.Reconnect(),
        () => IsChannelSelected);
      openContactUrl = new Command(() =>
        {
          var url = SelectedChannel.Channel.ChannelInfo.URL;
          Uri uri;
          if (!String.IsNullOrEmpty(url) && Uri.TryCreate(url, UriKind.Absolute, out uri))
          {
            Process.Start(uri.ToString());
          }
        },
        () => IsChannelSelected);
      copyStreamUrl = new Command(() =>
        {
          var channel_id = SelectedChannel.Channel.ChannelID;
          var endpoint = peerCast.OutputListeners[0].LocalEndPoint;
          var ext = SelectedChannel.Channel.ChannelInfo.ContentExtension;
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
          var url = SelectedChannel.Channel.ChannelInfo.URL;
          Uri uri;
          if (!String.IsNullOrEmpty(url) && Uri.TryCreate(url, UriKind.Absolute, out uri))
          {
            Clipboard.SetText(uri.ToString());
          }
        },
        () => IsChannelSelected);

      var sc = SynchronizationContext.Current;
      ChannelChangedEventHandler onChannelChanged
        = (sender, e) => sc.Post(o => UpdateChannelList(), null);
      peerCast.ChannelAdded += onChannelChanged;
      peerCast.ChannelRemoved += onChannelChanged;
    }

    internal void UpdateChannelList()
    {
      var new_list = peerCast.Channels;
      var old_list = channels.Select(item => item.Channel);
      foreach (var channel in old_list.Intersect(new_list).ToArray())
      {
        for (var i = 0; i < channels.Count; i++)
        {
          if ((channels[i] as ChannelListItem).Channel == channel)
          {
            channels[i] = new ChannelListItem(channel);
            if (selectedChannelsIndex == i)
            {
              // 選択してた項目が更新された時の処理？

              //UpdateChannelInfo(channel);
              //UpdateOutputList(channel);
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
