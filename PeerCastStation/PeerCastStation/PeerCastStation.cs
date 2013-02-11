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

    private IEnumerable<Type> plugins;
    override public IEnumerable<Type> Plugins {
      get { return plugins; }
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
      LoadSettings();
      peerCast.ChannelMonitors.Add(new ChannelCleaner(peerCast));
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
    IEnumerable<Type> LoadPluginAssemblies()
    {
      var res = Enumerable.Empty<Type>();
      foreach (var dll in System.IO.Directory.GetFiles(PluginPath, "*.dll")) {
        var asm = System.Reflection.Assembly.LoadFrom(dll);
        res = res.Concat(
          asm.GetTypes().Where(
            type => type.GetCustomAttributes(typeof(PluginAttribute), true).Length>0
          ).OrderBy(
            type => ((PluginAttribute)(type.GetCustomAttributes(typeof(PluginAttribute), true)[0])).Priority
          )
        );
      }
      return res;
    }

    private void AddUserInterfaceFactory(Type type)
    {
      var constructor = type.GetConstructor(Type.EmptyTypes);
      if (constructor!=null) {
        var obj = constructor.Invoke(null) as IUserInterfaceFactory;
        if (obj!=null) userInterfaceFactories.Add(obj);
      }
    }

    private void AddSourceStreamFactory(Type type)
    {
      ISourceStreamFactory factory = null;
      var constructor = type.GetConstructor(Type.EmptyTypes);
      if (constructor!=null) {
        factory = constructor.Invoke(null) as ISourceStreamFactory;
      }
      else if ((constructor=type.GetConstructor(new Type[] { typeof(PeerCast) }))!=null) {
        factory = constructor.Invoke(new object[] { peerCast }) as ISourceStreamFactory;
      }
      if (factory!=null) peerCast.SourceStreamFactories.Add(factory);
    }

    private void AddOutputStreamFactory(Type type)
    {
      IOutputStreamFactory factory = null;
      var constructor = type.GetConstructor(Type.EmptyTypes);
      if (constructor!=null) {
        factory = constructor.Invoke(null) as IOutputStreamFactory;
      }
      else if ((constructor=type.GetConstructor(new Type[] { typeof(PeerCast) }))!=null) {
        factory = constructor.Invoke(new object[] { peerCast }) as IOutputStreamFactory;
      }
      if (factory!=null) peerCast.OutputStreamFactories.Add(factory);
    }

    private void AddYellowPageClientFactory(Type type)
    {
      IYellowPageClientFactory factory = null;
      var constructor = type.GetConstructor(Type.EmptyTypes);
      if (constructor!=null) {
        factory = constructor.Invoke(null) as IYellowPageClientFactory;
      }
      else if ((constructor=type.GetConstructor(new Type[] { typeof(PeerCast) }))!=null) {
        factory = constructor.Invoke(new object[] { peerCast }) as IYellowPageClientFactory;
      }
      if (factory!=null) peerCast.YellowPageFactories.Add(factory);
    }

    private void AddContentReaderFactory(Type type)
    {
      IContentReaderFactory factory = null;
      var constructor = type.GetConstructor(Type.EmptyTypes);
      if (constructor!=null) {
        factory = constructor.Invoke(null) as IContentReaderFactory;
      }
      else if ((constructor=type.GetConstructor(new Type[] { typeof(PeerCast) }))!=null) {
        factory = constructor.Invoke(new object[] { peerCast }) as IContentReaderFactory;
      }
      if (factory!=null) peerCast.ContentReaderFactories.Add(factory);
    }

    void LoadPlugins()
    {
      plugins = LoadPluginAssemblies();
      foreach (var type in plugins) {
        var interfaces = type.GetInterfaces();
        if (interfaces.Contains(typeof(IUserInterfaceFactory)))    AddUserInterfaceFactory(type);
        if (interfaces.Contains(typeof(ISourceStreamFactory)))     AddSourceStreamFactory(type);
        if (interfaces.Contains(typeof(IOutputStreamFactory)))     AddOutputStreamFactory(type);
        if (interfaces.Contains(typeof(IYellowPageClientFactory))) AddYellowPageClientFactory(type);
        if (interfaces.Contains(typeof(IContentReaderFactory)))    AddContentReaderFactory(type);
      }
    }

    void LoadSettings()
    {
      var settings = PeerCastStation.Properties.Settings.Default;
      try {
        if (settings.AccessController!=null) {
          peerCast.AccessController.MaxPlays  = settings.AccessController.MaxDirects;
          peerCast.AccessController.MaxRelays = settings.AccessController.MaxRelays;
          peerCast.AccessController.MaxPlaysPerChannel  = settings.AccessController.MaxDirectsPerChannel;
          peerCast.AccessController.MaxRelaysPerChannel = settings.AccessController.MaxRelaysPerChannel;
          peerCast.AccessController.MaxUpstreamRate     = settings.AccessController.MaxUpstreamRate;
        }
        if ( settings.BroadcastID!=Guid.Empty &&
            (AtomCollectionExtensions.IDToByteArray(settings.BroadcastID)[0] & 0x01)==0) {
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
            try {
              peerCast.AddYellowPage(yellowpage.Protocol, yellowpage.Name, yellowpage.Uri);
            }
            catch (ArgumentException e) {
              logger.Error(e);
            }
          }
        }
        ChannelCleaner.InactiveLimit = settings.ChannelCleanerInactiveLimit;
      }
      catch (FormatException)
      {
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
          Protocol = yellowpage.Protocol,
          Name     = yellowpage.Name,
          Uri      = yellowpage.Uri,
        }
      ).ToArray();
      settings.ChannelCleanerInactiveLimit = ChannelCleaner.InactiveLimit;
      settings.Save();
    }

    static Mutex sharedMutex;
    [STAThread]
    static void Main(string[] args)
    {
      bool is_first_instance;
      sharedMutex = new Mutex(
        false,
        System.Reflection.Assembly.GetEntryAssembly().Location.Replace('\\', '/')+".mutex",
        out is_first_instance);
      if (is_first_instance) {
        (new Application()).Run();
      }
    }
  }
}
