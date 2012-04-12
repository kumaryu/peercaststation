using System;

namespace PeerCastStation.Core
{
  public interface IUserInterface
  {
    string Name { get; }
    void Start(PeerCastApplication app);
    void Stop();
  }

  public interface IUserInterfaceFactory
  {
    string Name { get; }
    IUserInterface CreateUserInterface();
  }

  public enum PluginType
  {
    Unknown,
    UserInterface,
  }

  public enum PluginPriority
  {
    Highest =  200,
    Higher  =  100,
    Normal  =    0,
    Lower   = -100,
    Lowest  = -200,
  }

  [AttributeUsage(AttributeTargets.Class)]
  public class PluginAttribute
    : Attribute
  {
    public PluginType     Type     { get; private set; }
    public PluginPriority Priority { get; private set; }

    public PluginAttribute(PluginType type, PluginPriority priority)
    {
      this.Type = type;
      this.Priority = priority;
    }

    public PluginAttribute(PluginType type)
      : this(type, PluginPriority.Normal)
    {
    }
  }

  public abstract class PeerCastApplication
    : MarshalByRefObject
  {
    private static PeerCastApplication current;
    public static PeerCastApplication Current {
      get { return current; }
      set { current = value; }
    }
    public abstract PeerCast PeerCast { get; }
    public abstract void Stop();
  }
}
