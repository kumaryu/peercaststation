using System;
using System.IO;
using System.Linq;

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
    static readonly string MainAppExe = "PeerCastStation.App.exe";
    static readonly string UpdaterExe = "PeerCastStation.Updater.exe";
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

    static int ProcessApp(string basepath, string[] args)
    {
      var apppath = Path.Combine(basepath, MainAppExe);
      var process = System.Diagnostics.Process.Start(apppath, ShellEscape(args));
      process.WaitForExit();
      return process.ExitCode;
    }

    static int ProcessUpdater(string basepath)
    {
      var updateurl = System.Configuration.ConfigurationManager.AppSettings["UpdateUrl"];
      var updaterpath = Path.Combine(basepath, UpdaterExe);
      using (var tmpdir=new TempDir("PeerCastStation.Updater")) {
        var tmppath = Path.Combine(tmpdir.Path, UpdaterExe);
        File.Copy(updaterpath, tmppath, true);
        var process = System.Diagnostics.Process.Start(tmppath, ShellEscape(updateurl, tmpdir.Path, basepath));
        process.WaitForExit();
        return process.ExitCode;
      }
    }

    [STAThread]
    static int Main(string[] args)
    {
      var basepath = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
      int appresult;
      do {
        appresult = ProcessApp(basepath, args);
        if (appresult==-1) {
          var updateresult = ProcessUpdater(basepath);
          if (updateresult!=0) return updateresult;
        }
      } while (appresult==-1);
      return appresult;
    }

  }
}
