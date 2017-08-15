using System;

namespace PeerCastStation.Main
{
  public class PeerCastStation
  {
    class ResultContainer : MarshalByRefObject
    {
      public int ExitCode;
    }

    [Serializable]
    class StartUpContext
    {
      public string   BasePath;
      public string[] Args;
      public ResultContainer Result;

      public void Run()
      {
        var asm = System.Reflection.Assembly.LoadFrom(System.IO.Path.Combine(this.BasePath, "PeerCastStation.App.dll"));
        var type = asm.GetType("PeerCastStation.App.StandaloneApp");
        var result = type.InvokeMember("Run",
          System.Reflection.BindingFlags.Public |
          System.Reflection.BindingFlags.Static |
          System.Reflection.BindingFlags.InvokeMethod,
          null,
          null,
          new object[] { this.BasePath, this.Args });
        if (result is Int32) {
          this.Result.ExitCode = (int)result;
        }
      }

    }

    [STAThread]
    static int Main(string[] args)
    {
    start:
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
        Result   = new ResultContainer { ExitCode = 1 },
      };
      appdomain.DoCallBack(new CrossAppDomainDelegate(ctx.Run));
      switch (ctx.Result.ExitCode) {
      case -1:
        goto start;
      default:
        return ctx.Result.ExitCode;
      }
    }

  }
}
