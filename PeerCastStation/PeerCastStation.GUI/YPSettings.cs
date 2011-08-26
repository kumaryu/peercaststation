using System;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Generic;

namespace PeerCastStation.GUI
{
  [Serializable]
  public class YPSettings
    : INotifyPropertyChanged
  {
    private string name     = "";
    private string address  = "";
    private string protocol = "PCP";
    private bool   enabled  = true;
    public string Name {
      get { return name; }
      set { if (name!=value) { name = value; OnPropertyChanged("Name"); } }
    }
    public string Address {
      get { return address; }
      set { if (address!=value) { address = value; OnPropertyChanged("Address"); } }
    }
    public string Protocol {
      get { return protocol; }
      set { if (protocol!=value) { protocol = value; OnPropertyChanged("Protocol"); } }
    }
    public bool Enabled {
      get { return enabled; }
      set { if (enabled!=value) { enabled = value; OnPropertyChanged("Enabled"); } }
    }
  
    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged(string name)
    {
      if (PropertyChanged!=null) PropertyChanged(this, new PropertyChangedEventArgs(name));
    }
  }

  [Serializable]
  public class YPSettingsCollection : Collection<YPSettings>
  {
    public YPSettingsCollection() : base() {}
    public YPSettingsCollection(IList<YPSettings> list) : base(list) {}
  }
}
