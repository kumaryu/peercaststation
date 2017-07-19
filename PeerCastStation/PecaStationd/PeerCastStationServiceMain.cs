using System;
using System.Threading.Tasks;

namespace PecaStationd
{
  public class PeerCastStationServiceMain
  {
    class ResultContainer : MarshalByRefObject
    {
      public AppContext AppContext;
    }

    class AppContext : MarshalByRefObject
    {
      public object serviceApp;
      public Task<int> mainTask;
      public Action<int> onStoppedCallback;

      public AppContext(string basepath, string[] args)
      {
        var asm = System.Reflection.Assembly.LoadFrom(
          System.IO.Path.Combine(basepath, "PeerCastStation.App.dll"));
        var type = asm.GetType("PeerCastStation.App.ServiceApp");
        serviceApp = type.InvokeMember("ServiceApp",
          System.Reflection.BindingFlags.CreateInstance,
          null,
          null,
          new object[] { basepath });
        mainTask = (Task<int>)serviceApp.GetType().InvokeMember("Start",
          System.Reflection.BindingFlags.Public |
          System.Reflection.BindingFlags.Instance |
          System.Reflection.BindingFlags.InvokeMethod,
          null,
          serviceApp,
          new object[] { });
        mainTask = mainTask.ContinueWith(prev => {
          if (prev.IsCanceled || prev.IsFaulted) return 1;
          onStoppedCallback?.Invoke(prev.Result);
          return prev.Result;
        });
      }

      public void SetOnStopped(Action<int> callback)
      {
        onStoppedCallback = callback;
        if (onStoppedCallback!=null && mainTask.IsCompleted) {
          onStoppedCallback(mainTask.Result);
        }
      }

      public int Stop()
      {
        serviceApp.GetType().InvokeMember("Stop",
          System.Reflection.BindingFlags.Public |
          System.Reflection.BindingFlags.Instance |
          System.Reflection.BindingFlags.InvokeMethod,
          null,
          serviceApp,
          new object[] { });
        mainTask.Wait();
        return mainTask.Result;
      }
    }

    [Serializable]
    class StartUpContext
    {
      public string   BasePath;
      public string[] Args;
      public ResultContainer Result;

      public void Start()
      {
        Result.AppContext = new AppContext(this.BasePath, this.Args);
      }
    }

    private AppContext appContext;
    public int Result { get; private set; } = -1;

    private class StoppedCallback
      : MarshalByRefObject
    {
      public PeerCastStationServiceMain Owner;
      public string[] Args;
      public void OnStopped(int result)
      {
        if (result==-1) Owner.Start(Args);
        else            Owner.Result = result;
      }
    }

    public void Start(string[] args)
    {
      var basepath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
      var appdomain = AppDomain.CreateDomain(
        "PeerCastStaion.App",
        null,
        AppDomain.CurrentDomain.BaseDirectory,
        AppDomain.CurrentDomain.RelativeSearchPath,
        true);
      var ctx = new StartUpContext() {
        BasePath = basepath,
        Args     = args,
        Result   = new ResultContainer { AppContext = null },
      };
      appdomain.DoCallBack(new CrossAppDomainDelegate(ctx.Start));
      appContext = ctx.Result.AppContext;
      var callback = new StoppedCallback { Owner=this, Args=args, };
      appContext.SetOnStopped(new Action<int>(callback.OnStopped));
    }

    public int Stop()
    {
      return appContext.Stop();
    }
  }
}
