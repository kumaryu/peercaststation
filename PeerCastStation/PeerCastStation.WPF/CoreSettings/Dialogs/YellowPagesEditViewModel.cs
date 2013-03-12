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
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PeerCastStation.Core;
using PeerCastStation.WPF.Commons;

namespace PeerCastStation.WPF.CoreSettings.Dialogs
{
  class YellowPagesEditViewModel
  {
    private readonly YellowPageFactoryItem[] protocols;
    public YellowPageFactoryItem[] Protocols { get { return protocols; } }
    private readonly Command add;
    public Command Add { get { return add; } }

    public string Name { get; set; }
    public YellowPageFactoryItem SelectedProtocol { get; set; }
    public string Address { get; set; }

    internal YellowPagesEditViewModel(PeerCast peerCast)
    {
      protocols = peerCast.YellowPageFactories
        .Select(factory => new YellowPageFactoryItem(factory)).ToArray();
      add = new Command(() =>
      {
        if (String.IsNullOrEmpty(Name)
            || SelectedProtocol == null)
          return;
        var factory = SelectedProtocol.Factory;
        var protocol = factory.Protocol;
        if (String.IsNullOrEmpty(protocol))
          return;

        Uri uri;
        var md = Regex.Match(Address, @"\A([^:/]+)(:(\d+))?\Z");
        if (md.Success &&
          Uri.CheckHostName(md.Groups[1].Value) != UriHostNameType.Unknown &&
          Uri.TryCreate(protocol + "://" + Address, UriKind.Absolute, out uri) &&
          factory.CheckURI(uri))
        {
        }
        else if (Uri.TryCreate(Address, UriKind.Absolute, out uri) &&
          factory.CheckURI(uri))
        {
        }
        else
        {
          return;
        }

        peerCast.AddYellowPage(protocol, Name, uri);
      });
    }
  }
}
