using System;
using System.ComponentModel;

namespace PeerCastStation.WPF
{
  [PeerCastStation.Core.PecaSettings]
  public class WPFSettings
  {
    private bool showWindowOnStartup = true;

    public bool ShowWindowOnStartup {
      get { return showWindowOnStartup; }
      set { showWindowOnStartup = value; }
    }
  }
}
