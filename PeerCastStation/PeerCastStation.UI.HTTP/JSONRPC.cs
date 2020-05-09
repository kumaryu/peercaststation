using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using PeerCastStation.Core;
using Microsoft.Owin;
using System.Xml.Linq;

namespace PeerCastStation.UI.HTTP.JSONRPC
{
  public class RPCMethodInfo
  {
    public string Name { get; private set; }
    public OutputStreamType Grant { get; private set; }
    public MethodInfo Method { get; private set; }
    public object Receiver { get; private set; }
    public RPCMethodInfo(object receiver, MethodInfo method)
    {
      this.Receiver = receiver;
      this.Method = method;
      var attr = Attribute.GetCustomAttributes(method).First(a => a.GetType()==typeof(RPCMethodAttribute));
      this.Name = ((RPCMethodAttribute)attr).Name;
      this.Grant = ((RPCMethodAttribute)attr).Grant;
    }

    private static string GetTypeSignatureForDoc(Type type)
    {
      if (type.IsGenericType && !type.IsGenericTypeDefinition) {
        return $"{GetTypeSignatureForDoc(type.GetGenericTypeDefinition())}{{{String.Join(",", type.GenericTypeArguments.Select(t => GetTypeSignatureForDoc(t)))}}}";
      }
      else if (type.IsGenericTypeDefinition) {
        var name = type.FullName.Replace('+', '.');
        return name.Substring(0, name.Length-2);
      }
      else {
        return type.FullName.Replace('+', '.');
      }
    }

    private static string GetMethodSignatureForDoc(MethodInfo method)
    {
      return $"M:{GetTypeSignatureForDoc(method.DeclaringType)}.{method.Name}({String.Join(",", method.GetParameters().Select(param => GetTypeSignatureForDoc(param.ParameterType)))})";
    }

    private static Dictionary<string, WeakReference<XDocument>> assemblyDocuments = new Dictionary<string, WeakReference<XDocument>>();
    private static XDocument GetAssemblyDocument(Assembly asm)
    {
      lock (assemblyDocuments) {
        if (assemblyDocuments.TryGetValue(asm.Location, out var reference) && reference.TryGetTarget(out var doc)) {
          return doc;
        }
        else {
          var path = System.IO.Path.ChangeExtension(asm.Location, ".xml");
          XDocument xml = null;
          try {
            xml = XDocument.Load(path);
          }
          catch (System.IO.FileNotFoundException) {
          }
          catch (System.Security.SecurityException) {
          }
          assemblyDocuments[asm.Location] = new WeakReference<XDocument>(xml);
          return xml;
        }
      }
    }

    struct DocumentCache {
      public enum CacheState {
        NotInitialized = 0,
        NotFound,
        Found,
      }
      public readonly CacheState State;
      public readonly XElement Document;
      public DocumentCache(CacheState state, XElement document)
      {
        State = state;
        Document = document;
      }
    }

    private DocumentCache document;
    private XElement GetMethodDocument()
    {
      switch (document.State) {
      case DocumentCache.CacheState.NotInitialized:
        {
          var doc = GetAssemblyDocument(Method.DeclaringType.Assembly);
          if (doc==null) {
            document = new DocumentCache(DocumentCache.CacheState.NotFound, null);
          }
          var elt = doc
            .Descendants("member")
            .Where(m => m.Attribute("name")?.Value==GetMethodSignatureForDoc(Method))
            .FirstOrDefault();
          if (elt!=null) {
            document = new DocumentCache(DocumentCache.CacheState.Found, elt);
          }
          else {
            document = new DocumentCache(DocumentCache.CacheState.NotFound, null);
          }
          return document.Document;
        }
      case DocumentCache.CacheState.Found:
        return document.Document;
      case DocumentCache.CacheState.NotFound:
      default:
        return null;
      }
    }

    public string GetMethodSummary()
    {
      var elt = GetMethodDocument();
      return elt?.Element("summary")?.Value?.Trim();
    }

    public string GetResultSummary()
    {
      var elt = GetMethodDocument();
      if (elt==null) return null;
      return
        elt.Elements("returns")
          .Select(p => p.Value)
          .FirstOrDefault();
    }

    public string GetParameterSummary(string name)
    {
      var elt = GetMethodDocument();
      if (elt==null) return null;
      return
        elt.Elements("param")
          .Where(p => p.Attribute("name")?.Value==name)
          .Select(p => p.Value)
          .FirstOrDefault();
    }

    private string JsonType(Type type)
    {
           if (type==typeof(JToken))   return "Any";
      else if (type==typeof(JArray))   return "Array";
      else if (type==typeof(JObject))  return "Object";
      else if (type==typeof(byte[]))   return "Binary";
      else if (type==typeof(string))   return "String";
      else if (type==typeof(long?))    return "Integer or null";
      else if (type==typeof(long))     return "Integer";
      else if (type==typeof(int?))     return "Integer or null";
      else if (type==typeof(int))      return "Integer";
      else if (type==typeof(short?))   return "Integer or null";
      else if (type==typeof(short))    return "Integer";
      else if (type==typeof(bool?))    return "Boolean or null";
      else if (type==typeof(bool))     return "Boolean";
      else if (type==typeof(double?))  return "Number or null";
      else if (type==typeof(double))   return "Number";
      else if (type==typeof(float?))   return "Number or null";
      else if (type==typeof(float))    return "Number";
      else if (type==typeof(ulong?))   return "Integer or null";
      else if (type==typeof(ulong))    return "Integer";
      else if (type==typeof(uint?))    return "Integer or null";
      else if (type==typeof(uint))     return "Integer";
      else if (type==typeof(ushort?))  return "Integer or null";
      else if (type==typeof(ushort))   return "Integer";
      else if (type==typeof(decimal?)) return "Number or null";
      else if (type==typeof(decimal))  return "Number";
      else if (type==typeof(DateTime?)) return "DateTime or null";
      else if (type==typeof(DateTime)) return "DateTime";
      else                             return "Unknown type";
    }

    private object ToObject(Type type, JToken value)
    {
           if (type==typeof(JToken))   return value;
      else if (type==typeof(JArray))   return (JArray)value;
      else if (type==typeof(JObject))  return (JObject)value;
      else if (type==typeof(byte[]))   return (byte[])value;
      else if (type==typeof(string))   return (string)value;
      else if (type==typeof(long?))    return (long?)value;
      else if (type==typeof(long))     return (long)value;
      else if (type==typeof(int?))     return (int?)value;
      else if (type==typeof(int))      return (int)value;
      else if (type==typeof(short?))   return (short?)value;
      else if (type==typeof(short))    return (short)value;
      else if (type==typeof(bool?))    return (bool?)value;
      else if (type==typeof(bool))     return (bool)value;
      else if (type==typeof(double?))  return (double?)value;
      else if (type==typeof(double))   return (double)value;
      else if (type==typeof(float?))   return (float?)value;
      else if (type==typeof(float))    return (float)value;
      else if (type==typeof(ulong?))   return (ulong?)value;
      else if (type==typeof(ulong))    return (ulong)value;
      else if (type==typeof(uint?))    return (uint?)value;
      else if (type==typeof(uint))     return (uint)value;
      else if (type==typeof(ushort?))  return (ushort?)value;
      else if (type==typeof(ushort))   return (ushort)value;
      else if (type==typeof(decimal?)) return (decimal?)value;
      else if (type==typeof(decimal))  return (decimal)value;
      else if (type==typeof(DateTime?)) return (DateTime?)value;
      else if (type==typeof(DateTime)) return (DateTime)value;
      else                             return value;
    }

    private JToken FromObject(Type type, object value)
    {
           if (type==typeof(void))     return null;
      else if (type==typeof(JToken))   return (JToken)value;
      else if (type==typeof(JArray))   return (JArray)value;
      else if (type==typeof(JObject))  return (JObject)value;
      else if (type==typeof(byte[]))   return (byte[])value;
      else if (type==typeof(string))   return (string)value;
      else if (type==typeof(long?))    return (long?)value;
      else if (type==typeof(long))     return (long)value;
      else if (type==typeof(int?))     return (int?)value;
      else if (type==typeof(int))      return (int)value;
      else if (type==typeof(short?))   return (short?)value;
      else if (type==typeof(short))    return (short)value;
      else if (type==typeof(bool?))    return (bool?)value;
      else if (type==typeof(bool))     return (bool)value;
      else if (type==typeof(double?))  return (double?)value;
      else if (type==typeof(double))   return (double)value;
      else if (type==typeof(float?))   return (float?)value;
      else if (type==typeof(float))    return (float)value;
      else if (type==typeof(ulong?))   return (ulong?)value;
      else if (type==typeof(ulong))    return (ulong)value;
      else if (type==typeof(uint?))    return (uint?)value;
      else if (type==typeof(uint))     return (uint)value;
      else if (type==typeof(ushort?))  return (ushort?)value;
      else if (type==typeof(ushort))   return (ushort)value;
      else if (type==typeof(decimal?)) return (decimal?)value;
      else if (type==typeof(decimal))  return (decimal)value;
      else if (type==typeof(DateTime?)) return (DateTime?)value;
      else if (type==typeof(DateTime)) return (DateTime)value;
      else                             return JToken.FromObject(value);
    }

    public JToken Invoke(IOwinContext ctx, JToken args)
    {
      var param_infos = Method.GetParameters();
      if (param_infos.Length==0) {
        try {
          var res = Method.Invoke(Receiver, new object[] {});
          return FromObject(Method.ReturnType, res);
        }
        catch (TargetInvocationException e) {
          throw e.InnerException;
        }
      }
      else {
        int pos = 0;
        int len = param_infos.Length;
        var arguments = new object[param_infos.Length];
        if (param_infos[0].ParameterType==typeof(IOwinContext)) {
          arguments[0] = ctx;
          pos += 1;
          len -= 1;
        }
        if (len==0) {
          try {
            var res = Method.Invoke(Receiver, arguments);
            return FromObject(Method.ReturnType, res);
          }
          catch (TargetInvocationException e) {
            throw e.InnerException;
          }
        }
        if (args==null) {
          throw new RPCError(RPCErrorCode.InvalidParams, "parameters required");
        }
        if (args.Type==JTokenType.Array) {
          var ary = (JArray)args;
          if (len!=ary.Count) {
            throw new RPCError(
              RPCErrorCode.InvalidParams, 
              String.Format("Wrong number of arguments ({0} for {1})", len, ary.Count));
          }
          for (var i=0; i<len; i++) {
            try {
              arguments[i+pos] = ToObject(param_infos[i+pos].ParameterType, ary[i]);
            }
            catch (ArgumentException) {
              throw new RPCError(
                RPCErrorCode.InvalidParams, 
                String.Format("{0} must be {1})", param_infos[i+pos].Name, JsonType(param_infos[i+pos].ParameterType)));
            }
          }
        }
        else if (args.Type==JTokenType.Object) {
          var obj = (JObject)args;
          for (var i=0; i<len; i++) {
            JToken value;
            if (obj.TryGetValue(param_infos[i+pos].Name, out value) && value.Type!=JTokenType.Undefined) {
              try {
                arguments[i+pos] = ToObject(param_infos[i+pos].ParameterType, value);
              }
              catch (ArgumentException) {
                throw new RPCError(
                  RPCErrorCode.InvalidParams, 
                  String.Format("{0} must be {1}", param_infos[i+pos].Name, JsonType(param_infos[i+pos].ParameterType)));
              }
              catch (InvalidCastException) {
                throw new RPCError(
                  RPCErrorCode.InvalidParams, 
                  String.Format("{0} must be {1}", param_infos[i+pos].Name, JsonType(param_infos[i+pos].ParameterType)));
              }
            }
            else if (param_infos[i+pos].DefaultValue!=DBNull.Value) {
              arguments[i+pos] = param_infos[i+pos].DefaultValue;
            }
            else if (param_infos[i+pos].IsOptional) {
              arguments[i+pos] = null;
            }
            else {
              throw new RPCError(
                RPCErrorCode.InvalidParams,
                String.Format("parameter `{0}' missing", param_infos[i].Name));
            }
          }
        }
        else {
          throw new RPCError(RPCErrorCode.InvalidParams, "parameters must be Array or Object");
        }
        try {
          var res = Method.Invoke(Receiver, arguments);
          return FromObject(Method.ReturnType, res);
        }
        catch (TargetInvocationException e) {
          if (e.InnerException is RPCError) {
            throw e.InnerException;
          }
          else {
            throw new RPCError(RPCErrorCode.InternalError, e.InnerException.Message);
          }
        }
      }
    }
  }

  public class RPCMethodAttribute : Attribute
  {
    public string Name { get; private set; }
    public OutputStreamType Grant { get; private set; }
    public RPCMethodAttribute(string name)
    {
      this.Name = name;
      this.Grant = OutputStreamType.Interface;
    }

    public RPCMethodAttribute(string name, OutputStreamType grant)
    {
      this.Name = name;
      this.Grant = grant;
    }
  }

  public enum RPCErrorCode
  {
    ParseError     = -32700,
    InvalidRequest = -32600,
    MethodNotFound = -32601,
    InvalidParams  = -32602,
    InternalError  = -32603,

    ChannelNotFound = -1,
  }

  public class RPCError
    : ApplicationException
  {
    public RPCErrorCode Code { get; private set; }
    public new JToken   Data { get; private set; }
    public RPCError(RPCErrorCode error)
      : this(error, error.ToString())
    {
    }

    public RPCError(RPCErrorCode error, JToken data)
      : this(error, error.ToString(), data)
    {
    }

    public RPCError(RPCErrorCode error, string message)
      : this(error, message, null)
    {
    }

    public RPCError(RPCErrorCode error, string message, JToken data)
      : base(message)
    {
      this.Code = error;
      this.Data = data;
    }

    public JToken ToJson()
    {
      var obj = new JObject();
      obj["code"] = (int)this.Code;
      obj["message"] = this.Message;
      if (this.Data!=null) obj["data"] = this.Data;
      return obj;
    }
  }

  public class RPCRequest
  {
    public string Version    { get; private set; }
    public string Method     { get; private set; }
    public JToken Parameters { get; private set; }
    public JToken Id         { get; private set; }
    public RPCRequest(string version, string method, JToken parameters, JToken id)
    {
      this.Version = version;
      this.Method = method;
      this.Parameters = parameters;
      this.Id = id;
    }

    public static RPCRequest FromJson(JToken req)
    {
      if (req.Type!=JTokenType.Object) throw new RPCError(RPCErrorCode.InvalidRequest);
      var obj = (JObject)req;
      JToken jsonrpc = obj["jsonrpc"];
      JToken method  = obj["method"];
      JToken args    = obj["params"];
      JToken id      = obj["id"];
      if (jsonrpc==null || jsonrpc.Type!=JTokenType.String || ((string)jsonrpc)!="2.0") {
        throw new RPCError(RPCErrorCode.InvalidRequest);
      }
      if (method==null || method.Type!=JTokenType.String) {
        throw new RPCError(RPCErrorCode.InvalidRequest);
      }
      if (args!=null && (args.Type!=JTokenType.Object && args.Type!=JTokenType.Array)) {
        throw new RPCError(RPCErrorCode.InvalidRequest);
      }
      if (obj.TryGetValue("id", out id)) {
        switch (id.Type) {
        case JTokenType.Null:
        case JTokenType.Float:
        case JTokenType.Integer:
        case JTokenType.String:
          break;
        default:
          throw new RPCError(RPCErrorCode.InvalidRequest);
        }
      }
      return new RPCRequest((string)jsonrpc, (string)method, args, id);
    }
  }

  public class RPCResponse
  {
    public JToken   Id     { get; private set; }
    public JToken   Result { get; private set; }
    public RPCError Error  { get; private set; }

    public RPCResponse(JToken id, RPCError error)
    {
      this.Id = id;
      this.Error = error;
    }

    public RPCResponse(JToken id, JToken result)
    {
      this.Id = id;
      this.Result = result;
    }

    public JToken ToJson()
    {
      var root = new JObject();
      root["jsonrpc"] = "2.0";
      root["id"] = this.Id;
      if (this.Error!=null) {
        root["error"] = this.Error.ToJson();
      }
      else {
        root["result"] = this.Result;
      }
      return root;
    }
  }

  public class JSONRPCHost
  {
    private void ProcessRequest(IOwinContext ctx, JArray results, JToken request, Func<OutputStreamType,bool> authFunc)
    {
      RPCRequest req = null;
      try {
        req = RPCRequest.FromJson(request);
      }
      catch (RPCError err) {
        results.Add(new RPCResponse(null, err).ToJson());
        return;
      }
      try {
        var methods = authFunc!=null ? this.methods.Where(method => authFunc(method.Grant)) : this.methods;
        var m = methods.FirstOrDefault(method => method.Name==req.Method);
        if (m!=null) {
          var res = m.Invoke(ctx, req.Parameters);
          if (req.Id!=null) {
            results.Add(new RPCResponse(req.Id, res).ToJson());
          }
        }
        else {
          throw new RPCError(RPCErrorCode.MethodNotFound);
        }
      }
      catch (RPCError err) {
        results.Add(new RPCResponse(req.Id, err).ToJson());
      }
    }

    private JToken ParseRequest(string request_str)
    {
      try {
        return JToken.Parse(request_str);
      }
      catch (Exception) {
        throw new RPCError(RPCErrorCode.ParseError);
      }
    }

    public JToken ProcessRequest(IOwinContext ctx, JToken req, Func<OutputStreamType,bool> authFunc)
    {
      if (req==null) return null;
      if (req.Type==JTokenType.Array) {
        var results = new JArray();
        foreach (var token in (JArray)req) {
          ProcessRequest(ctx, results, token, authFunc);
        }
        return results.Count>0 ? results : null;
      }
      else {
        var results = new JArray();
        ProcessRequest(ctx, results, req, authFunc);
        return results.Count>0 ? results.First : null;
      }
    }

    public JToken ProcessRequest(IOwinContext ctx, string request_str, Func<OutputStreamType,bool> authFunc)
    {
      JToken req;
      try {
        req = ParseRequest(request_str);
      }
      catch (RPCError err) {
        return new RPCResponse(null, err).ToJson();
      }
      return ProcessRequest(ctx, req, authFunc);
    }

    private object host;
    private RPCMethodInfo[] methods;
    public JSONRPCHost(object host)
    {
      this.host = host;
      this.methods =
        Enumerable.Concat(
          Enumerable.Repeat(this.GetType().GetMethod("GenerateAPIDescription"), 1)
          .Select(method => new RPCMethodInfo(this, method)),
          host.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
          .Where(method => Attribute.IsDefined(method, typeof(RPCMethodAttribute), true))
          .Select(method => new RPCMethodInfo(host, method))
        )
        .ToArray();
    }

    private JObject GenerateTypeSchema(Type t)
    {
      if (t.IsGenericType && t.GetGenericTypeDefinition()==typeof(Nullable<>)) {
        return GenerateTypeSchema(t.GenericTypeArguments[0]);
      }
      else if (t==typeof(string)) {
        return JObject.FromObject(new { type="string" });
      }
      else if (t==typeof(bool)) {
        return JObject.FromObject(new { type="boolean" });
      }
      else if (t==typeof(int) ||
               t==typeof(uint) ||
               t==typeof(byte) ||
               t==typeof(sbyte) ||
               t==typeof(short) ||
               t==typeof(ushort) ||
               t==typeof(long) ||
               t==typeof(ulong) ||
               t==typeof(float) ||
               t==typeof(double)) {
        return JObject.FromObject(new { type="number" });
      }
      else if (t==typeof(JObject) || t==typeof(JToken)) {
        return JObject.FromObject(new { type="object", properties=new JObject() });
      }
      else if (t==typeof(JArray)) {
        return JObject.FromObject(new { type="array", items=GenerateTypeSchema(typeof(JObject)) });
      }
      else if (t==typeof(void)) {
        return JObject.FromObject(new { type="null" });
      }
      else {
        throw new InvalidCastException();
      }
    }

    private JObject GenerateParameterDescription(string name, ParameterInfo param, Type t, string summary)
    {
      var schema = GenerateTypeSchema(t);
      if (!String.IsNullOrWhiteSpace(summary)) {
        return JObject.FromObject(new { name, summary, schema });
      }
      else {
        return JObject.FromObject(new { name, schema });
      }
    }

    private JObject GenerateParameterDescription(string name, ParameterInfo param, string summary)
    {
      return GenerateParameterDescription(name, param, param.ParameterType, summary);
    }

    private JObject GenerateParameterDescription(ParameterInfo param, string summary)
    {
      return GenerateParameterDescription(param.Name, param, param.ParameterType, summary);
    }

    [RPCMethod("rpc.discover")]
    public JToken GenerateAPIDescription()
    {
      var openrpc = "1.2.4";
      var info = JObject.FromObject(new {
        title = "PeerCastStation",
        version = "1.0.0",
      });
      var methods =
        new JArray(
          this.methods
            .Select(m => {
              var name = m.Name;
              var result = GenerateParameterDescription("result", m.Method.ReturnParameter, m.GetResultSummary());
              var summary = m.GetMethodSummary();
              var @params = new JArray(
                m.Method.GetParameters()
                .Where(p => p.ParameterType!=typeof(IOwinContext))
                .Select(p => GenerateParameterDescription(p, m.GetParameterSummary(p.Name)))
                .ToArray()
              );
              if (summary==null) {
                return JObject.FromObject(new { name, result, @params });
              }
              else {
                return JObject.FromObject(new { name, summary, result, @params });
              }
            })
            .ToArray()
        );
      return JObject.FromObject(new {
        openrpc,
        info,
        methods,
      });
    }

  }

}

