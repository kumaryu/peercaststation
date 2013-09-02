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

namespace PeerCastStation.WPF.ChannelLists.ConnectionLists
{
  interface IChannelConnectionItem
    : INotifyPropertyChanged
  {
    void Disconnect();
    void Reconnect();
    void Update();
    bool IsDisconnectable     { get; }
    bool IsReconnectable      { get; }
    BitmapImage AttributeIcon { get; }
    string Protocol           { get; }
    string Status             { get; }
    string RemoteName         { get; }
    string Bitrate            { get; }
    string ContentPosition    { get; }
    string Connections        { get; }
    string AgentName          { get; }
    object Connection         { get; }
  }

  internal static class AttributeIcons
  {
    private static BitmapImage[] icons = new BitmapImage[] {
      new BitmapImage(new Uri("pack://application:,,,/PeerCastStation.WPF;component/relay_icon_0.png")),
      new BitmapImage(new Uri("pack://application:,,,/PeerCastStation.WPF;component/relay_icon_1.png")),
      new BitmapImage(new Uri("pack://application:,,,/PeerCastStation.WPF;component/relay_icon_2.png")),
      new BitmapImage(new Uri("pack://application:,,,/PeerCastStation.WPF;component/relay_icon_3.png")),
      new BitmapImage(new Uri("pack://application:,,,/PeerCastStation.WPF;component/relay_icon_4.png")),
      new BitmapImage(new Uri("pack://application:,,,/PeerCastStation.WPF;component/relay_icon_5.png")),
      new BitmapImage(new Uri("pack://application:,,,/PeerCastStation.WPF;component/relay_icon_6.png")),
      new BitmapImage(new Uri("pack://application:,,,/PeerCastStation.WPF;component/relay_icon_7.png")),
      new BitmapImage(new Uri("pack://application:,,,/PeerCastStation.WPF;component/relay_icon_8.png")),
    };
    public static BitmapImage StatusToIcon(string status)
    {
      switch (status) {
      case "◎": return icons[0];
      case "○": return icons[1];
      case "△": return icons[2];
      case "×": return icons[3];
      case "？": return icons[4];
      case "　": return icons[5];
      case "■": return icons[6];
      case "Ｒ": return icons[7];
      case "Ｔ": return icons[8];
      default: return null;
      }
    }
  }

  class ChannelConnectionSourceItem : IChannelConnectionItem
  {
    private ISourceStream sourceStream;
    public ChannelConnectionSourceItem(ISourceStream ss)
    {
      sourceStream = ss;
    }

    public void Disconnect()
    {
      throw new InvalidOperationException();
    }

    public void Reconnect()
    {
      throw new InvalidOperationException();
    }

    public bool IsDisconnectable
    {
      get { return false; }
    }

    public bool IsReconnectable
    {
      get { return false; }
    }

    public BitmapImage AttributeIcon {
      get {
        var info = sourceStream.GetConnectionInfo();
        var status = "";
        if ((info.RemoteHostStatus & RemoteHostStatus.Root)!=0) {
          status = "Ｒ";
        }
        if ((info.RemoteHostStatus & RemoteHostStatus.Tracker)!=0) {
          status = "Ｔ";
        }
        return AttributeIcons.StatusToIcon(status);
      }
    }

    public string Protocol {
      get {
        var info = sourceStream.GetConnectionInfo();
        return info.ProtocolName;
      }
    }

    public string Status {
      get {
        var info = sourceStream.GetConnectionInfo();
        return info.Status.ToString();
      }
    }

    public string RemoteName
    {
      get {
        var info = sourceStream.GetConnectionInfo();
        return info.RemoteName;
      }
    }

    public string Bitrate
    {
      get {
        var info = sourceStream.GetConnectionInfo();
        var bitrate = (int)(((info.RecvRate ?? 0) + (info.SendRate ?? 0))*8/1000);
        return String.Format("{0}kbps", bitrate);
      }
    }

    public string ContentPosition
    {
      get { return ""; }
    }

    public string Connections
    {
      get { return ""; }
    }

    public string AgentName
    {
      get {
        var info = sourceStream.GetConnectionInfo();
        return info.AgentName;
      }
    }

    public object Connection { get { return sourceStream; } }

    public override bool Equals(object obj)
    {
      var other = obj as ChannelConnectionSourceItem;
      if (other == null)
        return false;
      return sourceStream.Equals(other.sourceStream);
    }

    public override int GetHashCode()
    {
      return sourceStream.GetHashCode();
    }

    public void Update()
    {
      if (PropertyChanged!=null) {
        PropertyChanged(this, new PropertyChangedEventArgs("AttributeIcon"));
        PropertyChanged(this, new PropertyChangedEventArgs("Status"));
        PropertyChanged(this, new PropertyChangedEventArgs("RemoteName"));
        PropertyChanged(this, new PropertyChangedEventArgs("Bitrate"));
        PropertyChanged(this, new PropertyChangedEventArgs("ContentPosition"));
        PropertyChanged(this, new PropertyChangedEventArgs("Connections"));
        PropertyChanged(this, new PropertyChangedEventArgs("AgentName"));
      }
    }
    public event PropertyChangedEventHandler PropertyChanged;
  }

  class ChannelConnectionOutputItem : IChannelConnectionItem
  {
    private IOutputStream outputStream;
    public ChannelConnectionOutputItem(IOutputStream os)
    {
      outputStream = os;
    }

    public void Disconnect()
    {
      outputStream.Stop();
    }

    public void Reconnect()
    {
      throw new InvalidOperationException();
    }

    public bool IsDisconnectable
    {
      get { return true; }
    }

    public bool IsReconnectable
    {
      get { return false; }
    }

    public BitmapImage AttributeIcon {
      get {
        var info = outputStream.GetConnectionInfo();
        var status = "";
        if (info.Type==ConnectionType.Relay) {
          if ((info.RemoteHostStatus & RemoteHostStatus.Receiving)!=0) {
            if ((info.RemoteHostStatus & RemoteHostStatus.Firewalled)!=0 &&
                (info.RemoteHostStatus & RemoteHostStatus.Local)==0) {
              if ((info.LocalRelays ?? 0)>0) {
                status = "？";
              }
              else {
                status = "×";
              }
            }
            else if ((info.RemoteHostStatus & RemoteHostStatus.RelayFull)!=0) {
              if ((info.LocalRelays ?? 0)>0) {
                status = "○";
              }
              else {
                status = "△";
              }
            }
            else {
              status = "◎";
            }
          }
          else {
            status = "■";
          }
        }
        return AttributeIcons.StatusToIcon(status);
      }
    }

    public string Protocol {
      get {
        var info = outputStream.GetConnectionInfo();
        return info.ProtocolName;
      }
    }

    public string Status {
      get {
        var info = outputStream.GetConnectionInfo();
        return info.Status.ToString();
      }
    }

    public string RemoteName {
      get {
        var info = outputStream.GetConnectionInfo();
        return info.RemoteName;
      }
    }

    public string Bitrate {
      get {
        var info = outputStream.GetConnectionInfo();
        var bitrate = (int)(((info.RecvRate ?? 0) + (info.SendRate ?? 0))*8/1000);
        return String.Format("{0}kbps", bitrate);
      }
    }

    public string ContentPosition {
      get {
        var info = outputStream.GetConnectionInfo();
        return (info.ContentPosition ?? 0).ToString();
      }
    }

    public string Connections {
      get {
        var info = outputStream.GetConnectionInfo();
        if (info.Type==ConnectionType.Relay) {
          return String.Format("[{0}/{1}]", info.LocalDirects ?? 0, info.LocalRelays ?? 0);
        }
        else {
          return "";
        }
      }
    }

    public string AgentName {
      get {
        var info = outputStream.GetConnectionInfo();
        return info.AgentName;
      }
    }

    public object Connection { get { return outputStream; } }

    public override bool Equals(object obj)
    {
      var other = obj as ChannelConnectionOutputItem;
      if (other == null)
        return false;
      return outputStream.Equals(other.outputStream);
    }

    public override int GetHashCode()
    {
      return outputStream.GetHashCode();
    }

    public void Update()
    {
      if (PropertyChanged!=null) {
        PropertyChanged(this, new PropertyChangedEventArgs("AttributeIcon"));
        PropertyChanged(this, new PropertyChangedEventArgs("Status"));
        PropertyChanged(this, new PropertyChangedEventArgs("RemoteName"));
        PropertyChanged(this, new PropertyChangedEventArgs("Bitrate"));
        PropertyChanged(this, new PropertyChangedEventArgs("ContentPosition"));
        PropertyChanged(this, new PropertyChangedEventArgs("Connections"));
        PropertyChanged(this, new PropertyChangedEventArgs("AgentName"));
      }
    }
    public event PropertyChangedEventHandler PropertyChanged;
  }

  class ChannelConnectionAnnouncingItem : IChannelConnectionItem
  {
    private IAnnouncingChannel announcingChannel;
    public ChannelConnectionAnnouncingItem(IAnnouncingChannel ac)
    {
      announcingChannel = ac;
    }

    public void Disconnect()
    {
      throw new InvalidOperationException();
    }

    public void Reconnect()
    {
      announcingChannel.YellowPage.RestartAnnounce(announcingChannel);
    }

    public bool IsDisconnectable
    {
      get { return false; }
    }

    public bool IsReconnectable
    {
      get { return true; }
    }

    public BitmapImage AttributeIcon {
      get {
        var info = announcingChannel.YellowPage.GetConnectionInfo();
        var status = "";
        if ((info.RemoteHostStatus & RemoteHostStatus.Root)!=0) {
          status = "Ｒ";
        }
        if ((info.RemoteHostStatus & RemoteHostStatus.Tracker)!=0) {
          status = "Ｔ";
        }
        return AttributeIcons.StatusToIcon(status);
      }
    }

    public string Protocol {
      get {
        var info = announcingChannel.YellowPage.GetConnectionInfo();
        return info.ProtocolName;
      }
    }

    public string Status {
      get {
        var info = announcingChannel.YellowPage.GetConnectionInfo();
        return info.Status.ToString();
      }
    }

    public string RemoteName {
      get {
        var info = announcingChannel.YellowPage.GetConnectionInfo();
        return info.RemoteName;
      }
    }

    public string Bitrate {
      get {
        var info = announcingChannel.YellowPage.GetConnectionInfo();
        var bitrate = (int)(((info.RecvRate ?? 0) + (info.SendRate ?? 0))*8/1000);
        return String.Format("{0}kbps", bitrate);
      }
    }

    public string ContentPosition {
      get { return ""; }
    }

    public string Connections {
      get { return ""; }
    }

    public string AgentName {
      get {
        var info = announcingChannel.YellowPage.GetConnectionInfo();
        return info.AgentName;
      }
    }

    public object Connection { get { return announcingChannel; } }

    public override bool Equals(object obj)
    {
      var other = obj as ChannelConnectionAnnouncingItem;
      if (other == null)
        return false;
      return announcingChannel.Equals(other.announcingChannel);
    }

    public override int GetHashCode()
    {
      return announcingChannel.GetHashCode();
    }

    public void Update()
    {
      if (PropertyChanged!=null) {
        PropertyChanged(this, new PropertyChangedEventArgs("AttributeIcon"));
        PropertyChanged(this, new PropertyChangedEventArgs("Status"));
        PropertyChanged(this, new PropertyChangedEventArgs("RemoteName"));
        PropertyChanged(this, new PropertyChangedEventArgs("Bitrate"));
        PropertyChanged(this, new PropertyChangedEventArgs("ContentPosition"));
        PropertyChanged(this, new PropertyChangedEventArgs("Connections"));
        PropertyChanged(this, new PropertyChangedEventArgs("AgentName"));
      }
    }
    public event PropertyChangedEventHandler PropertyChanged;
  }
}
