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
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using PeerCastStation.Core;
using PeerCastStation.WPF.Commons;

namespace PeerCastStation.WPF.ChannelLists.RelayTrees
{
  class RelayTreeViewModel : ViewModelBase
  {
    private readonly PeerCast peerCast;
    public IEnumerable<RelayTreeNodeViewModel> RelayTree { get; private set; }

    private ChannelViewModel? channel;
    private Command refresh;
    public System.Windows.Input.ICommand Refresh {
      get { return refresh;}
    }

    public RelayTreeViewModel(PeerCast peerCast)
    {
      this.peerCast = peerCast;
      this.RelayTree = new RelayTreeNodeViewModel[0];
      refresh = new Command(
        () => Update(this.channel),
        () => channel!=null);
    }

    internal void Update(ChannelViewModel? channel)
    {
      if (channel!=null) {
        this.RelayTree =
          channel.CreateHostTree().Nodes
            .Where(node => node.Host.SessionID==peerCast.SessionID)
            .Select(node => new RelayTreeNodeViewModel(node)).ToArray();
      }
      else {
        this.RelayTree = new RelayTreeNodeViewModel[0];
      }
      OnPropertyChanged("RelayTree");
      if (this.channel!=channel) {
        this.channel = channel;
        this.refresh.OnCanExecuteChanged();
      }
    }
  }

  public class RelayTreeNodeViewModel
  {
    public HostTreeNode Node { get; private set; }
    public IEnumerable<RelayTreeNodeViewModel> Children { get; private set; }

    public ConnectionStatus ConnectionStatus {
      get {
        var info = Node.Host;
        if (!info.IsReceiving) return ConnectionStatus.NotReceiving;
        if (info.IsFirewalled) {
          if (info.RelayCount>0) {
            return ConnectionStatus.FirewalledRelaying;
          }
          else {
            return ConnectionStatus.Firewalled;
          }
        }
        if (!info.IsRelayFull) return ConnectionStatus.Relayable;
        if (info.RelayCount>0) {
          return ConnectionStatus.RelayFull;
        }
        else {
          return ConnectionStatus.NotRelayable;
        }
      }
    }

    public string RemoteName {
      get {
        var settings = PeerCastApplication.Current!.Settings.Get<WPFSettings>();
        switch (settings.RemoteNodeName) {
        case RemoteNodeName.SessionID:
          if (Node.Host.SessionID!=Guid.Empty) {
            return Node.Host.SessionID.ToString("N").ToUpperInvariant();
          }
          else if (Node.Host.GlobalEndPoint!=null && Node.Host.GlobalEndPoint.Port!=0) {
            return Node.Host.GlobalEndPoint.ToString();
          }
          else {
            return Node.Host.LocalEndPoint?.ToString() ?? "";
          }
        default:
        case RemoteNodeName.Uri:
        case RemoteNodeName.EndPoint:
          if (Node.Host.GlobalEndPoint!=null && Node.Host.GlobalEndPoint.Port!=0) {
            return Node.Host.GlobalEndPoint.ToString();
          }
          else {
            return Node.Host.LocalEndPoint?.ToString() ?? "";
          }
        }
      }
    }

    public string Connections {
      get {
        return String.Format("[{0}/{1}]",
          Node.Host.DirectCount,
          Node.Host.RelayCount);
      }
    }

    public string AgentVersion {
      get {
        string version = "";
        var pcp = Node.Host.Extra.GetHostVersion();
        if (pcp.HasValue) {
          version += pcp.Value.ToString();
        }
        var vp = Node.Host.Extra.GetHostVersionVP();
        if (vp.HasValue) {
          version += " VP" + vp.Value.ToString();
        }
        var ex = Node.Host.Extra.GetHostVersionEXPrefix();
        var exnum = Node.Host.Extra.GetHostVersionEXNumber();
        if (ex!=null && exnum.HasValue) {
          try {
            version += " " + System.Text.Encoding.UTF8.GetString(ex) + exnum.ToString();
          }
          catch (ArgumentException) {
            //ignore
          }
        }
        return version;
      }
    }

    public RelayTreeNodeViewModel(HostTreeNode node)
    {
      this.Node = node;
      this.Children = node.Children.Select(c => new RelayTreeNodeViewModel(c)).ToArray();
    }
  }

}

