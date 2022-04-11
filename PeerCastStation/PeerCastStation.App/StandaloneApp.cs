using System;
using System.Linq;
using PeerCastStation.Core;
using System.Threading;

namespace PeerCastStation.App
{
  public class StandaloneApp
    : AppBase
  {
    public StandaloneApp(string basepath, string[] args)
      : base(basepath, args)
    {
    }

    RegisteredWaitHandle? registeredWaitHandle = null;
    protected override void DoSetup()
    {
      Console.CancelKeyPress += (sender, args) => {
        args.Cancel = true;
        Stop();
      };
      registeredWaitHandle = ThreadPool.RegisterWaitForSingleObject(killWaitHandle, (state, timedout) => {
        Stop();
        if (registeredWaitHandle!=null) {
          registeredWaitHandle.Unregister(null);
          registeredWaitHandle = null;
        }
      }, null, Timeout.Infinite, true);
      base.DoSetup();
    }

    protected override void DoCleanup()
    {
      if (registeredWaitHandle!=null) {
        registeredWaitHandle.Unregister(null);
        registeredWaitHandle = null;
      }
      base.DoCleanup();
    }

    static EventWaitHandle killWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
    private static bool CheckIsFirstInstance(string basepath, ref EventWaitHandle wait_handle)
    {
      if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)) {
        return true;
      }
      bool is_first_instance;
      var event_name = System.IO.Path.Combine(basepath, "PeerCastStation.exe").Replace('\\', '/')+".kill";
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
    public static int Run(string basepath, string[] args)
    {
      AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
      var first_instance = CheckIsFirstInstance(basepath, ref killWaitHandle);
      using (killWaitHandle) {
        if (args.Contains("-kill") || args.Contains("--kill")) {
          killWaitHandle.Set();
          return 0;
        }
        if (!first_instance && !(args.Contains("-multi") || args.Contains("--multi"))) {
          return 1;
        }
        return (new StandaloneApp(basepath, args)).Run();
      }
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
      var dir = System.IO.Path.GetDirectoryName(PecaSettings.DefaultFileName);
      System.IO.Directory.CreateDirectory(dir);
      using (var file=System.IO.File.AppendText(System.IO.Path.Combine(dir, "exception.log"))) {
        file.WriteLine("{0}: {1} (OS:{2}, CLR:{3})",
          DateTime.Now,
          AppSettingsReader.GetString("AgentName", "PeerCastStation"),
          Environment.OSVersion,
          Environment.Version);
        file.WriteLine(args.ExceptionObject);
      }
    }

  }
}
