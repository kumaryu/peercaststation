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
using PeerCastStation.Core;
using PeerCastStation.Core.Http;

namespace PeerCastStation.HTTP
{
  static class OwinContextExtensions
  {
    public static PeerCast? GetPeerCast(this OwinEnvironment ctx)
    {
      if (ctx.Environment.TryGetValue(OwinEnvironment.PeerCastStation.PeerCast, out var obj)) {
        return obj as PeerCast; 
      }
      else {
        return null;
      }
    }

    public static AccessControlInfo? GetAccessControlInfo(this OwinEnvironment ctx)
    {
      if (ctx.Environment.TryGetValue(OwinEnvironment.PeerCastStation.AccessControlInfo, out var obj)) {
        return obj as AccessControlInfo; 
      }
      else {
        return null;
      }
    }
  }

}

