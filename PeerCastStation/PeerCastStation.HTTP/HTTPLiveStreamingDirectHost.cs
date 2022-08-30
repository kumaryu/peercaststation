using PeerCastStation.Core;
using PeerCastStation.Core.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.HTTP
{
  public class HTTPLiveStreamingDirectOwinApp
  {
    class M3U8PlayList
    {
      private string scheme;
      public string MIMEType { get { return "application/vnd.apple.mpegurl"; } }
      public Channel Channel { get; private set; }

      public M3U8PlayList(string? scheme, Channel channel)
      {
        this.scheme = String.IsNullOrEmpty(scheme) ? "http" : scheme.ToLowerInvariant();
        Channel = channel;
      }

      public async Task<byte[]> CreatePlayListAsync(Uri baseuri, IEnumerable<KeyValuePair<string,string>> parameters, HTTPLiveStreamingSegmenter segmenter, CancellationToken cancellationToken)
      {
        var res = new System.Text.StringBuilder();
        var segments = await segmenter.GetSegmentsAsync(cancellationToken).ConfigureAwait(false);
        res.AppendLine("#EXTM3U");
        res.AppendLine("#EXT-X-VERSION:3");
        res.AppendLine("#EXT-X-ALLOW-CACHE:NO");
        res.AppendLine($"#EXT-X-TARGETDURATION:{segmenter.TargetDuration}");
        res.AppendLine($"#EXT-X-MEDIA-SEQUENCE:{segments.FirstOrDefault().Index}");
        var queries = String.Join("&", parameters.Select(kv => Uri.EscapeDataString(kv.Key) + "=" + Uri.EscapeDataString(kv.Value)));
        foreach (var seg in segments) {
          if (seg.Data==null) {
            res.AppendLine("#EXT-X-ENDLIST");
          }
          else {
            var url = new UriBuilder(new Uri(baseuri, Channel.ChannelID.ToString("N").ToUpperInvariant() + String.Format("/{0:00000}.ts", seg.Index)));
            url.Scheme = scheme;
            if (queries!="") {
              url.Query = queries;
            }
            res.AppendLine("#EXTINF:" + seg.Duration.ToString("F2") + ",");
            res.AppendLine(url.ToString());
          }
        }
        return System.Text.Encoding.UTF8.GetBytes(res.ToString());
      }
    }

    interface IContextHolder<TContext>
      : IDisposable
    {
        TContext Context { get; }
    }

    class SharedContextCollection<TKey,TContext>
      where TKey: notnull
      where TContext: IDisposable
    {
      class SharedContext
      {
        public TContext Context { get; }
        private int referenceCount = 0;

        public SharedContext(SharedContextCollection<TKey,TContext> owner, TKey key, TContext context)
        {
          Context = context;
        }

        internal int AddRef()
        {
          return ++referenceCount;
        }

        internal int Release()
        {
          if (--referenceCount==0) {
            Context.Dispose();
          }
          return referenceCount;
        }
      }

      class ContextHolder
        : IContextHolder<TContext>
      {
        public ContextHolder(SharedContextCollection<TKey, TContext> owner, TKey key, SharedContext sharedContext, Func<TContext, TimeSpan>? releaseDelayFunc = null)
        {
          this.owner = owner;
          Key = key;
          sharedContext.AddRef();
          this.sharedContext = sharedContext;
          this.releaseDelayFunc = releaseDelayFunc;
        }

        SharedContextCollection<TKey, TContext> owner;
        SharedContext sharedContext;
        bool disposed = false;
        Func<TContext, TimeSpan>? releaseDelayFunc;

        public TKey Key { get; }

        public TContext Context {
          get {
            if (disposed) {
              throw new ObjectDisposedException(GetType().Name);
            }
            return sharedContext.Context;
          }
        }

        public void Dispose()
        {
          if (!disposed) {
            disposed = true;
            var delay = releaseDelayFunc?.Invoke(sharedContext.Context) ?? TimeSpan.Zero;
            if (delay>TimeSpan.Zero) {
              Task.Run(async () => {
                await Task.Delay(delay).ConfigureAwait(false);
                owner.Release(Key);
              });
            }
            else {
              owner.Release(Key);
            }
          }
        }
      }

      private Dictionary<TKey,SharedContext> contexts = new ();

      public IContextHolder<TContext> GetOrAdd(TKey key, Func<TContext> createContextFunc)
      {
        lock (contexts) {
          if (contexts.TryGetValue(key, out var existingContext)) {
            return new ContextHolder(this, key, existingContext);
          }
          else {
            var newContext = new SharedContext(this, key, createContextFunc());
            contexts.Add(key, newContext);
            return new ContextHolder(this, key, newContext);
          }
        }
      }

      public IContextHolder<TContext> GetOrAdd(TKey key, Func<TContext> createContextFunc, Func<TContext, TimeSpan> releaseDelayFunc)
      {
        lock (contexts) {
          if (contexts.TryGetValue(key, out var existingContext)) {
            return new ContextHolder(this, key, existingContext, releaseDelayFunc);
          }
          else {
            var newContext = new SharedContext(this, key, createContextFunc());
            contexts.Add(key, newContext);
            return new ContextHolder(this, key, newContext, releaseDelayFunc);
          }
        }
      }

      public void Release(TKey key)
      {
        lock (contexts) {
          if (contexts.TryGetValue(key, out var existingContext)) {
            if (existingContext.Release()==0) {
              contexts.Remove(key);
            }
          }
        }
      }

      public async Task ReleaseWithDelay(TKey key, TimeSpan delay)
      {
        await Task.Delay(delay).ConfigureAwait(false);
        Release(key);
      }
    }

    class HLSContentSink
      : IDisposable
    {
      public HTTPLiveStreamingSegmenter Segmenter { get; private set; } = new HTTPLiveStreamingSegmenter();
      private HTTPLiveStreamingDirectOwinApp owner;
      private Channel channel;
      private IDisposable subscription;
      private HLSContentSink(HTTPLiveStreamingDirectOwinApp owner, Channel channel)
      {
        this.owner = owner;
        this.channel = channel;
        var sink =
            "flvtots".Split(',')
            .Select(name => channel.PeerCast.ContentFilters.FirstOrDefault(filter => filter.Name.ToLowerInvariant() == name.ToLowerInvariant()))
            .Where(filter => filter != null)
            .Aggregate((IContentSink)Segmenter, (r, filter) => filter!.Activate(r));
        subscription = channel.AddContentSink(sink);
      }

      public void Dispose()
      {
        subscription.Dispose();
      }

      public static IContextHolder<HLSContentSink> GetSubscription(HTTPLiveStreamingDirectOwinApp owner, Channel channel)
      {
        return owner.contentSinks.GetOrAdd(channel, () => new HLSContentSink(owner, channel));
      }
    }
    private SharedContextCollection<Channel,HLSContentSink> contentSinks = new ();

    class HLSChannelSink
      : IChannelSink,
        IDisposable
    {
      private HTTPLiveStreamingDirectOwinApp owner;
      private ConnectionInfoBuilder connectionInfo = new ConnectionInfoBuilder();
      private Func<float>? getRecvRate = null;
      private Func<float>? getSendRate = null;
      private (Channel Channel,string SessionId) session;
      private CancellationTokenSource stoppedCancellationTokenSource = new CancellationTokenSource();
      private IContextHolder<HLSContentSink> contentSink;
      private IDisposable subscription;
      private StopReason stopReason = StopReason.None;

      public string SessionId {
        get { return session.SessionId; }
      }
      public Channel Channel {
        get { return session.Channel; }
      }
      public CancellationToken Stopped {
        get { return stoppedCancellationTokenSource.Token; }
      }

      public HTTPLiveStreamingSegmenter Segmenter {
        get { return contentSink.Context.Segmenter; }
      }

      private HLSChannelSink(HTTPLiveStreamingDirectOwinApp owner, OwinEnvironment ctx, (Channel,string) session)
      {
        this.owner = owner;
        this.session = session;
        connectionInfo.AgentName = ctx.Request.Headers.Get("User-Agent");
        connectionInfo.LocalDirects = null;
        connectionInfo.LocalRelays = null;
        connectionInfo.ProtocolName = "HLS Direct";
        connectionInfo.RecvRate = null;
        connectionInfo.SendRate = null;
        connectionInfo.ContentPosition = 0;
        var remoteEndPoint = ctx.Request.GetRemoteEndPoint();
        connectionInfo.RemoteEndPoint = remoteEndPoint;
        connectionInfo.RemoteName = remoteEndPoint.ToString();
        connectionInfo.RemoteSessionID = null;
        connectionInfo.RemoteHostStatus = RemoteHostStatus.Receiving;
        if (remoteEndPoint.Address.GetAddressLocality()<2) {
          connectionInfo.RemoteHostStatus |= RemoteHostStatus.Local;
        }
        connectionInfo.Status = ConnectionStatus.Connected;
        connectionInfo.Type = ConnectionType.Direct;
        getRecvRate = ctx.Get<Func<float>>(OwinEnvironment.PeerCastStation.GetRecvRate);
        getSendRate = ctx.Get<Func<float>>(OwinEnvironment.PeerCastStation.GetSendRate);
        contentSink = HLSContentSink.GetSubscription(owner, Channel);
        subscription = Channel.AddOutputStream(this);
      }

      private void UpdateClient(OwinEnvironment ctx)
      {
        connectionInfo.AgentName = ctx.Request.Headers.Get("User-Agent");
        var remoteEndPoint = ctx.Request.GetRemoteEndPoint();
        connectionInfo.RemoteEndPoint = remoteEndPoint;
        connectionInfo.RemoteName = remoteEndPoint.ToString();
        if (remoteEndPoint.Address.GetAddressLocality()<2) {
          connectionInfo.RemoteHostStatus |= RemoteHostStatus.Local;
        }
        else {
          connectionInfo.RemoteHostStatus &= ~RemoteHostStatus.Local;
        }
        getRecvRate = ctx.Get<Func<float>>(OwinEnvironment.PeerCastStation.GetRecvRate);
        getSendRate = ctx.Get<Func<float>>(OwinEnvironment.PeerCastStation.GetSendRate);
        switch (stopReason) {
        case StopReason.UserReconnect:
        case StopReason.UserShutdown:
          stopReason = StopReason.None;
          Interlocked.Exchange(ref stoppedCancellationTokenSource, new CancellationTokenSource()).Dispose();
          Interlocked.Exchange(ref subscription, Channel.AddOutputStream(this)).Dispose();
          Interlocked.Exchange(ref contentSink, HLSContentSink.GetSubscription(owner, Channel)).Dispose();
          break;
        }
      }

      public void Dispose()
      {
        subscription.Dispose();
        contentSink.Dispose();
        stoppedCancellationTokenSource.Dispose();
      }

      ConnectionInfo IChannelSink.GetConnectionInfo()
      {
        connectionInfo.RecvRate = getRecvRate?.Invoke();
        connectionInfo.SendRate = getSendRate?.Invoke();
        return connectionInfo.Build();
      }

      void IChannelSink.OnBroadcast(Host? from, Atom packet)
      {
      }

      void IChannelSink.OnStopped(StopReason reason)
      {
        stopReason = reason;
        stoppedCancellationTokenSource.Cancel();
        subscription.Dispose();
        contentSink.Dispose();
      }

      public static IContextHolder<HLSChannelSink> GetSubscription(HTTPLiveStreamingDirectOwinApp owner, Channel channel, OwinEnvironment ctx, string session)
      {
        var channelSink = owner.channelSinks.GetOrAdd((channel, session), () => new HLSChannelSink(owner, ctx, (channel, session)), contentSink => TimeSpan.FromSeconds(contentSink.Segmenter.TargetDuration/0.7));
        channelSink.Context.UpdateClient(ctx);
        return channelSink;
      }

    }
    private SharedContextCollection<(Channel Channel, string SessionId), HLSChannelSink> channelSinks = new ();

    private struct ParsedRequest
    {
      public static readonly Regex ChannelIdPattern = new Regex(@"\A/([0-9a-fA-F]{32})(?:/(\d{1,32}))?(?:\.(\w+))?/?\z", RegexOptions.Compiled);
      public HttpStatusCode Status;
      public Guid ChannelId;
      public int? FragmentNumber;
      public string? Extension;
      public string? Session;
      public bool IsValid {
        get { return Status==HttpStatusCode.OK; }
      }
      public static ParsedRequest Parse(OwinEnvironment ctx)
      {
        var req = new ParsedRequest();
        var path = ctx.Request.Path ?? "/";
        if (path=="/") {
          req.Status = HttpStatusCode.Forbidden;
          return req;
        }
        var md = ChannelIdPattern.Match(path);
        if (!md.Success) {
          req.Status = HttpStatusCode.NotFound;
          return req;
        }
        req.Status = HttpStatusCode.OK;
        req.ChannelId = Guid.Parse(md.Groups[1].Value);
        req.FragmentNumber = md.Groups[2].Success ? (int?)Int32.Parse(md.Groups[2].Value) : null;
        req.Extension = md.Groups[3].Success ? md.Groups[3].Value : null;
        req.Session = ctx.Request.Query.Get("session");
        return req;
      }
    }

    private Uri AllocateNewSessionUri(OwinEnvironment ctx)
    {
      var source = ctx.Request.GetRemoteEndPoint().ToString();
      using (var md5=System.Security.Cryptography.MD5.Create()) {
        var bytes = new byte[16];
        Random.Shared.NextBytes(bytes);
        var session =
          md5
          .ComputeHash(bytes)
          .Aggregate(new System.Text.StringBuilder(), (builder,v) => builder.Append(v.ToString("X2")))
          .ToString();
        var location = new UriBuilder(ctx.Request.Uri);
        if (String.IsNullOrEmpty(location.Query)) {
          location.Query = $"session={session}";
        }
        else {
          location.Query = location.Query.Substring(1) + $"&session={session}"; 
        }
        return location.Uri;
      }
    }

    private async Task PlayListHandler(OwinEnvironment ctx, ParsedRequest req, Channel channel)
    {
      var ct = ctx.Request.CallCancelled;
      var session = req.Session;
      if (String.IsNullOrWhiteSpace(session)) {
        ctx.Response.Redirect(AllocateNewSessionUri(ctx).ToString());
        return;
      }
      var pls = new M3U8PlayList(ctx.Request.Query.Get("scheme"), channel);
      ctx.Response.StatusCode = HttpStatusCode.OK;
      ctx.Response.Headers.Add("Cache-Control", "private");
      ctx.Response.Headers.Add("Cache-Disposition", "inline");
      ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
      ctx.Response.ContentType = pls.MIMEType;
      using (var subscriptionContext=HLSChannelSink.GetSubscription(this, channel, ctx, session)) {
        var subscription = subscriptionContext.Context;
        subscription.Stopped.ThrowIfCancellationRequested();
        byte[] body;
        try {
          var baseuri = new Uri(
            new Uri(ctx.Request.Uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.UserInfo, UriFormat.UriEscaped)),
            "hls/");
          var acinfo = ctx.GetAccessControlInfo();
          using (var cts=CancellationTokenSource.CreateLinkedTokenSource(ct, subscription.Stopped)) {
            cts.CancelAfter(10000);
            if (acinfo?.AuthorizationRequired ?? false) {
              var parameters = new Dictionary<string, string>() {
                { "auth", acinfo.AuthenticationKey.GetToken() },
                { "session", subscription.SessionId },
              };
              body = await pls.CreatePlayListAsync(baseuri, parameters, subscription.Segmenter, cts.Token).ConfigureAwait(false);
            }
            else {
              var parameters = new Dictionary<string, string>() {
                { "session", subscription.SessionId },
              };
              body = await pls.CreatePlayListAsync(baseuri, parameters, subscription.Segmenter, cts.Token).ConfigureAwait(false);
            }
          }
        }
        catch (OperationCanceledException) {
          ctx.Response.StatusCode = HttpStatusCode.GatewayTimeout;
          return;
        }
        ctx.Response.StatusCode = HttpStatusCode.OK;
        await ctx.Response.WriteAsync(body, ct).ConfigureAwait(false);
      }
    }

    private async Task FragmentHandler(OwinEnvironment ctx, ParsedRequest req, Channel channel)
    {
      var ct = ctx.Request.CallCancelled;
      var session = req.Session;
      if (String.IsNullOrWhiteSpace(session)) {
        ctx.Response.Redirect(AllocateNewSessionUri(ctx).ToString());
        return;
      }
      using (var subscriptionContext=HLSChannelSink.GetSubscription(this, channel, ctx, session)) {
        var subscription = subscriptionContext.Context;
        subscription.Stopped.ThrowIfCancellationRequested();
        using (var cts=CancellationTokenSource.CreateLinkedTokenSource(ct, subscription.Stopped)) {
          cts.CancelAfter(10000);
          var segments = await subscription.Segmenter.GetSegmentsAsync(cts.Token).ConfigureAwait(false);
          var segment = segments.FirstOrDefault(s => s.Index==req.FragmentNumber);
          if (segment.Index==0 || segment.Data==null) {
            ctx.Response.StatusCode = HttpStatusCode.NotFound;
          }
          else {
            ctx.Response.StatusCode = HttpStatusCode.OK;
            ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            ctx.Response.ContentType = "video/MP2T";
            ctx.Response.ContentLength = segment.Data.LongLength;
            await ctx.Response.WriteAsync(segment.Data, cts.Token).ConfigureAwait(false);
          }
        }
      }
    }

    private async Task HLSHandler(OwinEnvironment ctx)
    {
      var ct = ctx.Request.CallCancelled;
      var req = ParsedRequest.Parse(ctx);
      if (!req.IsValid) {
        ctx.Response.StatusCode = req.Status;
        return;
      }

      bool newSession = String.IsNullOrWhiteSpace(req.Session);
      var (statusCode, channel) = await HTTPDirectOwinApp.GetChannelAsync(ctx, req.ChannelId, requestRelay:newSession, ct).ConfigureAwait(false);
      if (statusCode==HttpStatusCode.OK) {
        if (newSession) {
          ctx.Response.Redirect(AllocateNewSessionUri(ctx).ToString());
        }
        else if (req.FragmentNumber.HasValue) {
          await FragmentHandler(ctx, req, channel).ConfigureAwait(false);
        }
        else {
          await PlayListHandler(ctx, req, channel).ConfigureAwait(false);
        }
      }
      else {
        ctx.Response.StatusCode = statusCode;
      }
    }

    public static void BuildApp(IAppBuilder builder)
    {
      var app = new HTTPLiveStreamingDirectOwinApp();
      builder.MapGET("/hls", sub => {
        sub.UseAuth(OutputStreamType.Play);
        sub.Run(app.HLSHandler);
      });
    }

  }

  [Plugin]
  public class HTTPLiveStreamingDirectHostPlugin
    : PluginBase
  {
    override public string Name { get { return "HTTP Live Streaming Host"; } }

    private IDisposable? appRegistration = null;
    override protected void OnAttach(PeerCastApplication app)
    {
      var owin = app.Plugins.OfType<OwinHostPlugin>().FirstOrDefault();
      appRegistration = owin?.OwinHost?.Register(HTTPLiveStreamingDirectOwinApp.BuildApp);
    }

    override protected void OnDetach()
    {
      appRegistration?.Dispose();
      appRegistration = null;
    }
  }

}

