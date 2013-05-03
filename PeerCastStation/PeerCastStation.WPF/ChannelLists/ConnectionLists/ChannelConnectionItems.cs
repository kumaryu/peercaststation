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
using PeerCastStation.Core;

namespace PeerCastStation.WPF.ChannelLists.ConnectionLists
{
  interface IChannelConnectionItem
  {
    void Disconnect();
    void Reconnect();
    bool IsDisconnectable { get; }
    bool IsReconnectable { get; }
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

    public override string ToString()
    {
      var info = sourceStream.GetConnectionInfo();
      var status = "　";
      if ((info.RemoteHostStatus & RemoteHostStatus.Root)!=0) {
        status = "Ｒ";
      }
      if ((info.RemoteHostStatus & RemoteHostStatus.Tracker)!=0) {
        status = "Ｔ";
      }
      var bitrate = (int)(((info.RecvRate ?? 0) + (info.SendRate ?? 0))*8/1000);
      return String.Format(
        "{0}{1} {2} {3} {4}kbps pos:{5} {6}",
        status,
        info.ProtocolName,
        info.Status,
        info.RemoteName,
        bitrate,
        info.ContentPosition,
        info.AgentName);
    }

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

    public override string ToString()
    {
      var info = outputStream.GetConnectionInfo();
      var status = "　";
      var bitrate = (int)(((info.RecvRate ?? 0) + (info.SendRate ?? 0))*8/1000);
      switch (info.Type) {
      case ConnectionType.Relay:
        if ((info.RemoteHostStatus & RemoteHostStatus.Receiving)!=0) {
          if ((info.RemoteHostStatus & RemoteHostStatus.Firewalled)!=0 &&
              (info.RemoteHostStatus & RemoteHostStatus.Local)==0) {
            status = "×";
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
        return String.Format(
          "{0}{1} {2} {3} [{4}/{5}] {6}kbps pos:{7} {8}",
          status,
          info.ProtocolName,
          info.Status,
          info.RemoteEndPoint,
          info.LocalDirects,
          info.LocalRelays,
          bitrate,
          info.ContentPosition,
          info.AgentName);
      case ConnectionType.Direct:
        return String.Format(
          "{0}{1} {2} {3} {4}kbps pos:{5} {6}",
          status,
          info.ProtocolName,
          info.Status,
          info.RemoteEndPoint,
          bitrate,
          info.ContentPosition,
          info.AgentName);
      default:
        return String.Format(
          "{0}{1} {2} {3} {4}kbps {5}",
          status,
          info.ProtocolName,
          info.Status,
          info.RemoteEndPoint,
          bitrate,
          info.AgentName);
      }
    }

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

    public override string ToString()
    {
      var yp = announcingChannel.YellowPage;
      var info = yp.GetConnectionInfo();
      var status = "　";
      if ((info.RemoteHostStatus & RemoteHostStatus.Root)!=0) {
        status = "Ｒ";
      }
      if ((info.RemoteHostStatus & RemoteHostStatus.Tracker)!=0) {
        status = "Ｔ";
      }
      var bitrate = (int)(((info.RecvRate ?? 0) + (info.SendRate ?? 0))*8/1000);
      return String.Format(
        "{0}{1} {2} {3} {4}kbps {5}",
        status,
        info.ProtocolName,
        info.RemoteName,
        info.Status,
        bitrate,
        info.AgentName);
    }

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
  }
}
