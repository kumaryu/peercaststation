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
using System.Windows.Input;

namespace PeerCastStation.WPF.Commons
{
  public sealed class Command : ICommand
  {
    private Action execute;
    private Func<bool> canExecute;

    public Command(Action execute)
      : this(execute, () => true)
    {
    }
    public Command(Action execute, Func<bool> canExecute)
    {
      this.execute = execute;
      this.canExecute = canExecute;
    }

    #region ICommand メンバー

    public event EventHandler CanExecuteChanged;

    public bool CanExecute(object parameter)
    {
      return canExecute();
    }
    public void Execute(object parameter)
    {
      execute();
    }

    #endregion

    public void OnCanExecuteChanged()
    {
      if (CanExecuteChanged != null)
        CanExecuteChanged(this, new EventArgs());
    }
  }

  public class Command<T> : ICommand
  {
    private Action<T> execute;
    private Func<T, bool> canExecute;

    public Command(Action<T> execute)
      : this(execute, o => true)
    {
    }

    public Command(Action<T> execute, Func<T, bool> canExecute)
    {
      this.execute = execute;
      this.canExecute = canExecute;
    }

    public event EventHandler CanExecuteChanged;

    #region ICommand

    bool ICommand.CanExecute(object parameter)
    {
      return canExecute((T)parameter);
    }

    void ICommand.Execute(object parameter)
    {
      execute((T)parameter);
    }

    #endregion

    public void OnCanExecuteChanged()
    {
      if (CanExecuteChanged != null)
        CanExecuteChanged(this, new EventArgs());
    }
  }
}
