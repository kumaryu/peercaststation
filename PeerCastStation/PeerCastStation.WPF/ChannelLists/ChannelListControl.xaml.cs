using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using PeerCastStation.WPF.ChannelLists.Dialogs;

namespace PeerCastStation.WPF.ChannelLists
{
  /// <summary>
  /// AllChannels.xaml の相互作用ロジック
  /// </summary>
  public partial class ChannelListControl : UserControl
  {
    public ChannelListControl()
    {
      InitializeComponent();
    }

    private void BroadcastButton_Click(object sender, RoutedEventArgs e)
    {
      var dialog = new BroadcastWindow
      {
        Owner = Window.GetWindow(this),
        DataContext = ((ChannelListViewModel)DataContext).Broadcast
      };
      dialog.ShowDialog();
    }
  }
}
