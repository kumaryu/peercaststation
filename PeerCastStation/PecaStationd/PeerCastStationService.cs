using System;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace PecaStationd
{
  public partial class PeerCastStationService : ServiceBase
  {
    public PeerCastStationService()
    {
      InitializeComponent();
    }
    private PeerCastStationServiceMain main = new PeerCastStationServiceMain();

    protected override void OnStart(string[] args)
    {
      main.Start(args);
    }

    protected override void OnStop()
    {
      main.Stop();
    }
  }
}
