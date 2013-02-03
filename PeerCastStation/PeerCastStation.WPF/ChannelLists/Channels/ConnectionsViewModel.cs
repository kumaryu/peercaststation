using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using PeerCastStation.Core;
using PeerCastStation.WPF.Commons;

namespace PeerCastStation.WPF.ChannelLists.Channels
{
  class ConnectionsViewModel : ViewModelBase
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

    internal Channel Channel
    {
      set
      {
        var conn = connection;
        connections.Clear();
        connections.Add(new ChannelConnectionSourceItem(value.SourceStream));
        var announcings = peerCast.YellowPages
          .Select(yp => yp.AnnouncingChannels.FirstOrDefault(c => c.Channel.ChannelID == value.ChannelID))
          .Where(c => c != null);
        foreach (var announcing in announcings.ToArray())
        {
          connections.Add(new ChannelConnectionAnnouncingItem(announcing));
        }
        foreach (var os in value.OutputStreams.ToArray())
        {
          connections.Add(new ChannelConnectionOutputItem(os));
        }
        if (conn != null)
          Connection = connections.First(x => x.Equals(conn));
      }
    }

    public ConnectionsViewModel(PeerCast peerCast)
    {
      this.peerCast = peerCast;

      close = new Command(
        () => connection.Disconnect(),
        () => connection != null && connection.IsDisconnectable);
      reconnect = new Command(
        () => connection.Reconnect(),
        () => connection != null && connection.IsReconnectable);
    }
  }
}
