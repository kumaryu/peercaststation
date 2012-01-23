using System;
using System.Linq;
using System.IO;
using System.Net;
using System.Collections.Generic;
using PeerCastStation.Core;
using PeerCastStation.HTTP;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Reflection;

namespace PeerCastStation.UI.HTTP
{
  public class APIHost
    : MarshalByRefObject,
      IUserInterface
  {
    public string Name { get { return "HTTP API Host UI"; } }

    APIHostOutputStreamFactory factory;
    PeerCastApplication application;
    public void Start(PeerCastApplication app)
    {
      application = app;
      factory = new APIHostOutputStreamFactory(this, app.PeerCast);
      if (application.PeerCast.OutputStreamFactories.Count>0) {
        application.PeerCast.OutputStreamFactories.Insert(application.PeerCast.OutputStreamFactories.Count-1, factory);
      }
      else {
        application.PeerCast.OutputStreamFactories.Add(factory);
      }
    }

    public void Stop()
    {
      application.PeerCast.OutputStreamFactories.Remove(factory);
    }

    public class APIHostOutputStream
      : OutputStreamBase
    {
      APIHost owner;
      HTTPRequest request;
      IEnumerable<RPCMethodInfo> methods;
      public APIHostOutputStream(
        APIHost owner,
        PeerCast peercast,
        Stream input_stream,
        Stream output_stream,
        EndPoint remote_endpoint,
        HTTPRequest request)
        : base(peercast, input_stream, output_stream, remote_endpoint, null, null)
      {
        this.owner   = owner;
        this.request = request;
        methods = this.GetType().GetMethods(
          System.Reflection.BindingFlags.Instance |
          System.Reflection.BindingFlags.NonPublic).Where(method =>
            Attribute.IsDefined(method, typeof(RPCMethodAttribute), true)
          ).Select(method => new RPCMethodInfo(method));
        Logger.Debug("Initialized: Remote {0}", remote_endpoint);
      }

      class RPCMethodInfo
      {
        public string Name { get; private set; }
        private MethodInfo method;
        public RPCMethodInfo(MethodInfo method)
        {
          this.method = method;
          this.Name = ((RPCMethodAttribute)Attribute.GetCustomAttribute(method, typeof(RPCMethodAttribute), true)).Name;
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
                catch (ArgumentException e) {
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
                  catch (ArgumentException e) {
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

      class RPCMethodAttribute : Attribute
      {
        public string Name { get; private set; }
        public RPCMethodAttribute(string name)
        {
          this.Name = name;
        }
      }

      enum RPCErrorCode
      {
        ParseError     = -32700,
        InvalidRequest = -32600,
        MethodNotFound = -32601,
        InvalidParams  = -32602,
        InternalError  = -32603,

        ChannelNotFound = -1,
      }

      class RPCError
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

      class RPCRequest
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

      class RPCResponse
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

      private JToken[] GetArgs(JToken args, int offset, params string[] names)
      {
        if (args==null) throw new RPCError(RPCErrorCode.InvalidParams);
        var res = new JToken[names.Length];
        if (args.Type==JTokenType.Array) {
          var ary = (JArray)args;
          for (var i=0; i<Math.Min(names.Length, ary.Count); i++) {
            res[i] = ary[i+offset];
          }
        }
        else {
          for (var i=0; i<names.Length; i++) {
            res[i] = args[names[i]];
          }
        }
        return res;
      }

      private JToken[] GetArgs(JToken args, params string[] names)
      {
        return GetArgs(args, 0, names);
      }

      [RPCMethod("getVersionInfo")]
      private JObject GetVersionInfo()
      {
        var res = new JObject();
        res["agentName"]  = PeerCast.AgentName;
        res["apiVersion"] = "1.0.0";
        res["jsonrpc"]    = "2.0";
        return res;
      }

      [RPCMethod("getSettings")]
      private JToken GetSettings()
      {
        var res = new JObject();
        res["maxRelays"]            = PeerCast.AccessController.MaxRelays;
        res["maxDirects"]           = PeerCast.AccessController.MaxPlays;
        res["maxRelaysPerChannel"]  = PeerCast.AccessController.MaxRelaysPerChannel;
        res["maxDirectsPerChannel"] = PeerCast.AccessController.MaxPlaysPerChannel;
        res["maxUpstreamRate"]      = PeerCast.AccessController.MaxUpstreamRate;
        return res;
      }

      [RPCMethod("setSettings")]
      private void SetSettings(JObject settings)
      {
        if (settings["maxRelays"]!=null) {
          PeerCast.AccessController.MaxRelays = (int)settings["maxRelays"];
        }
        if (settings["maxRelaysPerChannel"]!=null) {
          PeerCast.AccessController.MaxRelaysPerChannel = (int)settings["maxRelaysPerChannel"];
        }
        if (settings["maxDirects"]!=null) {
          PeerCast.AccessController.MaxPlays = (int)settings["maxDirects"];
        }
        if (settings["maxDirectsPerChannel"]!=null) {
          PeerCast.AccessController.MaxPlaysPerChannel = (int)settings["maxDirectsPerChannel"];
        }
        if (settings["maxUpstreamRate"]!=null) {
          PeerCast.AccessController.MaxUpstreamRate = (int)settings["maxUpstreamRate"];
        }
      }

      [RPCMethod("getChannels")]
      private JArray GetChannels()
      {
        return new JArray(PeerCast.Channels.Select(c => c.ChannelID.ToString("N").ToUpper()));
      }

      private Channel GetChannel(string channel_id)
      {
        if (channel_id==null) throw new RPCError(RPCErrorCode.InvalidParams);
        Guid cid;
        try {
          cid = new Guid(channel_id);
        }
        catch (Exception) {
          throw new RPCError(RPCErrorCode.InvalidParams, "Invalid channelId");
        }
        var channel = PeerCast.Channels.FirstOrDefault(c => c.ChannelID==cid);
        if (channel==null) {
          throw new RPCError(RPCErrorCode.ChannelNotFound, "Channel not found");
        }
        else {
          return channel;
        }
      }

      [RPCMethod("getChannelStatus")]
      private JObject GetChannelStatus(string channelId)
      {
        var channel = GetChannel(channelId);
        var res = new JObject();
        res["status"]         = channel.Status.ToString();
        res["uptime"]         = (int)channel.Uptime.TotalSeconds;
        res["totalRelays"]    = channel.TotalRelays;
        res["totalDirects"]   = channel.TotalDirects;
        res["isBroadcasting"] = channel.BroadcastID==PeerCast.BroadcastID;
        res["isRelayFull"]    = channel.IsRelayFull;
        res["isDirectFull"]   = channel.IsRelayFull;
        return res;
      }

      [RPCMethod("getChannelInfo")]
      private JObject GetChannelInfo(string channelId)
      {
        var channel = GetChannel(channelId);
        var info = new JObject();
        info["name"]        = channel.ChannelInfo.Name;
        info["url"]         = channel.ChannelInfo.URL;
        info["genre"]       = channel.ChannelInfo.Genre;
        info["desc"]        = channel.ChannelInfo.Desc;
        info["comment"]     = channel.ChannelInfo.Comment;
        info["bitrate"]     = channel.ChannelInfo.Bitrate;
        info["contentType"] = channel.ChannelInfo.ContentType;
        info["mimeType"]    = channel.ChannelInfo.MIMEType;
        var track = new JObject();
        track["name"]    = channel.ChannelTrack.Name;
        track["genre"]   = channel.ChannelTrack.Genre;
        track["album"]   = channel.ChannelTrack.Album;
        track["creator"] = channel.ChannelTrack.Creator;
        track["url"]     = channel.ChannelTrack.URL;
        var res = new JObject();
        res["info"] = info;
        res["track"] = track;
        return res;
      }

      [RPCMethod("setChannelInfo")]
      private void SetChannelInfo(string channelId, JObject info, JObject track)
      {
        var channel = GetChannel(channelId);
        if (channel!=null && channel.BroadcastID==PeerCast.BroadcastID) {
          if (info!=null) {
            var new_info = new AtomCollection(channel.ChannelInfo.Extra);
            if (info["name"]!=null)    new_info.SetChanInfoName((string)info["name"]);
            if (info["url"]!=null)     new_info.SetChanInfoURL((string)info["url"]);
            if (info["genre"]!=null)   new_info.SetChanInfoGenre((string)info["genre"]);
            if (info["desc"]!=null)    new_info.SetChanInfoDesc((string)info["desc"]);
            if (info["comment"]!=null) new_info.SetChanInfoComment((string)info["comment"]);
            channel.ChannelInfo = new ChannelInfo(new_info);
          }
          if (track!=null) {
            var new_track = new AtomCollection(channel.ChannelTrack.Extra);
            if (track["name"]!=null)    new_track.SetChanTrackTitle((string)track["name"]);
            if (track["genre"]!=null)   new_track.SetChanTrackGenre((string)track["genre"]);
            if (track["album"]!=null)   new_track.SetChanTrackAlbum((string)track["album"]);
            if (track["creator"]!=null) new_track.SetChanTrackCreator((string)track["creator"]);
            if (track["url"]!=null)     new_track.SetChanTrackURL((string)track["url"]);
            channel.ChannelTrack = new ChannelTrack(new_track);
          }
        }
      }

      [RPCMethod("stopChannel")]
      private void StopChannel(string channelId)
      {
        var channel = GetChannel(channelId);
        PeerCast.CloseChannel(channel);
      }

      [RPCMethod("bumpChannel")]
      private void BumpChannel(string channelId)
      {
        var channel = GetChannel(channelId);
        channel.Reconnect();
      }

      [RPCMethod("getChannelOutputs")]
      private JArray GetChannelOutputs(string channelId)
      {
        var channel = GetChannel(channelId);
        return new JArray(channel.OutputStreams.Select(os => {
          var res = new JObject();
          res["id"]   = os.GetHashCode();
          res["name"] = os.ToString();
          return res;
        }));
      }

      [RPCMethod("stopChannelOutput")]
      private void StopChannelOutput(string channelId, int id)
      {
        var channel = GetChannel(channelId);
        var output_stream = channel.OutputStreams.FirstOrDefault(os => os.GetHashCode()==id);
        if (output_stream!=null) {
          channel.RemoveOutputStream(output_stream);
          output_stream.Stop();
        }
      }

      [RPCMethod("getContentReaders")]
      private JArray GetContentReaders()
      {
        return new JArray(PeerCast.ContentReaders.Select(reader => {
          var res = new JObject();
          res["id"]   = reader.GetHashCode();
          res["name"] = reader.Name;
          return res;
        }).ToArray());
      }

      [RPCMethod("getYellowPageProtocols")]
      private JArray GetYellowPageProtocols()
      {
        return new JArray(PeerCast.YellowPageFactories.Select(protocol => {
          var res = new JObject();
          res["id"] = protocol.GetHashCode();
          res["name"] = protocol.Name;
          return res;
        }).ToArray());
      }

      [RPCMethod("getYellowPages")]
      private JArray GetYellowPages()
      {
        return new JArray(PeerCast.YellowPages.Select(yp => {
          var res = new JObject();
          res["id"]   = yp.GetHashCode();
          res["name"] = yp.Name;
          res["uri"]  = yp.Uri.ToString();
          return res;
        }).ToArray());
      }

      [RPCMethod("addYellowPage")]
      private JObject AddYellowPage(int protocol, string name, string uri)
      {
        var factory = PeerCast.YellowPageFactories.FirstOrDefault(p => protocol==p.GetHashCode());
        if (factory==null) throw new RPCError(RPCErrorCode.InvalidParams, "protocol Not Found");
        if (name==null) throw new RPCError(RPCErrorCode.InvalidParams, "name must be String");
        Uri yp_uri;
        try {
          yp_uri = new Uri(uri);
        }
        catch (ArgumentNullException) {
          throw new RPCError(RPCErrorCode.InvalidParams, "uri must be String");
        }
        catch (UriFormatException) {
          throw new RPCError(RPCErrorCode.InvalidParams, "Invalid uri");
        }
        var yp = PeerCast.AddYellowPage(factory.Name, name, yp_uri);
        var res = new JObject();
        res["id"]   = yp.GetHashCode();
        res["name"] = yp.Name;
        res["uri"]  = yp.Uri.ToString();
        return res;
      }

      [RPCMethod("removeYellowPage")]
      private void RemoveYellowPage(int id)
      {
        var yp = PeerCast.YellowPages.FirstOrDefault(p => p.GetHashCode()==id);
        if (yp!=null) {
          PeerCast.RemoveYellowPage(yp);
        }
      }

      [RPCMethod("getListeners")]
      private JArray GetListeners()
      {
        return new JArray(PeerCast.OutputListeners.Select(ol => {
          var res = new JObject();
          res["id"]      = ol.GetHashCode();
          res["address"] = ol.LocalEndPoint.Address.ToString();
          res["port"]    = ol.LocalEndPoint.Port;
          return res;
        }).ToArray());
      }

      [RPCMethod("addListener")]
      private JObject AddListener(string address, int port)
      {
        IPAddress addr;
        OutputListener listener;
        if (address==null) {
          var endpoint = new IPEndPoint(IPAddress.Any, port);
          listener = PeerCast.StartListen(endpoint);

        }
        else if (IPAddress.TryParse(address, out addr)) {
          var endpoint = new IPEndPoint(addr, port);
          listener = PeerCast.StartListen(endpoint);
        }
        else {
          throw new RPCError(RPCErrorCode.InvalidParams, "Invalid ip address");
        }
        var res = new JObject();
        res["id"]      = listener.GetHashCode();
        res["address"] = listener.LocalEndPoint.Address.ToString();
        res["port"]    = listener.LocalEndPoint.Port;
        return res;
      }

      [RPCMethod("removeListener")]
      private void RemoveListener(int id)
      {
        foreach (var listener in PeerCast.OutputListeners.Where(ol => ol.GetHashCode()==id)) {
          PeerCast.StopListen(listener);
        }
      }

      [RPCMethod("broadcastChannel")]
      private string BroadcastChannel(int? yellowPage, string sourceUri, int contentReader, JObject info, JObject track)
      {
        IYellowPageClient yp = null;
        if (yellowPage.HasValue) {
          yp = PeerCast.YellowPages.FirstOrDefault(y => y.GetHashCode()==yellowPage.Value);
          if (yp==null) throw new RPCError(RPCErrorCode.InvalidParams, "Yellow page not found");
        }
        if (sourceUri==null) throw new RPCError(RPCErrorCode.InvalidParams, "source uri required");
        Uri source;
        try {
          source = new Uri(sourceUri);
        }
        catch (UriFormatException) {
          throw new RPCError(RPCErrorCode.InvalidParams, "Invalid Uri");
        }
        var content_reader = PeerCast.ContentReaders.FirstOrDefault(reader => reader.GetHashCode()==contentReader);
        if (content_reader==null) throw new RPCError(RPCErrorCode.InvalidParams, "Content reader not found");

        var new_info = new AtomCollection();
        if (info!=null) {
          if (info["name"]!=null)    new_info.SetChanInfoName(   (string)info["name"]);
          if (info["url"]!=null)     new_info.SetChanInfoURL(    (string)info["url"]);
          if (info["genre"]!=null)   new_info.SetChanInfoGenre(  (string)info["genre"]);
          if (info["desc"]!=null)    new_info.SetChanInfoDesc(   (string)info["desc"]);
          if (info["comment"]!=null) new_info.SetChanInfoComment((string)info["comment"]);
        }
        var channel_info  = new ChannelInfo(new_info);
        if (channel_info.Name==null || channel_info.Name=="") {
          throw new RPCError(RPCErrorCode.InvalidParams, "Channel name must not be empty");
        }
        var channel_id = Utils.CreateChannelID(
          PeerCast.BroadcastID,
          channel_info.Name,
          channel_info.Genre ?? "",
          source.ToString());
        var channel = PeerCast.BroadcastChannel(yp, channel_id, channel_info, source, content_reader);
        if (track!=null) {
          var new_track = new AtomCollection(channel.ChannelTrack.Extra);
          if (track["name"]!=null)    new_track.SetChanTrackTitle((string)track["name"]);
          if (track["genre"]!=null)   new_track.SetChanTrackGenre((string)track["genre"]);
          if (track["album"]!=null)   new_track.SetChanTrackAlbum((string)track["album"]);
          if (track["creator"]!=null) new_track.SetChanTrackCreator((string)track["creator"]);
          if (track["url"]!=null)     new_track.SetChanTrackURL((string)track["url"]);
          channel.ChannelTrack = new ChannelTrack(new_track);
        }
        return channel.ChannelID.ToString("N").ToUpper();
      }

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
            var res = m.Invoke(this, req.Parameters);
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

      public static readonly int RequestLimit = 64*1024;
      public static readonly int TimeoutLimit = 5000;
      private int bodyLength = -1;
      private System.Diagnostics.Stopwatch timeoutWatch = new System.Diagnostics.Stopwatch();
      protected override void OnStarted()
      {
        base.OnStarted();
        Logger.Debug("Started");
        try {
          if (this.request.Method=="HEAD" || this.request.Method=="GET") {
            var token = GetVersionInfo();
            var body = System.Text.Encoding.UTF8.GetBytes(token.ToString());
            var parameters = new Dictionary<string, string> {
              {"Content-Type",   "application/json" },
              {"Content-Length", body.Length.ToString() },
            };
            Send(HTTPUtils.CreateResponseHeader(HttpStatusCode.OK, parameters));
            if (this.request.Method!="HEAD") {
              Send(body);
            }
            Stop();
          }
          else if (this.request.Method=="POST") {
            string length;
            if (request.Headers.TryGetValue("CONTENT-LENGTH", out length)) {
              int len;
              if (int.TryParse(length, out len) && len>=0 && len<=RequestLimit) {
                bodyLength = len;
                timeoutWatch.Start();
              }
              else {
                throw new HTTPError(HttpStatusCode.BadRequest);
              }
            }
            else {
              throw new HTTPError(HttpStatusCode.LengthRequired);
            }
          }
          else {
            throw new HTTPError(HttpStatusCode.MethodNotAllowed);
          }
        }
        catch (HTTPError err) {
          Send(HTTPUtils.CreateResponseHeader(err.StatusCode, new Dictionary<string, string> { }));
          Stop();
        }
      }

      protected override void OnIdle()
      {
        base.OnIdle();
        if (this.request.Method!="POST" || bodyLength<0) return;
        string request_str = null;
        if (Recv(stream => {
          if (stream.Length-stream.Position<bodyLength) throw new EndOfStreamException();
          var buf = new byte[stream.Length-stream.Position];
          stream.Read(buf, 0, (int)(stream.Length-stream.Position));
          request_str = System.Text.Encoding.UTF8.GetString(buf);
        })) {
          JToken req = null;
          try {
            req = JToken.Parse(request_str);
          }
          catch (Exception) {
            SendJson(new RPCResponse(null, new RPCError(RPCErrorCode.ParseError)).ToJson());
          }
          if (req!=null) {
            if (req.Type==JTokenType.Array) {
              JArray results = new JArray();
              foreach (var token in (JArray)req) {
                ProcessRequest(results, token);
              }
              if (results.Count>0) {
                SendJson(results);
              }
              else {
                Send(HTTPUtils.CreateResponseHeader(HttpStatusCode.NoContent, new Dictionary<string, string>()));
              }
            }
            else {
              JArray results = new JArray();
              ProcessRequest(results, req);
              if (results.Count>0) {
                SendJson(results.First);
              }
              else {
                Send(HTTPUtils.CreateResponseHeader(HttpStatusCode.NoContent, new Dictionary<string, string>()));
              }
            }
          }
          Stop();
        }
        else if (timeoutWatch.ElapsedMilliseconds>TimeoutLimit) {
          Send(HTTPUtils.CreateResponseHeader(HttpStatusCode.RequestTimeout, new Dictionary<string, string>()));
          Stop();
        }
      }

      private void Send(string str)
      {
        Send(System.Text.Encoding.UTF8.GetBytes(str));
      }

      private void SendJson(JToken token)
      {
        var body = System.Text.Encoding.UTF8.GetBytes(token.ToString());
        var parameters = new Dictionary<string, string> {
          {"Content-Type",   "application/json" },
          {"Content-Length", body.Length.ToString() },
        };
        Send(HTTPUtils.CreateResponseHeader(HttpStatusCode.OK, parameters));
        Send(body);
      }

      protected override void OnStopped()
      {
        Logger.Debug("Finished");
       	base.OnStopped();
      }

      public override OutputStreamType OutputStreamType
      {
        get { return OutputStreamType.Interface; }
      }
    }

    public class APIHostOutputStreamFactory
      : OutputStreamFactoryBase
    {
      public override string Name
      {
        get { return "API Host UI"; }
      }

      public override IOutputStream Create(
        Stream input_stream,
        Stream output_stream,
        EndPoint remote_endpoint,
        Guid channel_id,
        byte[] header)
      {
        HTTPRequest request = null;
        using (var stream = new MemoryStream(header)) {
          try {
            request = HTTPRequestReader.Read(stream);
          }
          catch (EndOfStreamException) {
          }
        }
        return new APIHostOutputStream(owner, PeerCast, input_stream, output_stream, remote_endpoint, request);
      }

      public override Guid? ParseChannelID(byte[] header)
      {
        HTTPRequest res = null;
        using (var stream = new MemoryStream(header)) {
          try {
            res = HTTPRequestReader.Read(stream);
          }
          catch (EndOfStreamException) {
          }
        }
        if (res!=null && res.Uri.AbsolutePath=="/api/1") {
          return Guid.Empty;
        }
        else {
          return null;
        }
      }

      APIHost owner;
      public APIHostOutputStreamFactory(APIHost owner, PeerCast peercast)
        : base(peercast)
      {
        this.owner = owner;
      }
    }
  }

  [Plugin(PluginType.UserInterface)]
  public class APIHostFactory
    : IUserInterfaceFactory
  {
    public string Name { get { return "HTTP API Host UI"; } }

    public IUserInterface CreateUserInterface()
    {
      return new APIHost();
    }
  }
}
