using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using PeerCastStation.Core;
using PeerCastStation.WPF.Dialogs;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace PeerCastStation.WPF
{
  /// <summary>
  /// MainWindow.xaml の相互作用ロジック
  /// </summary>
  public partial class MainWindow : Window
  {
    public MainWindow()
    {
      InitializeComponent();

      Closing += (sender, e) =>
      {
        //System.Threading.Thread.Sleep(1000);
      };
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
      base.OnClosing(e);
      e.Cancel = true;
      Visibility = Visibility.Hidden;
      //this.Show();
    }

    private void VersionInfoButton_Click(object sender, RoutedEventArgs e)
    {
      var dialog = new VersionInfoWindow
      {
        Owner = Window.GetWindow(this),
        DataContext = ((MainWindowViewModel)DataContext).VersionInfo
      };
      dialog.ShowDialog();
    }
  }
}
