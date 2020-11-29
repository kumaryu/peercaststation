using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace PeerCastStation.Main
{
  public class PeerCastStation
  {
    struct SubAppDescription
    {
      public string AssemblyName;
      public string AppType;
    }
    static readonly SubAppDescription MainAppDesc = new SubAppDescription {
      AssemblyName = "PeerCastStation.App.dll",
      AppType      = "PeerCastStation.App.StandaloneApp",
    };

    static int ProcessMain(string basepath, string[] args)
    {
      var asm = Assembly.LoadFrom(Path.Combine(basepath, MainAppDesc.AssemblyName));
      var type = asm.GetType(MainAppDesc.AppType);
      var result = type.InvokeMember("Run",
        BindingFlags.Public |
        BindingFlags.Static |
        BindingFlags.InvokeMethod,
        null,
        null,
        new object[] { basepath, args });
      return (int)result;
    }

    static int ProcessUpdate(string basepath, string sourcepath, string targetpath, string[] args)
    {
      UI.Updater.Update(sourcepath, targetpath);
      UI.Updater.StartCleanup(targetpath, sourcepath, args);
      return 0;
    }

    static int ProcessInstall(string basepath, string zipfile, string[] args)
    {
      UI.Updater.Install(zipfile);
      return 0;
    }

    static int ProcessCleanup(string basepath, string tmppath, string[] args)
    {
      UI.Updater.Cleanup(tmppath);
      return ProcessMain(basepath, args);
    }

    [STAThread]
    static int Main(string[] args)
    {
      var basepath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
      if (args.Length>0) {
        switch (args[0]) {
        case "main":
          return ProcessMain(basepath, args.Skip(1).ToArray());
        case "install":
          if (args.Length<2) {
            Console.WriteLine("USAGE: PeerCastStation.exe install ZIPFILE");
            return 1;
          }
          return ProcessInstall(basepath, args[1], args.Skip(2).ToArray());
        case "update":
          if (args.Length<3) {
            Console.WriteLine("USAGE: PeerCastStation.exe update SOURCEPATH TARGETPATH");
            return 1;
          }
          return ProcessUpdate(basepath, args[1], args[2], args.Skip(3).ToArray());
        case "cleanup":
          if (args.Length<2) {
            Console.WriteLine("USAGE: PeerCastStation.exe cleanup TARGETPATH");
            return 1;
          }
          return ProcessCleanup(basepath, args[1], args.Skip(2).ToArray());
        default:
          return ProcessMain(basepath, args);
        }
      }
      else {
        return ProcessMain(basepath, args);
      }
    }

  }
}
