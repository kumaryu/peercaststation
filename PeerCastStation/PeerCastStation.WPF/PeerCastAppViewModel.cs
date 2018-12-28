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
using System.Linq;
using PeerCastStation.Core;
using PeerCastStation.WPF.ChannelLists;
using PeerCastStation.WPF.Commons;
using PeerCastStation.WPF.CoreSettings;
using PeerCastStation.WPF.Dialogs;
using PeerCastStation.WPF.Logs;
using System.Windows;
using System.Net.Sockets;
using PeerCastStation.UI;

namespace PeerCastStation.WPF
{
  class PeerCastAppViewModel : ViewModelBase, IDisposable
  {
    private readonly PeerCastApplication application;
    public PeerCastApplication Model { get { return application; } }
    public UpdaterViewModel Updater { get; private set; }

    private string GetPortStatus(AddressFamily family)
    {
      switch (application.PeerCast.GetPortStatus(family)) {
      case Core.PortStatus.Open:
        return "開放";
      case Core.PortStatus.Firewalled:
        return "未開放";
      case Core.PortStatus.Unknown:
      default:
        return "開放状態不明";
      }
    }

    public string PortStatus
    {
      get
      {
        var peerCast = application.PeerCast;
        return "リレー可能ポート:" + String.Join(", ",
            peerCast.OutputListeners
            .Where(listener => (listener.GlobalOutputAccepts & OutputStreamType.Relay)!=0)
            .Select(listener => listener.LocalEndPoint.Port)
            .Distinct()
            .Select(port => port.ToString())
            .ToArray()
          )
          + " IPv4:" + GetPortStatus(AddressFamily.InterNetwork)
          + " IPv6:" + GetPortStatus(AddressFamily.InterNetworkV6);
      }
    }

    private readonly ChannelListViewModel channelList;
    public ChannelListViewModel ChannelList { get { return channelList; } }

    private readonly SettingViewModel setting;
    public SettingViewModel Setting { get { return setting; } }

    public string Version { get { return this.application.PeerCast.AgentName; } }

    private readonly LogViewModel log = new LogViewModel();
    public LogViewModel Log { get { return log; } }

    internal VersionInfoViewModel VersionInfo
    {
      get { return new VersionInfoViewModel(application); }
    }

    internal PeerCastAppViewModel(PeerCastApplication application)
    {
      this.application = application;
      Updater = new UpdaterViewModel(new Updater(application));
      var peerCast = application.PeerCast;
      channelList = new ChannelListViewModel(peerCast);
      setting = new SettingViewModel(application);

      peerCast.ChannelAdded += OnChannelChanged;
      peerCast.ChannelRemoved += OnChannelChanged;
    }

    public void Dispose()
    {
      application.PeerCast.ChannelAdded   -= OnChannelChanged;
      application.PeerCast.ChannelRemoved -= OnChannelChanged;
      log.Dispose();
    }

    public void UpdateStatus()
    {
      OnPropertyChanged("PortStatus");
      channelList.UpdateChannelList();
      log.UpdateLog();
    }

    private void OnChannelChanged(object sender, EventArgs e)
    {
      Application.Current.Dispatcher.BeginInvoke(new Action(() => {
        channelList.UpdateChannelList();
      }));
    }

    public void OpenBrowserUI()
    {
      var listener = 
        application.PeerCast.FindListener(System.Net.IPAddress.Loopback, OutputStreamType.Interface) ??
        application.PeerCast.FindListener(System.Net.IPAddress.IPv6Loopback, OutputStreamType.Interface) ??
        application.PeerCast.FindListener(System.Net.IPAddress.Loopback, OutputStreamType.All) ??
        application.PeerCast.FindListener(System.Net.IPAddress.IPv6Loopback, OutputStreamType.All);
      if (listener != null) {
        var endpoint = listener.LocalEndPoint;
        var host =
          endpoint.Address.Equals(System.Net.IPAddress.Any) ||
          endpoint.Address.Equals(System.Net.IPAddress.IPv6Any) ?
          String.Format("localhost:{0}", endpoint.Port) :
          endpoint.ToString();
        System.Diagnostics.Process.Start(String.Format("http://{0}/html/index.html", host));
      }
    }

    public void OpenHelp()
    {
      var listener = 
        application.PeerCast.FindListener(System.Net.IPAddress.Loopback, OutputStreamType.Interface) ??
        application.PeerCast.FindListener(System.Net.IPAddress.IPv6Loopback, OutputStreamType.Interface) ??
        application.PeerCast.FindListener(System.Net.IPAddress.Loopback, OutputStreamType.All) ??
        application.PeerCast.FindListener(System.Net.IPAddress.IPv6Loopback, OutputStreamType.All);
      if (listener != null) {
        var endpoint = listener.LocalEndPoint;
        var host =
          endpoint.Address.Equals(System.Net.IPAddress.Any) ||
          endpoint.Address.Equals(System.Net.IPAddress.IPv6Any) ?
          String.Format("localhost:{0}", endpoint.Port) :
          endpoint.ToString();
        System.Diagnostics.Process.Start(String.Format("http://{0}/help/index.html", host));
      }
    }

    public void Quit()
    {
      application.Stop();
    }

  }
}
