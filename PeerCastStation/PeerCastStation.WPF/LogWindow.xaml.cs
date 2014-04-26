using System.Windows;

namespace PeerCastStation.WPF
{
  /// <summary>
  /// LogWindow.xaml の相互作用ロジック
  /// </summary>
  public partial class LogWindow : Window
  {
    public LogWindow()
    {
      InitializeComponent();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
      base.OnClosing(e);
      e.Cancel = true;
      this.Hide();
    }
  }
}
