using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using PeerCastStation.Core;
using PeerCastStation.WPF.Commons;

namespace PeerCastStation.WPF.CoreSettings
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
