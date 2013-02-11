using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.IO;

namespace PeerCastStation.Core
{
  public static class PecaSettings
  {
    static private List<object> values = new List<object>();

    static public IEnumerable<object> Values {
      get { return values.ToArray(); }
    }

    static public T Get<T>() where T:class, new()
    {
      var result = values.FirstOrDefault(v => v is T) as T;
      if (result==null) {
        result = new T();
        values.Add(result);
      }
      return result;
    }

    static public string FileName {
      get {
        var path = Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
          "PeerCastStation");
        Directory.CreateDirectory(path);
        return Path.Combine(path, "PecaSettings.xml");
      }
    }

    static public void Save()
    {
      Save(FileName);
    }

    static public bool Load()
    {
      return Load(FileName);
    }

    static public void Remove()
    {
      if (File.Exists(FileName)) {
        File.Delete(FileName);
      }
    }

    static private void Save(string file)
    {
      System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file));
      var serializer = new NetDataContractSerializer();
      using (var fd=System.IO.File.Create(file)) {
        serializer.Serialize(fd, values.ToArray());
      }
    }

    static private bool Load(string file)
    {
      var serializer = new NetDataContractSerializer();
      if (!System.IO.File.Exists(file)) return false;
      using (var fd=System.IO.File.OpenRead(file)) {
        values = new List<object>((object[])serializer.Deserialize(fd));
      }
      return true;
    }
  }
}

