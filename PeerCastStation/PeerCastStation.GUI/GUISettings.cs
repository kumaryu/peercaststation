using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PeerCastStation.GUI
{
  public class GUISettings
  {
    public bool ShowWindowOnStartup { get; set; }
    public GUISettings()
    {
      this.ShowWindowOnStartup = true;
    }
  }
}
