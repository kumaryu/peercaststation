using System;
using System.Windows.Input;

namespace PeerCastStation.WPF
{
  internal static class PeerCastCommands
  {
    private static RoutedUICommand startBroadcasting;
    private static RoutedUICommand openSettings;
    private static RoutedUICommand showLogs;
    public static RoutedUICommand StartBroadcasting { get { return startBroadcasting; } }
    public static RoutedUICommand OpenSettings      { get { return openSettings; } }
    public static RoutedUICommand ShowLogs          { get { return showLogs; } }

    static PeerCastCommands()
    {
      startBroadcasting = new RoutedUICommand("配信(_B)...", "StartBroadcasting", typeof(PeerCastCommands));
      openSettings      = new RoutedUICommand("設定(_S)...", "OpenSettings", typeof(PeerCastCommands));
      showLogs          = new RoutedUICommand("ログの表示(_L)...", "ShowLogs", typeof(PeerCastCommands));
    }
  }
}
