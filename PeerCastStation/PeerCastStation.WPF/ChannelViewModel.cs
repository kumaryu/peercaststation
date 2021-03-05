using System;
using System.Linq;
using System.Windows.Media.Imaging;
using PeerCastStation.Core;
using PeerCastStation.UI;
using System.ComponentModel;
using System.Collections.Generic;
using PeerCastStation.WPF.ChannelLists.ConnectionLists;

namespace PeerCastStation.WPF
{
  public enum ConnectionStatus {
    Unknown,
    Relayable,
    RelayFull,
    NotRelayable,
    Firewalled,
    FirewalledRelaying,
    NotReceiving,
    ConnectionToRoot,
    ConnectionToTracker,
  }

  public class ChannelViewModel
    : INotifyPropertyChanged
  {
    public Channel Model { get; private set; }
    public UISettings UISettings { get; private set; }
    public ChannelViewModel(Channel model)
    {
      this.Model = model;
      this.UISettings = PeerCastApplication.Current.Settings.Get<UISettings>();
    }

    private PlayProtocol GetProtocol()
    {
      if (UISettings.DefaultPlayProtocols.TryGetValue(Model.ChannelInfo.ContentType, out var protocol)) {
        return protocol;
      }
      else {
        return PlayProtocol.Unknown;
      }
    }

    public Uri PlayListUri {
      get {
        var endpoint = Model.PeerCast.GetLocalEndPoint(System.Net.Sockets.AddressFamily.InterNetwork, OutputStreamType.Play);
        if (endpoint==null) return null;
        var parameters = new List<string>();
        var ext = "";
        switch (GetProtocol()) {
        case PlayProtocol.Unknown:
          break;
        case PlayProtocol.MSWMSP:
          parameters.Add("fmt=asx");
          ext = ".asx";
          break;
        case PlayProtocol.HTTP:
          parameters.Add("scheme=http");
          ext = ".m3u";
          break;
        case PlayProtocol.RTMP:
          parameters.Add("scheme=rtmp");
          ext = ".m3u";
          break;
        case PlayProtocol.HLS:
          parameters.Add("fmt=m3u8");
          ext = ".m3u8";
          break;
        }
        var query = parameters.Count==0 ? "" : $"?{String.Join("&", parameters)}";
        if (endpoint.Address.Equals(System.Net.IPAddress.Any)) {
          return new Uri($"http://localhost:{endpoint.Port}/pls/{Model.ChannelID.ToString("N")}{ext}{query}");
        }
        else {
          return new Uri($"http://{endpoint}/pls/{Model.ChannelID.ToString("N")}{ext}{query}");
        }
      }
    }

    public Uri StreamUri {
      get {
        var ext = Model.ChannelInfo.ContentExtension;
        var endpoint = Model.PeerCast.GetLocalEndPoint(System.Net.Sockets.AddressFamily.InterNetwork, OutputStreamType.Play);
        if (endpoint==null) return null;
        if (endpoint.Address.Equals(System.Net.IPAddress.Any)) {
          return new Uri(String.Format("http://localhost:{0}/stream/{1}{2}", endpoint.Port, Model.ChannelID.ToString("N"), ext));
        }
        else {
          return new Uri(String.Format("http://{0}/stream/{1}{2}", endpoint.ToString(), Model.ChannelID.ToString("N"), ext));
        }
      }
    }

    public Uri ContactUri {
      get {
        var url = Model.ChannelInfo.URL;
        Uri uri;
        if (!String.IsNullOrEmpty(url) && Uri.TryCreate(url, UriKind.Absolute, out uri)) {
          return uri;
        }
        else {
          return null;
        }
      }
    }

    public void Disconnect()
    {
      Model.PeerCast.CloseChannel(Model);
    }

    public void Reconnect()
    {
      Model.Reconnect();
    }

    public Guid ChannelID {
      get { return Model.ChannelID; }
    }

    public bool IsBroadcasting {
      get { return Model.IsBroadcasting; }
    }

    public IReadOnlyCollection<Content> GetContents()
    {
      return Model.Contents.ToArray();
    }

    public ChannelInfo ChannelInfo {
      get { return Model.ChannelInfo; }
      set { 
        if (Model.ChannelInfo!=value) {
          Model.ChannelInfo = value;
          OnPropertyChanged("ChannelInfo");
        }
      }
    }

    public ChannelTrack ChannelTrack {
      get { return Model.ChannelTrack; }
      set { 
        if (Model.ChannelTrack!=value) {
          Model.ChannelTrack = value;
          OnPropertyChanged("ChannelTrack");
        }
      }
    }

    public TimeSpan Uptime {
      get { return Model.Uptime; }
    }

    public bool IsTrackerSource {
      get {
        if (Model.SourceStream==null) return false;
        var info = Model.SourceStream.GetConnectionInfo();
        return (info.RemoteHostStatus & RemoteHostStatus.Tracker)!=0;
      }
    }

    public string ChannelStatus {
      get { 
        var status = "UNKNOWN";
        switch (Model.Status) {
        case SourceStreamStatus.Idle:       status = "IDLE";    break;
        case SourceStreamStatus.Connecting: status = "CONNECT"; break;
        case SourceStreamStatus.Searching:  status = "SEARCH";  break;
        case SourceStreamStatus.Receiving:  status = "RECEIVE"; break;
        case SourceStreamStatus.Error:      status = "ERROR";   break;
        }
        return status;
      }
    }

    public NetworkType NetworkType {
      get { return Model.Network; }
    }

    public ConnectionStatus ConnectionStatus {
      get {
        if (Model.PeerCast.GetPortStatus(Model.NetworkAddressFamily)!=PortStatus.Open) {
          if (Model.LocalRelays>0) {
            return ConnectionStatus.FirewalledRelaying;
          }
          else {
            return ConnectionStatus.Firewalled;
          }
        }
        else if (Model.IsRelayFull) {
          if (Model.LocalRelays>0) {
            return ConnectionStatus.RelayFull;
          }
          else {
            return ConnectionStatus.NotRelayable;
          }
        }
        else {
          return ConnectionStatus.Relayable;
        }
      }
    }

    public string Name    { get { return Model.ChannelInfo.Name; } }
    public string Bitrate { get { return String.Format("{0}kbps", Model.ChannelInfo.Bitrate); } }
    public string ConnectionCount {
      get {
        return String.Format(
          "({0}/{1}) [{2}/{3}]",
          Model.TotalDirects,
          Model.TotalRelays,
          Model.LocalDirects,
          Model.LocalRelays);
      }
    }

    public HostTree CreateHostTree()
    {
      return new HostTree(Model);
    }

    public IEnumerable<ChannelConnectionViewModel> Connections {
      get {
        var connections = new List<ChannelConnectionViewModel>();
        if (Model.SourceStream!=null) {
          connections.Add(new SourceChannelConnectionViewModel(Model.SourceStream));
        }
        var announcings = Model.PeerCast.YellowPages
          .Select(yp => yp.GetAnnouncingChannels().FirstOrDefault(c => c.Channel.ChannelID==Model.ChannelID))
          .Where(c => c!=null);
        foreach (var announcing in announcings) {
          connections.Add(new AnnounceChannelConnectionViewModel(announcing));
        }
        foreach (var os in Model.OutputStreams) {
          connections.Add(new OutputChannelConnectionViewModel(os));
        }
        return connections;
      }
    }

    public void Update()
    {
      OnPropertyChanged("ChannelStatus");
      OnPropertyChanged("ConnectionStatus");
      OnPropertyChanged("Name");
      OnPropertyChanged("Bitrate");
      OnPropertyChanged("ConnectionCount");
      OnPropertyChanged("IsTrackerSource");
    }

    void OnPropertyChanged(string name)
    {
      if (PropertyChanged!=null) {
        PropertyChanged(this, new PropertyChangedEventArgs(name));
      }
    }
    public event PropertyChangedEventHandler PropertyChanged;

    public override int GetHashCode()
    {
      return Model.GetHashCode();
    }

    public override bool Equals(object obj)
    {
      if (obj==null) return false;
      if (obj.GetType()!=this.GetType()) return false;
      return Model.Equals(((ChannelViewModel)obj).Model);
    }
  }
}
