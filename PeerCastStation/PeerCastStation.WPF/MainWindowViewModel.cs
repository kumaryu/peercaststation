using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PeerCastStation.Core;
using PeerCastStation.WPF.CoreSettings;

namespace PeerCastStation.WPF
{
  class MainWindowViewModel
  {
    public string PortStatus { get { return "hoge"; } }

    private readonly SettingViewModel setting;
    public SettingViewModel Setting { get { return setting; } }

    private readonly LogViewModel log = new LogViewModel();
    public LogViewModel Log { get { return log; } }

    public MainWindowViewModel(PeerCast peerCast)
    {
      setting = new SettingViewModel(peerCast);
    }
  }
}
