using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.IO;
using System.Xml;

namespace PeerCastStation.Core
{
  public class PecaSettings
  {
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
      var serializer = new NetDataContractSerializer();
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
      var serializer = new NetDataContractSerializer();
      if (!System.IO.File.Exists(FileName)) return false;
      using (var fd=System.IO.File.OpenRead(FileName)) {
        values = new List<object>((object[])serializer.Deserialize(fd));
      }
      return true;
    }
  }
}

