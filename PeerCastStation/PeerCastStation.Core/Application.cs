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

  public class PluginAttribute
    : Attribute
  {
    public PluginType Type { get; set; }
    public PluginAttribute(PluginType type)
    {
      this.Type = type;
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
