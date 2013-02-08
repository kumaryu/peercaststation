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
  /// UpdaterWindow.xaml の相互作用ロジック
  /// </summary>
  public partial class UpdaterWindow : Window
  {
    System.Windows.Forms.WebBrowser webBrowser = new System.Windows.Forms.WebBrowser();

    public UpdaterWindow()
    {
      InitializeComponent();

      FormsHost.Child = webBrowser;
      FormsHost.DataContextChanged += (sender, e)
        => webBrowser.DocumentText = FormsHost.DataContext as string;
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
      Close();
    }
  }
}
