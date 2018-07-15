using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace PeerCastStation.Core.IPC
{
  public class IPCEndPoint
    : EndPoint
  {
    public string Path { get; private set; }

    public override AddressFamily AddressFamily {
      get { return AddressFamily.Unix; }
    }

    public enum PathType {
      User,
      System,
    }

    private enum DetailedPlatform {
      Windows,
      MacOS,
      Linux,
      Unix,
    }

    private static long UID()
    {
      var startinfo = new System.Diagnostics.ProcessStartInfo("uid", "-u") {
        CreateNoWindow = true,
        RedirectStandardError = true,
        RedirectStandardOutput = true
      };
      var proc = System.Diagnostics.Process.Start(startinfo);
      var result = proc.StandardOutput.ReadToEnd();
      proc.WaitForExit();
      return Int64.Parse(result);
    }

    private static string UName()
    {
      var startinfo = new System.Diagnostics.ProcessStartInfo("uname", "-a") {
        CreateNoWindow = true,
        RedirectStandardError = true,
        RedirectStandardOutput = true
      };
      var proc = System.Diagnostics.Process.Start(startinfo);
      var result = proc.StandardOutput.ReadToEnd();
      proc.WaitForExit();
      return result;
    }

    private static DetailedPlatform GetPlatform()
    {
      switch (Environment.OSVersion.Platform) {
      case PlatformID.Win32NT:
      case PlatformID.Win32S:
      case PlatformID.Win32Windows:
      case PlatformID.WinCE:
      case PlatformID.Xbox:
        return DetailedPlatform.Windows;
      case PlatformID.MacOSX:
        return DetailedPlatform.MacOS;
      case PlatformID.Unix:
      default:
        {
          var uname = UName();
          if (System.Text.RegularExpressions.Regex.IsMatch(uname, "Darwin")) {
            return DetailedPlatform.MacOS;
          }
          else if (System.Text.RegularExpressions.Regex.IsMatch(uname, "Linux")) {
            return DetailedPlatform.Linux;
          }
          else {
            return DetailedPlatform.Unix;
          }
        }
      }
    }

    private static string GetWindowsDefaultPath(PathType pathType, string prefix)
    {
      switch (pathType) {
      case PathType.System:
        return $"/run/{prefix}/{prefix}.sock";
      case PathType.User:
        return $"/run/user/{Environment.UserName}/{prefix}/{prefix}.sock";
      default:
        throw new ArgumentException("Unsupported PathType", nameof(pathType));
      }
    }

    private static string FindFolder(params string[] paths)
    {
      return paths.FirstOrDefault(path => !String.IsNullOrEmpty(path) && Directory.Exists(path));
    }

    private static string GetMacOSDefaultPath(PathType pathType, string prefix)
    {
      switch (pathType) {
      case PathType.System:
        return GetUnixDefaultPath(pathType, prefix);
      case PathType.User:
        {
          var basepath = FindFolder(
            Environment.GetFolderPath(Environment.SpecialFolder.InternetCache),
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library/Caches")
          );
          if (basepath!=null) {
            return System.IO.Path.Combine(basepath, prefix, $"{prefix}.sock");
          }
          else {
            return System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{prefix}-{UID()}", $"{prefix}.sock");
          }
        }
      default:
        throw new ArgumentException("Unsupported PathType", nameof(pathType));
      }
    }

    private static string GetUnixDefaultPath(PathType pathType, string prefix)
    {
      switch (pathType) {
      case PathType.System:
        return System.IO.Path.Combine(
          FindFolder(
            "/run",
            "/var/run",
            System.IO.Path.GetTempPath()
          ),
          prefix,
          $"{prefix}.sock"
        );
      case PathType.User:
        {
          var uid = UID();
          var basepath = FindFolder(
            Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR"),
            $"/run/user/{uid}",
            Environment.GetFolderPath(Environment.SpecialFolder.InternetCache)
          );
          if (basepath!=null) {
            return System.IO.Path.Combine(basepath, prefix, $"{prefix}.sock");
          }
          else {
            return System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{prefix}-{uid}", $"{prefix}.sock");
          }
        }
      default:
        throw new ArgumentException("Unsupported PathType", nameof(pathType));
      }
    }

    public static string GetDefaultPath(PathType pathType, string prefix)
    {
      switch (GetPlatform()) {
      case DetailedPlatform.Windows:
        return GetWindowsDefaultPath(pathType, prefix);
      case DetailedPlatform.MacOS:
        return GetMacOSDefaultPath(pathType, prefix);
      case DetailedPlatform.Linux:
      case DetailedPlatform.Unix:
      default:
        return GetUnixDefaultPath(pathType, prefix);
      }
    }

    public IPCEndPoint(string path)
    {
      Path = path;
    }

    public override SocketAddress Serialize()
    {
      var pathBytes = System.Text.Encoding.Default.GetBytes(Path);
      var addr = new SocketAddress(this.AddressFamily, 2+pathBytes.Length+1);
      for (var i=0; i<pathBytes.Length; i++) {
        addr[2+i] = pathBytes[i];
      }
      addr[2+pathBytes.Length] = 0;
      return addr;
    }
  }

}
