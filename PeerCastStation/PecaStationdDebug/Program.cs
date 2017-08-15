namespace PecaStationdDebug
{
  class Program
  {
    static int Main(string[] args)
    {
      var main = new PecaStationd.PeerCastStationServiceMain();
      main.Start(args);
      System.Console.ReadLine();
      return main.Stop();
    }
  }
}
