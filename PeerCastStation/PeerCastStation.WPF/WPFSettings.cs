using System;
using PeerCastStation.Core;

namespace PeerCastStation.WPF
{
  [PeerCastStation.Core.PecaSettings]
  public enum RemoteNodeName {
    Uri,
    EndPoint,
    SessionID,
  }

  [PecaSettings]
  public enum WindowTitleMode {
    Simple,
    Version,
    ChannelStats,
  }

  [PeerCastStation.Core.PecaSettings]
  public class WPFSettings
  {
    private bool showWindowOnStartup = true;
    private bool showNotifications = true;

    public bool ShowWindowOnStartup {
      get { return showWindowOnStartup; }
      set { showWindowOnStartup = value; }
    }
    public bool ShowNotifications {
      get { return showNotifications; }
      set { showNotifications = value; }
    }

    public RemoteNodeName RemoteNodeName { get; set; } = RemoteNodeName.SessionID;
    public double WindowLeft   { get; set; }
    public double WindowTop    { get; set; }
    public double WindowWidth  { get; set; }
    public double WindowHeight { get; set; }

    public WindowTitleMode WindowTitleMode { get; set; } = WindowTitleMode.Version;

    public WPFSettings()
    {
      WindowLeft   = Double.NaN;
      WindowTop    = Double.NaN;
      WindowWidth  = Double.NaN;
      WindowHeight = Double.NaN;
    }
  }

}
