using System;
using System.Windows.Input;

namespace PeerCastStation.WPF
{
  internal static class PeerCastCommands
  {
    public static RoutedUICommand StartBroadcasting =
      new RoutedUICommand("配信(_B)...", "StartBroadcasting", typeof(PeerCastCommands));
    public static RoutedUICommand OpenSettings =
      new RoutedUICommand("設定(_S)...", "OpenSettings", typeof(PeerCastCommands));
    public static RoutedUICommand ShowLogs =
      new RoutedUICommand("ログの表示(_L)", "ShowLogs", typeof(PeerCastCommands));
    public static RoutedUICommand OpenBrowserUI =
      new RoutedUICommand("ブラウザで表示(_B)", "OpenBrowserUI", typeof(PeerCastCommands));
    public static RoutedUICommand OpenHelp =
      new RoutedUICommand("ヘルプを表示(_H)", "OpenHelp", typeof(PeerCastCommands));
    public static RoutedUICommand Quit =
      new RoutedUICommand("終了(_Q)", "Quit", typeof(PeerCastCommands));
    public static RoutedUICommand About =
      new RoutedUICommand("バージョン情報(_A)...", "About", typeof(PeerCastCommands));

  }
}
