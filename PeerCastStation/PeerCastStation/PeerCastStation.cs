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

    private PecaSettings settings = new PecaSettings(PecaSettings.DefaultFileName);
    public override PecaSettings Settings
    {
      get { return settings; }
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
			Console.CancelKeyPress += (sender, args) => {
				args.Cancel = true;
				Stop();
			};
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
      Logger.Close();
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
      settings.Load();
      PeerCastStationSettings s;
      if (settings.Contains(typeof(PeerCastStationSettings))) {
        s = settings.Get<PeerCastStationSettings>();
      }
      else {
        s = settings.Get<PeerCastStationSettings>();
        s.Import(PeerCastStation.Properties.Settings.Default);
      }
      try {
        if (s.Logger!=null) {
          Logger.Level        = s.Logger.Level;
          Logger.LogFileName  = s.Logger.LogFileName;
          Logger.OutputTarget = s.Logger.OutputTarget;
        }
        if (s.AccessController!=null) {
          peerCast.AccessController.MaxPlays  = s.AccessController.MaxDirects;
          peerCast.AccessController.MaxRelays = s.AccessController.MaxRelays;
          peerCast.AccessController.MaxPlaysPerChannel  = s.AccessController.MaxDirectsPerChannel;
          peerCast.AccessController.MaxRelaysPerChannel = s.AccessController.MaxRelaysPerChannel;
          peerCast.AccessController.MaxUpstreamRate     = s.AccessController.MaxUpstreamRate;
        }
        if ( s.BroadcastID!=Guid.Empty &&
            (AtomCollectionExtensions.IDToByteArray(s.BroadcastID)[0] & 0x01)==0) {
          peerCast.BroadcastID = s.BroadcastID;
        }
        if (s.Listeners!=null) {
          foreach (var listener in s.Listeners) {
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
          if (!System.Net.IPAddress.TryParse(PeerCastStation.Properties.Settings.Default.DefaultListenAddress, out listen_addr)) {
            listen_addr = System.Net.IPAddress.Any;
          }
          try {
            peerCast.StartListen(
              new System.Net.IPEndPoint(listen_addr, PeerCastStation.Properties.Settings.Default.DefaultListenPort),
              OutputStreamType.All,
              OutputStreamType.Metadata | OutputStreamType.Relay);
          }
          catch (System.Net.Sockets.SocketException e) {
            logger.Error(e);
          }
        }
        if (s.YellowPages!=null) {
          foreach (var yellowpage in s.YellowPages) {
            try {
              peerCast.AddYellowPage(yellowpage.Protocol, yellowpage.Name, yellowpage.Uri);
            }
            catch (ArgumentException e) {
              logger.Error(e);
            }
          }
        }
      }
      catch (FormatException)
      {
      }
      ChannelCleaner.InactiveLimit = settings.Get<ChannelCleanerSettings>().InactiveLimit;
    }

    void SaveSettings()
    {
      var s = settings.Get<PeerCastStationSettings>();
      s.Logger = new PeerCastStationSettings.LoggerSettings {
        Level        = Logger.Level,
        LogFileName  = Logger.LogFileName,
        OutputTarget = Logger.OutputTarget,
      };
      s.AccessController = new PeerCastStationSettings.AccessControllerSettings {
        MaxDirects           = peerCast.AccessController.MaxPlays,
        MaxRelays            = peerCast.AccessController.MaxRelays,
        MaxDirectsPerChannel = peerCast.AccessController.MaxPlaysPerChannel,
        MaxRelaysPerChannel  = peerCast.AccessController.MaxRelaysPerChannel,
        MaxUpstreamRate      = peerCast.AccessController.MaxUpstreamRate,
      };
      s.BroadcastID = peerCast.BroadcastID;
      s.Listeners = peerCast.OutputListeners.Select(listener => 
        new PeerCastStationSettings.ListenerSettings {
          EndPoint      = listener.LocalEndPoint,
          GlobalAccepts = listener.GlobalOutputAccepts,
          LocalAccepts  = listener.LocalOutputAccepts,
        }
      ).ToArray();
      s.YellowPages = peerCast.YellowPages.Select(yellowpage =>
        new PeerCastStationSettings.YellowPageSettings {
          Protocol = yellowpage.Protocol,
          Name     = yellowpage.Name,
          Uri      = yellowpage.Uri,
        }
      ).ToArray();
      settings.Get<ChannelCleanerSettings>().InactiveLimit = ChannelCleaner.InactiveLimit;
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
