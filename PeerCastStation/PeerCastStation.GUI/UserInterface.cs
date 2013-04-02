using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using PeerCastStation.Core;

namespace PeerCastStation.GUI
{
  [Plugin]
  public class UserInterface
    : IPlugin
  {
    public string Name { get { return "GUI by Windows.Forms"; } }
    public bool IsUsable { get { return true; } }

    MainForm mainForm;
    Thread mainThread;
    public void Start(PeerCastApplication app)
    {
      System.Windows.Forms.Application.EnableVisualStyles();
      mainThread = new Thread(() => {
        mainForm = new MainForm(app);
        System.Windows.Forms.Application.ApplicationExit += (sender, args) => {
          app.Stop();
        };
        System.Windows.Forms.Application.Run();
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
}
