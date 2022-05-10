using PeerCastStation.Core.Http;
using PeerCastStation.UI.HTTP.JSONRPC;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace PeerCastStation.UI.HTTP
{
  public partial class PeerCastStationJS : PeerCastStationJSBase
  {
    private Type hostType;
    private RPCMethodInfo[] methods;
    private class RPCMethodInfo
    {
      public string Name { get; }
      public string[] Args { get; }

      public RPCMethodInfo(MethodInfo method, RPCMethodAttribute attr)
      {
        Name = attr.Name;
        Args = method.GetParameters()
                .Where(p => p.ParameterType!=typeof(OwinEnvironment))
                .Select(p => p.Name!)
                .ToArray();
      }

    }

    public PeerCastStationJS(Type hostType)
    {
      this.hostType = hostType;
      methods =
        hostType
        .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        .Select(method => (method, attr: method.GetCustomAttribute<RPCMethodAttribute>(true)))
        .Where(t => t.attr!=null)
        .Select(t => new RPCMethodInfo(t.method, t.attr!))
        .ToArray();
    }
  }

  public class PeerCastStationJSApp
  {
    private byte[] contents;

    public PeerCastStationJSApp(Type hostType)
    {
      var builder = new PeerCastStationJS(hostType);
      contents = System.Text.Encoding.UTF8.GetBytes(builder.TransformText());
    }
    
    public async Task Invoke(OwinEnvironment ctx)
    {
      var cancel_token = ctx.Request.CallCancelled;
      ctx.Response.ContentType = "application/javascript";
      ctx.Response.ContentLength = contents.LongLength;
      var acinfo = ctx.GetAccessControlInfo();
      if (acinfo?.AuthenticationKey!=null) {
        ctx.Response.Headers.Add("Set-Cookie", $"auth={acinfo.AuthenticationKey.GetToken()}; Path=/");
      }
      await ctx.Response.WriteAsync(contents, cancel_token).ConfigureAwait(false);
    }

  }

}

