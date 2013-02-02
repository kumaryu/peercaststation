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
using System.Windows.Shapes;

namespace PeerCastStation.WPF.Dialogs
{
  /// <summary>
  /// VersionInfoWindow.xaml の相互作用ロジック
  /// </summary>
  public partial class VersionInfoWindow : Window
  {
    public VersionInfoWindow()
    {
      InitializeComponent();
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
      Close();
    }
  }
}
