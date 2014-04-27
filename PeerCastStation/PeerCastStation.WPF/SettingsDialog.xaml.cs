using System.Windows;

namespace PeerCastStation.WPF
{
  /// <summary>
  /// SettingsDialog.xaml の相互作用ロジック
  /// </summary>
  public partial class SettingsDialog : Window
  {
    public SettingsDialog()
    {
      InitializeComponent();
    }

    internal static void ShowDialog(Window owner, PeerCastAppViewModel vm)
    {
      var window = new SettingsDialog { DataContext=vm.Setting };
      window.Owner = owner;
      window.ShowDialog();
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
      this.Close();
    }
  }
}
