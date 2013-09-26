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

    private readonly ObservableCollection<TreeViewModel> relayTree
      = new ObservableCollection<TreeViewModel>();
    public ObservableCollection<TreeViewModel> RelayTree
    {
      get { return relayTree; }
    }

    private Channel channel;
    private Command refresh;
    public System.Windows.Input.ICommand Refresh {
      get { return refresh;}
    }

    public RelayTreeViewModel(PeerCast peerCast)
    {
      this.peerCast = peerCast;
      refresh = new Command(
        () => Update(this.channel),
        () => channel!=null);
    }

    private void AddRelayTreeNode(
      ObservableCollection<TreeViewModel> tree_nodes,
      IEnumerable<Utils.HostTreeNode> nodes,
      HashSet<Guid> added)
    {
      foreach (var node in nodes)
      {
        if (added.Contains(node.Host.SessionID)) continue;
        added.Add(node.Host.SessionID);
        var endpoint = (node.Host.GlobalEndPoint != null && node.Host.GlobalEndPoint.Port != 0) ? node.Host.GlobalEndPoint : node.Host.LocalEndPoint;
        string version = "";
        var pcp = node.Host.Extra.GetHostVersion();
        if (pcp.HasValue)
        {
          version += pcp.Value.ToString();
        }
        var vp = node.Host.Extra.GetHostVersionVP();
        if (vp.HasValue)
        {
          version += " VP" + vp.Value.ToString();
        }
        var ex = node.Host.Extra.GetHostVersionEXPrefix();
        var exnum = node.Host.Extra.GetHostVersionEXNumber();
        if (ex != null && exnum.HasValue)
        {
          try
          {
            version += " " + System.Text.Encoding.UTF8.GetString(ex) + exnum.ToString();
          }
          catch (ArgumentException)
          {
            //ignore
          }
        }
        var nodeinfo = String.Format(
          "{0} ({1}/{2}) {3}{4}{5} {6}",
          endpoint,
          node.Host.DirectCount,
          node.Host.RelayCount,
          node.Host.IsFirewalled ? "0" : "",
          node.Host.IsRelayFull ? "-" : "",
          node.Host.IsReceiving ? "" : "B",
          version);
        var tree_node = new TreeViewModel { Text = nodeinfo };
        tree_nodes.Add(tree_node);
        AddRelayTreeNode(tree_node.Children, node.Children, added);
      }
    }

    internal void Update(PeerCastStation.Core.Channel channel)
    {
      relayTree.Clear();
      if (channel!=null) {
        var roots = channel.CreateHostTree()
          .Where(node => node.Host.SessionID==peerCast.SessionID);
        AddRelayTreeNode(relayTree, roots, new HashSet<Guid>());
      }
      if (this.channel!=channel) {
        this.channel = channel;
        this.refresh.OnCanExecuteChanged();
      }
    }
  }
}

