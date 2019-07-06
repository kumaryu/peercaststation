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

  }

}
