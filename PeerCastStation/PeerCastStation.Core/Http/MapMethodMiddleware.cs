using System;
using System.Linq;
using System.Net;
using System.Collections.Generic;
using System.Threading.Tasks;
using Owin;

namespace PeerCastStation.Core.Http
{
  public class MapMethodOptions
  {
    public Func<IDictionary<string, object>, Task> Branch { get; set; }
    public IEnumerable<string> MethodMatch { get; set; }
  }

  public class MapMethodMiddleware
  {
    private Func<IDictionary<string, object>, Task> nextApp;
    private Func<IDictionary<string, object>, Task> branch;
    private HashSet<string> methods;

    public MapMethodMiddleware(Func<IDictionary<string, object>, Task> nextApp, MapMethodOptions options)
    {
      this.nextApp = nextApp;
      this.branch  = options.Branch;
      this.methods = new HashSet<string>(options.MethodMatch);
    }

    public Task Invoke(IDictionary<string, object> arg)
    {
      var env = new OwinEnvironment(arg);
      if (env.TryGetValue(OwinEnvironment.Owin.RequestMethod, out string method) && methods.Contains(method)) {
        return branch.Invoke(arg);
      }
      else {
        return nextApp.Invoke(arg);
      }
    }

  }

  public static class MapMethodExtentions
  {
    private class NotAllowed
    {
      public string Methods { get; set; }
      public Task Invoke(IDictionary<string, object> arg)
      {
        var env = new OwinEnvironment(arg);
        env.Environment[OwinEnvironment.Owin.ResponseStatusCode] = (int)HttpStatusCode.MethodNotAllowed;
        env.SetResponseHeader("Allow", Methods);
        return Task.Delay(0);
      }
    }

    public static IAppBuilder MapMethod(this IAppBuilder appBuilder, IEnumerable<string> methods, Action<IAppBuilder> configuration)
    {
      var allowedMethods = Enumerable.Empty<string>();
      if (appBuilder.Properties.TryGetValue("mapMethod.Allow", out var allows) && allows is IEnumerable<string>) {
        allowedMethods = (IEnumerable<string>)allows;
      }
      allowedMethods = allowedMethods.Concat(methods);
      appBuilder.Properties["mapMethod.Allow"] = allowedMethods;
      var branchBuilder = appBuilder.New();
      branchBuilder.Properties["builder.DefaultApp"] = new NotAllowed { Methods=String.Join(",", allowedMethods.Distinct()) };
      configuration(branchBuilder);
      return appBuilder.Use<MapMethodMiddleware>(new MapMethodOptions { MethodMatch=methods, Branch=(Func<IDictionary<string,object>, Task>)branchBuilder.Build(typeof(Func<IDictionary<string,object>, Task>)) });
    }

    public static IAppBuilder MapMethod(this IAppBuilder appBuilder, string method, Action<IAppBuilder> configuration)
    {
      if (method=="GET") {
        return MapMethod(appBuilder, new string[] { "GET", "HEAD" }, configuration);
      }
      else {
        return MapMethod(appBuilder, new string[] { method }, configuration);
      }
    }

  }
}
