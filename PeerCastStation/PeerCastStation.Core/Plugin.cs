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
    void Attach(PeerCastApplication app);
    void Detach();
    void Start();
    void Stop();
  }

  public abstract class PluginBase
    : IPlugin
  {
    public abstract string Name { get; }
    public virtual bool IsUsable { get { return true; } }

    private PeerCastApplication application;
    public PeerCastApplication Application { get { return application; } }

    public void Attach(PeerCastApplication app)
    {
      application = app;
      OnAttach();
    }

    public void Detach()
    {
      OnDetach();
      application = null;
    }

    public void Start()
    {
      OnStart();
    }

    public void Stop()
    {
      OnStop();
    }

    protected virtual void OnAttach() {}
    protected virtual void OnDetach() {}
    protected virtual void OnStart() {}
    protected virtual void OnStop() {}
  }

  public enum NotificationMessageType
  {
    Normal,
    Info,
    Warning,
    Error,
  }

  public class NotificationMessage
  {
    public string Title   { get; private set; }
    public string Message { get; private set; }
    public NotificationMessageType Type { get; private set; }
    public object Data    { get; private set; }
    public NotificationMessage(string title, string message, NotificationMessageType type, object data=null)
    {
      this.Title   = title;
      this.Message = message;
      this.Type    = type;
      this.Data    = data;
    }
  }

  public interface IUserInterfacePlugin
    : IPlugin
  {
    void ShowNotificationMessage(NotificationMessage msg);
  }
}
