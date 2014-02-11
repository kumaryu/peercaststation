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
using System;

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
        if (ChannelCleaner.Mode!=value) {
          ChannelCleaner.Mode = value;
          OnPropertyChanged("ChannelCleanupMode");
        }
      }
    }

    public int ChannelCleanupInactiveLimit {
      get { return ChannelCleaner.InactiveLimit/60000; }
      set {
        if (ChannelCleaner.InactiveLimit/60000!=value) {
          ChannelCleaner.InactiveLimit = value * 60000;
          OnPropertyChanged("ChannelCleanupInactiveLimit");
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

    public class CheckBandwidthCommand
      : System.Windows.Input.ICommand,
        System.ComponentModel.INotifyPropertyChanged
    {
      private OtherSettingViewModel owner;
      private BandwidthChecker checker;
      private bool canExecute = true;
      private string status = "";

      public string Status {
        get { return status; }
        private set {
          if (status==value) return;
          status = value;
          OnPropertyChanged("Status");
        }
      }

      public CheckBandwidthCommand(OtherSettingViewModel owner)
      {
        this.owner = owner;
        Uri target_uri;
        if (AppSettingsReader.TryGetUri("BandwidthChecker", out target_uri)) {
          this.checker = new BandwidthChecker(target_uri);
          this.checker.BandwidthCheckCompleted += checker_BandwidthCheckCompleted;
        }
        else {
          canExecute = false;
        }
      }

      private void checker_BandwidthCheckCompleted(
          object sender,
          BandwidthCheckCompletedEventArgs args)
      {
        if (args.Success) {
          owner.MaxUpstreamRate = (int)((args.Bitrate / 1000) * 0.8 / 100) * 100;
          Status = String.Format("帯域測定完了: {0}kbps, 設定推奨値: {1}kbps",
            args.Bitrate/1000,
            (int)((args.Bitrate / 1000) * 0.8 / 100) * 100);
        }
        else {
          Status = "帯域測定失敗。接続できませんでした";
        }
        SetCanExecute(true);
      }

      public bool CanExecute(object parameter)
      {
        return canExecute;
      }

      private void SetCanExecute(bool value)
      {
        if (canExecute!=value) {
          canExecute = value;
          if (CanExecuteChanged!=null) {
            CanExecuteChanged(this, new EventArgs());
          }
        }
      }
      public event EventHandler CanExecuteChanged;

      public void Execute(object parameter)
      {
        if (!canExecute) return;
        SetCanExecute(false);
        checker.Run();
        Status = "帯域測定中";
      }

      public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
      private void OnPropertyChanged(string name)
      {
        if (PropertyChanged!=null) {
          PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(name));
        }
      }
    }

    public System.Windows.Input.ICommand CheckBandwidth { get; private set; }


    protected override void OnPropertyChanged(string propertyName)
    {
      pecaApp.SaveSettings();
      base.OnPropertyChanged(propertyName);
    }

    PeerCastApplication pecaApp;
    internal OtherSettingViewModel(PeerCastApplication peca_app)
    {
      pecaApp = peca_app;
      this.CheckBandwidth = new CheckBandwidthCommand(this);
    }
  }
}
