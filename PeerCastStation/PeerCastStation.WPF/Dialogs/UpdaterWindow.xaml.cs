﻿// PeerCastStation, a P2P streaming servent.
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
using System.Windows.Shapes;

namespace PeerCastStation.WPF.Dialogs
{
  /// <summary>
  /// UpdaterWindow.xaml の相互作用ロジック
  /// </summary>
  public partial class UpdaterWindow : Window
  {
    System.Windows.Forms.WebBrowser webBrowser = new System.Windows.Forms.WebBrowser();

    internal UpdaterWindow(UpdaterViewModel updater)
    {
      InitializeComponent();
      DataContext = updater;

      FormsHost.Child = webBrowser;
      FormsHost.DataContextChanged += (sender, e)
        => webBrowser.DocumentText = FormsHost.DataContext as string;
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
      Close();
    }

		private void UpdateButton_Click(object sender, RoutedEventArgs e)
		{
			((UpdaterViewModel)this.DataContext).Execute();
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			((UpdaterViewModel)this.DataContext).Execute();
		}

  }
}
