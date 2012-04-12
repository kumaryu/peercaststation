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
    private static Logger logger = new Logger(typeof(Application));
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
      peerCast.SourceStreamFactories.Add(new PeerCastStation.PCP.PCPSourceStreamFactory(peerCast));
      peerCast.SourceStreamFactories.Add(new PeerCastStation.HTTP.HTTPSourceStreamFactory(peerCast));
      peerCast.OutputStreamFactories.Add(new PeerCastStation.PCP.PCPPongOutputStreamFactory(peerCast));
      peerCast.OutputStreamFactories.Add(new PeerCastStation.PCP.PCPOutputStreamFactory(peerCast));
      peerCast.OutputStreamFactories.Add(new PeerCastStation.HTTP.HTTPOutputStreamFactory(peerCast));
      peerCast.OutputStreamFactories.Add(new PeerCastStation.Ohaoha.OhaohaCheckOutputStreamFactory(peerCast));
      peerCast.OutputStreamFactories.Add(new PeerCastStation.HTTP.HTTPDummyOutputStreamFactory(peerCast));
      peerCast.AddContentReader(new PeerCastStation.ASF.ASFContentReader());
      peerCast.AddContentReader(new RawContentReader());

      LoadSettings();
      var uis = userInterfaceFactories.Select(factory => factory.CreateUserInterface()).ToArray();
      foreach (var ui in uis) {
        ui.Start(this);
      }
      stoppedEvent.WaitOne();
      foreach (var ui in uis) {
        ui.Stop();
      }
      SaveSettings();

      peerCast.Stop();
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

    void LoadSettings()
    {
      var settings = PeerCastStation.Properties.Settings.Default;
      if (settings.AccessController!=null) {
        peerCast.AccessController.MaxPlays  = settings.AccessController.MaxDirects;
        peerCast.AccessController.MaxRelays = settings.AccessController.MaxRelays;
        peerCast.AccessController.MaxPlaysPerChannel  = settings.AccessController.MaxDirectsPerChannel;
        peerCast.AccessController.MaxRelaysPerChannel = settings.AccessController.MaxRelaysPerChannel;
        peerCast.AccessController.MaxUpstreamRate     = settings.AccessController.MaxUpstreamRate;
      }
      if (settings.BroadcastID==Guid.Empty) {
        peerCast.BroadcastID = Guid.NewGuid();
      }
      else {
        peerCast.BroadcastID = settings.BroadcastID;
      }
      if (settings.Listeners!=null) {
        foreach (var listener in settings.Listeners) {
          try {
            peerCast.StartListen(listener.EndPoint, listener.LocalAccepts, listener.GlobalAccepts);
          }
          catch (System.Net.Sockets.SocketException e) {
            logger.Error(e);
          }
        }
      }
      if (peerCast.OutputListeners.Count==0) {
        System.Net.IPAddress listen_addr;
        if (!System.Net.IPAddress.TryParse(settings.DefaultListenAddress, out listen_addr)) {
          listen_addr = System.Net.IPAddress.Any;
        }
        try {
          peerCast.StartListen(
            new System.Net.IPEndPoint(listen_addr, settings.DefaultListenPort),
            OutputStreamType.All,
            OutputStreamType.Metadata | OutputStreamType.Relay);
        }
        catch (System.Net.Sockets.SocketException e) {
          logger.Error(e);
        }
      }
      if (settings.YellowPages!=null) {
        foreach (var yellowpage in settings.YellowPages) {
          peerCast.AddYellowPage(yellowpage.Protocol, yellowpage.Name, yellowpage.Uri);
        }
      }
    }

    void SaveSettings()
    {
      var settings = PeerCastStation.Properties.Settings.Default;
      settings.AccessController = new PeerCastStation.Properties.AccessControllerSettings {
        MaxDirects           = peerCast.AccessController.MaxPlays,
        MaxRelays            = peerCast.AccessController.MaxRelays,
        MaxDirectsPerChannel = peerCast.AccessController.MaxPlaysPerChannel,
        MaxRelaysPerChannel  = peerCast.AccessController.MaxRelaysPerChannel,
        MaxUpstreamRate      = peerCast.AccessController.MaxUpstreamRate,
      };
      settings.BroadcastID = peerCast.BroadcastID;
      settings.Listeners = peerCast.OutputListeners.Select(listener => 
        new PeerCastStation.Properties.ListenerSettings {
          EndPoint      = listener.LocalEndPoint,
          GlobalAccepts = listener.GlobalOutputAccepts,
          LocalAccepts  = listener.LocalOutputAccepts,
        }
      ).ToArray();
      settings.YellowPages = peerCast.YellowPages.Select(yellowpage =>
        new PeerCastStation.Properties.YellowPageSettings {
          Protocol = yellowpage.Uri.Scheme,
          Name     = yellowpage.Name,
          Uri      = yellowpage.Uri,
        }
      ).ToArray();
      settings.Save();
    }

    [STAThread]
    static void Main(string[] args)
    {
      (new Application()).Run();
    }
  }
}
