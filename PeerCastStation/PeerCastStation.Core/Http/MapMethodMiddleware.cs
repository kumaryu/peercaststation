using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PeerCastStation.Core.Http
{
  public class MapMethodOptions
  {
    public Func<IDictionary<string, object>, Task> Branch { get; set; }
    public IEnumerable<string> MethodMatch { get; set; }
    public Regex PathPattern { get; set; }
  }

  public class MapMethodMiddleware
  {
    private Func<IDictionary<string, object>, Task> nextApp;
    private Func<IDictionary<string, object>, Task> branch;
    private HashSet<string> methods;
    private Regex pathPattern;

    public MapMethodMiddleware(Func<IDictionary<string, object>, Task> nextApp, MapMethodOptions options)
    {
      this.nextApp = nextApp;
      this.branch  = options.Branch;
      this.methods = new HashSet<string>(options.MethodMatch);
      this.pathPattern = options.PathPattern;
    }

    public Task Invoke(IDictionary<string, object> arg)
    {
      var env = new OwinEnvironment(arg);
      var pathMatch = pathPattern.Match(env.Request.Path);
      if (pathMatch.Success) {
        if (env.TryGetValue(OwinEnvironment.Owin.RequestMethod, out string method) && methods.Contains(method)) {
          env.Response.StatusCode = System.Net.HttpStatusCode.OK;
          env.Environment[OwinEnvironment.Owin.RequestPathMatch] = pathMatch;
          var pathBase = env.Get(OwinEnvironment.Owin.RequestPathBase, "");
          env.Environment[OwinEnvironment.Owin.RequestPathBase] = pathBase + pathMatch.Value;
          env.Environment[OwinEnvironment.Owin.RequestPath] = env.Request.Path.Substring(pathMatch.Length);
          env.RemoveResponseHeader("Allow");
          return branch.Invoke(arg);
        }
        else {
          env.Response.StatusCode = System.Net.HttpStatusCode.MethodNotAllowed;
          var allow = env.GetResponseHeader("Allow", (string)null);
          if (allow==null) {
            allow = String.Join(", ", methods);
          }
          else {
            allow = String.Join(", ", Enumerable.Repeat(allow, 1).Concat(methods));
          }
          env.SetResponseHeader("Allow", allow);
        }
      }
      return nextApp.Invoke(arg);
    }

  }

  public static class MapMethodExtentions
  {
    public static IAppBuilder MapMethod(this IAppBuilder appBuilder, IEnumerable<string> methods, Regex pathPattern, Action<IAppBuilder> configuration)
    {
      var branchBuilder = appBuilder.New();
      configuration(branchBuilder);
      return appBuilder.Use<MapMethodMiddleware>(new MapMethodOptions { MethodMatch=methods, Branch=branchBuilder.Build(), PathPattern=pathPattern });
    }

    private static Regex CreatePathPattern(string path)
    {
      if (!path.StartsWith("/")) {
        path = "/" + path;
      }
      path = Regex.Escape(path);
      return new Regex("^" + path, RegexOptions.None);
    }

    public static IAppBuilder MapMethod(this IAppBuilder appBuilder, IEnumerable<string> methods, string path, Action<IAppBuilder> configuration)
    {
      var branchBuilder = appBuilder.New();
      configuration(branchBuilder);
      return appBuilder.Use<MapMethodMiddleware>(new MapMethodOptions { MethodMatch=methods, Branch=branchBuilder.Build(), PathPattern=CreatePathPattern(path) });
    }

    public static IAppBuilder MapMethod(this IAppBuilder appBuilder, string method, string path, Action<IAppBuilder> configuration)
    {
      method = method.ToUpperInvariant();
      if (method=="GET") {
        return MapMethod(appBuilder, new string[] { "GET", "HEAD" }, path, configuration);
      }
      else {
        return MapMethod(appBuilder, new string[] { method }, path, configuration);
      }
    }

    public static IAppBuilder MapGET(this IAppBuilder appBuilder, string path, Action<IAppBuilder> configuration)
    {
        return MapMethod(appBuilder, new string[] { "GET", "HEAD" }, path, configuration);
    }

    public static IAppBuilder MapPOST(this IAppBuilder appBuilder, string path, Action<IAppBuilder> configuration)
    {
        return MapMethod(appBuilder, new string[] { "POST" }, path, configuration);
    }

    public static IAppBuilder MapGET(this IAppBuilder appBuilder, Regex pathPattern, Action<IAppBuilder> configuration)
    {
        return MapMethod(appBuilder, new string[] { "GET", "HEAD" }, pathPattern, configuration);
    }

    public static IAppBuilder MapPOST(this IAppBuilder appBuilder, Regex pathPattern, Action<IAppBuilder> configuration)
    {
        return MapMethod(appBuilder, new string[] { "POST" }, pathPattern, configuration);
    }

  }

}
