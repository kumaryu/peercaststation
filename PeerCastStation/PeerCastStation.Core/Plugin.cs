using System;

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
}
