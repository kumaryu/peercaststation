using System;
using System.Linq;
using System.IO;
using System.Net;
using System.Collections.Generic;
using PeerCastStation.Core;
using PeerCastStation.HTTP;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PeerCastStation.UI.HTTP
{
  class APIHost
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
      application.PeerCast.OutputStreamFactories.Insert(application.PeerCast.OutputStreamFactories.Count-1, factory);
    }

    public void Stop()
    {
      application.PeerCast.OutputStreamFactories.Remove(factory);
    }

    class APIHostOutputStream
      : OutputStreamBase
    {
      APIHost owner;
      HTTPRequest request;
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
        Logger.Debug("Initialized: Remote {0}", remote_endpoint);
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
          if (args==null || (args.Type!=JTokenType.Object && args.Type!=JTokenType.Array)) {
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

      enum RPCErrorCode
      {
        ParseError     = -32700,
        InvalidRequest = -32600,
        MethodNotFound = -32601,
        InvalidParams  = -32602,
        InternalError  = -32603,
      }

      class RPCError
        : ApplicationException
      {
        public RPCErrorCode Code { get; private set; }
        public new JToken   Data { get; private set; }
        public RPCError(RPCErrorCode error)
          : this(error, null)
        {
        }

        public RPCError(RPCErrorCode error, JToken data)
          : this(error, data, error.ToString())
        {
        }

        public RPCError(RPCErrorCode error, JToken data, string message)
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

      class RPCResponse
      {
        public JToken   Id     { get; private set; }
        public JToken   Result { get; private set; }
        public RPCError Error  { get; private set; }

        public RPCResponse(JToken id, RPCError error)
        {
          this.Id = null;
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

      private JToken[] GetArgs(JToken args, params string[] names)
      {
        if (args==null) throw new RPCError(RPCErrorCode.InvalidParams);
        var res = new JToken[names.Length];
        if (args.Type==JTokenType.Array) {
          var ary = (JArray)args;
          for (var i=0; i<Math.Min(names.Length, ary.Count); i++) {
            res[i] = ary[i];
          }
        }
        else {
          for (var i=0; i<names.Length; i++) {
            res[i] = args[names[i]];
          }
        }
        return res;
      }

      private JToken GetSettings(JToken args)
      {
        var res = new JObject();
        res["maxRelays"]            = PeerCast.AccessController.MaxRelays;
        res["maxDirects"]           = PeerCast.AccessController.MaxPlays;
        res["maxRelaysPerChannel"]  = PeerCast.AccessController.MaxRelaysPerChannel;
        res["maxDirectsPerChannel"] = PeerCast.AccessController.MaxPlaysPerChannel;
        res["maxUpstreamRate"]      = PeerCast.AccessController.MaxUpstreamRate;
        return res;
      }

      private JToken SetSettings(JToken args)
      {
        if (args==null || args.Type!=JTokenType.Object) {
          throw new RPCError(RPCErrorCode.InvalidParams);
        }
        if (args["maxRelays"]!=null) {
          PeerCast.AccessController.MaxRelays = (int)args["maxRelays"];
        }
        if (args["maxRelaysPerChannel"]!=null) {
          PeerCast.AccessController.MaxRelaysPerChannel = (int)args["maxRelaysPerChannel"];
        }
        if (args["maxDirects"]!=null) {
          PeerCast.AccessController.MaxPlays = (int)args["maxDirects"];
        }
        if (args["maxDirectsPerChannel"]!=null) {
          PeerCast.AccessController.MaxPlaysPerChannel = (int)args["maxDirectsPerChannel"];
        }
        if (args["maxUpstreamRate"]!=null) {
          PeerCast.AccessController.MaxUpstreamRate = (int)args["maxUpstreamRate"];
        }
        return null;
      }

      private JToken GetChannels(JToken args)
      {
        return new JArray(PeerCast.Channels.Select(c => c.ChannelID.ToString("N").ToUpper()));
      }

      private Channel GetChannel(JToken args)
      {
        if (args==null) throw new RPCError(RPCErrorCode.InvalidParams);
        Guid channel_id;
        try {
          if (args.Type==JTokenType.Array) channel_id = new Guid((string)args[0]);
          else                             channel_id = new Guid((string)args["channelId"]);
        }
        catch (Exception) {
          throw new RPCError(RPCErrorCode.InvalidParams);
        }
        return PeerCast.Channels.FirstOrDefault(c => c.ChannelID==channel_id);
      }

      private JToken GetChannelInfo(JToken args)
      {
        var channel = GetChannel(args);
        if (channel!=null) {
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
          track["album"]   = channel.ChannelTrack.Album;
          track["creator"] = channel.ChannelTrack.Creator;
          track["url"]     = channel.ChannelTrack.URL;
          var res = new JObject();
          res["info"] = info;
          res["track"] = track;
          return res;
        }
        else {
          return null;
        }
      }

      private JToken SetChannelInfo(JToken args)
      {
        var channel = GetChannel(args);
        JToken info;
        JToken track;
        if (args.Type==JTokenType.Array) {
          info  = args[1];
          track = args[2];
        }
        else {
          info  = args["info"];
          track = args["track"];
        }
        if (info!=null  && info.Type!=JTokenType.Object)  throw new RPCError(RPCErrorCode.InvalidParams);
        if (track!=null && track.Type!=JTokenType.Object) throw new RPCError(RPCErrorCode.InvalidParams);
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
            if (track["album"]!=null)   new_track.SetChanTrackAlbum((string)track["album"]);
            if (track["creator"]!=null) new_track.SetChanTrackCreator((string)track["creator"]);
            if (track["url"]!=null)     new_track.SetChanTrackURL((string)track["url"]);
            channel.ChannelTrack = new ChannelTrack(new_track);
          }
        }
        return null;
      }

      private JToken StopChannel(JToken args)
      {
        var channel = GetChannel(args);
        if (channel!=null) {
          PeerCast.CloseChannel(channel);
        }
        return null;
      }

      private JToken BumpChannel(JToken args)
      {
        var channel = GetChannel(args);
        if (channel!=null) {
          channel.Reconnect();
        }
        return null;
      }

      private JToken GetChannelOutputs(JToken args)
      {
        var channel = GetChannel(args);
        if (channel!=null) {
          return new JArray(channel.OutputStreams.Select(os => {
            var res = new JObject();
            res["id"]   = os.GetHashCode();
            res["name"] = os.ToString();
            return res;
          }));
        }
        else {
          return null;
        }
      }

      private JToken StopChannelOutput(JToken args)
      {
        var channel = GetChannel(args);
        if (channel!=null) {
          int id = -1;
          if (args.Type==JTokenType.Array) {
            if (args[1]!=null && args[1].Type!=JTokenType.Integer) {
              throw new RPCError(RPCErrorCode.InvalidParams);
            }
            id = (int)args[1];
          }
          else {
            if (args["id"]!=null && args["id"].Type!=JTokenType.Integer) {
              throw new RPCError(RPCErrorCode.InvalidParams);
            }
            id = (int)args["id"];
          }
          var output_stream = channel.OutputStreams.FirstOrDefault(os => os.GetHashCode()==id);
          if (output_stream!=null) {
            channel.RemoveOutputStream(output_stream);
            output_stream.Stop();
          }
        }
        return null;
      }

      private JToken GetContentReaders(JToken args)
      {
        return new JArray(PeerCast.ContentReaders.Select(reader => {
          var res = new JObject();
          res["id"]   = reader.GetHashCode();
          res["name"] = reader.Name;
          return res;
        }).ToArray());
      }

      private JToken GetYellowPageProtocols(JToken args)
      {
        return new JArray(PeerCast.YellowPageFactories.Select(protocol => {
          var res = new JObject();
          res["id"]   = protocol.GetHashCode();
          res["name"] = protocol.Name;
          return res;
        }).ToArray());
      }

      private JToken GetYellowPages(JToken args)
      {
        return new JArray(PeerCast.YellowPages.Select(yp => {
          var res = new JObject();
          res["id"]   = yp.GetHashCode();
          res["name"] = yp.Name;
          res["uri"]  = yp.Uri.ToString();
          return res;
        }).ToArray());
      }

      private JToken AddYellowPage(JToken args)
      {
        var ary = GetArgs(args, "protocol", "name", "uri");
        var protocol_id = (int?)ary[0];
        var protocol = PeerCast.YellowPageFactories.FirstOrDefault(p => protocol_id==p.GetHashCode());
        if (protocol!=null) {
          var yp = PeerCast.AddYellowPage(protocol.Name, (string)ary[1], new Uri((string)ary[2]));
          var res = new JObject();
          res["id"]   = yp.GetHashCode();
          res["name"] = yp.Name;
          res["uri"]  = yp.Uri.ToString();
          return res;
        }
        else {
          throw new RPCError(RPCErrorCode.InvalidParams, null, "Protocol Not Found");
        }
      }

      private IYellowPageClient GetYellowPage(JToken args)
      {
        if (args==null) throw new RPCError(RPCErrorCode.InvalidParams);
        int yp_id;
        try {
          if (args.Type==JTokenType.Array) yp_id = (int)args[0];
          else                             yp_id = (int)args["id"];
        }
        catch (Exception) {
          throw new RPCError(RPCErrorCode.InvalidParams);
        }
        return PeerCast.YellowPages.FirstOrDefault(yp => yp.GetHashCode()==yp_id);
      }

      private JToken RemoveYellowPage(JToken args)
      {
        var yp = GetYellowPage(args);
        if (yp!=null) {
          PeerCast.RemoveYellowPage(yp);
        }
        return null;
      }

      private JToken GetListeners(JToken args)
      {
        return new JArray(PeerCast.OutputListeners.Select(ol => {
          var res = new JObject();
          res["address"] = ol.LocalEndPoint.Address.ToString();
          res["port"]    = ol.LocalEndPoint.Port;
          return res;
        }).ToArray());
      }

      private JToken AddListener(JToken args)
      {
        var ary = GetArgs(args, "address", "port");
        IPAddress addr;
        if (IPAddress.TryParse((string)ary[0], out addr)) {
          var endpoint = new IPEndPoint(addr, (int)ary[1]);
          PeerCast.StartListen(endpoint);
          return null;
        }
        else {
          throw new RPCError(RPCErrorCode.InvalidParams);
        }
      }

      private JToken RemoveListener(JToken args)
      {
        var ary = GetArgs(args, "address", "port");
        IPAddress addr;
        if (IPAddress.TryParse((string)ary[0], out addr)) {
          var endpoint = new IPEndPoint(addr, (int)ary[1]);
          var listener = PeerCast.OutputListeners.FirstOrDefault(ol => ol.LocalEndPoint.Equals(endpoint));
          if (listener!=null) {
            PeerCast.StopListen(listener);
          }
          return null;
        }
        else {
          throw new RPCError(RPCErrorCode.InvalidParams);
        }
      }

      private JToken BroadcastChannel(JToken args)
      {
        var ary = GetArgs(args, "yp", "source", "contentReader", "channelInfo", "channelTrack");
        if (ary[0]==null || ary[1]==null || ary[2]==null || ary[3]==null) throw new RPCError(RPCErrorCode.InvalidParams);
        var yp             = PeerCast.YellowPages.FirstOrDefault(y => y.GetHashCode()==(int)ary[0]);
        var source         = new Uri((string)ary[1]);
        var content_reader = PeerCast.ContentReaders.FirstOrDefault(reader => reader.GetHashCode()==(int)ary[2]);
        var info           = ary[3];
        var track          = ary[4];

        if (yp==null) throw new RPCError(RPCErrorCode.InvalidParams, null, "Yellow page not found");
        if (content_reader==null) throw new RPCError(RPCErrorCode.InvalidParams, null, "Content reader not found");

        var new_info = new AtomCollection();
        if (info["name"]!=null)    new_info.SetChanInfoName(   (string)info["name"]);
        if (info["url"]!=null)     new_info.SetChanInfoURL(    (string)info["url"]);
        if (info["genre"]!=null)   new_info.SetChanInfoGenre(  (string)info["genre"]);
        if (info["desc"]!=null)    new_info.SetChanInfoDesc(   (string)info["desc"]);
        if (info["comment"]!=null) new_info.SetChanInfoComment((string)info["comment"]);
        var channel_info  = new ChannelInfo(new_info);
        if (channel_info.Name==null || channel_info.Name=="") {
          throw new RPCError(RPCErrorCode.InvalidParams, null, "Channel name must not be empty");
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
          JToken res;
          switch (req.Method) {
          case "getSettings": res = GetSettings(req.Parameters); break;
          case "setSettings": res = SetSettings(req.Parameters); break;
          case "getChannels": res = GetChannels(req.Parameters); break;
          case "getChannelInfo": res = GetChannelInfo(req.Parameters); break;
          case "setChannelInfo": res = SetChannelInfo(req.Parameters); break;
          case "stopChannel": res = StopChannel(req.Parameters); break;
          case "bumpChannel": res = BumpChannel(req.Parameters); break;
          case "getChannelOutputs": res = GetChannelOutputs(req.Parameters); break;
          case "stopChannelOutput": res = StopChannelOutput(req.Parameters); break;
          case "broadcastChannel": res = BroadcastChannel(req.Parameters); break;
          case "getListeners": res = GetListeners(req.Parameters); break;
          case "addListener": res = AddListener(req.Parameters); break;
          case "removeListener": res = RemoveListener(req.Parameters); break;
          case "getYellowPageProtocols": res = GetYellowPageProtocols(req.Parameters); break;
          case "getYellowPages":    res = GetYellowPages(req.Parameters); break;
          case "addYellowPage":     res = AddYellowPage(req.Parameters); break;
          case "removeYellowpage":  res = RemoveYellowPage(req.Parameters); break;
          case "getContentReaders": res = GetContentReaders(req.Parameters); break;
          default:
            throw new RPCError(RPCErrorCode.MethodNotFound);
          }
          if (req.Id!=null) {
            results.Add(new RPCResponse(req.Id, res).ToJson());
          }
        }
        catch (RPCError err) {
          results.Add(new RPCResponse(req.Id, err).ToJson());
        }
      }

      protected override void OnStarted()
      {
        base.OnStarted();
        Logger.Debug("Started");
        try {
          if (this.request.Method!="HEAD" &&
              this.request.Method!="GET" &&
              this.request.Method!="POST") {
            throw new HTTPError(HttpStatusCode.MethodNotAllowed);
          }
        }
        catch (HTTPError err) {
          Send(HTTPUtils.CreateResponseHeader(err.StatusCode, new Dictionary<string, string> { }));
        }
      }

      protected override void OnIdle()
      {
        base.OnIdle();
        JToken req = null;
        if (Recv(stream => { req = JToken.Load(new JsonTextReader(new StreamReader(stream))); })) {
          if (req.Type==JTokenType.Array) {
            JArray results = new JArray();
            foreach (var token in (JArray)req) {
              ProcessRequest(results, token);
            }
            if (results.Count>0) {
              Send(results);
            }
          }
          else {
            JArray results = new JArray();
            ProcessRequest(results, req);
            if (results.Count>0) {
              Send(results.First);
            }
          }
          Stop();
        }
      }

      private void Send(string str)
      {
        Send(System.Text.Encoding.UTF8.GetBytes(str));
      }

      private void Send(JToken token)
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

    class APIHostOutputStreamFactory
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
        if (res!=null && res.Uri.AbsolutePath=="/api") {
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
