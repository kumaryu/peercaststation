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
