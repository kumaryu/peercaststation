namespace PecaStationdDebug
{
  class Program
  {
    static void Main(string[] args)
    {
      var app = new PecaStationd.PeerCastStationService.PecaServiceApplication();
      var task = app.Start();
      System.Console.ReadLine();
      app.Stop();
      task.Wait();
    }
  }
}
