using System;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;

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

    private PeerCastApplication? application = null;
    public PeerCastApplication? Application { get { return application; } }

    public void Attach(PeerCastApplication app)
    {
      application = app;
      OnAttach(application);
    }

    public void Detach()
    {
      if (application!=null) {
        OnDetach(application);
        application = null;
      }
    }

    public void Start()
    {
      OnStart();
    }

    public void Stop()
    {
      OnStop();
    }

    protected virtual void OnAttach(PeerCastApplication application)
    {
      OnAttach();
    }
    protected virtual void OnDetach(PeerCastApplication application)
    {
      OnDetach();
    }
    protected virtual void OnAttach() {}
    protected virtual void OnDetach() {}
    protected virtual void OnStart() {}
    protected virtual void OnStop() {}
  }

  public static class PluginCollectionExtension
  {
    public static T? GetPlugin<T>(this IEnumerable<IPlugin> self)
      where T : class, IPlugin
    {
      return self.FirstOrDefault(plugin => plugin is T) as T;
    }
  }
}
