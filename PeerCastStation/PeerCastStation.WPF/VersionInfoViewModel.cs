using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using PeerCastStation.Core;

namespace PeerCastStation.WPF
{
  public class VersionInfoViewModel
  {
    private readonly object[] items;
    public object[] Items { get { return items; } }

    public VersionInfoViewModel(PeerCastApplication app)
    {
      items = app
        .Plugins
        .Select(type => type.Assembly)
        .Distinct()
        .Select(x =>
      {
        var info = FileVersionInfo.GetVersionInfo(x.Location);
        return new
        {
          FileName = Path.GetFileName(info.FileName),
          Version = info.FileVersion,
          AssemblyName = x.FullName,
          Copyright = info.LegalCopyright
        };
      }).ToArray();
    }
  }
}
