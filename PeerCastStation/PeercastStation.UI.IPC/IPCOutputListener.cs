using PeerCastStation.Core;
using PeerCastStation.Core.IPC;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.UI.IPC
{
  [Plugin(PluginPriority.Higher)]
  public class IPCOutputListener
    : PluginBase
  {
    public static readonly AccessControlInfo IPCAccessControlInfo = new AccessControlInfo(OutputStreamType.Interface | OutputStreamType.Metadata | OutputStreamType.Play, false, null);
    override public string Name { get { return "IPC Output Listener"; } }
    public string IPCPath { get; private set; } = IPCEndPoint.GetDefaultPath(IPCEndPoint.PathType.User, "peercaststation");
    private IPCOption options;
    private IPCServer server;
    private CancellationTokenSource cancellationSource = new CancellationTokenSource();
    private Logger logger = new Logger(nameof(IPCOutputListener));
    private Task serverTask = Task.Delay(0);

    override protected void OnAttach()
    {
      options = IPCOption.None;
      if (Application.Configurations.TryGetString("IPCPath", out var ipcpath) && !String.IsNullOrWhiteSpace(ipcpath)) {
        IPCPath = ipcpath;
      }
      else if (Application.Type==PeerCastApplication.AppType.Service) {
        IPCPath = IPCEndPoint.GetDefaultPath(IPCEndPoint.PathType.System, "peercaststation");
        options = IPCOption.AcceptAnyUsers;
      }
      else {
        IPCPath = IPCEndPoint.GetDefaultPath(IPCEndPoint.PathType.User, "peercaststation");
        options = IPCOption.None;
      }
      if (Application.Configurations.TryGetBool("IPCAcceptAnyUsers", out var ipcany)) {
        if (ipcany) {
          options = IPCOption.AcceptAnyUsers;
        }
        else {
          options = IPCOption.None;
        }
      }
      cancellationSource = new CancellationTokenSource();
      server = IPCServer.Create(IPCPath, options);
    }

    private async Task<IOutputStream> CreateMatchedHandler(IPCEndPoint endpoint, Stream stream, CancellationToken cancellationToken)
    {
      var factory = Application.PeerCast.OutputStreamFactories
        .Select(f => f as PeerCastStation.UI.HTTP.OWINHostOutputStreamFactory)
        .Where(f => f!=null)
        .FirstOrDefault();
      if (factory==null) return null;
      var buf = new byte[2048];
      var pos = 0;
      using (var cts=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)) {
        cts.CancelAfter(TimeSpan.FromMilliseconds(3000));
        var ct = cts.Token;
        ct.Register(stream.Close);
        try {
          while (!ct.IsCancellationRequested || pos==buf.Length) {
            var len = await stream.ReadAsync(buf, pos, buf.Length-pos, ct).ConfigureAwait(false);
            if (len<=0) return null;
            pos += len;
            var header = buf.Take(pos).ToArray();
            var channel_id = factory.ParseChannelID(header);
            if (channel_id.HasValue) {
              var os = factory.Create(
                stream, stream,
                endpoint,
                IPCAccessControlInfo,
                channel_id.Value,
                header);
              return os;
            }
          }
        }
        catch (System.ObjectDisposedException) {
        }
        catch (System.IO.IOException) {
        }
      }
      return null;
    }

    private void HandleClient(IPCClient client, CancellationToken ct)
    {
      Task.Run(async () => {
        logger.Debug("Output thread started");
        var stream = client.GetStream();
        int trying = 0;
        try {
          retry:
          stream.WriteTimeout = 3000;
          stream.ReadTimeout  = 3000;
          var handler = await CreateMatchedHandler(client.RemoteEndPoint, stream, ct).ConfigureAwait(false);
          if (handler!=null) {
            logger.Debug("Output stream started {0}", trying);
            var result = await handler.Start().ConfigureAwait(false);
            switch (result) {
            case HandlerResult.Continue:
              trying++;
              goto retry;
            case HandlerResult.Close:
            case HandlerResult.Error:
            default:
              break;
            }
          }
          else {
            logger.Debug("No protocol handler matched");
          }
        }
        finally {
          logger.Debug("Closing client connection");
          stream.Close();
          client.Close();
        }
      }, ct);
    }

    protected override void OnStart()
    {
      var ct = cancellationSource.Token;
      server.Start();
      serverTask = Task.Run(async () => {
        while (!ct.IsCancellationRequested) {
          try {
            var client = await server.AcceptAsync(ct).ConfigureAwait(false);
            HandleClient(client, ct);
          }
          catch (OperationCanceledException) {
          }
        }
      }, ct);
    }

    protected override void OnStop()
    {
      server.Stop();
      cancellationSource.Cancel();
      try {
        serverTask.Wait();
      }
      catch (AggregateException ex) {
        if (!(ex.InnerException is OperationCanceledException)) {
          logger.Error(ex);
        }
      }
    }

    protected override void OnDetach()
    {
      server.Dispose();
      server = null;
      cancellationSource.Dispose();
      cancellationSource = null;
    }

  }

}
