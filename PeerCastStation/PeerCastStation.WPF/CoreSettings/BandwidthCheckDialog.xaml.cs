using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
    private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
    public NetworkType NetworkType { get; private set; }
    public BandwidthCheckDialog(NetworkType networkType)
    {
      this.DataContext = this;
      this.NetworkType = networkType;
      InitializeComponent();
    }

    protected override async void OnInitialized(EventArgs e)
    {
      base.OnInitialized(e);
      string uri_key;
      switch (NetworkType) {
      case NetworkType.IPv6:
        uri_key = "BandwidthCheckerV6";
        break;
      case NetworkType.IPv4:
      default:
        uri_key = "BandwidthChecker";
        break;
      }
      if (AppSettingsReader.TryGetUri(uri_key, out var target_uri)) {
        Status = "帯域測定中";
        IsChecking = true;
        cancellationTokenSource = new CancellationTokenSource();
        var checker = new BandwidthChecker(target_uri, NetworkType);
        try {
          var result = await checker.RunAsync(cancellationTokenSource.Token);
          if (result.Succeeded) {
            Result = (int)((result.Bitrate / 1000) * 0.8 / 100) * 100;
            Status = String.Format("帯域測定完了: {0}kbps, 設定推奨値: {1}kbps",
              result.Bitrate/1000,
              (int)((result.Bitrate / 1000) * 0.8 / 100) * 100);
          }
          else {
            Status = "帯域測定失敗。接続できませんでした";
          }
        }
        catch (OperationCanceledException) {
          Status = "キャンセルされました";
        }
        IsChecking = false;
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

    private void Button_Click(object sender, RoutedEventArgs e)
    {
      cancellationTokenSource.Cancel();
      Close();
    }

  }
}
