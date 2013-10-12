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
using PeerCastStation.Core;
using PeerCastStation.WPF.Commons;
using System.Collections.Generic;

namespace PeerCastStation.WPF.CoreSettings
{
  class OtherSettingViewModel : ViewModelBase
  {
    public class ChannelCleanupModeItem {
      public string Name { get; set; }
      public PeerCastStation.ChannelCleaner.CleanupMode Mode { get; set; }
    }
    private static ChannelCleanupModeItem[] channelCleanupModeItems = new ChannelCleanupModeItem[] {
      new ChannelCleanupModeItem { Name="自動切断しない",                   Mode=ChannelCleaner.CleanupMode.None },
      new ChannelCleanupModeItem { Name="接続していないチャンネル",          Mode=ChannelCleaner.CleanupMode.Disconnected },
      new ChannelCleanupModeItem { Name="視聴・リレーをしていないチャンネル", Mode=ChannelCleaner.CleanupMode.NotRelaying },
      new ChannelCleanupModeItem { Name="視聴をしていないチャンネル",        Mode=ChannelCleaner.CleanupMode.NotPlaying },
    };
    public IEnumerable<ChannelCleanupModeItem> ChannelCleanupModeItems {
      get { return channelCleanupModeItems; }
    }

    public PeerCastStation.ChannelCleaner.CleanupMode ChannelCleanupMode {
      get { return ChannelCleaner.Mode; }
      set {
        var old = ChannelCleaner.Mode;
        if (SetProperty("ChannelCleanupMode", ref old, value)) {
          ChannelCleaner.Mode = value;
        }
      }
    }

    public int ChannelCleanupInactiveLimit {
      get { return ChannelCleaner.InactiveLimit/60000; }
      set {
        var old = ChannelCleaner.InactiveLimit/60000;
        if (SetProperty("ChannelCleanupInactiveLimit", ref old, value)) {
          ChannelCleaner.InactiveLimit = value * 60000;
        }
      }
    }

    public int MaxRelays {
      get { return pecaApp.PeerCast.AccessController.MaxRelays; }
      set {
        if (pecaApp.PeerCast.AccessController.MaxRelays!=value) {
          pecaApp.PeerCast.AccessController.MaxRelays = value;
          OnPropertyChanged("MaxRelays");
        }
      }
    }

    public int MaxRelaysPerChannel {
      get { return pecaApp.PeerCast.AccessController.MaxRelaysPerChannel; }
      set {
        if (pecaApp.PeerCast.AccessController.MaxRelaysPerChannel!=value) {
          pecaApp.PeerCast.AccessController.MaxRelaysPerChannel = value;
          OnPropertyChanged("MaxRelaysPerChannel");
        }
      }
    }

    public int MaxDirects {
      get { return pecaApp.PeerCast.AccessController.MaxPlays; }
      set {
        if (pecaApp.PeerCast.AccessController.MaxPlays!=value) {
          pecaApp.PeerCast.AccessController.MaxPlays = value;
          OnPropertyChanged("MaxPlays");
        }
      }
    }

    public int MaxDirectsPerChannel {
      get { return pecaApp.PeerCast.AccessController.MaxPlaysPerChannel; }
      set {
        if (pecaApp.PeerCast.AccessController.MaxPlaysPerChannel!=value) {
          pecaApp.PeerCast.AccessController.MaxPlaysPerChannel = value;
          OnPropertyChanged("MaxPlaysPerChannel");
        }
      }
    }

    public int MaxUpstreamRate {
      get { return pecaApp.PeerCast.AccessController.MaxUpstreamRate; }
      set {
        if (pecaApp.PeerCast.AccessController.MaxUpstreamRate!=value) {
          pecaApp.PeerCast.AccessController.MaxUpstreamRate = value;
          OnPropertyChanged("MaxUpstreamRate");
        }
      }
    }

    public int MaxUpstreamRatePerChannel {
      get { return pecaApp.PeerCast.AccessController.MaxUpstreamRatePerChannel; }
      set {
        if (pecaApp.PeerCast.AccessController.MaxUpstreamRatePerChannel!=value) {
          pecaApp.PeerCast.AccessController.MaxUpstreamRatePerChannel = value;
          OnPropertyChanged("MaxUpstreamRatePerChannel");
        }
      }
    }

    public bool IsShowWindowOnStartup
    {
      get { return pecaApp.Settings.Get<WPFSettings>().ShowWindowOnStartup; }
      set {
        if (pecaApp.Settings.Get<WPFSettings>().ShowWindowOnStartup!=value) {
          pecaApp.Settings.Get<WPFSettings>().ShowWindowOnStartup = value;
          OnPropertyChanged("IsShowWindowOnStartup");
        }
      }
    }

    PeerCastApplication pecaApp;
    internal OtherSettingViewModel(PeerCastApplication peca_app)
    {
      pecaApp = peca_app;
    }
  }
}
