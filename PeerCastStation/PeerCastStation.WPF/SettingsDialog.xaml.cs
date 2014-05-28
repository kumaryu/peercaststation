using System.Windows;
using PeerCastStation.Core;
using PeerCastStation.WPF.CoreSettings;

namespace PeerCastStation.WPF
{
  /// <summary>
  /// SettingsDialog.xaml の相互作用ロジック
  /// </summary>
  public partial class SettingsDialog : Window
  {
    SettingViewModel viewModel;
    internal SettingsDialog(PeerCastApplication app)
    {
      var viewmodel = new SettingViewModel(app);
      this.viewModel = viewmodel;
      this.DataContext = viewmodel;
      InitializeComponent();
    }

    internal static void ShowDialog(Window owner, PeerCastApplication app)
    {
      var window = new SettingsDialog(app);
      window.Owner = owner;
      window.ShowDialog();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
      this.Close();
    }

    private void OKButton_Click(object sender, RoutedEventArgs e)
    {
      viewModel.Apply();
      this.Close();
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
      viewModel.Apply();
    }
  }
}
