using PeerCastStation.Core;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace PeerCastStation.PCP
{
  public class PCPGIVOutputStream : OutputStreamBase
  {
    public override OutputStreamType OutputStreamType => OutputStreamType.Metadata;
    public override ConnectionInfo GetConnectionInfo()
    {
      ConnectionStatus status = ConnectionStatus.Connected;
      if (IsStopped) {
        status = HasError ? ConnectionStatus.Error : ConnectionStatus.Idle;
      }
      return new ConnectionInfoBuilder {
        ProtocolName     = "PCP GIV",
        Type             = ConnectionType.Metadata,
        Status           = status,
        RemoteName       = RemoteEndPoint?.ToString() ?? "",
        RemoteEndPoint   = (IPEndPoint?)RemoteEndPoint,
        RemoteHostStatus = IsLocal ? RemoteHostStatus.Local : RemoteHostStatus.None,
        RemoteSessionID  = null,
        RecvRate         = Connection.ReadRate,
        SendRate         = Connection.WriteRate,
      }.Build();
    }

    private readonly long headerLength;
    protected override async Task<StopReason> DoProcess(CancellationToken cancel_token)
    {
      // ヘッダー分を読み飛ばす
      await Connection.ReadAsync((int)headerLength).ConfigureAwait(false);
      if (Channel?.SourceStream is PCPSourceStream pcpSource) {
        await pcpSource.GivConnection(Connection).ConfigureAwait(false);
      }
      return StopReason.OffAir;
    }

    public PCPGIVOutputStream(PeerCast peerCast, ConnectionStream connection, AccessControlInfo access_control, Channel? channel, long headerLength)
      : base(peerCast, connection, access_control, channel)
    {
      this.headerLength = headerLength;
    }
  }

  public class PCPGIVOutputStreamFactory : OutputStreamFactoryBase
  {
    public override string Name => "PCPGIVOutputStream";

    public override OutputStreamType OutputStreamType {
      get { return OutputStreamType.Metadata; }
    }

    public static readonly ImmutableArray<byte> RequestEnding = ImmutableArray.Create((byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n');
    public static readonly ImmutableArray<byte> GivRequest = ImmutableArray.Create((byte)'G', (byte)'I', (byte)'V', (byte)' ', (byte)'/');
    public static readonly Regex RequestPattern = new Regex(@"\AGIV /([0-9a-fA-F]{32})\z", RegexOptions.Compiled);
    public override bool TryCreate(byte[] header, AccessControlInfo acinfo, Func<ConnectionStream> connectionCreator, [NotNullWhen(true)] out IOutputStream? outputStream)
    {
      if (acinfo.Accepts.HasFlag(OutputStreamType)) {
        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(header));
        if (reader.TryReadTo(out ReadOnlySpan<byte> lineseq, RequestEnding.AsSpan())) {
          if (lineseq.StartsWith(GivRequest.AsSpan())) {
            var line = System.Text.Encoding.ASCII.GetString(lineseq);
            var md = RequestPattern.Match(line);
            if (md.Success && Guid.TryParse(md.Groups[1].Value, out var channelID)) {
              var connection = connectionCreator();
              outputStream = new PCPGIVOutputStream(PeerCast, connection, acinfo, PeerCast.RequestChannel(channelID, null, false), reader.Consumed);
              return true;
            }
          }
        }
      }
      outputStream = null;
      return false;
    }

    public PCPGIVOutputStreamFactory(PeerCast peerCast)
      : base(peerCast)
    {
    }
  }

  [Plugin]
  public class PCPGIVOutputStreamPlugin
    : PluginBase
  {
    override public string Name { get { return "PCP GIV"; } }

    private PCPGIVOutputStreamFactory? factory;
    override protected void OnAttach(PeerCastApplication app)
    {
      if (factory==null) factory = new PCPGIVOutputStreamFactory(app.PeerCast);
      app.PeerCast.OutputStreamFactories.Add(factory);
    }

    override protected void OnDetach(PeerCastApplication app)
    {
      if (factory!=null) {
        app.PeerCast.OutputStreamFactories.Remove(factory);
      }
    }
  }


}
