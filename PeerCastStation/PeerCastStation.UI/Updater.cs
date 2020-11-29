﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
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

  public enum InstallerPlatform {
    Unknown = 0,
    Any,
    WindowsX86,
    WindowsX64,
    WindowsArm,
    WindowsArm64,
    LinuxX64,
    LinuxMUSLX64,
    LinuxArm,
    LinuxArm64,
    MacX64,
  }

  public class VersionEnclosure
  {
    public string Title  { get; set; }
    public long   Length { get; set; }
    public string Type   { get; set; }
    public Uri    Url    { get; set; }
    public InstallerType InstallerType { get; set; }
    public InstallerPlatform InstallerPlatform { get; set; }
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
        if (!Enum.TryParse(AppSettingsReader.GetString("InstallerType", "unknown"), true, out result)) {
          return InstallerType.Unknown;
        }
        return result;
      }
    }

    public static InstallerPlatform ParsePlatformString(string value)
    {
      switch (value.ToLowerInvariant()) {
      case "any":
        return InstallerPlatform.Any;
      case "windows-x86":
        return InstallerPlatform.WindowsX86;
      case "windows-x64":
        return InstallerPlatform.WindowsX64;
      case "windows-arm":
        return InstallerPlatform.WindowsArm;
      case "windows-arm64":
        return InstallerPlatform.WindowsArm64;
      case "linux-x64":
        return InstallerPlatform.LinuxX64;
      case "linux-musl-x64":
        return InstallerPlatform.LinuxMUSLX64;
      case "linux-arm":
        return InstallerPlatform.LinuxArm;
      case "linux-arm64":
        return InstallerPlatform.LinuxArm64;
      case "osx-x64":
        return InstallerPlatform.MacX64;
      case "unknown":
      default:
        return InstallerPlatform.Unknown;
      }
    }

    public static InstallerPlatform CurrentInstallerPlatform {
      get {
        var platform = ParsePlatformString(AppSettingsReader.GetString("InstallerPlatform", "unknown"));
        if (platform==InstallerPlatform.Unknown) {
          if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            switch (RuntimeInformation.ProcessArchitecture) {
            case Architecture.X86:
              return InstallerPlatform.WindowsX86;
            case Architecture.X64:
              return InstallerPlatform.WindowsX64;
            case Architecture.Arm:
              return InstallerPlatform.WindowsX86;
            case Architecture.Arm64:
              return InstallerPlatform.WindowsArm64;
            default:
              return InstallerPlatform.Unknown;
            }
          }
          else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
            switch (RuntimeInformation.ProcessArchitecture) {
            case Architecture.X64:
              return InstallerPlatform.LinuxX64;
            case Architecture.Arm:
              return InstallerPlatform.LinuxArm;
            case Architecture.Arm64:
              return InstallerPlatform.LinuxArm64;
            case Architecture.X86:
            default:
              return InstallerPlatform.Unknown;
            }
          }
          else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            switch (RuntimeInformation.ProcessArchitecture) {
            case Architecture.X64:
              return InstallerPlatform.MacX64;
            case Architecture.Arm:
            case Architecture.Arm64:
            case Architecture.X86:
            default:
              return InstallerPlatform.Unknown;
            }
          }
          else {
            return InstallerPlatform.Unknown;
          }
        }
        else {
          return platform;
        }
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

    private static VersionEnclosure SelectEnclocure(VersionEnclosure[] enclosures, InstallerType type, InstallerPlatform platform)
    {
      var enclosure = enclosures.FirstOrDefault(e => e.InstallerType==type && e.InstallerPlatform==platform);
      if (enclosure!=null) {
        return enclosure;
      }
      switch (platform) {
      case InstallerPlatform.WindowsArm:
      case InstallerPlatform.WindowsArm64:
        return SelectEnclocure(enclosures, type, InstallerPlatform.WindowsX86);
      case InstallerPlatform.Any:
        return SelectEnclocure(enclosures, type, InstallerPlatform.Unknown);
      case InstallerPlatform.Unknown:
        return enclosures.First(e => e.InstallerType==type && e.InstallerPlatform==InstallerPlatform.Any);
      default:
        return SelectEnclocure(enclosures, type, InstallerPlatform.Any);
      }
    }

    public static async Task<DownloadResult> DownloadAsync(VersionDescription version, Action<float> onprogress, CancellationToken ct)
    {
      var enclosure = SelectEnclocure(version.Enclosures, CurrentInstallerType, CurrentInstallerPlatform);
      using (var client = new System.Net.WebClient())
      using (ct.Register(() => client.CancelAsync(), false)) {
        if (onprogress!=null) {
          client.DownloadProgressChanged += (sender, args) => {
            onprogress(args.ProgressPercentage/100.0f);
          };
        }
        var filepath =
          System.IO.Path.Combine(
            GetDownloadPath(),
            System.IO.Path.GetFileName(enclosure.Url.AbsolutePath));
        await client.DownloadFileTaskAsync(enclosure.Url.ToString(), filepath).ConfigureAwait(false);
        return new DownloadResult(filepath, version, enclosure);
      }
    }

    private static string CreateTempPath(string prefix)
    {
      string tmppath;
      do {
        tmppath = Path.Combine(Path.GetTempPath(), $"{prefix}.{Guid.NewGuid()}");
      } while (Directory.Exists(tmppath));
      Directory.CreateDirectory(tmppath);
      return tmppath;
    }

    static string FindRootDirectory(string path)
    {
      var dir = new DirectoryInfo(path);
      var entries = dir.GetFileSystemInfos();
      if (entries.Length==1 && entries[0] is DirectoryInfo) {
        return FindRootDirectory(entries[0].FullName);
      }
      else {
        return dir.FullName;
      }
    }

    static string GetProcessEntryFile()
    {
      var entry = System.Reflection.Assembly.GetEntryAssembly().Location;
      if (Path.GetExtension(entry).ToLowerInvariant()==".dll") {
        //たぶん.NET Core以降
        return Process.GetCurrentProcess().MainModule.FileName;
      }
      else {
        //実行ファイル
        return entry;
      }
    }

    private static string ShellEscape(string arg)
    {
      if ((arg.Contains(" ") || arg.Contains("\"")) && !arg.StartsWith("\"") && !arg.EndsWith("\"")) {
        return "\"" + arg.Replace("\"", "\\\"") + "\"";
      }
      else {
        return arg;
      }
    }

    private static void StartNewSelf(string exepath, string[] additionalArgs, params string[] args)
    {
      var exefile = Path.Combine(FindRootDirectory(exepath), Path.GetFileName(GetProcessEntryFile()));
      var combinedArgs = String.Join(" ", args.Select(arg => ShellEscape(arg)).Concat(additionalArgs));
      Process.Start(exefile, combinedArgs);
    }

    public static bool Install(DownloadResult downloaded)
    {
      try {
        switch (downloaded.Enclosure.InstallerType) {
        case InstallerType.Archive:
        case InstallerType.ServiceArchive:
          {
            var tmpdir = CreateTempPath("PeerCastStation.Updater");
            ZipFile.ExtractToDirectory(downloaded.FilePath, tmpdir);
            var appdir = Path.GetDirectoryName(GetProcessEntryFile());
            StartUpdate(tmpdir, appdir, PeerCastApplication.Current?.Args ?? new string[0]);
          }
          PeerCastApplication.Current.Stop();
          break;
        case InstallerType.Installer:
          Process.Start(downloaded.FilePath);
          PeerCastApplication.Current.Stop();
          break;
        case InstallerType.ServiceInstaller:
          Process.Start(downloaded.FilePath, "/quiet");
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

    private static void DeleteEntry(DirectoryInfo dir, bool dryrun)
    {
      System.Diagnostics.Debug.WriteLine($"Removing Directory: {dir.FullName}");
      if (!dryrun) {
          dir.Delete(true);
      }
    }

    private static void DeleteEntry(FileInfo file, bool dryrun)
    {
      System.Diagnostics.Debug.WriteLine($"Removing File: {file.FullName}");
      if (!dryrun) {
          file.Delete();
      }
    }

    private static readonly int MaxRetries = 10;
    private static void CleanupTree(string path, bool removeself, bool dryrun)
    {
      var dst = new DirectoryInfo(path);
      if (!dst.Exists) return;
      int trying = 0;
    retry:
      try {
        if (removeself) {
          DeleteEntry(dst, dryrun);
        }
        else {
          foreach (var ent in dst.GetFileSystemInfos()) {
            switch (ent) {
            case DirectoryInfo dir:
              DeleteEntry(dir, dryrun);
              break;
            case FileInfo file:
              DeleteEntry(file, dryrun);
              break;
            }
          }
        }
      }
      catch (UnauthorizedAccessException) {
        if (trying++<MaxRetries) {
          System.Threading.Thread.Sleep(1000*trying);
          goto retry;
        }
        else {
          throw;
        }
      }
      catch (IOException) {
        if (trying++<MaxRetries) {
          System.Threading.Thread.Sleep(1000*trying);
          goto retry;
        }
        else {
          throw;
        }
      }
    }

    private static void CopyTree(string srcpath, string dstpath)
    {
      CleanupTree(dstpath, false, false);
      var dst = new DirectoryInfo(dstpath);
      dst.Create();
      var src = new DirectoryInfo(srcpath);
      foreach (var ent in src.GetFileSystemInfos()) {
        switch (ent) {
        case DirectoryInfo dir:
          {
            var dstdir = Path.Combine(dstpath, dir.Name);
            CopyTree(dir.FullName, dstdir);
          }
          break;
        case FileInfo file:
          {
            var dstfile = Path.Combine(dstpath, file.Name);
            file.CopyTo(dstfile, true);
          }
          break;
        }
      }
    }

    public static void Update(string sourcepath, string targetpath)
    {
      Directory.CreateDirectory(targetpath);
      CopyTree(FindRootDirectory(sourcepath), targetpath);
    }

    public static void StartUpdate(string sourcepath, string targetpath, string[] additionalArgs)
    {
      StartNewSelf(sourcepath, additionalArgs, "update", sourcepath, targetpath);
    }

    public static void Install(string zipfile)
    {
      Install(new Updater.DownloadResult(zipfile, new VersionDescription(), new VersionEnclosure { InstallerType=InstallerType.Archive }));
    }

    public static void Cleanup(string tmppath)
    {
      CleanupTree(tmppath, true, false);
    }

    public static void StartCleanup(string targetpath, string tmppath, string[] additionalArgs)
    {
      StartNewSelf(targetpath, additionalArgs, "cleanup", tmppath);
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
