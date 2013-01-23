using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using PeerCastStation.Core;
using System.Windows;
using System.Windows.Threading;
using PeerCastStation.WPF.Properties;

namespace PeerCastStation.WPF
{
  public class UserInterface
    : MarshalByRefObject, IUserInterface
  {
    public string Name
    {
      get { return "PeerCastStation.WPF"; }
    }

    Window window;
    Thread mainThread;
    volatile bool isShow;
    public void Start(PeerCastApplication application)
    {
      mainThread = new Thread(() =>
      {
        DispatcherSynchronizationContext.SetSynchronizationContext(
            new DispatcherSynchronizationContext());
        window = new MainWindow();
        var viewModel = new MainWindowViewModel();
        Load(Settings.Default, viewModel);
        window.DataContext = viewModel;
        window.Closing += (sender, e) => Save(viewModel, Settings.Default);
        window.Closed += (sender, e) => application.Stop();
        isShow = true;
        window.ShowDialog();
        isShow = false;
      });
      mainThread.SetApartmentState(ApartmentState.STA);
      mainThread.Start();
    }

    public void Stop()
    {
      if (window != null && isShow)
      {
        window.Dispatcher.Invoke(new Action(() =>
        {
          if (isShow)
            window.Close();
        }));
      }
      mainThread.Join();
    }

    private void Load(Settings settings, MainWindowViewModel mainWindow)
    {
      var log = mainWindow.Log;
      log.LogLevel = settings.LogLevel;
      log.IsOutputToGui = settings.LogToGUI;
      log.IsOutputToConsole = settings.LogToConsole;
      log.IsOutputToFile = settings.LogToFile;
      log.OutputFileName = settings.LogFileName;
    }

    private void Save(MainWindowViewModel mainWindow, Settings settings)
    {
      var log = mainWindow.Log;
      settings.LogLevel = log.LogLevel;
      settings.LogToGUI = log.IsOutputToGui;
      settings.LogToConsole = log.IsOutputToConsole;
      settings.LogToFile = log.IsOutputToFile;
      settings.LogFileName = log.OutputFileName;
      settings.Save();
    }
  }

  [Plugin]
  public class UserInterfaceFactory
    : MarshalByRefObject,
      IUserInterfaceFactory
  {
    public string Name
    {
      get { return "PeerCastStation.WPF"; }
    }

    public IUserInterface CreateUserInterface()
    {
      return new UserInterface();
    }
  }
}
