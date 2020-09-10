
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PeerCastStation.Core.Http
{
  public interface IAppBuilder
  {
    IDictionary<string, object> Properties { get; }
    IAppBuilder Use<T>(params object[] args);
    IAppBuilder New();
    Func<IDictionary<string,object>, Task> Build();
  }

}
