namespace PecaStationdDebug
{
  class Program
  {
    static void Main(string[] args)
    {
      var app = new PeerCastStation.App.ServiceApp();
      var task = app.Start();
      System.Console.ReadLine();
      app.Stop();
      task.Wait();
    }
  }
}
