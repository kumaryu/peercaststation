using System.ServiceProcess;

namespace PecaStationd
{
  public partial class PeerCastStationService : ServiceBase
  {
    public PeerCastStationService()
    {
      InitializeComponent();
    }

    private PeerCastStation.App.ServiceApp app = new PeerCastStation.App.ServiceApp();
    private System.Threading.Tasks.Task appTask;
    protected override void OnStart(string[] args)
    {
      appTask = app.Start();
    }

    protected override void OnStop()
    {
      app.Stop();
      if (appTask!=null) {
        appTask.Wait();
      }
    }
  }
}
