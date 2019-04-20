// PeerCastStation, a P2P streaming servent.
// Copyright (C) 2013 PROGRE (djyayutto@gmail.com)
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
using System.Linq;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace PeerCastStation.WPF.CoreSettings
{
  /// <summary>
  /// Settings.xaml の相互作用ロジック
  /// </summary>
  public partial class SettingControl : UserControl
  {
    public SettingControl()
    {
      InitializeComponent();
    }

    private void BandwidthCheckButton_Click(object sender, RoutedEventArgs args)
    {
      var dialog = new BandwidthCheckDialog(Core.NetworkType.IPv4);
      dialog.Owner = Window.GetWindow(this);
      dialog.ShowDialog();
      if (dialog.Result.HasValue) {
        ((SettingViewModel)this.DataContext).MaxUpstreamRate = dialog.Result.Value;
      }
    }

    private void BandwidthCheckButtonIPv6_Click(object sender, RoutedEventArgs args)
    {
      var dialog = new BandwidthCheckDialog(Core.NetworkType.IPv6);
      dialog.Owner = Window.GetWindow(this);
      dialog.ShowDialog();
      if (dialog.Result.HasValue) {
        ((SettingViewModel)this.DataContext).MaxUpstreamRateIPv6 = dialog.Result.Value;
      }
    }

    private void PortCheckButton_Click(object sender, RoutedEventArgs args)
    {
      ((SettingViewModel)this.DataContext).CheckPort();
    }

  }

  public class BindableAddressValidationRule
    : ValidationRule
  {
    public override ValidationResult Validate(object value, CultureInfo cultureInfo)
    {
      var str = value as string;
      if (str==null) return new ValidationResult(false, "Invalid Address");
      switch (str) {
      case "IPv4 Any":
      case "IPv6 Any":
        return new ValidationResult(true, null);
      default:
        if (System.Net.IPAddress.TryParse(str, out var addr)) {
          if (System.Net.IPAddress.IsLoopback(addr) ||
              System.Net.IPAddress.IPv6Any.Equals(addr) ||
              System.Net.IPAddress.Any.Equals(addr)) {
            return new ValidationResult(true, null);
          }
          else {
            var localAddresses =
              System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
              .Where(intf => !intf.IsReceiveOnly)
              .Where(intf => intf.OperationalStatus==System.Net.NetworkInformation.OperationalStatus.Up)
              .Where(intf => intf.NetworkInterfaceType!=System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
              .Select(intf => intf.GetIPProperties())
              .SelectMany(prop => prop.UnicastAddresses).ToArray();
            var valid = localAddresses.Any(uaddr => uaddr.Address.Equals(addr));
            if (valid) {
              return new ValidationResult(true, null);
            }
            else {
              return new ValidationResult(false, "Invalid Address");
            }
          }
        }
        else {
          return new ValidationResult(false, "Invalid Address");
        }
      }
    }

  }

}

