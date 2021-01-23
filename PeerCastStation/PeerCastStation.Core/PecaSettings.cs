using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.IO;
using System.Xml;
using System.Net;
using System.Reflection;
using System.Collections;

namespace PeerCastStation.Core
{
  [AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum | AttributeTargets.Struct)]
  public class PecaSettingsAttribute
    : Attribute
  {
    public PecaSettingsAttribute()
    {
      this.Alias = null;
    }

    public PecaSettingsAttribute(string alias)
    {
      this.Alias = alias;
    }

    public string Alias { get; private set; }
  }

  public class PecaSerializer
  {
    private Logger logger = new Logger(typeof(PecaSerializer));
    private Type FindType(string name)
    {
      Type t;
      if (PecaSettings.SettingTypes.TryGetValue(name, out t)) {
        return t;
      }
      else {
        return null;
      }
    }

    private string FindTypeName(Type type)
    {
      string name;
      if (PecaSettings.SettingTypeNames.TryGetValue(type, out name)) {
        return name;
      }
      else {
        return type.FullName;
      }
    }

    public class RoundtripObject
    {
      public string TypeName { get; private set; }
      public Dictionary<string,object> Properties { get; private set; }
      public RoundtripObject(string typename, Dictionary<string, object> properties)
      {
        this.TypeName = typename;
        this.Properties = properties;
      }
    }

    public class RoundtripEnum
    {
      public string TypeName { get; private set; }
      public string Value { get; private set; }
      public RoundtripEnum(string typename, string value)
      {
        this.TypeName = typename;
        this.Value = value;
      }
    }

    private void SerializeValue(XmlWriter writer, object obj)
    {
           if (obj==null)     SerializeNull(writer, obj);
      else if (obj is long)   SerializeInteger(writer, (long)obj);
      else if (obj is int)    SerializeInteger(writer, (long)(int)obj);
      else if (obj is short)  SerializeInteger(writer, (long)(short)obj);
      else if (obj is byte)   SerializeInteger(writer, (long)(byte)obj);
      else if (obj is double) SerializeFloat(writer, (double)obj);
      else if (obj is float)  SerializeFloat(writer, (double)(float)obj);
      else if (obj is bool)   SerializeBoolean(writer, (bool)obj);
      else if (obj is Enum)   SerializeEnum(writer, (Enum)obj);
      else if (obj is String) SerializeString(writer, (string)obj);
      else if (obj is Uri)    SerializeUri(writer, (Uri)obj);
      else if (obj is Guid)   SerializeGuid(writer, (Guid)obj);
      else if (obj is DateTime)   SerializeDateTime(writer, (DateTime)obj);
      else if (obj is TimeSpan)   SerializeTimeSpan(writer, (TimeSpan)obj);
      else if (obj is IPEndPoint) SerializeIPEndPoint(writer, (IPEndPoint)obj);
      else if (obj is IPAddress)  SerializeIPAddress(writer, (IPAddress)obj);
      else if (obj is IDictionary) SerializeDictionary(writer, (IDictionary)obj);
      else if (obj is IEnumerable) SerializeArray(writer, (IEnumerable)obj);
      else if (obj is RoundtripObject) SerializeRoundtripObject(writer, (RoundtripObject)obj);
      else if (obj is RoundtripEnum) SerializeRoundtripEnum(writer, (RoundtripEnum)obj);
      else SerializeObject(writer, obj);
    }

    private void SerializeRoundtripObject(XmlWriter writer, RoundtripObject obj)
    {
      writer.WriteStartElement("object");
      writer.WriteAttributeString("type", obj.TypeName);
      foreach (var prop in obj.Properties) {
        writer.WriteStartElement("property");
        writer.WriteAttributeString("name", prop.Key);
        SerializeValue(writer, prop.Value);
        writer.WriteEndElement();
      }
      writer.WriteEndElement();
    }

    private void SerializeObject(XmlWriter writer, object obj)
    {
      var type = obj.GetType();
      writer.WriteStartElement("object");
      writer.WriteAttributeString("type", FindTypeName(type));
      var properties = type.GetProperties(
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty | BindingFlags.SetProperty)
        .Where(prop => prop.CanRead && prop.CanWrite);
      foreach (var prop in properties) {
        writer.WriteStartElement("property");
        writer.WriteAttributeString("name", prop.Name);
        SerializeValue(writer, prop.GetValue(obj, null));
        writer.WriteEndElement();
      }
      writer.WriteEndElement();
    }

    private void SerializeArray(XmlWriter writer, IEnumerable ary)
    {
      writer.WriteStartElement("array");
      foreach (var obj in ary) {
        SerializeValue(writer, obj);
      }
      writer.WriteEndElement();
    }

    private void SerializeDictionary(XmlWriter writer, IDictionary dic)
    {
      writer.WriteStartElement("dictionary");
      foreach (DictionaryEntry kv in dic) {
        writer.WriteStartElement("key");
        SerializeValue(writer, kv.Key);
        writer.WriteEndElement();
        writer.WriteStartElement("value");
        SerializeValue(writer, kv.Value);
        writer.WriteEndElement();
      }
      writer.WriteEndElement();
    }

    private void SerializeIPAddress(XmlWriter writer, IPAddress value)
    {
      writer.WriteStartElement("ipaddress");
      writer.WriteValue(value.ToString());
      writer.WriteEndElement();
    }

    private void SerializeIPEndPoint(XmlWriter writer, IPEndPoint value)
    {
      writer.WriteStartElement("ipendpoint");
      SerializeIPAddress(writer, value.Address);
      writer.WriteStartElement("port");
      writer.WriteValue(value.Port);
      writer.WriteEndElement();
      writer.WriteEndElement();
    }

    private void SerializeTimeSpan(XmlWriter writer, TimeSpan value)
    {
      writer.WriteStartElement("timespan");
      writer.WriteValue(value);
      writer.WriteEndElement();
    }

    private void SerializeDateTime(XmlWriter writer, DateTime value)
    {
      writer.WriteStartElement("datetime");
      writer.WriteValue(value);
      writer.WriteEndElement();
    }

    private void SerializeGuid(XmlWriter writer, Guid value)
    {
      writer.WriteStartElement("guid");
      writer.WriteString(value.ToString());
      writer.WriteEndElement();
    }

    private void SerializeUri(XmlWriter writer, Uri value)
    {
      writer.WriteStartElement("uri");
      writer.WriteString(value.ToString());
      writer.WriteEndElement();
    }

    private void SerializeString(XmlWriter writer, string value)
    {
      writer.WriteStartElement("string");
      writer.WriteValue(value);
      writer.WriteEndElement();
    }

    private void SerializeEnum(XmlWriter writer, Enum value)
    {
      writer.WriteStartElement("enum");
      writer.WriteAttributeString("type", FindTypeName(value.GetType()));
      writer.WriteString(value.ToString());
      writer.WriteEndElement();
    }

    private void SerializeRoundtripEnum(XmlWriter writer, RoundtripEnum obj)
    {
      writer.WriteStartElement("enum");
      writer.WriteAttributeString("type", obj.TypeName);
      writer.WriteString(obj.Value);
      writer.WriteEndElement();
    }

    private void SerializeBoolean(XmlWriter writer, bool value)
    {
      writer.WriteStartElement("boolean");
      writer.WriteValue(value);
      writer.WriteEndElement();
    }

    private void SerializeFloat(XmlWriter writer, double value)
    {
      writer.WriteStartElement("float");
      writer.WriteValue(value);
      writer.WriteEndElement();
    }

    private void SerializeInteger(XmlWriter writer, long value)
    {
      writer.WriteStartElement("integer");
      writer.WriteValue(value);
      writer.WriteEndElement();
    }

    private void SerializeNull(XmlWriter writer, object obj)
    {
      writer.WriteElementString("null", null);
    }

    public void Serialize(XmlWriter writer, object root) 
    {
      writer.WriteStartDocument();
      SerializeValue(writer, root);
      writer.WriteEndDocument();
    }

    public bool TryDeserialize(XmlReader reader, out object result)
    {
      if (reader.NodeType==XmlNodeType.None) reader.Read();
      if (!reader.IsStartElement()) {
        result = null;
        return false;
      }
      switch (reader.LocalName) {
      case "null":       result = DeserializeNull(reader); return true;
      case "object":     result = DeserializeObject(reader); return true;
      case "array":      result = DeserializeArray(reader); return true;
      case "dictionary": result = DeserializeDictionary(reader); return true;
      case "ipaddress":  result = DeserializeIPAddress(reader); return true;
      case "ipendpoint": result = DeserializeIPEndPoint(reader); return true;
      case "timespan":   result = DeserializeTimeSpan(reader); return true;
      case "datetime":   result = DeserializeDateTime(reader); return true;
      case "guid":       result = DeserializeGuid(reader); return true;
      case "uri":        result = DeserializeUri(reader); return true;
      case "string":     result = DeserializeString(reader); return true;
      case "enum":       result = DeserializeEnum(reader); return true;
      case "boolean":    result = DeserializeBoolean(reader); return true;
      case "float":      result = DeserializeFloat(reader); return true;
      case "integer":    result = DeserializeInteger(reader); return true;
      default: result = null; return false;
      }
    }

    public object Deserialize(XmlReader reader)
    {
      object result;
      TryDeserialize(reader, out result);
      return result;
    }

    private object DeserializeNull(XmlReader reader)
    {
      reader.IsStartElement("null");
      if (reader.IsEmptyElement) {
        reader.Read();
      }
      else {
        reader.ReadEndElement();
      }
      return null;
    }

    private object ChangeType(object value, Type target)
    {
      if (target.IsArray) {
        var values = ((IEnumerable)value).OfType<object>().Select(obj => ChangeType(obj, target.GetElementType())).ToArray();
        var ary = Array.CreateInstance(target.GetElementType(), values.Length);
        for (var i=0; i<ary.Length; i++) {
          ary.SetValue(values[i], i);
        }
        return ary;
      }
      else if (target.IsGenericType && !target.ContainsGenericParameters) {
        if (target.GetGenericTypeDefinition()==typeof(List<>)) {
          var valuetype = target.GetGenericArguments()[0];
          var lst = (IList)(target.InvokeMember("", BindingFlags.CreateInstance, null, null, new object[] {}));
          foreach (var v in ((IEnumerable)value)) {
            lst.Add(ChangeType(v, valuetype));
          }
          return lst;
        }
        if (target.GetGenericTypeDefinition()==typeof(Dictionary<,>)) {
          var keytype   = target.GetGenericArguments()[0];
          var valuetype = target.GetGenericArguments()[1];
          var dic = (IDictionary)(target.InvokeMember("", BindingFlags.CreateInstance, null, null, new object[] {}));
          foreach (DictionaryEntry kv in ((IDictionary)value)) {
            dic.Add(ChangeType(kv.Key, keytype), ChangeType(kv.Value, valuetype));
          }
          return dic;
        }
      }
      return Convert.ChangeType(value, target);
    }

    private object DeserializeObject(XmlReader reader)
    {
      if (!reader.IsStartElement("object")) return null;
      var typename = reader.GetAttribute("type");
      var properties = new Dictionary<string,object>();
      if (!reader.IsEmptyElement) {
        reader.Read();
        while (reader.IsStartElement("property")) {
          var name = reader.GetAttribute("name");
          reader.Read();
          var value = Deserialize(reader);
          reader.ReadEndElement();
          properties.Add(name, value);
        }
        reader.ReadEndElement();
      }
      else {
        reader.Read();
      }
      var type = FindType(typename);
      if (type!=null) {
        var obj = type.InvokeMember("", BindingFlags.CreateInstance, null, null, new object[] {});
        foreach (var prop in properties) {
          var member = type.GetMember(
            prop.Key,
            MemberTypes.Property,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.SetProperty | BindingFlags.GetProperty)
            .Cast<PropertyInfo>()
            .FirstOrDefault();
          if (member!=null && member.CanWrite) {
            member.SetValue(obj, ChangeType(prop.Value, member.PropertyType), null);
          }
        }
        return obj;
      }
      else {
        logger.Info($"Unknown Type found: {typename}");
        return new RoundtripObject(typename, properties);
      }
    }

    private object DeserializeArray(XmlReader reader)
    {
      if (!reader.IsStartElement("array")) return null;
      if (reader.IsEmptyElement) {
        reader.Read();
        return new object[0];
      }
      else {
        reader.Read();
        var elements = new List<object>();
        object value;
        while (TryDeserialize(reader, out value)) {
          elements.Add(value);
        }
        reader.ReadEndElement();
        return elements.ToArray();
      }
    }

    private object DeserializeDictionary(XmlReader reader)
    {
      if (!reader.IsStartElement("dictionary")) return null;
      var dic = new Dictionary<object,object>();
      if (reader.IsEmptyElement) {
        reader.Read();
        return dic;
      }
      reader.ReadStartElement("dictionary");
      while (reader.IsStartElement("key")) {
        reader.Read();
        var key = Deserialize(reader);
        reader.ReadEndElement();
        reader.ReadStartElement("value");
        var value = Deserialize(reader);
        reader.ReadEndElement();
        dic.Add(key, value);
      }
      reader.ReadEndElement();
      return dic;
    }

    private object DeserializeIPAddress(XmlReader reader)
    {
      reader.ReadStartElement("ipaddress");
      var value = IPAddress.Parse(reader.ReadContentAsString());
      reader.ReadEndElement();
      return value;
    }

    private object DeserializeIPEndPoint(XmlReader reader)
    {
      reader.ReadStartElement("ipendpoint");
      var addr = DeserializeIPAddress(reader);
      reader.ReadStartElement("port");
      var port = reader.ReadContentAsInt();
      reader.ReadEndElement();
      reader.ReadEndElement();
      return new IPEndPoint((IPAddress)addr, port);
    }

    private object DeserializeTimeSpan(XmlReader reader)
    {
      reader.ReadStartElement("timespan");
      var value = DateTime.Parse(reader.ReadContentAsString());
      reader.ReadEndElement();
      return value;
    }

    private object DeserializeDateTime(XmlReader reader)
    {
      reader.ReadStartElement("string");
      var value = reader.ReadContentAsDateTime();
      reader.ReadEndElement();
      return value;
    }

    private object DeserializeGuid(XmlReader reader)
    {
      reader.ReadStartElement("guid");
      var value = new Guid(reader.ReadContentAsString());
      reader.ReadEndElement();
      return value;
    }

    private object DeserializeUri(XmlReader reader)
    {
      reader.ReadStartElement("uri");
      try {
        var value = new Uri(reader.ReadContentAsString());
        return value;
      }
      catch (UriFormatException) {
        return null;
      }
      finally {
        reader.ReadEndElement();
      }
    }

    private object DeserializeString(XmlReader reader)
    {
      reader.ReadStartElement("string");
      var value = reader.ReadContentAsString();
      reader.ReadEndElement();
      return value;
    }

    private Type FindEnumType(string name)
    {
      var t = FindType(name);
      if (t!=null && t.IsSubclassOf(typeof(Enum))) {
        return t;
      }
      else {
        return null;
      }
    }

    private object DeserializeEnum(XmlReader reader)
    {
      if (!reader.IsStartElement("enum")) return null;
      var typename = reader.GetAttribute("type");
      var type = FindEnumType(typename);
      reader.Read();
      var value = reader.ReadContentAsString();
      reader.ReadEndElement();
      if (type==null) {
        logger.Info($"Unknown Enum type found: {typename}");
        return new RoundtripEnum(typename, value);
      }
      if (String.IsNullOrEmpty(value)) return null;
      return Enum.Parse(type, value);
    }

    private object DeserializeBoolean(XmlReader reader)
    {
      reader.ReadStartElement("boolean");
      var value = reader.ReadContentAsBoolean();
      reader.ReadEndElement();
      return value;
    }

    private object DeserializeFloat(XmlReader reader)
    {
      reader.ReadStartElement("float");
      var value = reader.ReadContentAsDouble();
      reader.ReadEndElement();
      return value;
    }

    private object DeserializeInteger(XmlReader reader)
    {
      reader.ReadStartElement("integer");
      var value = reader.ReadContentAsLong();
      reader.ReadEndElement();
      return value;
    }
  }

  public class PecaSettings
  {
    private static Dictionary<string, Type> settingTypes = new Dictionary<string, Type>();
    private static Dictionary<Type, string> settingTypeNames = new Dictionary<Type, string>();
    public static IDictionary<string, Type> SettingTypes { get { return settingTypes; } }
    public static IDictionary<Type, string> SettingTypeNames { get { return settingTypeNames; } }

    public static void RegisterType(string name, Type type)
    {
      if (settingTypes.ContainsKey(name)) return;
      settingTypes.Add(name, type);
      settingTypeNames.Add(type, name);
      foreach (var t in type.GetNestedTypes()) {
        RegisterType(name + "+" + t.FullName.Split('+').Last(), t);
      }
    }

    public static string DefaultFileName {
      get {
        var path = Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create), 
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
      using (var writer=XmlWriter.Create(FileName, new XmlWriterSettings { Indent = true, })) {
        var serializer = new PecaSerializer();
        lock (values) {
          serializer.Serialize(writer, values.ToArray());
        }
      }
    }

    private bool LoadOriginalFormat(string filename)
    {
      try {
        using (var reader=XmlReader.Create(FileName)) {
          var serializer = new PecaSerializer();
          var ary = (object[])serializer.Deserialize(reader);
          if (ary!=null) {
            values = new List<object>(ary);
          }
        }
      }
      catch (SerializationException) { return false; }
      catch (XmlException) { return false; }
      catch (IOException)  { return false; }
      return true;
    }

    public bool Load()
    {
      if (!System.IO.File.Exists(FileName)) return false;
      return LoadOriginalFormat(FileName);
    }
  }
}

