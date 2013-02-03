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
