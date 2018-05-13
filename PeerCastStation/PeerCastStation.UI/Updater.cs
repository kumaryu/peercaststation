using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PeerCastStation.Core;

namespace PeerCastStation.UI
{
	public enum InstallerType {
		Unknown   = 0,
		Installer,
		Archive,
		ServiceInstaller,
		ServiceArchive,
	}

	public class VersionEnclosure
	{
		public string Title  { get; set; }
		public long   Length { get; set; }
		public string Type   { get; set; }
		public Uri    Url    { get; set; }
		public InstallerType InstallerType { get; set; }
	}

  public class VersionDescription
  {
    public DateTime PublishDate { get; set; }
    public Uri      Link        { get; set; }
    public string   Title       { get; set; }
    public string   Description { get; set; }
		public VersionEnclosure[] Enclosures { get; set; }
  }

  public class NewVersionFoundEventArgs : EventArgs
  {
    public IEnumerable<VersionDescription> VersionDescriptions { get; private set; }
    public NewVersionFoundEventArgs(IEnumerable<VersionDescription> desc)
    {
      this.VersionDescriptions = desc;
    }
  }
  public delegate void NewVersionFoundEventHandler(object sender, NewVersionFoundEventArgs args);

  public class Updater
  {
    private Uri url;
    private DateTime currentVersion;
    private AppCastReader appcastReader = new AppCastReader();
    public Updater()
    {
      this.url            = AppSettingsReader.GetUri("UpdateUrl", new Uri("http://www.pecastation.org/files/appcast.xml"));
      this.currentVersion = AppSettingsReader.GetDate("CurrentVersion", DateTime.Today);
    }

		public static InstallerType CurrentInstallerType {
			get {
				InstallerType result;
				if (!Enum.TryParse<InstallerType>(AppSettingsReader.GetString("InstallerType", "unknwon"), true, out result)) {
					return InstallerType.Unknown;
				}
				return result;
			}
		}

		public async Task<IEnumerable<VersionDescription>> CheckVersionTaskAsync(CancellationToken cancel_token)
		{
			var results = await appcastReader.DownloadVersionInfoTaskAsync(url, cancel_token).ConfigureAwait(false);
			if (results==null) return null;
			return results
				.Where(v => v.PublishDate.Date>currentVersion)
				.OrderByDescending(v => v.PublishDate);
		}

    public bool CheckVersion()
    {
      return appcastReader.DownloadVersionInfoAsync(url, desc => {
        var new_versions = desc
          .Where(v => v.PublishDate.Date>currentVersion)
          .OrderByDescending(v => v.PublishDate);
        if (new_versions.Count()>0 && NewVersionFound!=null) {
          NewVersionFound(this, new NewVersionFoundEventArgs(new_versions));
        }
      });
    }

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

    static string ShellEscape(string arg)
    {
      if (arg.Contains(" ") && !arg.StartsWith("\"") && !arg.EndsWith("\"")) {
        return "\"" + arg + "\"";
      }
      else {
        return arg;
      }
    }

    public static void ExecUpdater(string destpath, string filename)
    {
      var pid = System.Diagnostics.Process.GetCurrentProcess().Id;
      var entry = System.IO.Path.GetFileName(Environment.GetCommandLineArgs()[0]);
      var args = String.Join(" ", Environment.GetCommandLineArgs().Skip(1).Select(ShellEscape));

      System.Diagnostics.Process.Start(
        "PeerCastStation.Updater.exe",
        $"{pid} {ShellEscape(filename)} {ShellEscape(destpath)} {ShellEscape(entry)} {args}"
      );
    }

    public static void InplaceUpdate(string destpath, string filename, string[] excludes)
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
          var path = System.IO.Path.Combine(destpath, ent.FullName.Substring(rootpath.Length).Replace('/', '\\'));
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
              using (var dst = System.IO.File.OpenWrite(path))
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
          .Select(ent => System.IO.Path.Combine(destpath, ent.Substring(rootpath.Length).Replace('/', '\\'))) 
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

    public static string GetDownloadPath()
    {
      return
        Environment.GetEnvironmentVariable("TEMP") ??
        Environment.GetFolderPath(Environment.SpecialFolder.InternetCache);
    }

    public class DownloadResult
    {
      public string FilePath { get; private set; }
      public VersionDescription Version { get; private set; }
      public VersionEnclosure Enclosure { get; private set; }
      public DownloadResult(string filepath, VersionDescription version, VersionEnclosure enclosure)
      {
        FilePath = filepath;
        Version = version;
        Enclosure = enclosure;
      }
    }

    public static async Task<DownloadResult> DownloadAsync(VersionDescription version, Action<float> onprogress, CancellationToken ct)
    {
      using (var client = new System.Net.WebClient()) {
        if (onprogress!=null) {
          client.DownloadProgressChanged += (sender, args) => {
            onprogress(args.ProgressPercentage/100.0f);
          };
        }
        ct.Register(() => { client.CancelAsync(); }, true);
        var enclosure = version.Enclosures.First(e => e.InstallerType==Updater.CurrentInstallerType);
        var filepath =
          System.IO.Path.Combine(
            GetDownloadPath(),
            System.IO.Path.GetFileName(enclosure.Url.AbsolutePath));
        await client.DownloadFileTaskAsync(enclosure.Url.ToString(), filepath).ConfigureAwait(false);
        return new DownloadResult(filepath, version, enclosure);
      }
    }

    public static bool Install(DownloadResult downloaded)
    {
      try {
        switch (downloaded.Enclosure.InstallerType) {
        case InstallerType.Archive:
        case InstallerType.ServiceArchive:
          Updater.ExecUpdater(
            PeerCastApplication.Current.BasePath,
            downloaded.FilePath
          );
          PeerCastApplication.Current.Stop(-1);
          break;
        case InstallerType.Installer:
          System.Diagnostics.Process.Start(downloaded.FilePath);
          PeerCastApplication.Current.Stop();
          break;
        case InstallerType.ServiceInstaller:
          System.Diagnostics.Process.Start(downloaded.FilePath, "/quiet");
          break;
        case InstallerType.Unknown:
          throw new ApplicationException();
        }
        return true;
      }
      catch (Exception) {
        return false;
      }
    }

    public event NewVersionFoundEventHandler NewVersionFound;
  }

  public class NewVersionNotificationMessage
    : NotificationMessage
  {
    public NewVersionNotificationMessage(
        string title,
        string message,
        NotificationMessageType type,
        IEnumerable<VersionDescription> new_versions)
      : base(title, message, type)
    {
      this.VersionDescriptions = new_versions;
    }

    public NewVersionNotificationMessage(IEnumerable<VersionDescription> new_versions)
      : this("新しいバージョンがあります", new_versions.First().Title, NotificationMessageType.Info, new_versions)
    {
    }

    public IEnumerable<VersionDescription> VersionDescriptions { get; private set; }
  }


}
