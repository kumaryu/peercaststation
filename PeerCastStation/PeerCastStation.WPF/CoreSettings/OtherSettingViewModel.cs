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

namespace PeerCastStation.WPF.CoreSettings
{
  class OtherSettingViewModel : ViewModelBase
  {
    private int maxRelays;
    public int MaxRelays
    {
      get { return maxRelays; }
      set
      {
        SetProperty("MaxRelays", ref maxRelays, value,
          applyOthers.OnCanExecuteChanged);
      }
    }

    private int maxRelaysPerChannel;
    public int MaxRelaysPerChannel
    {
      get { return maxRelaysPerChannel; }
      set
      {
        SetProperty("MaxRelaysPerChannel", ref maxRelaysPerChannel, value,
          applyOthers.OnCanExecuteChanged);
      }
    }

    private int maxDirects;
    public int MaxDirects
    {
      get { return maxDirects; }
      set
      {
        SetProperty("MaxDirects", ref maxDirects, value,
          applyOthers.OnCanExecuteChanged);
      }
    }

    private int maxDirectsPerChannel;
    public int MaxDirectsPerChannel
    {
      get { return maxDirectsPerChannel; }
      set
      {
        SetProperty("MaxDirectsPerChannel", ref maxDirectsPerChannel, value,
          applyOthers.OnCanExecuteChanged);
      }
    }

    private int maxUpstreamRate;
    public int MaxUpstreamRate
    {
      get { return maxUpstreamRate; }
      set
      {
        SetProperty("MaxUpstreamRate", ref maxUpstreamRate, value,
          applyOthers.OnCanExecuteChanged);
      }
    }

    private readonly Command applyOthers;
    public Command ApplyOthers { get { return applyOthers; } }

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
      var accessController = pecaApp.PeerCast.AccessController;
      applyOthers = new Command(
        () => WriteTo(accessController),
        () => IsChanged(accessController));

      ReadFrom(accessController);
    }

    internal void ReadFrom(AccessController from)
    {
      MaxRelays = from.MaxRelays;
      MaxRelaysPerChannel = from.MaxRelaysPerChannel;
      MaxDirects = from.MaxPlays;
      MaxDirectsPerChannel = from.MaxPlaysPerChannel;
      MaxUpstreamRate = from.MaxUpstreamRate;
    }

    private bool IsChanged(AccessController ctrler)
    {
      if (ctrler.MaxRelays != MaxRelays ||
          ctrler.MaxPlays != MaxDirects ||
          ctrler.MaxRelaysPerChannel != MaxRelaysPerChannel ||
          ctrler.MaxPlaysPerChannel != MaxDirectsPerChannel ||
          ctrler.MaxUpstreamRate != MaxUpstreamRate)
        return true;
      else
        return false;
    }

    private void WriteTo(AccessController to)
    {
      to.MaxRelays = MaxRelays;
      to.MaxPlays = MaxDirects;
      to.MaxRelaysPerChannel = MaxRelaysPerChannel;
      to.MaxPlaysPerChannel = MaxDirectsPerChannel;
      to.MaxUpstreamRate = MaxUpstreamRate;
      applyOthers.OnCanExecuteChanged();
    }
  }
}
