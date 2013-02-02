using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using PeerCastStation.Core;
using PeerCastStation.WPF.Channels;
using PeerCastStation.WPF.Commons;
using PeerCastStation.WPF.CoreSettings;

namespace PeerCastStation.WPF
{
  class MainWindowViewModel : ViewModelBase
  {
    private readonly Timer timer;
    private readonly PeerCast peerCast;

    public string PortStatus
    {
      get
      {
        return "リレー可能ポート:" + String.Join(", ",
          peerCast.OutputListeners.Where(listener =>
            (listener.GlobalOutputAccepts & OutputStreamType.Relay) != 0
          ).Select(
            listener => listener.LocalEndPoint.Port
          ).Distinct().Select(
            port => port.ToString()
          ).ToArray())
          + " " + (peerCast.IsFirewalled.HasValue
          ? peerCast.IsFirewalled.Value ? "未開放" : "開放"
          : "開放状態不明");
      }
    }

    private readonly ChannelsViewModel allChannels;
    public ChannelsViewModel AllChannels { get { return allChannels; } }

    private readonly SettingViewModel setting;
    public SettingViewModel Setting { get { return setting; } }

    private readonly LogViewModel log = new LogViewModel();
    public LogViewModel Log { get { return log; } }

    internal MainWindowViewModel(PeerCast peerCast)
    {
      this.peerCast = peerCast;
      allChannels = new ChannelsViewModel(peerCast);
      setting = new SettingViewModel(peerCast);

      var sc = SynchronizationContext.Current;
      timer = new Timer(
        o => sc.Post(p => UpdateStatus(), null), null,
        1000, 1000);
    }

    private void UpdateStatus()
    {
      OnPropertyChanged("PortStatus");
      allChannels.UpdateChannelList();
      log.UpdateLog();
    }
  }
}
