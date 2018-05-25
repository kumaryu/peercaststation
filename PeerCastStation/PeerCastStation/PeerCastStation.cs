using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace PeerCastStation.Main
{
  internal class TempDir
    : IDisposable
  {
    public TempDir(string prefix)
    {
      string tmppath;
      do {
        tmppath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{prefix}.{Guid.NewGuid()}");
      } while (Directory.Exists(tmppath));
      Directory.CreateDirectory(tmppath);
      Path = tmppath;
    }

    public void Dispose()
    {
      Directory.Delete(Path, true);
    }

    public string Path { get; private set; }
  }

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
    static readonly SubAppDescription UpdaterAppDesc = new SubAppDescription {
      AssemblyName = "PeerCastStation.Updater.dll",
      AppType      = "PeerCastStation.Updater.UpdaterApp",
    };

    static string ShellEscape(string arg)
    {
      if (arg.Contains(" ") && !arg.StartsWith("\"") && !arg.EndsWith("\"")) {
        return "\"" + arg + "\"";
      }
      else {
        return arg;
      }
    }

    static string ShellEscape(params string[] args)
    {
      return String.Join(" ", args.Select(ShellEscape));
    }

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

    static int ProcessUpdate(string basepath, string tmpdir, string[] args)
    {
      Directory.CreateDirectory(tmpdir);
      var updateurl = new Uri(System.Configuration.ConfigurationManager.AppSettings["UpdateUrl"], UriKind.Absolute);
      var updaterpath = Path.Combine(basepath, UpdaterAppDesc.AssemblyName);
      var tmppath = Path.Combine(tmpdir, UpdaterAppDesc.AssemblyName);
      File.Copy(updaterpath, tmppath, true);
      var asm = Assembly.LoadFrom(tmppath);
      var type = asm.GetType(UpdaterAppDesc.AppType);
      var result = type.InvokeMember(
        "Run",
        BindingFlags.Public |
        BindingFlags.Static |
        BindingFlags.InvokeMethod,
        null,
        null,
        new object[] { updateurl, tmpdir, basepath });
      return (int)result;
    }

    static int InvokeApp(string method, params string[] args)
    {
      var proc = System.Diagnostics.Process.Start(
        Assembly.GetEntryAssembly().Location,
        String.Join(" ", Enumerable.Repeat(method, 1).Concat(args).Select(ShellEscape))
      );
      proc.WaitForExit();
      return proc.ExitCode;
    }

    [STAThread]
    static int Main(string[] args)
    {
      var basepath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
      if (args.Length>0) {
        switch (args[0]) {
        case "main":
          return ProcessMain(basepath, args.Skip(1).ToArray());
        case "update":
          if (args.Length<2) {
            Console.WriteLine("USAGE: PeerCastStation.exe update TMPPATH");
            return 1;
          }
          return ProcessUpdate(basepath, args[1], args.Skip(2).ToArray());
        }
      }

      int appresult;
      do {
        appresult = InvokeApp("main", args);
        if (appresult==3) {
          using (var tmpdir=new TempDir("PeerCastStation.Updater")) {
            var updateresult = InvokeApp("update", tmpdir.Path);
            if (updateresult!=0) return updateresult;
          }
        }
      } while (appresult==3);
      return appresult;
    }

  }
}
