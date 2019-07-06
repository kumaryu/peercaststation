using Owin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Owin.Builder;
using System.Threading;

namespace PeerCastStation.Core.Http
{
  internal class PeerCastOwinAppBuilder
    : AppBuilder
  {
    public PeerCastApplication Application { get; private set; }
    public PeerCast PeerCast { get; private set; }

    private List<Func<Task>> onInitActions = new List<Func<Task>>();
    public IReadOnlyList<Func<Task>> OnInitActions { get { return onInitActions; } }
    public IDictionary<string,object> Capabilities { get; private set; } = new Dictionary<string,object>(StringComparer.Ordinal);
    public TextWriter TraceOutput {
      get { return Properties[OwinEnvironment.Host.TraceOutput] as TextWriter; }
      set { Properties[OwinEnvironment.Host.TraceOutput] = value; }
    }
    public IList<IDictionary<string,object>> Addresses { get; private set; } = new List<IDictionary<string,object>>();

    public void AddAddress(string scheme, string host, string port, string path)
    {
      Addresses.Add(new Dictionary<string,object>(StringComparer.Ordinal) {
        { "scheme", scheme },
        { "host", host },
        { "port", port },
        { "path", path },
      });
    }

    public PeerCastOwinAppBuilder(PeerCastApplication app, PeerCast peerCast, CancellationToken cancellationToken)
    {
      Application = app;
      PeerCast = peerCast;
      Properties[OwinEnvironment.Owin.Version] = "1.0.1";
      Properties[OwinEnvironment.Opaque.Version] = "1.0";
      Properties[OwinEnvironment.PeerCastStation.PeerCastApplication] = app;
      Properties[OwinEnvironment.PeerCastStation.PeerCast] = peerCast;
      Properties[OwinEnvironment.Server.OnInit] = new Action<Func<Task>>(func => onInitActions.Add(func));
      Properties[OwinEnvironment.Server.OnDispose] = cancellationToken;
      Properties[OwinEnvironment.Server.Capabilities] = Capabilities;
      Properties[OwinEnvironment.Host.TraceOutput] = TextWriter.Null;
      Properties[OwinEnvironment.Host.Addresses] = new List<Dictionary<string,object>>();
    }
  }

  internal class LoggerWriter
    : TextWriter
  {
    public override System.Text.Encoding Encoding {
      get { return System.Text.Encoding.UTF8; }
    }

    public Logger Logger { get; private set; }
    public LogLevel Level { get; private set; }
    private System.Text.StringBuilder buffer = new System.Text.StringBuilder(256);

    public LoggerWriter(Logger logger, LogLevel level)
    {
      Logger = logger;
      Level = level;
    }

    public override void Write(char value)
    {
      buffer.Append(value);
      if (value=='\n') Flush();
    }

    public override void Write(char[] buffer, int index, int count)
    {
      if (buffer==null) throw new ArgumentNullException("buffer");
      if (index<0) throw new ArgumentOutOfRangeException("index");
      if (count<0) throw new ArgumentOutOfRangeException("count");
      if (buffer.Length-index<count) throw new ArgumentException();
      this.buffer.Append(buffer, index, count);
      if (count==0 || buffer[index+count-1]=='\n') Flush();
    }

    public override void Write(string value)
    {
      if (value==null) return;
      buffer.Append(value);
      if (value.EndsWith("\n")) Flush();
    }

    public override void Flush()
    {
      if (buffer.Length==0) return;
      Logger.Write(Level, buffer.ToString());
      buffer.Clear();
    }
  }

  [Plugin(PluginType.Protocol, PluginPriority.Higher)]
  public class OwinHost
    : IDisposable
  {
    private class Factory
      : IDisposable
    {
      public OwinHost Host { get; private set; }
      public int Key { get; private set; }
      public Action<IAppBuilder> ConfigAction { get; private set; }

      public Factory(OwinHost host, int key, Action<IAppBuilder> action)
      {
        Host = host;
        Key = key;
        ConfigAction = action;
      }

      public void Dispose()
      {
        if (Host==null || Key<0) return;
        Host.Unregister(Key);
      }
    }

    private Logger logger = new Logger(typeof(OwinHost));
    private SortedList<int,Factory> applicationFactories = new SortedList<int, Factory>();
    private CancellationTokenSource stopCancellationTokenSource = new CancellationTokenSource();
    private int nextKey = 0;

    private Func<IDictionary<string,object>, Task> owinApp = null;
    public Func<IDictionary<string,object>, Task> OwinApp {
      get {
        if (owinApp==null) {
          owinApp = Build();
        }
        return owinApp;
      }
    }

    public PeerCastApplication Application { get; private set; }
    public PeerCast PeerCast { get; private set; }

    public OwinHost(PeerCastApplication application, PeerCast peerCast)
    {
      Application = application;
      PeerCast = peerCast;
    }

    public void Dispose()
    {
      owinApp = null;
      stopCancellationTokenSource.Cancel();
    }

    public IDisposable Register(Action<IAppBuilder> configAction)
    {
      var key = nextKey++;
      var factory = new Factory(this, key, configAction);
      applicationFactories.Add(key, factory);
      return factory;
    }

    internal void Unregister(int key)
    {
      applicationFactories.Remove(key);
      owinApp = null;
    }

    private Func<IDictionary<string,object>, Task> Build()
    {
      var builder = new PeerCastOwinAppBuilder(Application, PeerCast, stopCancellationTokenSource.Token);
      foreach (var factory in applicationFactories) {
        factory.Value.ConfigAction(builder);
      }
      builder.TraceOutput = new LoggerWriter(logger, LogLevel.Debug);
      var appfunc = builder.Build<Func<IDictionary<string,object>, Task>>();
      return (env) => {
        env[OwinEnvironment.Server.Capabilities] = builder.Capabilities;
        env[OwinEnvironment.Host.TraceOutput] = builder.TraceOutput;
        return appfunc(env);
      };
    }

    public Task Invoke(
      HttpRequest req,
      ConnectionStream stream,
      IPEndPoint localEndPoint,
      IPEndPoint remoteEndPoint,
      AccessControlInfo accessControlInfo,
      CancellationToken cancellationToken)
    {
      var ctx = new OwinContext(PeerCast, req, stream, localEndPoint, remoteEndPoint, accessControlInfo);
      return ctx.Invoke(OwinApp, cancellationToken);
    }
  }

}
