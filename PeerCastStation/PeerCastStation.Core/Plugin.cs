using System;
using System.Linq;
using System.Diagnostics;

namespace PeerCastStation.Core
{
  public enum PluginPriority {
    Highest = -200,
    Higher  = -100,
    Normal  =    0,
    Lower   =  100,
    Lowest  =  200,
  }

  [Flags]
  public enum PluginType {
    Unknown       = 0x00,
    UserInterface = 0x01,
    GUI           = 0x02,
    Protocol      = 0x04,
    Content       = 0x08,
    Utility       = 0x10,
    Automation    = 0x20,
  }

  [AttributeUsage(AttributeTargets.Class)]
  public class PluginAttribute
    : Attribute
  {
    public PluginType Type { get; private set; }
    public PluginPriority Priority { get; private set; }

    public PluginAttribute(PluginType type, PluginPriority priority)
    {
      this.Type     = type;
      this.Priority = priority;
    }

    public PluginAttribute(PluginPriority priority)
      : this(PluginType.Unknown, priority)
    {
    }

    public PluginAttribute(PluginType type)
      : this(type, PluginPriority.Normal)
    {
    }

    public PluginAttribute()
      : this(PluginType.Unknown, PluginPriority.Normal)
    {
    }
  }

  public class PluginVersionInfo
  {
    public string FileName     { get; private set; }
    public string AssemblyName { get; private set; }
    public string Version      { get; private set; }
    public string Copyright    { get; private set; }

    public PluginVersionInfo(
      string filename,
      string assembly_name,
      string version,
      string copyright)
    {
      this.FileName     = filename;
      this.AssemblyName = assembly_name;
      this.Version      = version;
      this.Copyright    = copyright;
    }
  }

  public interface IPlugin
  {
    string Name   { get; }
    bool IsUsable { get; }
    PluginVersionInfo GetVersionInfo();
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
    public virtual PluginVersionInfo GetVersionInfo()
    {
      var asm = this.GetType().Assembly;
      var file_version = FileVersionInfo.GetVersionInfo(asm.Location);
      var info_version = asm.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false).FirstOrDefault() as System.Reflection.AssemblyInformationalVersionAttribute;
      var version = info_version!=null ? info_version.InformationalVersion : file_version.FileVersion;
      return new PluginVersionInfo(
        System.IO.Path.GetFileName(asm.Location),
        asm.FullName,
        version,
        file_version.LegalCopyright);
    }

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

    public override bool Equals(object obj)
    {
      if (this==obj) return true;
      if (this.GetType()!=obj.GetType()) return false;
      var x = (NotificationMessage)obj;
      return
        Object.Equals(this.Title, x.Title) &&
        Object.Equals(this.Message, x.Message) &&
        Object.Equals(this.Type, x.Type) &&
        Object.Equals(this.Data, x.Data);
    }

    public override int GetHashCode()
    {
      return
        (this.Title==null   ? 0 : this.Title.GetHashCode()) +
        (this.Message==null ? 0 : this.Message.GetHashCode()) +
        (this.Type.GetHashCode()) +
        (this.Data==null    ? 0 : this.Data.GetHashCode());
    }
  }

  public interface IUserInterfacePlugin
    : IPlugin
  {
    void ShowNotificationMessage(NotificationMessage msg);
  }
}
