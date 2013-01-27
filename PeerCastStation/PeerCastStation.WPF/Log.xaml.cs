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
using Microsoft.Win32;

namespace PeerCastStation.WPF
{
  /// <summary>
  /// Log.xaml の相互作用ロジック
  /// </summary>
  public partial class Log : UserControl
  {
    public Log()
    {
      InitializeComponent();
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
      var dialog = new SaveFileDialog();
      dialog.Title = "ログ記録ファイルの選択";
      dialog.Filter = "ログファイル(*.txt;*.log)|*.txt;*.log|全てのファイル(*.*)|*.*";
      dialog.ShowDialog();
      outputFileName.Text = dialog.FileName;
      outputFileName.GetBindingExpression(TextBox.TextProperty).UpdateSource();
    }
  }
}
