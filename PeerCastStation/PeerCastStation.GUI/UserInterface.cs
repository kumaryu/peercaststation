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
      });
      mainThread.SetApartmentState(ApartmentState.STA);
      mainThread.Start();
    }

    public void Stop()
    {
      System.Windows.Forms.Application.Exit();
      mainThread.Join();
    }
  }

  [Plugin(PluginType.UserInterface)]
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
