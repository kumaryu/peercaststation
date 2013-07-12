using System;
using System.ComponentModel;

namespace PeerCastStation.GUI
{
  [PeerCastStation.Core.PecaSettings]
  public class GUISettings
  {
    private bool showWindowOnStartup = true;

    public bool ShowWindowOnStartup {
      get { return showWindowOnStartup; }
      set { showWindowOnStartup = value; }
    }
    public int WindowTop { get; set; }
    public int WindowLeft   { get; set; }
    public int WindowWidth  { get; set; }
    public int WindowHeight { get; set; }

    public GUISettings()
    {
      this.WindowTop    = -1;
      this.WindowLeft   = -1;
      this.WindowWidth  = -1;
      this.WindowHeight = -1;
    }
  }
}
