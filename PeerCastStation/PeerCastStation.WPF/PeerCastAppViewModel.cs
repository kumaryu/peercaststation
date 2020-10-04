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
using System.Linq;
using PeerCastStation.Core;
using PeerCastStation.WPF.ChannelLists;
using PeerCastStation.WPF.Commons;
using PeerCastStation.WPF.Dialogs;
using PeerCastStation.WPF.Logs;
using System.Windows;
using System.Net.Sockets;

namespace PeerCastStation.WPF
{
  class PeerCastAppViewModel : ViewModelBase, IDisposable, IPeerCastMonitor
  {
    private readonly PeerCastApplication application;
    public PeerCastApplication Model { get { return application; } }

    public string WindowTitle { get; private set; }

    private string GetPortStatus(AddressFamily family)
    {
      switch (application.PeerCast.GetPortStatus(family)) {
      case Core.PortStatus.Open:
        return "開放";
      case Core.PortStatus.Firewalled:
        return "未開放";
      case Core.PortStatus.Unavailable:
        return "利用不可";
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

    public string Version { get { return this.application.PeerCast.AgentName; } }

    private readonly LogViewModel log = new LogViewModel();
    public LogViewModel Log { get { return log; } }

    internal VersionInfoViewModel VersionInfo
    {
      get { return new VersionInfoViewModel(application); }
    }

    private readonly WPFSettings settings;

    internal PeerCastAppViewModel(PeerCastApplication application)
    {
      this.application = application;
      settings = application.Settings.Get<WPFSettings>();
      var peerCast = application.PeerCast;
      channelList = new ChannelListViewModel(peerCast);
      peerCast.AddChannelMonitor(this);
      WindowTitle = CreateWindowTitle();
    }

    public void Dispose()
    {
      application.PeerCast.RemoveChannelMonitor(this);
      log.Dispose();
    }

    public void UpdateStatus()
    {
      OnPropertyChanged("PortStatus");
      channelList.UpdateChannelList();
      UpdateWindowTitle();
      log.UpdateLog();
    }

    public void OnChannelChanged(PeerCastChannelAction action, Channel channel)
    {
      Application.Current.Dispatcher.BeginInvoke(new Action(() => {
        channelList.UpdateChannelList();
      }));
    }

    public void OnTimer()
    {
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
        System.Diagnostics.Process.Start(
          new System.Diagnostics.ProcessStartInfo($"http://{host}/html/index.html") {
            UseShellExecute = true,
          }
        );
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
        System.Diagnostics.Process.Start(
          new System.Diagnostics.ProcessStartInfo($"http://{host}/help/index.html") {
            UseShellExecute = true,
          }
        );
      }
    }

    public void Quit()
    {
      application.Stop();
    }

    private string CreateWindowTitle()
    {
      switch (settings.WindowTitleMode) {
      case WindowTitleMode.Simple:
        return System.Text.RegularExpressions.Regex.Replace(application.PeerCast.AgentName, "/.*", "");
      case WindowTitleMode.Version:
        return application.PeerCast.AgentName;
      case WindowTitleMode.ChannelStats:
        var cnt = application.PeerCast.Channels.Count;
        var (localDirects, localRelays, totalDirects, totalRelays) = 
          application.PeerCast.Channels
          .Aggregate((0, 0, 0, 0), (r, c) =>
            (r.Item1 + c.LocalDirects, r.Item2 + c.LocalRelays, r.Item3 + c.TotalDirects, r.Item4 + c.TotalRelays)
          );
        return $"{cnt}ch ({totalDirects}/{totalRelays}) [{localDirects}/{localRelays}]";
      default:
        return "";
      }
    }

    private WindowTitleMode lastWindowTitleMode = WindowTitleMode.Simple;
    private void UpdateWindowTitle()
    {
      switch (settings.WindowTitleMode) {
      case WindowTitleMode.Simple:
        if (lastWindowTitleMode!=settings.WindowTitleMode) {
          WindowTitle = CreateWindowTitle();
          OnPropertyChanged(nameof(WindowTitle));
          lastWindowTitleMode = settings.WindowTitleMode;
        }
        break;
      case WindowTitleMode.Version:
        if (lastWindowTitleMode!=settings.WindowTitleMode) {
          WindowTitle = CreateWindowTitle();
          OnPropertyChanged(nameof(WindowTitle));
        }
        break;
      case WindowTitleMode.ChannelStats:
        WindowTitle = CreateWindowTitle();
        OnPropertyChanged(nameof(WindowTitle));
        break;
      }
      lastWindowTitleMode = settings.WindowTitleMode;
    }

  }

}
