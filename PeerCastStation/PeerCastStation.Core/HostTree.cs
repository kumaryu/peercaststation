using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace PeerCastStation.Core
{
  public class HostTree
  {
    public IList<HostTreeNode> Nodes { get; private set; }

    public HostTree(IList<HostTreeNode> nodes)
    {
      this.Nodes = nodes;
    }

    public HostTree(IPeerCast peerCast, Channel channel)
      : this(new HostTreeNode[] { CreateChannelHostTree(peerCast, channel) })
    {
    }

    public static IList<HostTreeNode> CreateHostTree(IEnumerable<Host> hosts)
    {
      var nodemap = new Dictionary<IPEndPoint, HostTreeNode>();
      var topnodes = new List<HostTreeNode>();
      foreach (var host in hosts) {
        var endpoint = (host.GlobalEndPoint==null || host.GlobalEndPoint.Port==0) ? host.LocalEndPoint : host.GlobalEndPoint;
        if (endpoint==null) continue;
        nodemap[endpoint] = new HostTreeNode(host);
      }
      foreach (var node in nodemap.Values) {
        var uphost = node.Host.Extra.GetHostUphostEndPoint();
        if (uphost!=null && nodemap.ContainsKey(uphost)) {
          nodemap[uphost].Children.Add(node);
        }
        else {
          topnodes.Add(node);
        }
      }
      return topnodes;
    }

    public static HostTreeNode CreateChannelHostTree(IPeerCast peerCast, Channel channel)
    {
      var nodes = channel.Nodes;
      var children =
        channel.OutputStreams
          .Select(os => os.GetConnectionInfo())
          .Where(ci => ci.Type.HasFlag(ConnectionType.Relay))
          .Where(ci => ci.RemoteSessionID!=null)
          .Select(ci => nodes.SingleOrDefault(node => node.SessionID==ci.RemoteSessionID!.Value))
          .Where(node => node!=null);
      var tree = CreateHostTree(nodes);
      return new HostTreeNode(
        CreateChannelHost(peerCast, channel),
        tree.Where(node => children.Contains(node.Host)).ToArray());
    }

    public static Host CreateChannelHost(IPeerCast peerCast, Channel channel)
    {
      var source = channel.SourceStream;
      var listeners = peerCast.GetListenerInfos().Where(
          listener => listener.NetworkType==channel.Network &&
          listener.AccessControl.Accepts.HasFlag(OutputStreamType.Relay)).ToArray();
      var host = new HostBuilder();
      host.SessionID      = peerCast.SessionID;
      host.LocalEndPoint  = listeners.FirstOrDefault(listener => listener.Locality==NetworkLocality.Local)?.EndPoint;
      host.GlobalEndPoint = listeners.FirstOrDefault(listener => listener.Locality==NetworkLocality.Global)?.EndPoint;
      bool globalPortIsOpened =
        peerCast.GetListenerInfos().Where(listener =>
          listener.NetworkType==channel.Network &&
          listener.Locality==NetworkLocality.Global &&
          listener.AccessControl.Accepts.HasFlag(OutputStreamType.Relay) &&
          listener.Status==PortStatus.Open)
          .Any();
      host.IsFirewalled   = !globalPortIsOpened;
      host.DirectCount    = channel.LocalDirects;
      host.RelayCount     = channel.LocalRelays;
      host.IsDirectFull   = channel.IsDirectFull;
      host.IsRelayFull    = channel.IsRelayFull;
      host.IsReceiving    = source!=null && (source.GetConnectionInfo().RecvRate ?? 0.0f)>0;
      return host.ToHost();
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

    public HostTreeNode(Host host, IList<HostTreeNode> children)
    {
      this.Host = host;
      this.Children = children;
    }
  }

}
