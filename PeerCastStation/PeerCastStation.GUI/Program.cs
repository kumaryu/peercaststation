// PeerCastStation, a P2P streaming servent.
// Copyright (C) 2011 Ryuichi Sakamoto (kumaryu@kumaryu.net)
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
using System.Windows.Forms;

namespace PeerCastStation.GUI
{
  static class Program
  {
    /// <summary>
    /// アプリケーションのメイン エントリ ポイントです。
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
      /*
      if (ProcessControl.IsFirstInstance) {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var main_form = new MainForm();
        ProcessControl.OpenUriRequested += (sender, e) => {
          main_form.OpenPeerCastUri(e.Uri);
        };
        ProcessControl.Start();
        for (var i=0; i<args.Length; i++) {
          if (args[i]=="-url" && i+1<args.Length) {
            main_form.OpenPeerCastUri(args[++i]);
          }
        }
        Application.Run(main_form);
      }
      else {
        ProcessControl.Start();
        for (var i=0; i<args.Length; i++) {
          if (args[i]=="-url" && i+1<args.Length) {
            ProcessControl.Instance.OpenUri(args[++i]);
          }
        }
      }
       */
    }

    static bool isOSX;
    static public bool IsOSX { get { return isOSX; } }
    static Program()
    {
      if (PlatformID.Unix  ==Environment.OSVersion.Platform ||
          PlatformID.MacOSX==Environment.OSVersion.Platform) {
        var start_info = new System.Diagnostics.ProcessStartInfo("uname");
        start_info.RedirectStandardOutput = true;
        start_info.UseShellExecute = false;
        start_info.ErrorDialog = false;
        var process = System.Diagnostics.Process.Start(start_info);
        if (process!=null) {
          isOSX = System.Text.RegularExpressions.Regex.IsMatch(
              process.StandardOutput.ReadToEnd(), @"Darwin");
        }
        else {
          isOSX = false;
        }
      }
      else {
        isOSX = false;
      }
    }
  }
}
