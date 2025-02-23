using System.Runtime.Versioning;
using System.ServiceProcess;

namespace PecaStationd
{
  static class Program
  {
    /// <summary>
    /// アプリケーションのメイン エントリ ポイントです。
    /// </summary>
    [SupportedOSPlatform("windows")]
    static void Main()
    {
      ServiceBase.Run([new PeerCastStationService()]);
    }
  }
}
