using System;
using System.Net;
using System.Threading.Tasks;
using System.Collections.Generic;
using PeerCastStation.Core;
using PeerCastStation.Core.Http;
using Microsoft.Owin;

namespace PeerCastStation.UI.HTTP
{
  static class OwinContextExtensions
  {
    public static PeerCast GetPeerCast(this IOwinContext ctx)
    {
      if (ctx.Environment.TryGetValue(OwinEnvironment.PeerCastStation.PeerCast, out var obj)) {
        return obj as PeerCast; 
      }
      else {
        return null;
      }
    }

    public static AccessControlInfo GetAccessControlInfo(this IOwinContext ctx)
    {
      if (ctx.Environment.TryGetValue(OwinEnvironment.PeerCastStation.AccessControlInfo, out var obj)) {
        return obj as AccessControlInfo; 
      }
      else {
        return null;
      }
    }

    public static void Upgrade(this IOwinContext ctx, Func<IDictionary<string,object>, Task> handler)
    {
      var upgradeAction =
        (Action<IDictionary<string,object>, Func<IDictionary<string,object>, Task>>)
        ctx.Environment[OwinEnvironment.Opaque.Upgrade];
      upgradeAction.Invoke(new Dictionary<string,object>(), handler);
    }

    public static int? GetPCPVersion(this IOwinRequest request)
    {
      if (Int32.TryParse(request.Headers.Get("x-peercast-pcp"), out var ver)) {
        return ver;
      }
      else {
        return null;
      }
    }

    public static long? GetPCPPos(this IOwinRequest request)
    {
      if (Int64.TryParse(request.Headers.Get("x-peercast-pos"), out var pos)) {
        return pos;
      }
      else {
        return null;
      }
    }

    public static IPEndPoint GetRemoteEndPoint(this IOwinRequest request)
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
