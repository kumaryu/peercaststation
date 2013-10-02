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
using System.Linq;
using System.Text;
using System.Windows.Media.Imaging;
using PeerCastStation.Core;
using System.ComponentModel;

namespace PeerCastStation.WPF.ChannelLists
{
  class ContentReaderItem
  {
    public IContentReaderFactory ContentReaderFactory { get; private set; }
    public ContentReaderItem(IContentReaderFactory reader)
    {
      ContentReaderFactory = reader;
    }

    public override string ToString()
    {
      return ContentReaderFactory.Name;
    }
  }

  class ChannelListItem
    : INotifyPropertyChanged
  {
    private static BitmapImage[] StatusIcons = new BitmapImage[] {
      new BitmapImage(new Uri("pack://application:,,,/PeerCastStation.WPF;component/relay_icon_0.png")),
      new BitmapImage(new Uri("pack://application:,,,/PeerCastStation.WPF;component/relay_icon_1.png")),
      new BitmapImage(new Uri("pack://application:,,,/PeerCastStation.WPF;component/relay_icon_2.png")),
      new BitmapImage(new Uri("pack://application:,,,/PeerCastStation.WPF;component/relay_icon_3.png")),
      new BitmapImage(new Uri("pack://application:,,,/PeerCastStation.WPF;component/relay_icon_4.png")),
      new BitmapImage(new Uri("pack://application:,,,/PeerCastStation.WPF;component/relay_icon_5.png")),
    };
    public Channel Channel { get; private set; }
    public ChannelListItem(Channel channel)
    {
      this.Channel = channel;
    }

    public bool IsTrackerSource {
      get {
        if (this.Channel.SourceStream==null) return false;
        var info = this.Channel.SourceStream.GetConnectionInfo();
        return (info.RemoteHostStatus & RemoteHostStatus.Tracker)!=0;
      }
    }

    public string ChannelStatus {
      get { 
        var status = "UNKNOWN";
        switch (Channel.Status) {
        case SourceStreamStatus.Idle:       status = "IDLE";    break;
        case SourceStreamStatus.Connecting: status = "CONNECT"; break;
        case SourceStreamStatus.Searching:  status = "SEARCH";  break;
        case SourceStreamStatus.Receiving:  status = "RECEIVE"; break;
        case SourceStreamStatus.Error:      status = "ERROR";   break;
        }
        return status;
      }
    }
    public string RelayStatus {
      get {
        var relay_status = "　";
        if (Channel.IsRelayFull) {
          if (Channel.LocalRelays > 0) {
            relay_status = "○";
          }
          else if (!Channel.PeerCast.IsFirewalled.HasValue || Channel.PeerCast.IsFirewalled.Value) {
            if (Channel.LocalRelays > 0) {
              relay_status = "？";
            }
            else {
              relay_status = "×";
            }
          }
          else {
            relay_status = "△";
          }
        }
        else {
          relay_status = "◎";
        }
        return relay_status;
      }
    }
    public BitmapImage RelayStatusIcon {
      get {
        switch (RelayStatus) {
        case "◎": return StatusIcons[0];
        case "○": return StatusIcons[1];
        case "△": return StatusIcons[2];
        case "×": return StatusIcons[3];
        case "？": return StatusIcons[4];
        default: return StatusIcons[5];
        }
      }
    }
    public string Name    { get { return Channel.ChannelInfo.Name; } }
    public string Bitrate { get { return String.Format("{0}kbps", Channel.ChannelInfo.Bitrate); } }
    public string Connections {
      get {
        return String.Format(
          "({0}/{1}) [{2}/{3}]",
          Channel.TotalDirects,
          Channel.TotalRelays,
          Channel.LocalDirects,
          Channel.LocalRelays);
      }
    }

    internal void Update()
    {
      OnPropertyChanged("ChannelStatus");
      OnPropertyChanged("RelayStatus");
      OnPropertyChanged("RelayStatusIcon");
      OnPropertyChanged("Name");
      OnPropertyChanged("Bitrate");
      OnPropertyChanged("Connections");
    }

    void OnPropertyChanged(string name)
    {
      if (PropertyChanged!=null) {
        PropertyChanged(this, new PropertyChangedEventArgs(name));
      }
    }
    public event PropertyChangedEventHandler PropertyChanged;
  }
}
