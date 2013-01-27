using System;
using System.Collections.Generic;
using PeerCastStation.Core;

namespace PeerCastStation.WPF.CoreSettings
{
  class PortListItem
  {
    public OutputListener Listener { get; set; }

    public PortListItem(OutputListener listener)
    {
      Listener = listener;
    }

    public override string ToString()
    {
      var addr = Listener.LocalEndPoint.Address.ToString();
      if (Listener.LocalEndPoint.Address.Equals(System.Net.IPAddress.Any)) addr = "IPv4 Any";
      if (Listener.LocalEndPoint.Address.Equals(System.Net.IPAddress.IPv6Any)) addr = "IPv6 Any";
      var local_accepts = "無し";
      if ((Listener.LocalOutputAccepts & ~OutputStreamType.Metadata) != OutputStreamType.None)
      {
        var accepts = new List<string>();
        if ((Listener.LocalOutputAccepts & OutputStreamType.Interface) != 0) accepts.Add("操作");
        if ((Listener.LocalOutputAccepts & OutputStreamType.Relay) != 0) accepts.Add("リレー");
        if ((Listener.LocalOutputAccepts & OutputStreamType.Play) != 0) accepts.Add("視聴");
        local_accepts = String.Join(",", accepts.ToArray());
      }
      var global_accepts = "無し";
      if ((Listener.GlobalOutputAccepts & ~OutputStreamType.Metadata) != OutputStreamType.None)
      {
        var accepts = new List<string>();
        if ((Listener.GlobalOutputAccepts & OutputStreamType.Interface) != 0) accepts.Add("操作");
        if ((Listener.GlobalOutputAccepts & OutputStreamType.Relay) != 0) accepts.Add("リレー");
        if ((Listener.GlobalOutputAccepts & OutputStreamType.Play) != 0) accepts.Add("視聴");
        global_accepts = String.Join(",", accepts.ToArray());
      }
      return String.Format(
        "{0}:{1} LAN:{2} WAN:{3}",
        addr,
        Listener.LocalEndPoint.Port,
        local_accepts,
        global_accepts);
    }

    public override bool Equals(object obj)
    {
      var other = obj as PortListItem;
      if (other == null)
        return false;
      return Listener == other.Listener;
    }
  }

  class YellowPageItem
  {
    public string Name { get; private set; }
    public IYellowPageClient YellowPageClient { get; private set; }
    public YellowPageItem(string name, IYellowPageClient yellowpage)
    {
      this.Name = name;
      this.YellowPageClient = yellowpage;
    }

    public YellowPageItem(IYellowPageClient yellowpage)
      : this(String.Format("{0} ({1})", yellowpage.Name, yellowpage.Uri), yellowpage)
    {
    }

    public override string ToString()
    {
      return this.Name;
    }
  }

  class YellowPageFactoryItem
  {
    public IYellowPageClientFactory Factory { get; private set; }
    public YellowPageFactoryItem(IYellowPageClientFactory factory)
    {
      this.Factory = factory;
    }
    public override string ToString()
    {
      return this.Factory.Name;
    }
  }
}
