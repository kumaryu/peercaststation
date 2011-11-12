using System;

namespace PeerCastStation.Core
{
  public interface IUserInterface
  {
    string Name { get; }
    void Start(IApplication app);
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

  public interface IApplication
  {
    PeerCast PeerCast { get; }
    void Stop();
  }
}
