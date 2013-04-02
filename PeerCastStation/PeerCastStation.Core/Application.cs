using System;
using System.Collections.Generic;

namespace PeerCastStation.Core
{
  public enum PluginPriority
  {
    Highest = -200,
    Higher  = -100,
    Normal  =    0,
    Lower   =  100,
    Lowest  =  200,
  }

  [AttributeUsage(AttributeTargets.Class)]
  public class PluginAttribute
    : Attribute
  {
    public PluginPriority Priority { get; private set; }

    public PluginAttribute(PluginPriority priority)
    {
      this.Priority = priority;
    }

    public PluginAttribute()
      : this(PluginPriority.Normal)
    {
    }
  }

  public interface IPlugin
  {
    string Name   { get; }
    bool IsUsable { get; }
    void Start(PeerCastApplication app);
    void Stop();
  }

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
    public PeerCastApplication()
    {
      if (current==null) current = this;
    }
  }
}
