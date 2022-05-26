using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
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
      OKCommand = new Commons.Command(() => {
        viewModel.Apply();
        this.Close();
      }, () => errorControls.Count==0);
      CancelCommand = new Commons.Command(() => {
        this.Close();
      });
      ApplyCommand = new Commons.Command(() => {
        viewModel.Apply();
      }, () => viewModel.IsModified && errorControls.Count==0);
      viewModel.PropertyChanged += ViewModel_PropertyChanged;
      InitializeComponent();
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
      switch (e.PropertyName) {
      case nameof(SettingViewModel.IsModified):
        ApplyCommand.OnCanExecuteChanged();
        break;
      }
    }

    public Commons.Command OKCommand { get; private set; }
    public Commons.Command CancelCommand { get; private set; }
    public Commons.Command ApplyCommand { get; private set; }

    internal static void ShowDialog(Window owner, PeerCastApplication app)
    {
      var window = new SettingsDialog(app);
      window.Owner = owner;
      window.ShowDialog();
    }

    private HashSet<DependencyObject> errorControls = new HashSet<DependencyObject>();

    private void Window_Error(object sender, ValidationErrorEventArgs e)
    {
      var src = (DependencyObject)e.OriginalSource;
      switch (e.Action) {
      case ValidationErrorEventAction.Added:
        if (errorControls.Add(src)) {
          OKCommand.OnCanExecuteChanged();
          ApplyCommand.OnCanExecuteChanged();
          if (e.OriginalSource is FrameworkElement) {
            ((FrameworkElement)e.OriginalSource).Unloaded += Control_Unloaded;
          }
        }
        break;
      case ValidationErrorEventAction.Removed:
        if (!Validation.GetHasError((DependencyObject)e.OriginalSource)) {
          if (errorControls.Remove(src)) {
            OKCommand.OnCanExecuteChanged();
            ApplyCommand.OnCanExecuteChanged();
            if (e.OriginalSource is FrameworkElement) {
              ((FrameworkElement)e.OriginalSource).Unloaded -= Control_Unloaded;
            }
          }
        }
        break;
      }
    }

    private void Control_Unloaded(object sender, RoutedEventArgs e)
    {
      if (sender is FrameworkElement) {
        ((FrameworkElement)sender).Unloaded -= Control_Unloaded;
      }
      errorControls.Remove((DependencyObject)sender);
      OKCommand.OnCanExecuteChanged();
      ApplyCommand.OnCanExecuteChanged();
    }

  }

}
