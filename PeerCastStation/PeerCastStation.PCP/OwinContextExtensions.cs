// PeerCastStation, a P2P streaming servent.
// Copyright (C) 2019 Ryuichi Sakamoto (kumaryu@kumaryu.net)
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
using PeerCastStation.Core;
using PeerCastStation.Core.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net;

namespace PeerCastStation.PCP
{
  static class OwinContextExtensions
  {
    public static PeerCast GetPeerCast(this OwinEnvironment ctx)
    {
      if (ctx.Environment.TryGetValue(OwinEnvironment.PeerCastStation.PeerCast, out var obj)) {
        return obj as PeerCast; 
      }
      else {
        return null;
      }
    }

    public static AccessControlInfo GetAccessControlInfo(this OwinEnvironment ctx)
    {
      if (ctx.Environment.TryGetValue(OwinEnvironment.PeerCastStation.AccessControlInfo, out var obj)) {
        return obj as AccessControlInfo; 
      }
      else {
        return null;
      }
    }

    public static void Upgrade(this OwinEnvironment ctx, Func<IDictionary<string,object>, Task> handler)
    {
      var upgradeAction =
        (Action<IDictionary<string,object>, Func<IDictionary<string,object>, Task>>)
        ctx.Environment[OwinEnvironment.Opaque.Upgrade];
      upgradeAction.Invoke(new Dictionary<string,object>(), handler);
    }

    public static int? GetPCPVersion(this OwinEnvironment.OwinRequest request)
    {
      if (Int32.TryParse(request.Headers.Get("x-peercast-pcp"), out var ver)) {
        return ver;
      }
      else {
        return null;
      }
    }

    public static long? GetPCPPos(this OwinEnvironment.OwinRequest request)
    {
      if (Int64.TryParse(request.Headers.Get("x-peercast-pos"), out var pos)) {
        return pos;
      }
      else {
        return null;
      }
    }

    public static IPEndPoint GetRemoteEndPoint(this OwinEnvironment.OwinRequest request)
    {
      if (IPAddress.TryParse(request.RemoteIpAddress, out var addr)) {
        return new IPEndPoint(addr, request.RemotePort ?? 0);
      }
      else {
        return null;
      }
    }

  }

}

