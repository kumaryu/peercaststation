using System;

namespace PeerCastStation.Core.IPC
{
  [Flags]
  public enum IPCOption {
    None = 0,
    AcceptAnyUser = 1,
  }

}
