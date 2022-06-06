using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PeerCastStation.Core;

namespace PeerCastStation.App
{
  public class AppBase
    : PeerCastApplication
  {
    public static string GetDefaultBasePath()
    {
      return 
        System.IO.Path.GetDirectoryName(
          (System.Reflection.Assembly.GetEntryAssembly()?.Location ??
           System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ??
           "").AsSpan()
        ).ToString();
    }

    private static Logger logger = new Logger(typeof(AppBase));
    private IEnumerable<IPlugin> plugins;
    override public IEnumerable<IPlugin> Plugins {
      get { return plugins.Where(p => p.IsUsable); }
    }

    public string SettingsFileName { get; private set; }

    private PecaSettings settings;
    public override PecaSettings Settings
    {
      get { return settings; }
    }

    public System.Diagnostics.Process? LinkProcess { get; private set; }

    private string basePath;
    public override string BasePath {
      get { return basePath; }
    }

    public override string[] Args { get; }

    private static readonly OptionParser optionParser = new OptionParser {
      {"--settings", "-s", OptionArg.Required },
      {"--linkPID", null, OptionArg.Required },
      {"--kill", "-kill", OptionArg.None },
      {"--multi", "-multi", OptionArg.None },
    };

    public AppBase(string basepath, string[] args)
    {
      Args = args;
      basePath = basepath;
      var opts = optionParser.Parse(args);
      if (opts.TryGetArgumentOf("--settings", out var optSettings)) {
        SettingsFileName = optSettings;
      }
      else {
        SettingsFileName = PecaSettings.DefaultFileName;
      }
      if (opts.TryGetArgumentOf("--linkPID", out var optLinkPID) && Int32.TryParse(optLinkPID, out var pid)) {
        try {
          LinkProcess = System.Diagnostics.Process.GetProcessById(pid);
          LinkProcess.Exited += (sender, ev) => {
            Stop();
          };
          LinkProcess.EnableRaisingEvents = true;
        }
        catch (Exception) {
        }
      }
      settings = new PecaSettings(SettingsFileName);
      peerCast.AgentName = AppSettingsReader.GetString("AgentName", "PeerCastStation");
      plugins = LoadPlugins();
    }

    private TaskCompletionSource<int> stopTask = new TaskCompletionSource<int>();
    private Action? cleanupHandler = null;
    override public void Stop(int exit_code, Action cleanupHandler)
    {
      if (stopTask.TrySetResult(exit_code)) {
        this.cleanupHandler = cleanupHandler;
      }
    }

    PeerCast peerCast = new PeerCast();
    override public PeerCast PeerCast { get { return peerCast; } }
    public int Run()
    {
      DoSetup();
      var task = Start();
      task.Wait();
      DoCleanup();
      return task.Result;
    }

    protected virtual void DoSetup()
    {
      Console.CancelKeyPress += (sender, args) => {
        args.Cancel = true;
        Stop();
      };
    }

    protected virtual void DoCleanup()
    {
      Logger.Close();
      cleanupHandler?.Invoke();
    }

    public virtual async Task<int> Start()
    {
      settings.Load();
      foreach (var plugin in Plugins) {
        plugin.Attach(this);
      }
      peerCast.AddChannelMonitor(new ChannelCleaner(peerCast));
      peerCast.AddChannelMonitor(new ChannelNotifier(this));
      LoadConfigurations();
      LoadSettings();
      foreach (var plugin in Plugins) {
        plugin.Start();
      }
      var result = await stopTask.Task.ConfigureAwait(false);
      foreach (var plugin in Plugins.Reverse()) {
        plugin.Stop();
      }
      SaveSettings();
      peerCast.Stop();
      foreach (var plugin in Plugins.Reverse()) {
        plugin.Detach();
      }
      return result;
    }

    IEnumerable<Type> LoadPluginAssemblies()
    {
      var types = Enumerable.Empty<Type>();
      var entry = System.Reflection.Assembly.GetEntryAssembly();
      if (entry!=null) {
        types = Enumerable.Concat(types, LoadPluginAssembly(entry));
      }
      types = Enumerable.Concat(
        types,
        System.IO.Directory.GetFiles(BasePath, "*.dll").SelectMany(dll => LoadPluginAssembly(dll))
      );
      return types
        .OrderBy(type => ((PluginAttribute)(type.GetCustomAttributes(typeof(PluginAttribute), true)[0])).Priority);
    }

    IEnumerable<Type> LoadPluginAssembly(string filename)
    {
      try {
        return LoadPluginAssembly(System.Reflection.Assembly.LoadFrom(filename));
      }
      catch (BadImageFormatException) {
        return Enumerable.Empty<Type>();
      }
      catch (System.IO.FileLoadException) {
        return Enumerable.Empty<Type>();
      }
    }

    IEnumerable<Type> LoadPluginAssembly(System.Reflection.Assembly asm)
    {
      try {
        var res = asm.GetTypes()
            .Where(type => type.GetCustomAttributes(typeof(PluginAttribute), true).Length>0)
            .Where(type => type.GetInterfaces().Contains(typeof(IPlugin)));
        foreach (var settingtype in asm.GetTypes().Where(type => type.GetCustomAttributes(typeof(PecaSettingsAttribute), true).Length>0 && type.FullName!=null)) {
          foreach (var attr in settingtype.GetCustomAttributes(typeof(PecaSettingsAttribute), true).Cast<PecaSettingsAttribute>()) {
            if (attr.Alias!=null) {
              PecaSettings.RegisterType(attr.Alias, settingtype);
            }
            else {
              PecaSettings.RegisterType(settingtype.FullName!, settingtype);
            }
          }
        }
        foreach (var enumtype in asm.GetTypes().Where(type => type.IsEnum && type.IsPublic && !type.IsNested)) {
          PecaSettings.RegisterType(enumtype.FullName!, enumtype);
        }
        return res;
      }
      catch (BadImageFormatException) {
        return Enumerable.Empty<Type>();
      }
      catch (System.Reflection.ReflectionTypeLoadException) {
        return Enumerable.Empty<Type>();
      }
    }

    IEnumerable<IPlugin> LoadPlugins()
    {
      return LoadPluginAssemblies().Select(type => {
        var constructor = type.GetConstructor(Type.EmptyTypes);
        if (constructor!=null) {
          return constructor.Invoke(null) as IPlugin;
        }
        else {
          return null;
        }
      })
      .Where(plugin => plugin!=null)
      .ToArray()!;
    }

    void LoadConfigurations()
    {
      int backlog;
      if (AppSettingsReader.TryGetInt("MaxPendingConnections", out backlog) && backlog>0) {
        OutputListener.MaxPendingConnections = backlog;
      }
    }

    void LoadSettings()
    {
      var s = settings.Get<PeerCastStationSettings>();
      try {
        if (s.Logger!=null) {
          Logger.Level        = s.Logger.Level;
          Logger.LogFileName  = s.Logger.LogFileName;
          Logger.OutputTarget = s.Logger.OutputTarget;
        }
        if (s.AccessController!=null) {
          peerCast.AccessController.MaxPlays                     = s.AccessController.MaxDirects;
          peerCast.AccessController.MaxRelays                    = s.AccessController.MaxRelays;
          peerCast.AccessController.MaxPlaysPerBroadcastChannel  = s.AccessController.MaxDirectsPerBroadcastChannel;
          peerCast.AccessController.MaxPlaysPerRelayChannel      = s.AccessController.MaxDirectsPerChannel;
          peerCast.AccessController.MaxRelaysPerBroadcastChannel = s.AccessController.MaxRelaysPerBroadcastChannel;
          peerCast.AccessController.MaxRelaysPerRelayChannel     = s.AccessController.MaxRelaysPerChannel;
          peerCast.AccessController.MaxUpstreamRate           = s.AccessController.MaxUpstreamRate;
          peerCast.AccessController.MaxUpstreamRateIPv6       = s.AccessController.MaxUpstreamRateIPv6;
          peerCast.AccessController.MaxUpstreamRatePerBroadcastChannel = s.AccessController.MaxUpstreamRatePerBroadcastChannel;
          peerCast.AccessController.MaxUpstreamRatePerRelayChannel     = s.AccessController.MaxUpstreamRatePerChannel;
        }
        if ( s.BroadcastID!=Guid.Empty &&
            (AtomCollectionExtensions.IDToByteArray(s.BroadcastID)[0] & 0x01)==0) {
          peerCast.BroadcastID = s.BroadcastID;
        }
        if (s.Listeners!=null) {
          foreach (var listener in s.Listeners) {
            try {
              if (listener.EndPoint==null) continue;
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
          var endpoint =
            new System.Net.IPEndPoint(
              AppSettingsReader.GetIPAddress("DefaultListenAddress", System.Net.IPAddress.Any),
              AppSettingsReader.GetInt("DefaultListenPort", 7144)
            );
          try {
            peerCast.StartListen(
              endpoint,
              OutputStreamType.All,
              OutputStreamType.Metadata | OutputStreamType.Relay);
          }
          catch (System.Net.Sockets.SocketException e) {
            logger.Error(e);
            try {
              peerCast.StartListen(
                new System.Net.IPEndPoint(endpoint.Address, 0),
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
              if (String.IsNullOrEmpty(yellowpage.Protocol) || String.IsNullOrEmpty(yellowpage.Name)) continue;
              peerCast.AddYellowPage(yellowpage.Protocol, yellowpage.Name, yellowpage.Uri, yellowpage.ChannelsUri);
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
        MaxDirects                     = peerCast.AccessController.MaxPlays,
        MaxDirectsPerBroadcastChannel  = peerCast.AccessController.MaxPlaysPerBroadcastChannel,
        MaxDirectsPerChannel           = peerCast.AccessController.MaxPlaysPerRelayChannel,
        MaxRelays                      = peerCast.AccessController.MaxRelays,
        MaxRelaysPerBroadcastChannel   = peerCast.AccessController.MaxRelaysPerBroadcastChannel,
        MaxRelaysPerChannel            = peerCast.AccessController.MaxRelaysPerRelayChannel,
        MaxUpstreamRate                = peerCast.AccessController.MaxUpstreamRate,
        MaxUpstreamRateIPv6            = peerCast.AccessController.MaxUpstreamRateIPv6,
        MaxUpstreamRatePerBroadcastChannel = peerCast.AccessController.MaxUpstreamRatePerBroadcastChannel,
        MaxUpstreamRatePerChannel          = peerCast.AccessController.MaxUpstreamRatePerRelayChannel,
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
          Uri      = yellowpage.AnnounceUri,
          ChannelsUri = yellowpage.ChannelsUri,
        }
      ).ToArray();
      settings.Get<ChannelCleanerSettings>().InactiveLimit = ChannelCleaner.InactiveLimit;
      settings.Get<ChannelCleanerSettings>().Mode = ChannelCleaner.Mode;
      settings.Save();
    }

  }
}
