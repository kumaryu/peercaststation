using System;
using System.IO;

namespace PeerCastStation.Core
{
  public class IOTimeoutException
    : IOException
  {
    public IOTimeoutException()
      : base()
    {
    }

    public IOTimeoutException(string message)
      : base(message)
    {
    }
  }
}
