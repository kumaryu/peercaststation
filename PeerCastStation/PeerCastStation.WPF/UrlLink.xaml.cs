using PeerCastStation.WPF.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace PeerCastStation.WPF
{
  /// <summary>
  /// UrlLink.xaml の相互作用ロジック
  /// </summary>
  public partial class UrlLink : UserControl
  {
    internal Command OpenCommand { get; }
    internal Command CopyCommand { get; }

    public string Url {
      get { return (string)GetValue(UrlProperty); }
      set { SetValue(UrlProperty, value); }
    }
    public static readonly DependencyProperty UrlProperty =
        DependencyProperty.Register("Url", typeof(string), typeof(UrlLink), new PropertyMetadata("", (d,e) => ((UrlLink)d).OnUrlChanged(e.OldValue, e.NewValue)));

    private void OnUrlChanged(object oldValue, object newValue)
    {
      OpenCommand.OnCanExecuteChanged();
      CopyCommand.OnCanExecuteChanged();
    }

    public string Text {
      get { return (string)GetValue(TextProperty); }
      set { SetValue(TextProperty, value); }
    }
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register("Text", typeof(string), typeof(UrlLink), new PropertyMetadata(""));

    public UrlLink()
    {
      InitializeComponent();
      this.OpenCommand = new Command(() => {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(this.Url) { UseShellExecute = true });
      }, () => !String.IsNullOrWhiteSpace(this.Url));
      this.CopyCommand = new Command(() => {
        try {
          Clipboard.SetText(this.Url);
        }
        catch (System.Runtime.InteropServices.COMException) {}
      }, () => !String.IsNullOrWhiteSpace(this.Url));
      openMenu.Command = this.OpenCommand;
      copyMenu.Command = this.CopyCommand;
    }

    private void Hyperlink_Click(object sender, RoutedEventArgs e)
    {
      var menu = ((Hyperlink)sender).ContextMenu;
      menu.IsOpen = true;
    }
  }
}
