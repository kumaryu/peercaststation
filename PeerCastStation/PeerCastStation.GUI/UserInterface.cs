using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using PeerCastStation.Core;

namespace PeerCastStation.GUI
{
  public class UserInterface
    : MarshalByRefObject,
      IUserInterface
  {
    public string Name
    {
      get { return "PeerCastStation.GUI"; }
    }

    MainForm mainForm;
    Thread mainThread;
    public void Start(PeerCastApplication app)
    {
      System.Windows.Forms.Application.EnableVisualStyles();
      mainThread = new Thread(() => {
        mainForm = new MainForm(app.PeerCast);
        System.Windows.Forms.Application.ApplicationExit += (sender, args) => {
          app.Stop();
        };
        System.Windows.Forms.Application.Run(mainForm);
        mainForm = null;
      });
      mainThread.SetApartmentState(ApartmentState.STA);
      mainThread.Start();
    }

    public void Stop()
    {
      if (mainForm!=null && !mainForm.IsDisposed) {
        mainForm.Invoke(new Action(() => {
          if (!mainForm.IsDisposed) {
            System.Windows.Forms.Application.ExitThread();
          }
        }));
      }
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
      get { return "PeerCastStation.GUI"; }
    }

    public IUserInterface CreateUserInterface()
    {
      return new UserInterface();
    }
  }
}
