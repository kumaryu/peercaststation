using System;
using System.Linq;
using System.Collections.Generic;
using PeerCastStation.Core;
using System.Threading;

namespace PeerCastStation.Main
{
  public class Application
    : PeerCastApplication
  {
    private List<IUserInterfaceFactory> userInterfaceFactories = new List<IUserInterfaceFactory>();
    public IList<IUserInterfaceFactory> UserInterfaceFactories {
      get { return userInterfaceFactories; }
    }

    public Application()
    {
      LoadPlugins();
    }

    ManualResetEvent stoppedEvent = new ManualResetEvent(false);
    override public void Stop()
    {
      stoppedEvent.Set();
    }

    PeerCast peerCast = new PeerCast();
    override public PeerCast PeerCast { get { return peerCast; } }
    public void Run()
    {
      peerCast.YellowPageFactories.Add(new PeerCastStation.PCP.PCPYellowPageClientFactory(peerCast));
      peerCast.SourceStreamFactories["pcp"]  = new PeerCastStation.PCP.PCPSourceStreamFactory(peerCast);
      peerCast.SourceStreamFactories["http"] = new PeerCastStation.HTTP.HTTPSourceStreamFactory(peerCast);
      peerCast.OutputStreamFactories.Add(new PeerCastStation.PCP.PCPPongOutputStreamFactory(peerCast));
      peerCast.OutputStreamFactories.Add(new PeerCastStation.PCP.PCPOutputStreamFactory(peerCast));
      peerCast.OutputStreamFactories.Add(new PeerCastStation.HTTP.HTTPOutputStreamFactory(peerCast));
      peerCast.OutputStreamFactories.Add(new PeerCastStation.Ohaoha.OhaohaCheckOutputStreamFactory(peerCast));
      peerCast.OutputStreamFactories.Add(new PeerCastStation.HTTP.HTTPDummyOutputStreamFactory(peerCast));
      peerCast.AddContentReader(new PeerCastStation.ASF.ASFContentReader());
      peerCast.AddContentReader(new RawContentReader());

      var uis = userInterfaceFactories.Select(factory => factory.CreateUserInterface()).ToArray();
      foreach (var ui in uis) {
        ui.Start(this);
      }
      stoppedEvent.WaitOne();
      foreach (var ui in uis) {
        ui.Stop();
      }
    }

    public static readonly string PluginPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
    IEnumerable<KeyValuePair<PluginAttribute, Type>> LoadPluginAssemblies()
    {
      var res = Enumerable.Empty<KeyValuePair<PluginAttribute, Type>>();
      foreach (var dll in System.IO.Directory.GetFiles(PluginPath, "*.dll")) {
        var asm = System.Reflection.Assembly.LoadFrom(dll);
        res = res.Concat(
          asm.GetTypes().Where(
            type => type.GetCustomAttributes(typeof(PluginAttribute), true).Length>0
          ).Select(
            type => new KeyValuePair<PluginAttribute, Type>((PluginAttribute)type.GetCustomAttributes(typeof(PluginAttribute), true)[0], type)
          )
        );
      }
      return res;
    }

    void LoadPlugins()
    {
      var types = LoadPluginAssemblies();
      foreach (var attr_type in types) {
        switch (attr_type.Key.Type) {
        case PluginType.UserInterface:
          var constructor = attr_type.Value.GetConstructor(Type.EmptyTypes);
          if (constructor!=null) {
            var obj = constructor.Invoke(null) as IUserInterfaceFactory;
            if (obj!=null) userInterfaceFactories.Add(obj);
          }
          break;
        default:
          break;
        }
      }
    }

    [STAThread]
    static void Main(string[] args)
    {
      (new Application()).Run();
    }
  }
}
