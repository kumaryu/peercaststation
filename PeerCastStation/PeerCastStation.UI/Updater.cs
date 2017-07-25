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
			var results = await appcastReader.DownloadVersionInfoTaskAsync(url, cancel_token);
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
