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
using System.Net;
using System.Net.Sockets;
using System.Text;
using PeerCastStation.Core;
using PeerCastStation.WPF.Commons;

namespace PeerCastStation.WPF.CoreSettings.Dialogs
{
  class ListenerEditViewModel
  {
    public string Address { get; set; }
    public int Port { get; set; }

    public bool IsLocalRelay { get; set; }
    public bool IsLocalDirect { get; set; }
    public bool IsLocalInterface { get; set; }
    public bool IsGlobalRelay { get; set; }
    public bool IsGlobalDirect { get; set; }
    public bool IsGlobalInterface { get; set; }

    private readonly Command add;
    public Command Add { get { return add; } }

    internal ListenerEditViewModel(PeerCast peerCast)
    {
      Address = "IPv4 Any";
      Port = 7144;
      IsLocalRelay = true;
      IsLocalDirect = true;
      IsLocalInterface = true;
      IsGlobalRelay = true;

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
          IsLocalRelay, IsLocalDirect, IsLocalInterface);
        var glocalAccepts = ToOutputStreamType(
          IsGlobalRelay, IsGlobalDirect, IsGlobalInterface);
        try
        {
          peerCast.StartListen(
            new IPEndPoint(address, Port), localAccepts, glocalAccepts);
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
  }
}
