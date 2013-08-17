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
using PeerCastStation.Core;

namespace PeerCastStation.WPF.CoreSettings
{
  class PortListItem
  {
    internal OutputListener Listener { get; set; }

    internal PortListItem(OutputListener listener)
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

    public override int GetHashCode()
    {
      return Listener.GetHashCode();
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
    internal YellowPageItem(string name, IYellowPageClient yellowpage)
    {
      this.Name = name;
      this.YellowPageClient = yellowpage;
    }

    internal YellowPageItem(IYellowPageClient yellowpage)
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
    internal IYellowPageClientFactory Factory { get; private set; }
    internal YellowPageFactoryItem(IYellowPageClientFactory factory)
    {
      this.Factory = factory;
    }
    public override string ToString()
    {
      return this.Factory.Name;
    }
  }
}
