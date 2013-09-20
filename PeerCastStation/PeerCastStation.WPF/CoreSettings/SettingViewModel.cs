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
using System.Collections.ObjectModel;
using System.Linq;
using PeerCastStation.Core;
using PeerCastStation.WPF.Commons;
using PeerCastStation.WPF.CoreSettings.Dialogs;
using System.ComponentModel;

namespace PeerCastStation.WPF.CoreSettings
{
  public class OutputListenerViewModel
    : INotifyPropertyChanged
  {
    private OutputListener model;
    public OutputListener Model { get { return model; } }

    public OutputListenerViewModel(OutputListener model)
    {
      this.model = model;
      RegenerateAuthKey = new Command(DoRegenerateAuthKey);
    }

    public System.Net.IPAddress Address {
      get { return model.LocalEndPoint.Address; }
    }
    public int Port {
      get { return model.LocalEndPoint.Port; }
    }
    public bool GlobalRelay {
      get { return (model.GlobalOutputAccepts & OutputStreamType.Relay)!=0; }
      set {
        if (((model.GlobalOutputAccepts & OutputStreamType.Relay)!=0)!=value) {
          if (value) model.GlobalOutputAccepts |=  OutputStreamType.Relay;
          else       model.GlobalOutputAccepts &= ~OutputStreamType.Relay;
          OnPropertyChanged("GlobalRelay");
        }
      }
    }
    public bool GlobalPlay {
      get { return (model.GlobalOutputAccepts & OutputStreamType.Play)!=0; }
      set {
        if (((model.GlobalOutputAccepts & OutputStreamType.Play)!=0)!=value) {
          if (value) model.GlobalOutputAccepts |=  OutputStreamType.Play;
          else       model.GlobalOutputAccepts &= ~OutputStreamType.Play;
          OnPropertyChanged("GlobalPlay");
        }
      }
    }
    public bool GlobalInterface {
      get { return (model.GlobalOutputAccepts & OutputStreamType.Interface)!=0; }
      set {
        if (((model.GlobalOutputAccepts & OutputStreamType.Interface)!=0)!=value) {
          if (value) model.GlobalOutputAccepts |=  OutputStreamType.Interface;
          else       model.GlobalOutputAccepts &= ~OutputStreamType.Interface;
          OnPropertyChanged("GlobalInterface");
        }
      }
    }
    public bool GlobalAuthRequired {
      get { return model.GlobalAuthorizationRequired; }
      set {
        if (model.GlobalAuthorizationRequired!=value) {
          model.GlobalAuthorizationRequired = value;
          OnPropertyChanged("GlobalAuthRequired");
        }
      }
    }
    public bool LocalRelay {
      get { return (model.LocalOutputAccepts & OutputStreamType.Relay)!=0; }
      set {
        if (((model.LocalOutputAccepts & OutputStreamType.Relay)!=0)!=value) {
          if (value) model.LocalOutputAccepts |=  OutputStreamType.Relay;
          else       model.LocalOutputAccepts &= ~OutputStreamType.Relay;
          OnPropertyChanged("LocalRelay");
        }
      }
    }
    public bool LocalPlay {
      get { return (model.LocalOutputAccepts & OutputStreamType.Play)!=0; }
      set {
        if (((model.LocalOutputAccepts & OutputStreamType.Play)!=0)!=value) {
          if (value) model.LocalOutputAccepts |=  OutputStreamType.Play;
          else       model.LocalOutputAccepts &= ~OutputStreamType.Play;
          OnPropertyChanged("LocalPlay");
        }
      }
    }
    public bool LocalInterface {
      get { return (model.LocalOutputAccepts & OutputStreamType.Interface)!=0; }
      set {
        if (((model.LocalOutputAccepts & OutputStreamType.Interface)!=0)!=value) {
          if (value) model.LocalOutputAccepts |=  OutputStreamType.Interface;
          else       model.LocalOutputAccepts &= ~OutputStreamType.Interface;
          OnPropertyChanged("LocalInterface");
        }
      }
    }
    public bool LocalAuthRequired {
      get { return model.LocalAuthorizationRequired; }
      set {
        if (model.LocalAuthorizationRequired!=value) {
          model.LocalAuthorizationRequired = value;
          OnPropertyChanged("LocalAuthRequired");
        }
      }
    }
    public string AuthId {
      get { return model.AuthenticationKey!=null ? model.AuthenticationKey.Id : null; }
    }
    public string AuthPassword {
      get { return model.AuthenticationKey!=null ? model.AuthenticationKey.Password : null; }
    }

    public System.Windows.Input.ICommand RegenerateAuthKey { get; private set; }

    private void DoRegenerateAuthKey()
    {
      model.AuthenticationKey = AuthenticationKey.Generate();
      OnPropertyChanged("AuthId");
      OnPropertyChanged("AuthPassword");
    }

    private void OnPropertyChanged(string name)
    {
      if (PropertyChanged!=null) PropertyChanged(this, new PropertyChangedEventArgs(name));
    }
    public event PropertyChangedEventHandler PropertyChanged;

    public override string ToString()
    {
      var addr = model.LocalEndPoint.Address.ToString();
      if (model.LocalEndPoint.Address.Equals(System.Net.IPAddress.Any)) addr = "IPv4 Any";
      if (model.LocalEndPoint.Address.Equals(System.Net.IPAddress.IPv6Any)) addr = "IPv6 Any";
      var local_accepts = "無し";
      if ((model.LocalOutputAccepts & ~OutputStreamType.Metadata) != OutputStreamType.None)
      {
        var accepts = new List<string>();
        if ((model.LocalOutputAccepts & OutputStreamType.Interface) != 0) accepts.Add("操作");
        if ((model.LocalOutputAccepts & OutputStreamType.Relay) != 0) accepts.Add("リレー");
        if ((model.LocalOutputAccepts & OutputStreamType.Play) != 0) accepts.Add("視聴");
        local_accepts = String.Join(",", accepts.ToArray());
      }
      var global_accepts = "無し";
      if ((model.GlobalOutputAccepts & ~OutputStreamType.Metadata) != OutputStreamType.None)
      {
        var accepts = new List<string>();
        if ((model.GlobalOutputAccepts & OutputStreamType.Interface) != 0) accepts.Add("操作");
        if ((model.GlobalOutputAccepts & OutputStreamType.Relay) != 0) accepts.Add("リレー");
        if ((model.GlobalOutputAccepts & OutputStreamType.Play) != 0) accepts.Add("視聴");
        global_accepts = String.Join(",", accepts.ToArray());
      }
      return String.Format(
        "{0}:{1} LAN:{2} WAN:{3}",
        addr,
        model.LocalEndPoint.Port,
        local_accepts,
        global_accepts);
    }
  }

  class SettingViewModel : ViewModelBase
  {
    private readonly PeerCast peerCast;

    private readonly ListViewModel<OutputListenerViewModel> ports
      = new ListViewModel<OutputListenerViewModel>();
    public ListViewModel<OutputListenerViewModel> Ports {
      get {
        ports.Items = peerCast.OutputListeners
          .Select(listener => new OutputListenerViewModel(listener)).ToArray();
        return ports;
      }
    }

    internal ListenerEditViewModel ListenerEdit
    {
      get { return new ListenerEditViewModel(peerCast); }
    }

    private readonly OtherSettingViewModel otherSetting;
    public OtherSettingViewModel OtherSetting
    {
      get { return otherSetting; }
    }

    private readonly ListViewModel<YellowPageItem> yellowPagesList
      = new ListViewModel<YellowPageItem>();
    public ListViewModel<YellowPageItem> YellowPagesList
    {
      get
      {
        yellowPagesList.Items = peerCast.YellowPages
          .Select(yp => new YellowPageItem(yp)).ToArray();
        return yellowPagesList;
      }
    }

    internal YellowPagesEditViewModel YellowPagesEdit
    {
      get { return new YellowPagesEditViewModel(peerCast); }
    }

    internal SettingViewModel(PeerCastApplication peca_app)
    {
      this.peerCast = peca_app.PeerCast;
      otherSetting = new OtherSettingViewModel(peca_app);

      ports.ItemRemoving += (sender, e) => {
        peerCast.StopListen(e.Item.Model);
        OnPropertyChanged("Ports");
      };

      yellowPagesList.ItemRemoving += (sender, e) => {
        peerCast.RemoveYellowPage(e.Item.YellowPageClient);
        OnPropertyChanged("YellowPagesList");
      };
    }
  }
}
