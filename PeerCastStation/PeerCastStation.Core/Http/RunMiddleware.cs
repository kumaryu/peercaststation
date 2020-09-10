using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PeerCastStation.Core.Http
{
  public class RunMiddleware
  {
    private Func<IDictionary<string, object>, Task> appFunc;

    public RunMiddleware(Func<IDictionary<string, object>, Task> nextApp, Func<IDictionary<string, object>, Task> appFunc)
    {
      this.appFunc = appFunc;
    }

    public RunMiddleware(Func<IDictionary<string, object>, Task> nextApp, Func<OwinEnvironment, Task> appFunc)
      : this(nextApp, (args) => appFunc.Invoke(new OwinEnvironment(args)))
    {
    }

    public Task Invoke(IDictionary<string, object> arg)
    {
      return appFunc.Invoke(arg);
    }

  }

  public static class RunExtentions
  {
    public static void Run(this IAppBuilder appBuilder, Func<OwinEnvironment, Task> appFunc)
    {
      appBuilder.Use<RunMiddleware>(appFunc);
    }

  }

}
