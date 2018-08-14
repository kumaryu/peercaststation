using System;

namespace PeerCastStation.Core.IPC
{
  [Flags]
  public enum IPCOption {
    None = 0,
    AcceptAnyUsers = 1,
  }

}
