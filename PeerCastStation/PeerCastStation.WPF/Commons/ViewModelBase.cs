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

    protected virtual void OnPropertyChanged(string propertyName)
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

    protected bool SetProperty<T>(string propertyName, ref T obj, T value, Action onSuccess)
    {
      if (obj != null && obj.Equals(value))
        return false;
      obj = value;
      onSuccess();
      OnPropertyChanged(propertyName);
      return true;
    }
  }
}
