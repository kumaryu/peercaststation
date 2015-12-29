using System;
using System.ServiceProcess;

namespace PecaStationd
{
	static class Program
	{
		/// <summary>
		/// アプリケーションのメイン エントリ ポイントです。
		/// </summary>
		static void Main()
		{
			ServiceBase[] ServicesToRun;
			ServicesToRun = new ServiceBase[] { 
				new PeerCastStationService() 
			};
			ServiceBase.Run(ServicesToRun);
		}
	}
}
