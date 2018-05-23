namespace PeerCastStation.App
{
  public static class Program
  {
    public static int Main(string[] args)
    {
      var basepath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
      return StandaloneApp.Run(basepath, args);
    }
  }
}

