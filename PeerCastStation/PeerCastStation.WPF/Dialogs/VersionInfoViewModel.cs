// PeerCastStation, a P2P streaming servent.
// Copyright (C) 2013 PROGRE (djyayutto@gmail.com)
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using PeerCastStation.Core;

namespace PeerCastStation.WPF.Dialogs
{
  public class VersionInfoViewModel
  {
    private readonly object[] items;
    public object[] Items { get { return items; } }

    public VersionInfoViewModel(PeerCastApplication app)
    {
      items = app
        .Plugins
        .Select(plugin => plugin.GetType().Assembly)
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
