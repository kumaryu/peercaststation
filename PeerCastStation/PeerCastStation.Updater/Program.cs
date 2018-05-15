using System;
using System.Collections.Generic;
using System.Linq;

namespace PeerCastStation.Updater
{
  class Program
  {
    private static IEnumerable<string> Glob(string path)
    {
      try {
        return
          System.IO.Directory.GetFiles(path)
          .Concat(
            System.IO.Directory.GetDirectories(path).SelectMany(subpath => Glob(subpath)))
          .Select(subpath => System.IO.Path.GetFullPath(subpath));
      }
      catch (Exception) {
        return Enumerable.Empty<string>();
      }
    }

    private static void InplaceUpdate(string destpath, string filename, string[] excludes)
    {
      destpath = System.IO.Path.GetFullPath(destpath);
      using (var file = System.IO.File.OpenRead(filename))
      using (var archive = new System.IO.Compression.ZipArchive(file, System.IO.Compression.ZipArchiveMode.Read)) {
        var entries = archive.Entries.OrderBy(ent => ent.FullName);
        var root = entries.First();
        string rootpath = "";
        if (root.FullName.EndsWith("/") &&
            entries.All(ent => ent.FullName.StartsWith(root.FullName))) {
          rootpath = root.FullName;
        }
        foreach (var ent in entries) {
          var path = System.IO.Path.Combine(destpath, ent.FullName.Substring(rootpath.Length).Replace('\\', '/'));
          if (ent.FullName.EndsWith("/")) {
            var info = System.IO.Directory.CreateDirectory(path);
            try {
              info.LastWriteTime = ent.LastWriteTime.DateTime;
            }
            catch (System.IO.IOException) {
            }
          }
          else {
            try {
              using (var dst = System.IO.File.Create(path))
              using (var src = ent.Open()) {
                src.CopyTo(dst);
              }
              var info = new System.IO.FileInfo(path);
              try {
                info.LastWriteTime = ent.LastWriteTime.DateTime;
              }
              catch (System.IO.IOException) {
              }
            }
            catch (System.IO.IOException) {
              if (!excludes.Contains(ent.Name)) throw;
            }
          }
        }
        var oldentries = Glob(destpath).ToArray();
        var newentries = entries
          .Select(ent => ent.FullName)
          .Where(ent => !ent.EndsWith("/"))
          .Select(ent => System.IO.Path.Combine(destpath, ent.Substring(rootpath.Length).Replace('\\', '/'))) 
          .ToArray();
        foreach (var old in oldentries.Except(newentries)) {
          try {
            System.IO.File.Delete(old);
          }
          catch (System.IO.IOException) {
          }
        }
      }
    }

    static bool DoUpdate(string dest_dir, string source_path)
    {
      InplaceUpdate(dest_dir, source_path, new string[0]);
      return true;
    }

    static string ShellEscape(string arg)
    {
      if (arg.Contains(" ") && !arg.StartsWith("\"") && !arg.EndsWith("\"")) {
        return "\"" + arg + "\"";
      }
      else {
        return arg;
      }
    }

    static int Main(string[] args)
    {
      if (args.Length<4) {
        System.Console.Error.WriteLine("USAGE: PeerCastStation.Updater.exe PID SOURCE.ZIP DESTDIR PEERCASTSTATION.EXE [ARGS...]");
        return 1;
      }

      int parent_pid = 0;
      if (!Int32.TryParse(args[0], out parent_pid)) {
        System.Console.Error.WriteLine("USAGE: PeerCastStation.Updater.exe PID SOURCE.ZIP DESTDIR PEERCASTSTATION.EXE [ARGS...]");
        return 1;
      }
      string source_path = args[1];
      string dest_dir = System.IO.Path.GetFullPath(args[2]);
      if (System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)==dest_dir) {
        var path = System.IO.Path.Combine(
          System.IO.Path.GetTempPath(),
          "PeerCastStation.Updater.exe"
        );
        System.IO.File.Copy(System.Reflection.Assembly.GetExecutingAssembly().Location, path, true);
        System.Diagnostics.Process.Start(path, String.Join(" ", args.Select(ShellEscape)));
        return 0;
      }

      if (parent_pid!=0) {
        try {
          var process = System.Diagnostics.Process.GetProcessById(parent_pid);
          process.WaitForExit(3000);
        }
        catch (ArgumentException) {
          //Process not found
        }
        catch (SystemException) {
          //Process not found
        }
      }

      if (DoUpdate(dest_dir, source_path)) {
        System.Diagnostics.Process.Start(System.IO.Path.Combine(dest_dir, args[3]), String.Join(" ", args.Skip(4)));
        return 0;
      }
      else {
        return 2;
      }
    }
  }

}
