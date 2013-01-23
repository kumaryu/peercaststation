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

  public sealed class Command<T> : ICommand
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
