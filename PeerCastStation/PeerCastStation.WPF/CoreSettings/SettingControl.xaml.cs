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
using PeerCastStation.WPF.CoreSettings.Dialogs;

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

      Ports.AddItemButton.Click += (sender, e) =>
        {
          var dialog = new ListenerEditWindow
          {
            Owner = Window.GetWindow(this),
            DataContext = ((SettingViewModel)DataContext).ListenerEdit
          };
          dialog.ShowDialog();
          Ports.GetBindingExpression(UserControl.DataContextProperty).UpdateTarget();
        };

      YellowPagesList.AddItemButton.Click += (sender, e) =>
        {
          var dialog = new YellowPagesEditWindow
          {
            Owner = Window.GetWindow(this),
            DataContext = ((SettingViewModel)DataContext).YellowPagesEdit
          };
          dialog.ShowDialog();
          YellowPagesList.GetBindingExpression(UserControl.DataContextProperty).UpdateTarget();
        };
    }

    private void AddYellowPagesButton_Click(object sender, RoutedEventArgs e)
    {
      MessageBox.Show("実装ないのねーん");
    }
  }
}
