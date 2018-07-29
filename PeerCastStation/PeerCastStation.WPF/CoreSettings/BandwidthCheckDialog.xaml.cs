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
using PeerCastStation.Core;

namespace PeerCastStation.WPF.CoreSettings
{
  /// <summary>
  /// BandwidthCheckDialog.xaml の相互作用ロジック
  /// </summary>
  public partial class BandwidthCheckDialog : Window
  {
    private BandwidthChecker checker;
    public NetworkType NetworkType { get; private set; }
    public BandwidthCheckDialog(PeerCastApplication app, NetworkType networkType)
    {
      InitializeComponent();
      this.DataContext = this;
      this.NetworkType = networkType;
      Uri target_uri;
      string uri_key;
      switch (networkType) {
      case NetworkType.IPv6:
        uri_key = "BandwidthCheckerV6";
        break;
      case NetworkType.IPv4:
      default:
        uri_key = "BandwidthChecker";
        break;
      }
      if (app.Configurations.TryGetUri(uri_key, out target_uri)) {
        this.checker = new BandwidthChecker(target_uri);
        this.checker.BandwidthCheckCompleted += checker_BandwidthCheckCompleted;
        this.checker.RunAsync();
        this.Status = "帯域測定中";
      }
      else {
        this.IsChecking = false;
        this.Status = "接続先設定が取得できません";
      }
    }

    public string Status {
      get { return (string)GetValue(StatusProperty); }
      set { SetValue(StatusProperty, value); }
    }
    public static readonly DependencyProperty StatusProperty = 
      DependencyProperty.Register("Status", typeof(string), typeof(BandwidthCheckDialog), new PropertyMetadata(""));

    public bool IsChecking {
      get { return (bool)GetValue(IsCheckingProperty); }
      set { SetValue(IsCheckingProperty, value); }
    }
    public static readonly DependencyProperty IsCheckingProperty = 
      DependencyProperty.Register("IsChecking", typeof(bool), typeof(BandwidthCheckDialog), new PropertyMetadata(true));

    public int? Result { get; private set; }

    private void checker_BandwidthCheckCompleted(
        object sender,
        BandwidthCheckCompletedEventArgs args)
    {
      if (args.Success) {
        Result = (int)((args.Bitrate / 1000) * 0.8 / 100) * 100;
        Status = String.Format("帯域測定完了: {0}kbps, 設定推奨値: {1}kbps",
          args.Bitrate/1000,
          (int)((args.Bitrate / 1000) * 0.8 / 100) * 100);
      }
      else {
        Status = "帯域測定失敗。接続できませんでした";
      }
      IsChecking = false;
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
      this.Close();
    }

  }
}
