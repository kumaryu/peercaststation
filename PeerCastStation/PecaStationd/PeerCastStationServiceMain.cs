using System;
using System.Threading;
using System.Threading.Tasks;
using PeerCastStation.App;

namespace PecaStationd
{
  public class PeerCastStationServiceMain
  {
    private record Context(ServiceApp Application, Task<int> ApplicationTask);
    private Context? context;

    public void Start(string[] args)
    {
      var basepath = ServiceApp.GetDefaultBasePath();
      var application = new ServiceApp(basepath, args);
      var appTask = application.Start();

      var old_context = Interlocked.Exchange(ref context, new Context(application, appTask));
      if (old_context != null) {
        Stop(old_context);
      }
    }

    private int Stop(Context ctx)
    {
      if (ctx==null) return -2;
      ctx.Application.Stop();
      ctx.ApplicationTask.Wait();
      return ctx.ApplicationTask.Result;
    }

    public int Stop()
    {
      var ctx = Interlocked.Exchange(ref context, null);
      if (ctx!=null) {
        return Stop(ctx);
      }
      else {
        return -2;
      }
    }
  }

}
