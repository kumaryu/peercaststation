using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.Updater
{
  public static class UpdaterApp
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
              System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
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
      InplaceUpdate(dest_dir, source_path, new string[] { "PeerCastStation.exe", "PecaStationd.exe" });
      return true;
    }

    static async Task<string> DoDownload(Uri source, string dstpath)
    {
      int progress = -1;
      var appcastReader = new AppCastReader();
      var versions = await appcastReader.DownloadVersionInfoTaskAsync(source, CancellationToken.None).ConfigureAwait(false);
      var version   = versions.OrderByDescending(ver => ver.PublishDate).First();
      var enclosure = version.Enclosures.Where(enc => enc.InstallerType==InstallerType.Archive).First();
      using (var client = new System.Net.WebClient()) {
        var locker = new Object();
        client.DownloadProgressChanged += (sender, args) => {
          lock (locker) {
            if (args.ProgressPercentage>progress) {
              progress = args.ProgressPercentage;
              Console.WriteLine($"Downloading... {progress}%");
            }
          }
        };
        var filepath =
          System.IO.Path.Combine(
            dstpath,
            System.IO.Path.GetFileName(enclosure.Url.AbsolutePath));
        await client.DownloadFileTaskAsync(enclosure.Url.ToString(), filepath).ConfigureAwait(false);
        return filepath;
      }
    }

    public static int Run(Uri updaterUri, string tempPath, string destDir)
    {
      var download = DoDownload(updaterUri, tempPath);
      download.Wait();

      try {
      if (DoUpdate(System.IO.Path.GetFullPath(destDir), download.Result)) {
        return 0;
      }
      else {
        return 2;
      }
      }
      finally {
        Thread.Sleep(10000);
      }
    }

  }

}
