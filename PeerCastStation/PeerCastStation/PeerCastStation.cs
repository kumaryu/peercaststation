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
    private IEnumerable<IPlugin> plugins;
    override public IEnumerable<IPlugin> Plugins {
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
      foreach (var plugin in plugins) {
        plugin.Attach(this);
      }
      peerCast.ChannelMonitors.Add(new ChannelCleaner(peerCast));
      LoadSettings();
      foreach (var plugin in plugins) {
        plugin.Start();
      }
      stoppedEvent.WaitOne();
      foreach (var plugin in plugins) {
        plugin.Stop();
      }
      SaveSettings();
      peerCast.Stop();
      foreach (var plugin in plugins) {
        plugin.Detach();
      }
      Logger.Close();
    }

    public static readonly string PluginPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
    IEnumerable<Type> LoadPluginAssemblies()
    {
      var res = LoadPluginAssembly(System.Reflection.Assembly.GetExecutingAssembly());
      foreach (var dll in System.IO.Directory.GetFiles(PluginPath, "*.dll")) {
        res = res.Concat(LoadPluginAssembly(System.Reflection.Assembly.LoadFrom(dll)));
      }
      return res;
    }

    IEnumerable<Type> LoadPluginAssembly(System.Reflection.Assembly asm)
    {
      var res = asm.GetTypes()
          .Where(type => type.GetCustomAttributes(typeof(PluginAttribute), true).Length>0)
          .Where(type => type.GetInterfaces().Contains(typeof(IPlugin)))
          .OrderBy(type => ((PluginAttribute)(type.GetCustomAttributes(typeof(PluginAttribute), true)[0])).Priority);
      foreach (var settingtype in asm.GetTypes().Where(type => type.GetCustomAttributes(typeof(PecaSettingsAttribute), true).Length>0)) {
        PecaSettings.RegisterType(settingtype);
      }
      return res;
    }

    void LoadPlugins()
    {
      plugins = LoadPluginAssemblies().Select(type => {
        var constructor = type.GetConstructor(Type.EmptyTypes);
        if (constructor!=null) {
          return constructor.Invoke(null) as IPlugin;
        }
        else {
          return null;
        }
      })
      .Where(plugin => plugin!=null)
      .ToArray();
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
            try {
              peerCast.StartListen(
                new System.Net.IPEndPoint(listen_addr, 0),
                OutputStreamType.All,
                OutputStreamType.None);
            }
            catch (System.Net.Sockets.SocketException e2) {
              logger.Error(e2);
            }
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
      ChannelCleaner.InactiveLimit  = settings.Get<ChannelCleanerSettings>().InactiveLimit;
      ChannelCleaner.NoPlayingLimit = settings.Get<ChannelCleanerSettings>().NoPlayingLimit;
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
      settings.Get<ChannelCleanerSettings>().InactiveLimit  = ChannelCleaner.InactiveLimit;
      settings.Get<ChannelCleanerSettings>().NoPlayingLimit = ChannelCleaner.NoPlayingLimit;
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
