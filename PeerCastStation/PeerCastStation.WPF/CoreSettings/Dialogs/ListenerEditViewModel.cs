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
using System.Net;
using System.Net.Sockets;
using PeerCastStation.Core;
using PeerCastStation.WPF.Commons;
using System.ComponentModel;

namespace PeerCastStation.WPF.CoreSettings.Dialogs
{
  class ListenerEditViewModel
    : INotifyPropertyChanged
  {
    public string address;
    public string Address {
      get { return address; }
      set { if (address!=value) { address = value; OnPropertyChanged("Address"); } }
    }
    public int port;
    public int Port {
      get { return port; }
      set { if (port!=value) { port = value; OnPropertyChanged("Port"); } }
    }

    public bool localRelay;
    public bool LocalRelay {
      get { return localRelay; }
      set { if (localRelay!=value) { localRelay = value; OnPropertyChanged("LocalRelay"); } }
    }
    public bool localPlay;
    public bool LocalPlay {
      get { return localPlay; }
      set { if (localPlay!=value) { localPlay = value; OnPropertyChanged("LocalPlay"); } }
    }
    public bool localInterface;
    public bool LocalInterface {
      get { return localInterface; }
      set { if (localInterface!=value) { localInterface = value; OnPropertyChanged("LocalInterface"); } }
    }
    public bool localAuthRequired;
    public bool LocalAuthRequired {
      get { return localAuthRequired; }
      set { if (localAuthRequired!=value) { localAuthRequired = value; OnPropertyChanged("LocalAuthRequired"); } }
    }
    public bool globalRelay;
    public bool GlobalRelay {
      get { return globalRelay; }
      set { if (globalRelay!=value) { globalRelay = value; OnPropertyChanged("GlobalRelay"); } }
    }
    public bool globalPlay;
    public bool GlobalPlay {
      get { return globalPlay; }
      set { if (globalPlay!=value) { globalPlay = value; OnPropertyChanged("GlobalPlay"); } }
    }
    public bool globalInterface;
    public bool GlobalInterface {
      get { return globalInterface; }
      set { if (globalInterface!=value) { globalInterface = value; OnPropertyChanged("GlobalInterface"); } }
    }
    public bool globalAuthRequired;
    public bool GlobalAuthRequired {
      get { return globalAuthRequired; }
      set { if (globalAuthRequired!=value) { globalAuthRequired = value; OnPropertyChanged("GlobalAuthRequired"); } }
    }
    public string authId;
    public string AuthId {
      get { return authId; }
      set { if (authId!=value) { authId = value; OnPropertyChanged("AuthId"); } }
    }
    public string authPassword;
    public string AuthPassword {
      get { return authPassword; }
      set { if (authPassword!=value) { authPassword = value; OnPropertyChanged("AuthPassword"); } }
    }
    public System.Windows.Input.ICommand RegenerateAuthKey { get; private set; }

    private readonly Command add;
    public Command Add { get { return add; } }

    internal ListenerEditViewModel(PeerCast peerCast)
    {
      Address = "IPv4 Any";
      Port = 7144;
      LocalRelay = true;
      LocalPlay = true;
      LocalInterface = true;
      GlobalRelay = true;
      GlobalAuthRequired = true;
      var key = AuthenticationKey.Generate();
      AuthId       = key.Id;
      AuthPassword = key.Password;
      RegenerateAuthKey = new Command(() => {
        key = AuthenticationKey.Generate();
        AuthId       = key.Id;
        AuthPassword = key.Password;
      });

      add = new Command(() =>
      {
        IPAddress address;
        try
        {
          address = ToIPAddress(Address);
        }
        catch (FormatException)
        {
          return;
        }
        var localAccepts = ToOutputStreamType(
          LocalRelay, LocalPlay, LocalInterface);
        var glocalAccepts = ToOutputStreamType(
          GlobalRelay, GlobalPlay, GlobalInterface);
        try
        {
          var listener = peerCast.StartListen(
            new IPEndPoint(address, Port), localAccepts, glocalAccepts);
          listener.LocalAuthorizationRequired = LocalAuthRequired;
          listener.GlobalAuthorizationRequired = GlobalAuthRequired;
          listener.AuthenticationKey = new AuthenticationKey(AuthId, AuthPassword);
        }
        catch (SocketException)
        {
        }
      });
    }

    private IPAddress ToIPAddress(string value)
    {
      switch (value)
      {
        case "IPv4 Any":
          return IPAddress.Any;
        case "IPv6 Any":
          return IPAddress.IPv6Any;
        default:
          return IPAddress.Parse(value);
      }
    }

    private OutputStreamType ToOutputStreamType(bool relay, bool direct, bool interface_)
    {
      var type = OutputStreamType.Metadata;
      if (relay) type |= OutputStreamType.Relay;
      if (direct) type |= OutputStreamType.Play;
      if (interface_) type |= OutputStreamType.Interface;
      return type;
    }

    private void OnPropertyChanged(string name)
    {
      if (PropertyChanged!=null) {
        PropertyChanged(this, new PropertyChangedEventArgs(name));
      }
    }

    public event PropertyChangedEventHandler  PropertyChanged;
  }
}
