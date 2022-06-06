using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace PeerCastStation.Core.Http
{
  public class NotFoundOwinApp
  {
    public Task Invoke(IDictionary<string, object> arg)
    {
      var status = (int)HttpStatusCode.NotFound;
      try {
        if (arg.TryGetValue(OwinEnvironment.Owin.ResponseStatusCode, out var code) && (Convert.ToInt32(code))/100==4) {
          status = Convert.ToInt32(code);
        }
      }
      catch (SystemException) {
      }
      arg[OwinEnvironment.Owin.ResponseStatusCode] = status;
      return Task.Delay(0);
    }
  }

  public class AppBuilder
    : IAppBuilder
  {
    public IDictionary<string, object> Properties { get; }
    private Func<IDictionary<string, object>, Task> nextApp;
    private static Func<IDictionary<string, object>, Task>? _defaultApp;
    public static Func<IDictionary<string, object>, Task> DefaultApp {
      get {
        if (_defaultApp==null) {
          _defaultApp = new NotFoundOwinApp().Invoke;
        }
        return _defaultApp;
      }
      set {
        _defaultApp = value;
      }
    }

    private class MiddlewareRegistration
    {
      public Type Middleware { get; }
      public object[] Args { get; }
      public MiddlewareRegistration(Type middleware, object[] args)
      {
        Middleware = middleware;
        Args = args;
      }

      public Func<IDictionary<string, object>, Task> Build(Func<IDictionary<string, object>, Task> nextApp)
      {
        var constructor =
          Middleware.GetConstructors()
          .Where(ctor => {
            var prms = ctor.GetParameters();
            if (prms.Length!=Args.Length+1) return false;
            var argTypes =
              Enumerable.Concat(
                Enumerable.Repeat(typeof(Func<IDictionary<string, object>, Task>), 1),
                Args.Select(arg => arg.GetType())
              ).ToArray();
            return Enumerable.Zip(prms, argTypes, (prm, type) => prm.ParameterType.IsAssignableFrom(type)).All(r => r);
          })
          .Single();
        var middleware = constructor.Invoke(Enumerable.Concat(Enumerable.Repeat(nextApp, 1), Args).ToArray());
        var invoke = middleware.GetType().GetMethod("Invoke", new [] { typeof(IDictionary<string, object>) });
        if (invoke==null) {
          throw new InvalidOperationException("Middleware must have suitable Invoke method.");
        }
        return (arg) => (Task)invoke.Invoke(middleware, new [] { arg })!;
      }
    }
    private Stack<MiddlewareRegistration> middlewares = new Stack<MiddlewareRegistration>();

    public AppBuilder()
      : this(DefaultApp)
    {
    }

    public AppBuilder(Func<IDictionary<string, object>, Task> app)
      : this(new Dictionary<string, object>(), app)
    {
    }

    private AppBuilder(IDictionary<string, object> properties, Func<IDictionary<string, object>, Task> app)
    {
      Properties = new Dictionary<string, object>(properties);
      nextApp = app;
    }

    public Func<IDictionary<string, object>, Task> Build()
    {
      return middlewares.Aggregate(nextApp, (app, middleware) => middleware.Build(app));
    }

    public IAppBuilder New()
    {
      return new AppBuilder(nextApp);
    }

    public IAppBuilder Use<T>(params object[] args)
    {
      middlewares.Push(new MiddlewareRegistration(typeof(T), args));
      return this;
    }
  }

}
