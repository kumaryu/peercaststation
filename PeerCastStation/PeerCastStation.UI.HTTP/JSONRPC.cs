using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PeerCastStation.UI.HTTP.JSONRPC
{
  public class RPCMethodInfo
  {
    public string Name { get; private set; }
    private MethodInfo method;
    public RPCMethodInfo(MethodInfo method)
    {
      this.method = method;
      var attr = Attribute.GetCustomAttributes(method).First(a => a.GetType()==typeof(RPCMethodAttribute));
      this.Name = ((RPCMethodAttribute)attr).Name;
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

    public JToken Invoke(object receiver, JToken args)
    {
      var param_infos = method.GetParameters();
      if (param_infos.Length==0) {
        try {
          return method.Invoke(receiver, new object[] {}) as JToken;
        }
        catch (TargetInvocationException e) {
          throw e.InnerException;
        }
      }
      else {
        if (args==null) throw new RPCError(RPCErrorCode.InvalidParams, "parameters required");
        var arguments = new object[param_infos.Length];
        if (args.Type==JTokenType.Array) {
          var ary = (JArray)args;
          if (param_infos.Length!=ary.Count) {
            throw new RPCError(
              RPCErrorCode.InvalidParams, 
              String.Format("Wrong number of arguments ({0} for {1})", param_infos.Length, ary.Count));
          }
          for (var i=0; i<param_infos.Length; i++) {
            try {
              arguments[i] = ToObject(param_infos[i].ParameterType, ary[i]);
            }
            catch (ArgumentException) {
              throw new RPCError(
                RPCErrorCode.InvalidParams, 
                String.Format("{0} must be {1})", param_infos[i].Name, JsonType(param_infos[i].ParameterType)));
            }
          }
        }
        else if (args.Type==JTokenType.Object) {
          var obj = (JObject)args;
          for (var i=0; i<param_infos.Length; i++) {
            JToken value;
            if (obj.TryGetValue(param_infos[i].Name, out value)) {
              try {
                arguments[i] = ToObject(param_infos[i].ParameterType, value);
              }
              catch (ArgumentException) {
                throw new RPCError(
                  RPCErrorCode.InvalidParams, 
                  String.Format("{0} must be {1})", param_infos[i].Name, JsonType(param_infos[i].ParameterType)));
              }
            }
            else if (param_infos[i].DefaultValue!=DBNull.Value) {
              arguments[i] = param_infos[i].DefaultValue;
            }
            else if (param_infos[i].IsOptional) {
              arguments[i] = null;
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
          var res = method.Invoke(receiver, arguments);
          return FromObject(method.ReturnType, res);
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
    public RPCMethodAttribute(string name)
    {
      this.Name = name;
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
    private void ProcessRequest(JArray results, JToken request)
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
        var m = methods.FirstOrDefault(method => method.Name==req.Method);
        if (m!=null) {
          var res = m.Invoke(host, req.Parameters);
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

    public JToken ProcessRequest(JToken req)
    {
      if (req==null) return null;
      if (req.Type==JTokenType.Array) {
        JArray results = new JArray();
        foreach (var token in (JArray)req) {
          ProcessRequest(results, token);
        }
        return results.Count>0 ? results : null;
      }
      else {
        JArray results = new JArray();
        ProcessRequest(results, req);
        return results.Count>0 ? results.First : null;
      }
    }

    public JToken ProcessRequest(string request_str)
    {
      JToken req;
      try {
        req = ParseRequest(request_str);
      }
      catch (RPCError err) {
        return new RPCResponse(null, err).ToJson();
      }
      return ProcessRequest(req);
    }

    private object host;
    private IEnumerable<RPCMethodInfo> methods;
    public JSONRPCHost(object host)
    {
      this.host = host;
      this.methods = host.GetType().GetMethods(
        System.Reflection.BindingFlags.Instance |
        System.Reflection.BindingFlags.Public |
        System.Reflection.BindingFlags.NonPublic).Where(method =>
          Attribute.IsDefined(method, typeof(RPCMethodAttribute), true)
        ).Select(method => new RPCMethodInfo(method));
    }
  }
}
