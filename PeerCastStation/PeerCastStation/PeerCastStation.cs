using PeerCastStation.App;
using System;

namespace PeerCastStation.Main
{
  public class PeerCastStation
  {
    [STAThread]
    static void Main(string[] args)
    {
      StandaloneApp.Run(args);
    }

  }
}
