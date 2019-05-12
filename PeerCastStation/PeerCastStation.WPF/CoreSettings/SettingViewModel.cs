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
using PeerCastStation.UI;
using PeerCastStation.WPF.Commons;
using System.ComponentModel;
using System.Threading.Tasks;

namespace PeerCastStation.WPF.CoreSettings
{
  enum PortCheckStatus {
    Unknown,
    Checking,
    Opened,
    Closed,
    Failed,
  }

  class SettingViewModel
    : ViewModelBase
  {
    internal class OutputListenerViewModel
      : INotifyPropertyChanged
    {
      private SettingViewModel owner;

      private string address;
      private int    port;
      private bool   globalRelay;
      private bool   globalPlay;
      private bool   globalInterface;
      private bool   globalAuthRequired;
      private bool   localRelay;
      private bool   localPlay;
      private bool   localInterface;
      private bool   localAuthRequired;
      private string authId;
      private string authPassword;
      private bool?  isOpen;

      public OutputListenerViewModel(SettingViewModel owner, OutputListener model)
      {
        this.owner = owner;
        if (model.LocalEndPoint.Address.Equals(System.Net.IPAddress.Any)) {
          address = "IPv4 Any";
        }
        else if (model.LocalEndPoint.Address.Equals(System.Net.IPAddress.IPv6Any)) {
          address = "IPv6 Any";
        }
        else {
          address = model.LocalEndPoint.Address.ToString();
        }
        port    = model.LocalEndPoint.Port;
        globalRelay     = (model.GlobalOutputAccepts & OutputStreamType.Relay)!=0;
        globalPlay      = (model.GlobalOutputAccepts & OutputStreamType.Play)!=0;
        globalInterface = (model.GlobalOutputAccepts & OutputStreamType.Interface)!=0;
        globalAuthRequired = model.GlobalAuthorizationRequired;
        localRelay      = (model.LocalOutputAccepts & OutputStreamType.Relay)!=0;
        localPlay       = (model.LocalOutputAccepts & OutputStreamType.Play)!=0;
        localInterface  = (model.LocalOutputAccepts & OutputStreamType.Interface)!=0;
        localAuthRequired = model.LocalAuthorizationRequired;
        authId       = model.AuthenticationKey!=null ? model.AuthenticationKey.Id : null;
        authPassword = model.AuthenticationKey!=null ? model.AuthenticationKey.Password : null;
        isOpen = null;
        RegenerateAuthKey = new Command(DoRegenerateAuthKey);
      }

      public OutputListenerViewModel(SettingViewModel owner, System.Net.IPEndPoint endpoint)
      {
        this.owner = owner;
        if (endpoint.Address.Equals(System.Net.IPAddress.Any)) {
          address = "IPv4 Any";
        }
        else if (endpoint.Address.Equals(System.Net.IPAddress.IPv6Any)) {
          address = "IPv6 Any";
        }
        else {
          address = endpoint.Address.ToString();
        }
        port    = endpoint.Port;
        globalRelay     = true;
        globalPlay      = false;
        globalInterface = false;
        globalAuthRequired = true;
        localRelay      = true;
        localPlay       = true;
        localInterface  = true;
        localAuthRequired = false;
        var authkey = AuthenticationKey.Generate();
        authId       = authkey.Id;
        authPassword = authkey.Password;
        isOpen = null;
        RegenerateAuthKey = new Command(DoRegenerateAuthKey);
      }

      public OutputListenerViewModel(SettingViewModel owner, int new_port)
      {
        this.owner = owner;
        address = "IPv4 Any";
        port    = new_port;
        globalRelay     = true;
        globalPlay      = false;
        globalInterface = false;
        globalAuthRequired = true;
        localRelay      = true;
        localPlay       = true;
        localInterface  = true;
        localAuthRequired = false;
        var authkey = AuthenticationKey.Generate();
        authId       = authkey.Id;
        authPassword = authkey.Password;
        isOpen = null;
        RegenerateAuthKey = new Command(DoRegenerateAuthKey);
      }

      public string Address {
        get { return address; }
        set {
          if (address==value) return;
          address = value;
          OnPropertyChanged("Address");
        }
      }
      public int Port {
        get { return port; }
        set {
          if (port==value) return;
          port = value;
          OnPropertyChanged("Port");
        }
      }

      public NetworkType NetworkType {
        get {
          switch (EndPoint.AddressFamily) {
          case System.Net.Sockets.AddressFamily.InterNetworkV6:
            return NetworkType.IPv6;
          case System.Net.Sockets.AddressFamily.InterNetwork:
          default:
            return NetworkType.IPv4;
          }
        }
      }

      public System.Net.IPEndPoint EndPoint {
        get {
          System.Net.IPAddress addr;
          switch (address) {
          case "IPv4 Any":
            addr = System.Net.IPAddress.Any;
            break;
          case "IPv6 Any":
            addr = System.Net.IPAddress.IPv6Any;
            break;
          default:
            addr = System.Net.IPAddress.Parse(address);
            break;
          }
          return new System.Net.IPEndPoint(addr, port);
        }
      }

      public bool GlobalRelay {
        get { return globalRelay; }
        set {
          if (globalRelay==value) return;
          globalRelay = value;
          OnPropertyChanged("GlobalRelay");
        }
      }
      public bool GlobalPlay {
        get { return globalPlay; }
        set {
          if (globalPlay==value) return;
          globalPlay = value;
          OnPropertyChanged("GlobalPlay");
          OnPropertyChanged("AuthRequired");
        }
      }
      public bool GlobalInterface {
        get { return globalInterface; }
        set {
          if (globalInterface==value) return;
          globalInterface = value;
          OnPropertyChanged("GlobalInterface");
          OnPropertyChanged("AuthRequired");
        }
      }

      public OutputStreamType GlobalAccepts {
        get {
          var res = OutputStreamType.Metadata;
          if (globalRelay)     res |= OutputStreamType.Relay;
          if (globalPlay)      res |= OutputStreamType.Play;
          if (globalInterface) res |= OutputStreamType.Interface;
          return res;
        }
      }

      public bool GlobalAuthRequired {
        get { return globalAuthRequired; }
        set {
          if (globalAuthRequired==value) return;
          globalAuthRequired = value;
          OnPropertyChanged("GlobalAuthRequired");
          OnPropertyChanged("AuthRequired");
        }
      }
      public bool LocalRelay {
        get { return localRelay; }
        set {
          if (localRelay==value) return;
          localRelay = value;
          OnPropertyChanged("LocalRelay");
        }
      }
      public bool LocalPlay {
        get { return localPlay; }
        set {
          if (localPlay==value) return;
          localPlay = value;
          OnPropertyChanged("LocalPlay");
          OnPropertyChanged("AuthRequired");
        }
      }
      public bool LocalInterface {
        get { return localInterface; }
        set {
          if (localInterface==value) return;
          localInterface = value;
          OnPropertyChanged("LocalInterface");
          OnPropertyChanged("AuthRequired");
        }
      }

      public OutputStreamType LocalAccepts {
        get {
          var res = OutputStreamType.Metadata;
          if (localRelay)     res |= OutputStreamType.Relay;
          if (localPlay)      res |= OutputStreamType.Play;
          if (localInterface) res |= OutputStreamType.Interface;
          return res;
        }
      }

      public bool LocalAuthRequired {
        get { return localAuthRequired; }
        set {
          if (localAuthRequired==value) return;
          localAuthRequired = value;
          OnPropertyChanged("LocalAuthRequired");
          OnPropertyChanged("AuthRequired");
        }
      }
      public bool AuthRequired {
        get {
          return
            GlobalAuthRequired && (GlobalInterface || GlobalPlay) ||
            LocalAuthRequired  && (LocalInterface || LocalPlay);
        }
      }
      public string AuthId {
        get { return authId; }
      }
      public string AuthPassword {
        get { return authPassword; }
      }

      public AuthenticationKey AuthenticationKey {
        get {
          return new AuthenticationKey(authId, authPassword);
        }
      }

      public bool? IsOpen {
        get { return isOpen; }
        set {
          if (isOpen==value) return;
          isOpen = value;
          OnPropertyChanged("IsOpen");
        }
      }

      public System.Windows.Input.ICommand RegenerateAuthKey { get; private set; }

      private void DoRegenerateAuthKey()
      {
        var authkey = AuthenticationKey.Generate();
        authId = authkey.Id;
        authPassword = authkey.Password;
        OnPropertyChanged("AuthId");
        OnPropertyChanged("AuthPassword");
      }

      private void OnPropertyChanged(string name)
      {
        if (PropertyChanged!=null) PropertyChanged(this, new PropertyChangedEventArgs(name));
        switch (name) {
        case "IsOpen":
          break;
        default:
          owner.IsListenersModified = true;
          break;
        }
      }
      public event PropertyChangedEventHandler PropertyChanged;
    }

    internal class YellowPageClientViewModel
      : INotifyPropertyChanged
    {
      private string name;
      private Uri    announceUri;
      private Uri    channelsUri;
      private IYellowPageClientFactory protocol;

      public string Name {
        get { return name; }
        set {
          if (name==value) return;
          name = value;
          OnPropertyChanged("Name");
        }
      }

      public string AnnounceUri {
        get { return announceUri==null ? null : announceUri.ToString(); }
        set {
          if (String.IsNullOrEmpty(value)) {
            if (announceUri==null) return;
            announceUri = null;
            OnPropertyChanged(nameof(AnnounceUri));
            return;
          }
          if (protocol==null) new ArgumentException("プロトコルが選択されていません");
          if (Uri.TryCreate(value, UriKind.RelativeOrAbsolute, out var uri)) {
            var result = protocol.ValidateUriAsync(YellowPageUriType.Announce, uri).Result;
            if (result.IsValid) {
              announceUri = uri;
              OnPropertyChanged(nameof(AnnounceUri));
            }
            else if (!result.IsValid && result.Candidate!=null) {
              announceUri = result.Candidate;
              OnPropertyChanged(nameof(AnnounceUri));
            }
            else {
              throw new ArgumentException(result.Message);
            }
          }
          else {
            throw new ArgumentException("正しいURLが指定されていません");
          }
        }
      }

      public string ChannelsUri {
        get { return channelsUri==null ? null : channelsUri.ToString(); }
        set {
          if (String.IsNullOrEmpty(value)) {
            if (channelsUri==null) return;
            channelsUri = null;
            OnPropertyChanged(nameof(ChannelsUri));
            return;
          }
          if (protocol==null) new ArgumentException("プロトコルが選択されていません");
          if (Uri.TryCreate(value, UriKind.RelativeOrAbsolute, out var uri)) {
            var result = protocol.ValidateUriAsync(YellowPageUriType.Channels, uri).Result;
            if (result.IsValid) {
              channelsUri = uri;
              OnPropertyChanged(nameof(ChannelsUri));
            }
            else if (!result.IsValid && result.Candidate!=null) {
              channelsUri = result.Candidate;
              OnPropertyChanged(nameof(ChannelsUri));
            }
            else {
              throw new ArgumentException(result.Message);
            }
          }
          else {
            throw new ArgumentException("正しいURLが指定されていません");
          }
        }
      }

      public IYellowPageClientFactory Protocol {
        get { return protocol; }
        set {
          if (protocol==value) return;
          protocol = value;
          OnPropertyChanged("Protocol");
        }
      }

      public IEnumerable<IYellowPageClientFactory> Protocols {
        get { return owner.peerCast.YellowPageFactories; }
      }

      private SettingViewModel owner;
      internal YellowPageClientViewModel(
          SettingViewModel owner,
          IYellowPageClient model)
      {
        this.owner       = owner;
        this.name        = model.Name;
        this.announceUri = model.AnnounceUri;
        this.channelsUri = model.ChannelsUri;
        this.protocol    = owner.peerCast.YellowPageFactories.FirstOrDefault(factory => factory.Protocol==model.Protocol);
      }

      internal YellowPageClientViewModel(SettingViewModel owner)
      {
        this.owner = owner;
        this.protocol = owner.peerCast.YellowPageFactories.FirstOrDefault();
      }

      public event PropertyChangedEventHandler PropertyChanged;
      private void OnPropertyChanged(string name)
      {
        if (PropertyChanged!=null) {
          PropertyChanged(this, new PropertyChangedEventArgs(name));
        }
        owner.IsYellowPagesModified = true;
      }
    }

    private readonly PeerCast peerCast;

    private bool isModified;
    public bool IsModified {
      get { return isModified; }
      private set { SetProperty("IsModified", ref isModified, value); }
    }

    private bool isListenersModified;
    public bool IsListenersModified {
      get { return isListenersModified; }
      private set {
        if (SetProperty("IsListenersModified", ref isListenersModified, value) && value) {
          IsModified = true;
        }
      }
    }

    private bool isYellowPagesModified;
    public bool IsYellowPagesModified {
      get { return isYellowPagesModified; }
      private set {
        if (SetProperty("IsYellowPagesModified", ref isYellowPagesModified, value) && value) {
          IsModified = true;
        }
      }
    }

    public int PrimaryPort {
      get {
        var listenerv4 = ports.FirstOrDefault(port => port.NetworkType==NetworkType.IPv4);
        if (listenerv4==null) {
          AddPort(7144, NetworkType.IPv4);
          return PrimaryPort;
        }
        else {
          return listenerv4.Port;
        }
      }
      set {
        bool changed = false;
        var listenerv4 = ports.FirstOrDefault(port => port.NetworkType==NetworkType.IPv4);
        if (listenerv4==null) {
          AddPort(value, NetworkType.IPv4);
          changed = true;
        }
        else if (listenerv4.Port!=value) {
          listenerv4.Port = value;
          changed = true;
        }

        var listenerv6 = ports.FirstOrDefault(port => port.NetworkType==NetworkType.IPv6);
        if (listenerv6!=null && listenerv6.Port!=value) {
          listenerv6.Port = value;
          changed = true;
        }
        if (changed) {
          OnPropertyChanged(nameof(PrimaryPort));
        }
      }

    }

    public bool IPv6Enabled {
      get {
        return ports.Any(p => p.EndPoint.AddressFamily==System.Net.Sockets.AddressFamily.InterNetworkV6);
      }
      set {
        if (value && !ports.Any(p => p.EndPoint.AddressFamily==System.Net.Sockets.AddressFamily.InterNetworkV6)) {
          ports.Add(new OutputListenerViewModel(this, new System.Net.IPEndPoint(System.Net.IPAddress.IPv6Any, PrimaryPort)));
          IsListenersModified = true;
          OnPropertyChanged(nameof(IPv6Enabled));
        }
        else if (!value) {
          bool changed = false;
          foreach (var listener in ports.Where(p => p.EndPoint.AddressFamily==System.Net.Sockets.AddressFamily.InterNetworkV6).ToArray()) {
            ports.Remove(listener);
            IsListenersModified = true;
            changed = true;
          }
          if (changed) {
            OnPropertyChanged(nameof(IPv6Enabled));
          }
        }
      }
    }

    private ObservableCollection<OutputListenerViewModel> ports =
      new ObservableCollection<OutputListenerViewModel>();
    public IEnumerable<OutputListenerViewModel> Ports {
      get { return ports; }
    }
    private OutputListenerViewModel selectedPort;
    public OutputListenerViewModel SelectedPort {
      get { return selectedPort; }
      set { 
        if (SetProperty("SelectedPort", ref selectedPort, value)) {
          RemovePortCommand.OnCanExecuteChanged();
        }
      }
    }

    public Command AddPortCommand { get; private set; }
    public Command RemovePortCommand { get; private set; }

    private ObservableCollection<YellowPageClientViewModel> yellowPages =
      new ObservableCollection<YellowPageClientViewModel>();
    public IEnumerable<YellowPageClientViewModel> YellowPages {
      get { return yellowPages; }
    }

    private bool portMapperEnabled;
    public bool PortMapperEnabled {
      get { return portMapperEnabled; }
      set { SetProperty("PortMapperEnabled", ref portMapperEnabled, value); }
    }

    public string PortMapperExternalAddresses { 
      get {
        var port_mapper = pecaApp.Plugins.GetPlugin<PeerCastStation.UI.PortMapperPlugin>();
        if (port_mapper!=null) {
          return String.Join(",", port_mapper.GetExternalAddresses().Select(addr => addr.ToString()));
        }
        else {
          return "";
        }
      }
    }

    private System.Net.IPAddress[] EnumGlobalAddressesV6()
    {
      return System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
        .Where(intf => !intf.IsReceiveOnly)
        .Where(intf => intf.OperationalStatus==System.Net.NetworkInformation.OperationalStatus.Up)
        .Where(intf => intf.NetworkInterfaceType!=System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
        .Select(intf => intf.GetIPProperties())
        .SelectMany(prop => prop.UnicastAddresses)
        .Where(uaddr => uaddr.Address.AddressFamily==System.Net.Sockets.AddressFamily.InterNetworkV6)
        .Where(uaddr => !uaddr.Address.IsSiteLocal())
        .Select(uaddr => uaddr.Address)
        .ToArray();
    }

    public string ExternalAddressesV6 {
      get {
        var listeners = ports
          .Where(p =>
            p.GlobalAccepts!=OutputStreamType.None &&
            p.EndPoint.AddressFamily==System.Net.Sockets.AddressFamily.InterNetworkV6);
        var addresses = listeners
          .Select(p => p.EndPoint.Address)
          .Where(addr =>
            !addr.Equals(System.Net.IPAddress.IPv6Loopback) &&
            !addr.Equals(System.Net.IPAddress.IPv6Any) &&
            !addr.Equals(System.Net.IPAddress.IPv6None) &&
            !addr.IsIPv6Teredo &&
            !addr.IsIPv6LinkLocal &&
            !addr.IsIPv6SiteLocal);
        if (listeners.Any(p => p.EndPoint.Address.Equals(System.Net.IPAddress.IPv6Any))) {
          addresses = addresses.Concat(EnumGlobalAddressesV6());
        }
        return String.Join(", ", addresses.Distinct().Select(addr => addr.ToString()));
      }
    }

    private YellowPageClientViewModel selectedYellowPage;
    public YellowPageClientViewModel SelectedYellowPage {
      get { return selectedYellowPage; }
      set {
        if (SetProperty("SelectedYellowPage", ref selectedYellowPage, value)) {
          RemoveYellowPageCommand.OnCanExecuteChanged();
        }
      }
    }

    public Command AddYellowPageCommand { get; private set; }
    public Command RemoveYellowPageCommand { get; private set; }

    public class ChannelCleanupModeItem {
      public string Name { get; set; }
      public PeerCastStation.ChannelCleaner.CleanupMode Mode { get; set; }
    }
    private static ChannelCleanupModeItem[] channelCleanupModeItems = new ChannelCleanupModeItem[] {
      new ChannelCleanupModeItem { Name="自動切断しない", Mode=ChannelCleaner.CleanupMode.None },
      new ChannelCleanupModeItem { Name="接続していない", Mode=ChannelCleaner.CleanupMode.Disconnected },
      new ChannelCleanupModeItem { Name="視聴・リレーをしていない", Mode=ChannelCleaner.CleanupMode.NotRelaying },
      new ChannelCleanupModeItem { Name="視聴をしていない", Mode=ChannelCleaner.CleanupMode.NotPlaying },
    };
    public IEnumerable<ChannelCleanupModeItem> ChannelCleanupModeItems {
      get { return channelCleanupModeItems; }
    }

    private PeerCastStation.ChannelCleaner.CleanupMode channelCleanupMode;
    public PeerCastStation.ChannelCleaner.CleanupMode ChannelCleanupMode {
      get { return channelCleanupMode; }
      set { SetProperty("ChannelCleanupMode", ref channelCleanupMode, value); }
    }

    public int channelCleanupInactiveLimit;
    public int ChannelCleanupInactiveLimit {
      get { return channelCleanupInactiveLimit; }
      set { SetProperty("ChannelCleanupInactiveLimit", ref channelCleanupInactiveLimit, value); }
    }

    private int maxRelays;
    public int MaxRelays {
      get { return maxRelays; }
      set { SetProperty("MaxRelays", ref maxRelays, value); }
    }

    private int maxRelaysPerBroadcastChannel;
    public int MaxRelaysPerBroadcastChannel {
      get { return maxRelaysPerBroadcastChannel; }
      set { SetProperty(nameof(MaxRelaysPerBroadcastChannel), ref maxRelaysPerBroadcastChannel, value); }
    }

    private int maxRelaysPerRelayChannel;
    public int MaxRelaysPerRelayChannel {
      get { return maxRelaysPerRelayChannel; }
      set { SetProperty(nameof(MaxRelaysPerRelayChannel), ref maxRelaysPerRelayChannel, value); }
    }

    private int maxPlays;
    public int MaxPlays {
      get { return maxPlays; }
      set { SetProperty("MaxPlays", ref maxPlays, value); }
    }

    private int maxPlaysPerBroadcastChannel;
    public int MaxPlaysPerBroadcastChannel {
      get { return maxPlaysPerBroadcastChannel; }
      set { SetProperty(nameof(MaxPlaysPerBroadcastChannel), ref maxPlaysPerBroadcastChannel, value); }
    }

    private int maxPlaysPerRelayChannel;
    public int MaxPlaysPerRelayChannel {
      get { return maxPlaysPerRelayChannel; }
      set { SetProperty(nameof(MaxPlaysPerRelayChannel), ref maxPlaysPerRelayChannel, value); }
    }

    private int maxUpstreamRate;
    public int MaxUpstreamRate {
      get { return maxUpstreamRate; }
      set { SetProperty("MaxUpstreamRate", ref maxUpstreamRate, value); }
    }

    private int maxUpstreamRateIPv6;
    public int MaxUpstreamRateIPv6 {
      get { return maxUpstreamRateIPv6; }
      set { SetProperty(nameof(MaxUpstreamRateIPv6), ref maxUpstreamRateIPv6, value); }
    }

    private int maxUpstreamRatePerBroadcastChannel;
    public int MaxUpstreamRatePerBroadcastChannel {
      get { return maxUpstreamRatePerBroadcastChannel; }
      set { SetProperty(nameof(MaxUpstreamRatePerBroadcastChannel), ref maxUpstreamRatePerBroadcastChannel, value); }
    }

    private int maxUpstreamRatePerRelayChannel;
    public int MaxUpstreamRatePerRelayChannel {
      get { return maxUpstreamRatePerRelayChannel; }
      set { SetProperty(nameof(MaxUpstreamRatePerRelayChannel), ref maxUpstreamRatePerRelayChannel, value); }
    }

    private bool isShowWindowOnStartup;
    public bool IsShowWindowOnStartup
    {
      get { return isShowWindowOnStartup; }
      set { SetProperty("IsShowWindowOnStartup", ref isShowWindowOnStartup, value); }
    }

    private bool isShowNotifications;
    public bool IsShowNotifications {
      get { return isShowNotifications; }
      set { SetProperty("IsShowNotifications", ref isShowNotifications, value); }
    }

    private static readonly NamedValueList<RemoteNodeName> remoteNodeNameItems =
      new NamedValueList<RemoteNodeName> {
        { "セッションID", RemoteNodeName.SessionID },
        { "アドレス", RemoteNodeName.Uri },
      };
    public IEnumerable<NamedValue<RemoteNodeName>> RemoteNodeNameItems {
      get { return remoteNodeNameItems; }
    }

    private RemoteNodeName remoteNodeName;
    public RemoteNodeName RemoteNodeName {
      get { return remoteNodeName; }
      set { SetProperty(nameof(RemoteNodeName), ref remoteNodeName, value); }
    }

    private static readonly Dictionary<string, NamedValueList<PlayProtocol>> playProtocols =
      new Dictionary<string, NamedValueList<PlayProtocol>> {
        {
          "FLV",
          new NamedValueList<PlayProtocol> {
            { "既定", PlayProtocol.Unknown },
            { "HTTP", PlayProtocol.HTTP },
            { "RTMP", PlayProtocol.RTMP },
            { "HTTP Live Streaming", PlayProtocol.HLS },
          }
        },
      };

    public IDictionary<string, NamedValueList<PlayProtocol>> PlayProtocols {
      get { return playProtocols; }
    }

    public ObservableDictionary<string, PlayProtocol> DefaultPlayProtocols { get; private set; }

    PeerCastApplication pecaApp;
    internal SettingViewModel(PeerCastApplication peca_app)
    {
      this.pecaApp = peca_app;
      this.peerCast = peca_app.PeerCast;
      this.AddPortCommand = new Command(() => AddPort(7144, NetworkType.IPv4));
      this.RemovePortCommand = new Command(() => RemovePort(), () => SelectedPort!=null);
      this.AddYellowPageCommand = new Command(() => AddYellowPage());
      this.RemoveYellowPageCommand = new Command(() => RemoveYellowPage(), () => SelectedYellowPage!=null);
      channelCleanupMode = ChannelCleaner.Mode;
      channelCleanupInactiveLimit = ChannelCleaner.InactiveLimit/60000;
      maxRelays                    = peerCast.AccessController.MaxRelays;
      maxRelaysPerBroadcastChannel = peerCast.AccessController.MaxRelaysPerBroadcastChannel;
      maxRelaysPerRelayChannel     = peerCast.AccessController.MaxRelaysPerRelayChannel;
      maxPlays                     = peerCast.AccessController.MaxPlays;
      maxPlaysPerBroadcastChannel  = peerCast.AccessController.MaxPlaysPerBroadcastChannel;
      maxPlaysPerRelayChannel      = peerCast.AccessController.MaxPlaysPerRelayChannel;
      maxUpstreamRate                    = peerCast.AccessController.MaxUpstreamRate;
      maxUpstreamRateIPv6                = peerCast.AccessController.MaxUpstreamRateIPv6;
      maxUpstreamRatePerBroadcastChannel = peerCast.AccessController.MaxUpstreamRatePerBroadcastChannel;
      maxUpstreamRatePerRelayChannel     = peerCast.AccessController.MaxUpstreamRatePerRelayChannel;
      isShowWindowOnStartup = pecaApp.Settings.Get<WPFSettings>().ShowWindowOnStartup;
      isShowNotifications   = pecaApp.Settings.Get<WPFSettings>().ShowNotifications;
      remoteNodeName        = pecaApp.Settings.Get<WPFSettings>().RemoteNodeName;
      ports = new ObservableCollection<OutputListenerViewModel>(
        peerCast.OutputListeners
        .Select(listener => new OutputListenerViewModel(this, listener))
      );
      ports.CollectionChanged += (sender, args) => {
        OnPropertyChanged(nameof(ExternalAddressesV6));
        OnPropertyChanged(nameof(IPv6Enabled));
      };
      yellowPages = new ObservableCollection<YellowPageClientViewModel>(
        peerCast.YellowPages
        .Select(yp => new YellowPageClientViewModel(this, yp))
      );
      var port_mapper = pecaApp.Plugins.GetPlugin<PeerCastStation.UI.PortMapperPlugin>();
      if (port_mapper!=null) {
        portMapperEnabled = port_mapper.Enabled;
        port_mapper.DiscoverAsync()
          .ContinueWith(prev => OnPropertyChanged("PortMapperExternalAddresses"));
      }
      PortCheckStatus = PortCheckStatus.Checking;
      PortCheckV6Status = PortCheckStatus.Checking;
      CheckPortAsync().ContinueWith(prev => {
        if (prev.IsCanceled || prev.IsFaulted) {
          PortCheckStatus = PortCheckStatus.Failed;
          PortCheckV6Status = PortCheckStatus.Failed;
        }
        else {
          PortCheckStatus = prev.Result.ResultV4;
          PortCheckV6Status = prev.Result.ResultV6;
        }
      });
      DefaultPlayProtocols = new ObservableDictionary<string, PlayProtocol>(new Dictionary<string, PlayProtocol>(pecaApp.Settings.Get<UISettings>().DefaultPlayProtocols).WithDefaultValue());
      DefaultPlayProtocols.ItemChanged += (sender, args) => {
        OnPropertyChanged(nameof(DefaultPlayProtocols));
      };
    }

    private class PortCheckResult {
      public PortCheckStatus ResultV4 { get; set; }
      public PortCheckStatus ResultV6 { get; set; }

      public static readonly PortCheckResult Failed = new PortCheckResult {
        ResultV4 = PortCheckStatus.Failed, 
        ResultV6 = PortCheckStatus.Failed,
      };
    }

    private async Task<PortCheckResult> CheckPortAsync()
    {
      var port_checker = pecaApp.Plugins.GetPlugin<PeerCastStation.UI.PCPPortCheckerPlugin>();
      if (port_checker==null) return PortCheckResult.Failed;
      var results = await port_checker.CheckAsync();
      foreach (var result in results) {
        if (!result.Success) continue;
        foreach (var port in ports) {
          if (!port.EndPoint.Address.Equals(result.LocalAddress)) continue;
          port.IsOpen = result.Ports.Contains(port.Port);
        }
      }
      var r = new PortCheckResult();
      var resultsv4 = 
        results.Where(result => result.LocalAddress.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork);
      if (resultsv4.All(result => !result.Success)) {
        r.ResultV4 = PortCheckStatus.Failed;
      }
      else if (ports.Where(port => port.EndPoint.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork).Any(port => port.IsOpen.HasValue && port.IsOpen.Value)) {
        r.ResultV4 = PortCheckStatus.Opened;
      }
      else {
        r.ResultV4 = PortCheckStatus.Closed;
      }
      var resultsv6 = 
        results.Where(result => result.LocalAddress.AddressFamily==System.Net.Sockets.AddressFamily.InterNetworkV6);
      if (resultsv6.All(result => !result.Success)) {
        r.ResultV6 = PortCheckStatus.Failed;
      }
      else if (ports.Where(port => port.EndPoint.AddressFamily==System.Net.Sockets.AddressFamily.InterNetworkV6).Any(port => port.IsOpen.HasValue && port.IsOpen.Value)) {
        r.ResultV6 = PortCheckStatus.Opened;
      }
      else {
        r.ResultV6 = PortCheckStatus.Closed;
      }
      return r;
    }

    public void AddPort(int port, NetworkType network)
    {
      var new_port = port;
      try {
        new_port =
          ports
            .Where(prt => prt.NetworkType==network)
            .Max(prt => prt.Port)+1;
      }
      catch (InvalidOperationException) {
      }
      switch (network) {
      case NetworkType.IPv4:
        ports.Add(new OutputListenerViewModel(this, new System.Net.IPEndPoint(System.Net.IPAddress.Any, new_port)));
        break;
      case NetworkType.IPv6:
        ports.Add(new OutputListenerViewModel(this, new System.Net.IPEndPoint(System.Net.IPAddress.IPv6Any, new_port)));
        break;
      }
      IsListenersModified = true;
    }

    public void RemovePort()
    {
      if (SelectedPort==null) return;
      ports.Remove(SelectedPort);
      IsListenersModified = true;
    }

    public void AddYellowPage()
    {
      yellowPages.Add(new YellowPageClientViewModel(this));
      IsYellowPagesModified = true;
    }

    public void RemoveYellowPage()
    {
      if (SelectedYellowPage==null) return;
      yellowPages.Remove(SelectedYellowPage);
      IsYellowPagesModified = true;
    }

    private PortCheckStatus portCheckStatus;
    public PortCheckStatus PortCheckStatus {
      get { return portCheckStatus; }
      set { SetProperty("PortCheckStatus", ref portCheckStatus, value); }
    }

    private PortCheckStatus portCheckV6Status;
    public PortCheckStatus PortCheckV6Status {
      get { return portCheckV6Status; }
      set { SetProperty(nameof(PortCheckV6Status), ref portCheckV6Status, value); }
    }

    public void CheckPort()
    {
      PortCheckStatus = PortCheckStatus.Checking;
      CheckPortAsync().ContinueWith(prev => {
        if (prev.IsCanceled || prev.IsFaulted) {
          PortCheckStatus = PortCheckStatus.Failed;
          PortCheckV6Status = PortCheckStatus.Failed;
        }
        else {
          PortCheckStatus = prev.Result.ResultV4;
          PortCheckV6Status = prev.Result.ResultV6;
        }
      });
    }

    protected override void OnPropertyChanged(string propertyName)
    {
      switch (propertyName) {
      case "SelectedPort":
      case "SelectedYellowPage":
      case "IsModified":
      case "IsListenersModified":
      case "IsYellowPagesModified":
      case "PortCheckStatus":
      case nameof(PortCheckV6Status):
      case "PortMapperExternalAddresses":
      case nameof(ExternalAddressesV6):
        break;
      default:
        IsModified = true;
        break;
      }
      base.OnPropertyChanged(propertyName);
    }

    public void Apply()
    {
      if (!IsModified) return;
      IsModified = false;

      ChannelCleaner.Mode = channelCleanupMode;
      ChannelCleaner.InactiveLimit = channelCleanupInactiveLimit*60000;
      peerCast.AccessController.MaxRelays = maxRelays;
      peerCast.AccessController.MaxRelaysPerBroadcastChannel = maxRelaysPerBroadcastChannel;
      peerCast.AccessController.MaxRelaysPerRelayChannel     = maxRelaysPerRelayChannel;
      peerCast.AccessController.MaxPlays = maxPlays;
      peerCast.AccessController.MaxPlaysPerBroadcastChannel = maxPlaysPerBroadcastChannel;
      peerCast.AccessController.MaxPlaysPerRelayChannel     = maxPlaysPerRelayChannel;
      peerCast.AccessController.MaxUpstreamRate = maxUpstreamRate;
      peerCast.AccessController.MaxUpstreamRateIPv6 = maxUpstreamRateIPv6;
      peerCast.AccessController.MaxUpstreamRatePerBroadcastChannel = maxUpstreamRatePerBroadcastChannel;
      peerCast.AccessController.MaxUpstreamRatePerRelayChannel     = maxUpstreamRatePerRelayChannel;
      pecaApp.Settings.Get<WPFSettings>().ShowWindowOnStartup = isShowWindowOnStartup;
      pecaApp.Settings.Get<WPFSettings>().ShowNotifications = isShowNotifications;
      pecaApp.Settings.Get<WPFSettings>().RemoteNodeName = remoteNodeName;
      if (IsListenersModified) {
        foreach (var listener in peerCast.OutputListeners.ToArray()) {
          peerCast.StopListen(listener);
        }
        foreach (var listener in ports) {
          var newlistener = peerCast.StartListen(listener.EndPoint, listener.LocalAccepts, listener.GlobalAccepts);
          newlistener.GlobalAuthorizationRequired = listener.GlobalAuthRequired;
          newlistener.LocalAuthorizationRequired = listener.LocalAuthRequired;
          newlistener.AuthenticationKey = listener.AuthenticationKey;
        }
        isListenersModified = false;
      }
      if (IsYellowPagesModified) {
        foreach (var yp in peerCast.YellowPages.ToArray()) {
          peerCast.RemoveYellowPage(yp);
        }
        foreach (var yp in yellowPages) {
          if (String.IsNullOrEmpty(yp.Name)) continue;
          if (String.IsNullOrEmpty(yp.AnnounceUri) && String.IsNullOrEmpty(yp.ChannelsUri)) continue;
          Uri announce_uri = String.IsNullOrEmpty(yp.AnnounceUri) ? null : new Uri(yp.AnnounceUri, UriKind.Absolute);
          Uri channels_uri = String.IsNullOrEmpty(yp.ChannelsUri) ? null : new Uri(yp.ChannelsUri, UriKind.Absolute);
          peerCast.AddYellowPage(yp.Protocol.Protocol, yp.Name, announce_uri, channels_uri);
        }
        isYellowPagesModified = false;
      }
      var port_mapper = pecaApp.Plugins.GetPlugin<PortMapperPlugin>();
      if (port_mapper!=null) {
        port_mapper.Enabled = portMapperEnabled;
        port_mapper.DiscoverAsync()
          .ContinueWith(prev => OnPropertyChanged("PortMapperExternalAddresses"));
      }
      PortCheckStatus = PortCheckStatus.Checking;
      PortCheckV6Status = PortCheckStatus.Checking;
      CheckPortAsync().ContinueWith(prev => {
        if (prev.IsCanceled || prev.IsFaulted) {
          PortCheckStatus = PortCheckStatus.Failed;
          PortCheckV6Status = PortCheckStatus.Failed;
        }
        else {
          PortCheckStatus = prev.Result.ResultV4;
          PortCheckV6Status = prev.Result.ResultV6;
          peerCast.SetPortStatus(System.Net.Sockets.AddressFamily.InterNetwork, prev.Result.ResultV4==PortCheckStatus.Opened ? PortStatus.Open : PortStatus.Firewalled);
          peerCast.SetPortStatus(System.Net.Sockets.AddressFamily.InterNetworkV6, prev.Result.ResultV6==PortCheckStatus.Opened ? PortStatus.Open : PortStatus.Firewalled);
        }
      });
      pecaApp.Settings.Get<UISettings>().DefaultPlayProtocols = new Dictionary<string, PlayProtocol>(DefaultPlayProtocols);
      pecaApp.SaveSettings();
    }

  }

}
