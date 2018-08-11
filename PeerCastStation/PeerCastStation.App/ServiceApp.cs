using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PeerCastStation.Core;

namespace PeerCastStation.App
{
  public class ServiceApp
    : AppBase
  {
    public override AppType Type {
      get { return AppType.Service; }
    }

    public ServiceApp(string basepath, string[] args)
      : base(basepath, args)
    {
    }

  }
}
