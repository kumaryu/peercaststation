using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.Core.Http
{
  internal class OwinContext
  {
    public class OnSendingHeaderCollection
    {
      private List<Tuple<Action<object?>,object?>> items = new List<Tuple<Action<object?>, object?>>();

      public void Add(Action<object?> action, object? state)
      {
        items.Add(new Tuple<Action<object?>,object?>(action, state));
      }

      public void Invoke()
      {
        foreach (var item in items) {
          item.Item1.Invoke(item.Item2);
        }
      }
    }

    public OwinEnvironment Environment { get; private set; }
    public Stream RequestBody { get; private set; }
    public ResponseStream ResponseBody { get; private set; }
    public OnSendingHeaderCollection OnSendingHeaders { get; private set; } = new OnSendingHeaderCollection();
    public ConnectionStream ConnectionStream { get; private set; }
    private Func<IDictionary<string,object>, Task>? opaqueHandler = null;

    public OwinContext(
      PeerCast peerCast,
      HttpRequest req,
      ConnectionStream stream,
      IPEndPoint localEndPoint,
      IPEndPoint remoteEndPoint,
      AccessControlInfo accessControlInfo)
    {
      ConnectionStream = stream;
      RequestBody = new OwinRequestBodyStream(this, ConnectionStream);
      ResponseBody = new OwinResponseBodyStream(this, ConnectionStream);
      Dictionary<string, object> env = new();
      env[OwinEnvironment.Owin.Version] = "1.0.1";
      env[OwinEnvironment.Host.TraceOutput] = TextWriter.Null;
      env[OwinEnvironment.Owin.RequestBody] = RequestBody;
      var requestHeaders = req.Headers.ToDictionary();
      if (!requestHeaders.TryGetValue("Host", out var values) || values.Length==0 || String.IsNullOrEmpty(values[0])) {
        requestHeaders["Host"] = new string[] { localEndPoint.ToString() };
      }
      env[OwinEnvironment.Owin.RequestHeaders] = requestHeaders;
      env[OwinEnvironment.Owin.RequestPath] = req.Path;
      env[OwinEnvironment.Owin.RequestPathBase] = "";
      env[OwinEnvironment.Owin.RequestProtocol] = req.Protocol;
      env[OwinEnvironment.Owin.RequestQueryString] = req.QueryString;
      env[OwinEnvironment.Owin.RequestScheme] = "http";
      env[OwinEnvironment.Owin.RequestMethod] = req.Method;
      env[OwinEnvironment.Owin.ResponseBody] = ResponseBody;
      env[OwinEnvironment.Owin.ResponseHeaders] = new Dictionary<string,string[]>(StringComparer.OrdinalIgnoreCase);
      env[OwinEnvironment.Server.RemoteIpAddress] = remoteEndPoint.Address.ToString();
      env[OwinEnvironment.Server.RemotePort] = remoteEndPoint.Port.ToString();
      env[OwinEnvironment.Server.IsLocal] = remoteEndPoint.Address.GetAddressLocality()==0;
      env[OwinEnvironment.Server.LocalIpAddress] = localEndPoint.Address.ToString();
      env[OwinEnvironment.Server.LocalPort] = localEndPoint.Port.ToString();
      env[OwinEnvironment.Server.OnSendingHeaders] = new Action<Action<object?>,object?>(OnSendingHeaders.Add);
      env[OwinEnvironment.PeerCastStation.PeerCast] = peerCast;
      env[OwinEnvironment.PeerCastStation.AccessControlInfo] = accessControlInfo;
      env[OwinEnvironment.PeerCastStation.GetRecvRate] = new Func<float>(() => stream.ReadRate);
      env[OwinEnvironment.PeerCastStation.GetSendRate] = new Func<float>(() => stream.WriteRate);
      env[OwinEnvironment.Opaque.Upgrade] = new Action<IDictionary<string,object>, Func<IDictionary<string,object>, Task>>(OpaqueUpgrade);
      Environment = new OwinEnvironment(env);
    }

    public void OpaqueUpgrade(IDictionary<string,object> parameters, Func<IDictionary<string,object>, Task>? func)
    {
      opaqueHandler = func;
    }

    public async Task Invoke(
      Func<IDictionary<string,object>, Task> func,
      CancellationToken cancellationToken)
    {
      using var cts = CancellationTokenSource.CreateLinkedTokenSource(ConnectionStream.WriteCompleted, cancellationToken);
      Environment.Environment[OwinEnvironment.Owin.CallCancelled] = cts.Token;
      await func.Invoke(Environment.Environment).ConfigureAwait(false);
      if (opaqueHandler!=null) {
        var opaqueEnv = new OpaqueEnvironment(ConnectionStream, cts.Token);
        await opaqueHandler.Invoke(opaqueEnv.Environment).ConfigureAwait(false);
      }
      else {
        await ResponseBody.CompleteAsync(cancellationToken).ConfigureAwait(false);
      }
    }

    public bool IsKeepAlive {
      get { return opaqueHandler==null && Environment.IsKeepAlive(); }
    }
  }

}
