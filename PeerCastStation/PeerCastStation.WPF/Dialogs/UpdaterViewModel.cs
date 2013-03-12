﻿// PeerCastStation, a P2P streaming servent.
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
using System.Linq;
using System.Text;
using PeerCastStation.WPF;
using PeerCastStation.WPF.Commons;

namespace PeerCastStation.WPF.Dialogs
{
  class UpdaterViewModel:ViewModelBase
  {
    private readonly VersionDescription versionInfo;

    private readonly Command download;
    public Command Download { get { return download; } }

    public string Description
    {
      get { return versionInfo.Description; }
    }

    public UpdaterViewModel(VersionDescription versionInfo)
    {
      this.versionInfo = versionInfo;

      download = new Command(
        () => Process.Start(versionInfo.Link.ToString()));
    }
  }
}
