using System;
using System.Collections.Generic;
using System.Threading;

namespace PeerCastStation.Core.Http
{
  internal class OpaqueEnvironment
  {
    public IDictionary<string,object> Environment { get; private set; } = new Dictionary<string,object>();
    public OpaqueEnvironment(ConnectionStream stream, CancellationToken cancellationToken)
    {
      Environment[OwinEnvironment.Opaque.Version] = "1.0";
      Environment[OwinEnvironment.Opaque.Stream] = stream;
      Environment[OwinEnvironment.Opaque.CallCancelled] = cancellationToken;
    }
  }

}

