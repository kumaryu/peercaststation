// PeerCastStation, a P2P streaming servent.
// Copyright (C) 2011 Ryuichi Sakamoto (kumaryu@kumaryu.net)
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
using System.Net;

namespace PeerCastStation.Core
{
  static public class Utils
  {
    static public bool IsSiteLocal(IPAddress addr)
    {
      switch (addr.AddressFamily) {
      case System.Net.Sockets.AddressFamily.InterNetwork:
        var addr_bytes = addr.GetAddressBytes();
        return
          addr_bytes[0] == 10 ||
          addr_bytes[0] == 127 ||
          addr_bytes[0] == 169 && addr_bytes[1] == 254 ||
          addr_bytes[0] == 172 && (addr_bytes[1]&0xF0) == 16 ||
          addr_bytes[0] == 192 && addr_bytes[1] == 168;
      case System.Net.Sockets.AddressFamily.InterNetworkV6:
        return
          addr.IsIPv6LinkLocal ||
          addr.IsIPv6SiteLocal ||
          addr==IPAddress.IPv6Loopback;
      default:
        return false;
      }
    }

    static public int GetAddressLocality(IPAddress addr)
    {
      switch (addr.AddressFamily) {
      case System.Net.Sockets.AddressFamily.InterNetwork:
        if (addr==IPAddress.Any || addr==IPAddress.None || addr==IPAddress.Broadcast) return -1;
        if (addr==IPAddress.Loopback) return 0;
        if (IsSiteLocal(addr)) return 1;
        return 2;
      case System.Net.Sockets.AddressFamily.InterNetworkV6:
        if (addr==IPAddress.IPv6Any || addr==IPAddress.IPv6None) return -1;
        if (addr==IPAddress.IPv6Loopback) return 0;
        if (IsSiteLocal(addr)) return 1;
        return 2;
      default:
        return -1;
      }
    }

    static public void ReplaceCollection<T>(ref T collection, Func<T,T> newcollection_func) where T : class
    {
      bool replaced = false;
      while (!replaced) {
        var prev = collection;
        var new_collection = newcollection_func(collection);
        System.Threading.Interlocked.CompareExchange(ref collection, new_collection, prev);
        replaced = Object.ReferenceEquals(collection, new_collection);
      }
    }

  }
}
