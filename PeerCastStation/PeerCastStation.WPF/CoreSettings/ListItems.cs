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
using PeerCastStation.Core;

namespace PeerCastStation.WPF.CoreSettings
{
  class YellowPageItem
  {
    public string Name { get; private set; }
    public IYellowPageClient YellowPageClient { get; private set; }
    internal YellowPageItem(string name, IYellowPageClient yellowpage)
    {
      this.Name = name;
      this.YellowPageClient = yellowpage;
    }

    internal YellowPageItem(IYellowPageClient yellowpage)
      : this(String.Format("{0} ({1})", yellowpage.Name, yellowpage.Uri), yellowpage)
    {
    }

    public override string ToString()
    {
      return this.Name;
    }
  }

  class YellowPageFactoryItem
  {
    internal IYellowPageClientFactory Factory { get; private set; }
    internal YellowPageFactoryItem(IYellowPageClientFactory factory)
    {
      this.Factory = factory;
    }
    public override string ToString()
    {
      return this.Factory.Name;
    }
  }
}
