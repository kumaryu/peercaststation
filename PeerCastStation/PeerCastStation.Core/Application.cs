using System;
using System.Collections.Generic;

namespace PeerCastStation.Core
{
  public abstract class PeerCastApplication
  {
    private static PeerCastApplication current;
    public static PeerCastApplication Current {
      get { return current; }
      set { current = value; }
    }
    public abstract PecaSettings Settings { get; }
    public abstract IEnumerable<IPlugin> Plugins { get; }
    public abstract PeerCast PeerCast { get; }
    public abstract void Stop();
    public abstract void SaveSettings();
    public PeerCastApplication()
    {
      if (current==null) current = this;
    }
  }
}
