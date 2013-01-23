using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PeerCastStation.WPF
{
  class MainWindowViewModel
  {
    public string PortStatus { get { return "hoge"; } }

    private readonly LogViewModel log = new LogViewModel();
    public LogViewModel Log { get { return log; } }
  }
}
