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
      private List<Tuple<Action<object>,object>> items = new List<Tuple<Action<object>, object>>();

      public void Add(Action<object> action, object state)
      {
        items.Add(new Tuple<Action<object>,object>(action, state));
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
    private Func<IDictionary<string,object>, Task> opaqueHandler = null;

    public OwinContext(
      PeerCast peerCast,
      HttpRequest req,
      ConnectionStream stream,
      IPEndPoint localEndPoint,
      IPEndPoint remoteEndPoint,
      AccessControlInfo accessControlInfo)
    {
      Environment = new OwinEnvironment();
      ConnectionStream = stream;
      RequestBody = new OwinRequestBodyStream(this, ConnectionStream);
      ResponseBody = new OwinResponseBodyStream(this, ConnectionStream);
      Environment.Environment[OwinEnvironment.Owin.Version] = "1.0.1";
      Environment.Environment[OwinEnvironment.Host.TraceOutput] = TextWriter.Null;
      Environment.Environment[OwinEnvironment.Owin.RequestBody] = RequestBody;
      Environment.Environment[OwinEnvironment.Owin.RequestHeaders] = req.Headers.ToDictionary();
      Environment.Environment[OwinEnvironment.Owin.RequestPath] = req.Path;
      Environment.Environment[OwinEnvironment.Owin.RequestPathBase] = "/";
      Environment.Environment[OwinEnvironment.Owin.RequestProtocol] = req.Protocol;
      Environment.Environment[OwinEnvironment.Owin.RequestQueryString] = req.QueryString;
      Environment.Environment[OwinEnvironment.Owin.RequestScheme] = "http";
      Environment.Environment[OwinEnvironment.Owin.RequestMethod] = req.Method;
      Environment.Environment[OwinEnvironment.Owin.ResponseBody] = ResponseBody;
      Environment.Environment[OwinEnvironment.Owin.ResponseHeaders] = new Dictionary<string,string[]>(StringComparer.OrdinalIgnoreCase);
      Environment.Environment[OwinEnvironment.Server.RemoteIpAddress] = remoteEndPoint.Address.ToString();
      Environment.Environment[OwinEnvironment.Server.RemotePort] = remoteEndPoint.Port.ToString();
      Environment.Environment[OwinEnvironment.Server.IsLocal] = remoteEndPoint.Address.GetAddressLocality()==0;
      Environment.Environment[OwinEnvironment.Server.LocalIpAddress] = localEndPoint.Address.ToString();
      Environment.Environment[OwinEnvironment.Server.LocalPort] = localEndPoint.Port.ToString();
      Environment.Environment[OwinEnvironment.Server.OnSendingHeaders] = new Action<Action<object>,object>(OnSendingHeaders.Add);
      Environment.Environment[OwinEnvironment.PeerCastStation.PeerCast] = peerCast;
      Environment.Environment[OwinEnvironment.PeerCastStation.AccessControlInfo] = accessControlInfo;
      Environment.Environment[OwinEnvironment.PeerCastStation.GetRecvRate] = new Func<float>(() => stream.ReadRate);
      Environment.Environment[OwinEnvironment.PeerCastStation.GetSendRate] = new Func<float>(() => stream.WriteRate);
      Environment.Environment[OwinEnvironment.Opaque.Upgrade] = new Action<IDictionary<string,object>, Func<IDictionary<string,object>, Task>>(OpaqueUpgrade);
    }

    public void OpaqueUpgrade(IDictionary<string,object> parameters, Func<IDictionary<string,object>, Task> func)
    {
      opaqueHandler = func;
    }

    public async Task Invoke(
      Func<IDictionary<string,object>, Task> func,
      CancellationToken cancellationToken)
    {
      Environment.Environment[OwinEnvironment.Owin.CallCancelled] = cancellationToken;
      await func.Invoke(Environment.Environment).ConfigureAwait(false);
      if (opaqueHandler!=null) {
        var opaqueEnv = new OpaqueEnvironment(ConnectionStream, cancellationToken);
        await opaqueHandler.Invoke(opaqueEnv.Environment).ConfigureAwait(false);
      }
      else {
        await ResponseBody.CompleteAsync(cancellationToken).ConfigureAwait(false);
      }
    }

    public bool IsKeepAlive {
      get { return opaqueHandler!=null && Environment.IsKeepAlive(); }
    }
  }

}
