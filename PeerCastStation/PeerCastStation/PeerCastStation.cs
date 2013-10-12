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
      get { return plugins.Where(p => p.IsUsable); }
    }

    private PecaSettings settings = new PecaSettings(PecaSettings.DefaultFileName);
    public override PecaSettings Settings
    {
      get { return settings; }
    }

    public Application()
    {
      peerCast.AgentName = PeerCastStation.Properties.Settings.Default.AgentName;
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
      foreach (var plugin in Plugins) {
        plugin.Attach(this);
      }
      peerCast.ChannelMonitors.Add(new ChannelCleaner(peerCast));
      peerCast.ChannelMonitors.Add(new ChannelNotifier(this));
      LoadSettings();
      foreach (var plugin in Plugins) {
        plugin.Start();
      }
      WaitHandle.WaitAny(new WaitHandle[] { killWaitHandle, stoppedEvent });
      foreach (var plugin in Plugins) {
        plugin.Stop();
      }
      SaveSettings();
      peerCast.Stop();
      foreach (var plugin in Plugins) {
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
      try {
        var res = asm.GetTypes()
            .Where(type => type.GetCustomAttributes(typeof(PluginAttribute), true).Length>0)
            .Where(type => type.GetInterfaces().Contains(typeof(IPlugin)))
            .OrderBy(type => ((PluginAttribute)(type.GetCustomAttributes(typeof(PluginAttribute), true)[0])).Priority);
        foreach (var settingtype in asm.GetTypes().Where(type => type.GetCustomAttributes(typeof(PecaSettingsAttribute), true).Length>0)) {
          PecaSettings.RegisterType(settingtype);
        }
        return res;
      }
      catch (System.Reflection.ReflectionTypeLoadException) {
        return Enumerable.Empty<Type>();
      }
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
          peerCast.AccessController.MaxPlays                  = s.AccessController.MaxDirects;
          peerCast.AccessController.MaxRelays                 = s.AccessController.MaxRelays;
          peerCast.AccessController.MaxPlaysPerChannel        = s.AccessController.MaxDirectsPerChannel;
          peerCast.AccessController.MaxRelaysPerChannel       = s.AccessController.MaxRelaysPerChannel;
          peerCast.AccessController.MaxUpstreamRate           = s.AccessController.MaxUpstreamRate;
          peerCast.AccessController.MaxUpstreamRatePerChannel = s.AccessController.MaxUpstreamRatePerChannel;
        }
        if ( s.BroadcastID!=Guid.Empty &&
            (AtomCollectionExtensions.IDToByteArray(s.BroadcastID)[0] & 0x01)==0) {
          peerCast.BroadcastID = s.BroadcastID;
        }
        if (s.Listeners!=null) {
          foreach (var listener in s.Listeners) {
            try {
              var ol = peerCast.StartListen(listener.EndPoint, listener.LocalAccepts, listener.GlobalAccepts);
              ol.GlobalAuthorizationRequired = listener.GlobalAuthRequired;
              ol.LocalAuthorizationRequired  = listener.LocalAuthRequired;
              ol.AuthenticationKey = new AuthenticationKey(listener.AuthId, listener.AuthPassword);
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
      ChannelCleaner.Mode          = settings.Get<ChannelCleanerSettings>().Mode;
      ChannelCleaner.InactiveLimit = settings.Get<ChannelCleanerSettings>().InactiveLimit;
    }

    public override void SaveSettings()
    {
      var s = settings.Get<PeerCastStationSettings>();
      s.Logger = new PeerCastStationSettings.LoggerSettings {
        Level        = Logger.Level,
        LogFileName  = Logger.LogFileName,
        OutputTarget = Logger.OutputTarget,
      };
      s.AccessController = new PeerCastStationSettings.AccessControllerSettings {
        MaxDirects                = peerCast.AccessController.MaxPlays,
        MaxDirectsPerChannel      = peerCast.AccessController.MaxPlaysPerChannel,
        MaxRelays                 = peerCast.AccessController.MaxRelays,
        MaxRelaysPerChannel       = peerCast.AccessController.MaxRelaysPerChannel,
        MaxUpstreamRate           = peerCast.AccessController.MaxUpstreamRate,
        MaxUpstreamRatePerChannel = peerCast.AccessController.MaxUpstreamRatePerChannel,
      };
      s.BroadcastID = peerCast.BroadcastID;
      s.Listeners = peerCast.OutputListeners.Select(listener => 
        new PeerCastStationSettings.ListenerSettings {
          EndPoint           = listener.LocalEndPoint,
          GlobalAccepts      = listener.GlobalOutputAccepts,
          GlobalAuthRequired = listener.GlobalAuthorizationRequired,
          LocalAccepts       = listener.LocalOutputAccepts,
          LocalAuthRequired  = listener.LocalAuthorizationRequired,
          AuthId             = listener.AuthenticationKey.Id,
          AuthPassword       = listener.AuthenticationKey.Password,
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
      settings.Get<ChannelCleanerSettings>().Mode = ChannelCleaner.Mode;
      settings.Save();
    }

    static EventWaitHandle killWaitHandle;
    static private bool CheckIsFirstInstance(ref EventWaitHandle wait_handle)
    {
      bool is_first_instance;
      var event_name = System.Reflection.Assembly.GetEntryAssembly().Location
        .Replace('\\', '/')+".kill";
      try {
        wait_handle = EventWaitHandle.OpenExisting(event_name);
        is_first_instance = false;
      }
      catch (WaitHandleCannotBeOpenedException) {
        wait_handle = new EventWaitHandle(false, EventResetMode.ManualReset, event_name);
        is_first_instance = true;
      }
      return is_first_instance;
    }

    [STAThread]
    static void Main(string[] args)
    {
      var first_instance = CheckIsFirstInstance(ref killWaitHandle);
      if (args.Contains("-kill")) {
        killWaitHandle.Set();
        return;
      }
      if (!first_instance && !args.Contains("-multi")) {
        return;
      }
#if !DEBUG
      try
#endif
      {
        (new Application()).Run();
      }
#if !DEBUG
      catch (Exception e) {
        logger.Fatal("Unhandled exception");
        logger.Fatal(e);
        throw;
      }
#endif
    }
  }
}
