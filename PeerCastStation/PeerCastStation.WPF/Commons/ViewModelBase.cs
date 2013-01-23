using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace PeerCastStation.WPF.Commons
{
  internal class ViewModelBase : INotifyPropertyChanged
  {
    #region INotifyPropertyChanged メンバー

    public event PropertyChangedEventHandler PropertyChanged;

    #endregion

    protected void OnPropertyChanged(string propertyName)
    {
      if (PropertyChanged == null)
        return;

      PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(string propertyName, ref T obj, T value)
    {
      if (obj != null && obj.Equals(value))
        return false;
      obj = value;
      OnPropertyChanged(propertyName);
      return true;
    }
  }
}
