using System;
using System.Threading.Tasks;
using PeerCastStation.App;

namespace PecaStationd
{
  public class PeerCastStationServiceMain
  {
    private ServiceApp application;
    private Task<int> appTask;
    public void Start(string[] args)
    {
      var basepath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
      application = new ServiceApp(basepath);
      appTask = application.Start();
    }

    public int Stop()
    {
      if (application==null) return -2;
      application.Stop();
      appTask.Wait();
      return appTask.Result;
    }
  }

}
