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
using System.ComponentModel;

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
      private Uri    uri;
      private IYellowPageClientFactory protocol;

      public string Name {
        get { return name; }
        set {
          if (name==value) return;
          name = value;
          OnPropertyChanged("Name");
        }
      }

      public string Uri {
        get { return uri==null ? null : uri.ToString(); }
        set {
          if (String.IsNullOrEmpty(value)) return;
          if (protocol==null) new ArgumentException("プロトコルが選択されていません");
          Uri newvalue;
          if (System.Uri.TryCreate(value, UriKind.Absolute, out newvalue) && newvalue.Scheme=="pcp") {
            if (uri==newvalue || (uri!=null && uri.Equals(newvalue))) return;
            uri = newvalue;
            OnPropertyChanged("Uri");
          }
          if (System.Uri.TryCreate(value, UriKind.Absolute, out newvalue) &&
              (newvalue.Scheme=="http" || newvalue.Scheme=="file")) {
            throw new ArgumentException("指定したプロトコルでは使用できないURLです");
          }
          else if (System.Uri.TryCreate("pcp://"+value, UriKind.Absolute, out newvalue)) {
            if (uri==newvalue || (uri!=null && uri.Equals(newvalue))) return;
            uri = newvalue;
            OnPropertyChanged("Uri");
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
        this.owner = owner;
        this.name     = model.Name;
        this.uri      = model.Uri;
        this.protocol = owner.peerCast.YellowPageFactories.FirstOrDefault(factory => factory.Protocol==model.Protocol);
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

    internal class CheckBandwidthCommand
      : System.Windows.Input.ICommand,
        System.ComponentModel.INotifyPropertyChanged
    {
      private SettingViewModel owner;
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

      public CheckBandwidthCommand(SettingViewModel owner)
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
        checker.RunAsync();
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
        var listener = ports.FirstOrDefault();
        if (listener==null) {
          AddPort();
          return PrimaryPort;
        }
        else {
          return listener.Port;
        }
      }
      set {
        var listener = ports.FirstOrDefault();
        if (listener==null) {
          AddPort();
          PrimaryPort = value;
        }
        else if ( listener.Port!=value) {
          listener.Port = value;
          OnPropertyChanged("PrimaryPort");
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
        var port_mapper = pecaApp.Plugins.GetPlugin<PeerCastStation.UI.PortMapper>();
        if (port_mapper!=null) {
          return String.Join(",", port_mapper.GetExternalAddresses().Select(addr => addr.ToString()));
        }
        else {
          return "";
        }
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

    private int maxRelaysPerChannel;
    public int MaxRelaysPerChannel {
      get { return maxRelaysPerChannel; }
      set { SetProperty("MaxRelaysPerChannel", ref maxRelaysPerChannel, value); }
    }

    private int maxPlays;
    public int MaxPlays {
      get { return maxPlays; }
      set { SetProperty("MaxPlays", ref maxPlays, value); }
    }

    private int maxPlaysPerChannel;
    public int MaxPlaysPerChannel {
      get { return maxPlaysPerChannel; }
      set { SetProperty("MaxPlaysPerChannel", ref maxPlaysPerChannel, value); }
    }

    private int maxUpstreamRate;
    public int MaxUpstreamRate {
      get { return maxUpstreamRate; }
      set { SetProperty("MaxUpstreamRate", ref maxUpstreamRate, value); }
    }

    private int maxUpstreamRatePerChannel;
    public int MaxUpstreamRatePerChannel {
      get { return maxUpstreamRatePerChannel; }
      set { SetProperty("MaxUpstreamRatePerChannel", ref maxUpstreamRatePerChannel, value); }
    }

    private bool isShowWindowOnStartup;
    public bool IsShowWindowOnStartup
    {
      get { return isShowWindowOnStartup; }
      set { SetProperty("IsShowWindowOnStartup", ref isShowWindowOnStartup, value); }
    }

    public System.Windows.Input.ICommand CheckBandwidth { get; private set; }

    PeerCastApplication pecaApp;
    internal SettingViewModel(PeerCastApplication peca_app)
    {
      this.pecaApp = peca_app;
      this.peerCast = peca_app.PeerCast;
      this.AddPortCommand = new Command(() => AddPort());
      this.RemovePortCommand = new Command(() => RemovePort(), () => SelectedPort!=null);
      this.AddYellowPageCommand = new Command(() => AddYellowPage());
      this.RemoveYellowPageCommand = new Command(() => RemoveYellowPage(), () => SelectedYellowPage!=null);
      this.CheckBandwidth = new CheckBandwidthCommand(this);
      channelCleanupMode = ChannelCleaner.Mode;
      channelCleanupInactiveLimit = ChannelCleaner.InactiveLimit/60000;
      maxRelays           = peerCast.AccessController.MaxRelays;
      maxRelaysPerChannel = peerCast.AccessController.MaxRelaysPerChannel;
      maxPlays            = peerCast.AccessController.MaxPlays;
      maxPlaysPerChannel  = peerCast.AccessController.MaxPlaysPerChannel;
      maxUpstreamRate           = peerCast.AccessController.MaxUpstreamRate;
      maxUpstreamRatePerChannel = peerCast.AccessController.MaxUpstreamRatePerChannel;
      isShowWindowOnStartup = pecaApp.Settings.Get<WPFSettings>().ShowWindowOnStartup;
      ports = new ObservableCollection<OutputListenerViewModel>(
        peerCast.OutputListeners
        .Select(listener => new OutputListenerViewModel(this, listener))
      );
      yellowPages = new ObservableCollection<YellowPageClientViewModel>(
        peerCast.YellowPages
        .Select(yp => new YellowPageClientViewModel(this, yp))
      );
      var port_mapper = pecaApp.Plugins.GetPlugin<PeerCastStation.UI.PortMapper>();
      if (port_mapper!=null) portMapperEnabled = port_mapper.Enabled;
    }

    public void AddPort()
    {
      var new_port = 7144;
      try {
        new_port = ports.Max(port => port.Port)+1;
      }
      catch (InvalidOperationException) {
      }
      ports.Add(new OutputListenerViewModel(this, new_port));
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

    public void PortCheck()
    {
      var ports = peerCast.OutputListeners
        .Where( listener => (listener.GlobalOutputAccepts & OutputStreamType.Relay)!=0)
        .Select(listener => listener.LocalEndPoint.Port);
      Uri target_uri;
      if (!AppSettingsReader.TryGetUri("PCPPortChecker", out target_uri)) return;
      var checker = new PeerCastStation.UI.PCPPortChecker(peerCast.SessionID, target_uri, ports);
      checker.PortCheckCompleted += checker_PortCheckCompleted;
      checker.RunAsync();
      PortCheckStatus = PortCheckStatus.Checking;
    }

    void checker_PortCheckCompleted(object sender, UI.PortCheckCompletedEventArgs args)
    {
      if (args.Success) {
        var status = PortCheckStatus.Closed;
        foreach (var port in ports) {
          port.IsOpen = args.Ports.Contains(port.Port);
          if (port.IsOpen.HasValue && port.IsOpen.Value) {
            status = PortCheckStatus.Opened;
          }
        }
        PortCheckStatus = status;
      }
      else {
        PortCheckStatus = PortCheckStatus.Failed;
      }
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
      case "PortMapperExternalAddresses":
        break;
      default:
        IsModified = true;
        break;
      }
      base.OnPropertyChanged(propertyName);
    }

    public async void Apply()
    {
      if (!IsModified) return;
      IsModified = false;

      ChannelCleaner.Mode = channelCleanupMode;
      ChannelCleaner.InactiveLimit = channelCleanupInactiveLimit*60000;
      peerCast.AccessController.MaxRelays = maxRelays;
      peerCast.AccessController.MaxRelaysPerChannel = maxRelaysPerChannel;
      peerCast.AccessController.MaxPlays = maxPlays;
      peerCast.AccessController.MaxPlaysPerChannel = maxPlaysPerChannel;
      peerCast.AccessController.MaxUpstreamRate = maxUpstreamRate;
      peerCast.AccessController.MaxUpstreamRatePerChannel = maxUpstreamRatePerChannel;
      pecaApp.Settings.Get<WPFSettings>().ShowWindowOnStartup = isShowWindowOnStartup;
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
          if (String.IsNullOrEmpty(yp.Name) || yp.Uri==null) continue;
          peerCast.AddYellowPage(yp.Protocol.Protocol, yp.Name, new Uri(yp.Uri, UriKind.Absolute));
        }
        isYellowPagesModified = false;
      }
      var port_mapper = pecaApp.Plugins.GetPlugin<PeerCastStation.UI.PortMapper>();
      if (port_mapper!=null) port_mapper.Enabled = portMapperEnabled;
      pecaApp.SaveSettings();
      await System.Threading.Tasks.Task.Delay(200).ContinueWith(prev => {
        OnPropertyChanged("PortMapperExternalAddresses");
      });
    }

  }

}
