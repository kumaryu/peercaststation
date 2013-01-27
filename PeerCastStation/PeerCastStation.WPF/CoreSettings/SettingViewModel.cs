using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using PeerCastStation.Core;
using PeerCastStation.WPF.Commons;

namespace PeerCastStation.WPF.CoreSettings
{
  class SettingViewModel : ViewModelBase
  {
    private readonly PeerCast peerCast;

    private readonly ListViewModel<PortListItem> ports
      = new ListViewModel<PortListItem>();
    public ListViewModel<PortListItem> Ports
    {
      get
      {
        ports.Items = peerCast.OutputListeners
          .Select(listener => new PortListItem(listener));
        return ports;
      }
    }
    public OutputListener SelectedListener
    {
      get { return ports.SelectedItem.Listener; }
    }

    public bool? IsLocalRelay
    {
      get
      {
        return SelectedListener.GetFromLocalOutputAccepts(OutputStreamType.Relay);
      }
      set
      {
        SelectedListener.SetToLocalOutputAccepts(OutputStreamType.Relay, value);
        OnPropertyChanged("IsLocalRelay");
      }
    }

    public bool? IsLocalDirect
    {
      get
      {
        return SelectedListener.GetFromLocalOutputAccepts(OutputStreamType.Play);
      }
      set
      {
        SelectedListener.SetToLocalOutputAccepts(OutputStreamType.Play, value);
        OnPropertyChanged("IsLocalDirect");
      }
    }

    public bool? IsLocalInterface
    {
      get
      {
        return SelectedListener.GetFromLocalOutputAccepts(OutputStreamType.Interface);
      }
      set
      {
        SelectedListener.SetToLocalOutputAccepts(OutputStreamType.Interface, value);
        OnPropertyChanged("IsLocalInterface");
      }
    }

    public bool? IsGlobalRelay
    {
      get
      {
        return SelectedListener.GetFromGlobalOutputAccepts(OutputStreamType.Relay);
      }
      set
      {
        SelectedListener.SetToGlobalOutputAccepts(OutputStreamType.Relay, value);
        OnPropertyChanged("IsGlobalRelay");
      }
    }

    public bool? IsGlobalDirect
    {
      get
      {
        return SelectedListener.GetFromGlobalOutputAccepts(OutputStreamType.Play);
      }
      set
      {
        SelectedListener.SetToGlobalOutputAccepts(OutputStreamType.Play, value);
        OnPropertyChanged("IsGlobalDirect");
      }
    }

    public bool? IsGlobalInterface
    {
      get
      {
        return SelectedListener.GetFromGlobalOutputAccepts(OutputStreamType.Interface);
      }
      set
      {
        SelectedListener.SetToGlobalOutputAccepts(OutputStreamType.Interface, value);
        OnPropertyChanged("IsGlobalInterface");
      }
    }

    private readonly OtherSettingViewModel otherSetting;
    public OtherSettingViewModel OtherSetting
    {
      get { return otherSetting; }
    }

    private readonly ObservableCollection<YellowPageItem> yellowPagesList;
    public ObservableCollection<YellowPageItem> YellowPagesList
    {
      get { return yellowPagesList; }
    }

    public SettingViewModel(PeerCast peerCast)
    {
      this.peerCast = peerCast;

      ports.SelectedItemChanged += (sender, e) =>
      {
        OnPropertyChanged("IsPortSelected");
        OnPropertyChanged("IsLocalRelay");
        OnPropertyChanged("IsLocalDirect");
        OnPropertyChanged("IsLocalInterface");
        OnPropertyChanged("IsGlobalRelay");
        OnPropertyChanged("IsGlobalDirect");
        OnPropertyChanged("IsGlobalInterface");
      };
      ports.ItemRemoving += (sender, e) =>
      {
        peerCast.StopListen(e.Item.Listener);
        OnPropertyChanged("Ports");
      };

      otherSetting = new OtherSettingViewModel(peerCast.AccessController);

      yellowPagesList = new ObservableCollection<YellowPageItem>(
        peerCast.YellowPages.Select(yp => new YellowPageItem(yp)));
    }
  }
}
