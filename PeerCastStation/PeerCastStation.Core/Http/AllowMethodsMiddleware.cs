using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Owin;

namespace PeerCastStation.Core.Http
{
  public class AllowMethodsMiddleware
  {
    private Func<IDictionary<string, object>, Task> nextApp;
    private HashSet<string> methods;

    public AllowMethodsMiddleware(Func<IDictionary<string, object>, Task> nextApp, IEnumerable<string> methods)
    {
      this.nextApp = nextApp;
      this.methods = new HashSet<string>(methods);
      if (this.methods.Contains("GET") && !this.methods.Contains("HEAD")) {
        this.methods.Add("HEAD");
      }
    }

    public AllowMethodsMiddleware(Func<IDictionary<string, object>, Task> nextApp, params string[] methods)
      : this(nextApp, (IEnumerable<string>)methods)
    {
    }

    public Task Invoke(IDictionary<string, object> arg)
    {
      var env = new OwinEnvironment(arg);
      if (env.TryGetValue(OwinEnvironment.Owin.RequestMethod, out string method) && methods.Contains(method)) {
        return nextApp.Invoke(arg);
      }
      else {
        env.Environment[OwinEnvironment.Owin.ResponseStatusCode] = (int)HttpStatusCode.MethodNotAllowed;
        env.SetResponseHeader("Allow", String.Join(",", methods));
        return Task.Delay(0);
      }
    }
  }

  public static class AllowMethodsExtentions
  {
    public static IAppBuilder UseAllowMethods(this IAppBuilder appBuilder, params string[] methods)
    {
      return appBuilder.Use<AllowMethodsMiddleware>(new object[] { methods });
    }
  }

}

