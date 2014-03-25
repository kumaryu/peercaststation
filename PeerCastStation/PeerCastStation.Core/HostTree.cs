using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace PeerCastStation.Core
{
  public class HostTree
  {
    public IEnumerable<HostTreeNode> Nodes { get; private set; }
    public HostTree(IEnumerable<Host> hosts)
    {
      var nodes = new Dictionary<IPEndPoint, HostTreeNode>();
      var roots = new List<HostTreeNode>();
      foreach (var host in hosts) {
        var endpoint = (host.GlobalEndPoint==null || host.GlobalEndPoint.Port==0) ? host.LocalEndPoint : host.GlobalEndPoint;
        if (endpoint==null) continue;
        nodes[endpoint] = new HostTreeNode(host);
      }
      foreach (var node in nodes.Values) {
        var uphost = node.Host.Extra.GetHostUphostEndPoint();
        if (uphost!=null && nodes.ContainsKey(uphost)) {
          nodes[uphost].Children.Add(node);
        }
        else {
          roots.Add(node);
        }
      }
      Nodes = roots;
    }

    public HostTree(Channel channel)
      : this(new Host[] { channel.SelfNode }.Concat(channel.Nodes))
    {
    }
  }

  public class HostTreeNode
  {
    public Host Host { get; private set; }
    public IList<HostTreeNode> Children { get; private set; }
    public HostTreeNode(Host host)
    {
      this.Host = host;
      this.Children = new List<HostTreeNode>();
    }
  }

}
