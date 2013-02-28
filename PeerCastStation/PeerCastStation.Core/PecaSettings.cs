using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.IO;
using System.Xml;

namespace PeerCastStation.Core
{
  [AttributeUsage(AttributeTargets.Class)]
  public class PecaSettingsAttribute
    : Attribute
  {
  }

  public class PecaSettings
  {
    private static List<Type> settingTypes = new List<Type>();
    public static IEnumerable<Type> SettingTypes { get; private set; }
    public static void RegisterType(Type type)
    {
      settingTypes.Add(type);
    }

    public static void UnregisterType(Type type)
    {
      settingTypes.Remove(type);
    }


    public static string DefaultFileName {
      get {
        var path = Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
          "PeerCastStation");
        Directory.CreateDirectory(path);
        return Path.Combine(path, "PecaSettings.xml");
      }
    }
    public string FileName { get; private set; }

    public PecaSettings(string filename)
    {
      this.FileName = filename;
    }

    private List<object> values = new List<object>();
    public IEnumerable<object> Values {
      get {
        lock (values) {
          return values.ToArray();
        }
      }
      set { values = new List<object>(value); }
    }

    public T Get<T>() where T:class, new()
    {
      lock (values) {
        var result = values.FirstOrDefault(v => v is T) as T;
        if (result==null) {
          result = new T();
          values.Add(result);
        }
        return result;
      }
    }

    public void Set(object value)
    {
      lock (value) {
        Remove(value.GetType());
        values.Add(value);
      }
    }

    public void Remove(Type type)
    {
      lock (values) {
        values.RemoveAll(v => v.GetType()==type);
      }
    }

    public bool Contains(Type type)
    {
      lock (values) {
        return values.Exists(v => v.GetType()==type);
      }
    }

    public void Save()
    {
      System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FileName));
      var serializer = new DataContractSerializer(typeof(object[]), settingTypes);
      using (var writer=XmlWriter.Create(System.IO.File.Create(FileName), new XmlWriterSettings { Indent = true, })) {
        object[] ary;
        lock (values) {
          ary = values.ToArray();
        }
        serializer.WriteObject(writer, ary);
      }
    }

    public bool Load()
    {
      var serializer = new DataContractSerializer(typeof(object[]), settingTypes);
      if (!System.IO.File.Exists(FileName)) return false;
      try {
        using (var fd=System.IO.File.OpenRead(FileName)) {
          values = new List<object>((object[])serializer.ReadObject(fd));
        }
      }
      catch (SerializationException) { return false; }
      catch (XmlException) { return false; }
      catch (IOException)  { return false; }
      return true;
    }
  }
}

