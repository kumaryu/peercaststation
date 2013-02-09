using System;
using System.Threading;
using System.Windows.Threading;
using PeerCastStation.Core;
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

    MainViewModel viewModel;
    Thread notifyIconThread;
    NotifyIconManager notifyIconManager;
    Thread mainThread;
    WindowManager windowManager;
    public void Start(PeerCastApplication application)
    {
      DispatcherSynchronizationContext.SetSynchronizationContext(
          new DispatcherSynchronizationContext());

      var settings = Settings.Default;
      viewModel = new MainViewModel(
        application, settings.UpdateURL, settings.CurrentVersion);
      viewModel.NewVersionFound += (sender, e)
        => notifyIconManager.NewVersionInfo = e.VersionDescription;
      Load(settings, viewModel);
      notifyIconThread = new Thread(() =>
      {
        notifyIconManager = new NotifyIconManager(application.PeerCast);
        notifyIconManager.CheckVersionClicked += (sender, e)
          => viewModel.CheckVersion();
        notifyIconManager.QuitClicked += (sender, e) => application.Stop();
        notifyIconManager.ShowWindowClicked += (sender, e)
          => windowManager.ShowMainWindow();
        viewModel.CheckVersion();
        notifyIconManager.Run();
      });
      notifyIconThread.SetApartmentState(ApartmentState.STA);
      notifyIconThread.Start();

      mainThread = new Thread(() =>
      {
        windowManager = new WindowManager(viewModel);
        windowManager.Run(settings.ShowWindowOnStartup);
      });
      mainThread.SetApartmentState(ApartmentState.STA);
      mainThread.Start();
    }

    public void Stop()
    {
      Save(viewModel, Settings.Default);
      viewModel.Dispose();
      notifyIconManager.Dispose();
      windowManager.Dispose();
      notifyIconThread.Join();
      mainThread.Join();
    }

    private void Load(Settings settings, MainViewModel mainWindow)
    {
      var log = mainWindow.Log;
      log.LogLevel = settings.LogLevel;
      log.IsOutputToGui = settings.LogToGUI;
      log.IsOutputToConsole = settings.LogToConsole;
      log.IsOutputToFile = settings.LogToFile;
      log.OutputFileName = settings.LogFileName;
      mainWindow.Setting.OtherSetting.IsShowWindowOnStartup = settings.ShowWindowOnStartup;
    }

    private void Save(MainViewModel mainWindow, Settings settings)
    {
      var log = mainWindow.Log;
      settings.LogLevel = log.LogLevel;
      settings.LogToGUI = log.IsOutputToGui;
      settings.LogToConsole = log.IsOutputToConsole;
      settings.LogToFile = log.IsOutputToFile;
      settings.LogFileName = log.OutputFileName;
      settings.ShowWindowOnStartup = mainWindow.Setting.OtherSetting.IsShowWindowOnStartup;
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
