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
  {
    public Channel Channel { get; private set; }
    public ChannelListItem(Channel channel)
    {
      this.Channel = channel;
    }

    public override string ToString()
    {
      var status = "UNKNOWN";
      switch (Channel.Status)
      {
        case SourceStreamStatus.Idle: status = "IDLE"; break;
        case SourceStreamStatus.Connecting: status = "CONNECT"; break;
        case SourceStreamStatus.Searching: status = "SEARCH"; break;
        case SourceStreamStatus.Receiving: status = "RECEIVE"; break;
        case SourceStreamStatus.Error: status = "ERROR"; break;
      }
      var relay_status = "　";
      if (Channel.IsRelayFull)
      {
        if (Channel.LocalRelays > 0)
        {
          relay_status = "○";
        }
        else if (!Channel.PeerCast.IsFirewalled.HasValue || Channel.PeerCast.IsFirewalled.Value)
        {
          relay_status = "×";
        }
        else
        {
          relay_status = "△";
        }
      }
      else
      {
        relay_status = "◎";
      }
      return String.Format(
        "{0} {1} {2}kbps ({3}/{4}) [{5}/{6}] {7}",
        relay_status,
        Channel.ChannelInfo.Name,
        Channel.ChannelInfo.Bitrate,
        Channel.TotalDirects,
        Channel.TotalRelays,
        Channel.LocalDirects,
        Channel.LocalRelays,
        status);
    }
  }
}
