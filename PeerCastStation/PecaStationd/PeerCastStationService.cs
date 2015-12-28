using System.ServiceProcess;

namespace PecaStationd
{
  public partial class PeerCastStationService : ServiceBase
  {
    public class PecaServiceApplication : PeerCastStation.Main.Application
    {
    }

    public PeerCastStationService()
    {
      InitializeComponent();
    }

    private PecaServiceApplication app = new PecaServiceApplication();
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
