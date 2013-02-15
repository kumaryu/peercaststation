using System;
using System.ComponentModel;

namespace PeerCastStation.GUI
{
  public class GUISettings
    : INotifyPropertyChanged
  {
    private bool showWindowOnStartup = true;

    public bool ShowWindowOnStartup {
      get { return showWindowOnStartup; }
      set { showWindowOnStartup = value; OnPropertyChanged("ShowWindowOnStartup"); }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged(string name)
    {
      if (this.PropertyChanged==null) return;
      this.PropertyChanged(this, new PropertyChangedEventArgs(name));
    }
  }
}
