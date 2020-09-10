using PeerCastStation.Core;
using PeerCastStation.Core.Http;

namespace PeerCastStation.UI.HTTP
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

  }

}
