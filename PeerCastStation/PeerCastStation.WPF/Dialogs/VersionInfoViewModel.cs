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
using PeerCastStation.Core;
using System.Linq;

namespace PeerCastStation.WPF.Dialogs
{
  public class VersionInfoViewModel
  {
    private readonly object[] items;
    public object[] Items { get { return items; } }
    public string AgentName { get; private set; }

    public VersionInfoViewModel(PeerCastApplication app)
    {
      this.AgentName = app.Configurations.GetString("AgentName", "PeerCastStation/Unknown");
      items = app.Plugins.Select(plugin => {
        var info = plugin.GetVersionInfo();
        return new {
          Name         = plugin.Name,
          IsUsable     = plugin.IsUsable,
          FileName     = info.FileName,
          Version      = info.Version,
          AssemblyName = info.AssemblyName,
          Copyright    = info.Copyright,
        };
      }).ToArray();
    }
  }
}
