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

    WindowManager windowManager = new WindowManager();
    Thread mainThread;
    public void Start(PeerCastApplication application)
    {
      mainThread = new Thread(() =>
      {
        DispatcherSynchronizationContext.SetSynchronizationContext(
            new DispatcherSynchronizationContext());
        windowManager.ShowMainWindow(application, Settings.Default);
      });
      mainThread.SetApartmentState(ApartmentState.STA);
      mainThread.Start();
    }

    public void Stop()
    {
      windowManager.Dispose();
      mainThread.Join();
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
