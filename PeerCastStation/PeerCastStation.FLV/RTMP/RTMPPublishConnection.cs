using PeerCastStation.Core;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.FLV.RTMP
{
  public class RTMPPublishConnection
    : RTMPConnection
  {
    private readonly IRTMPContentSink rtmpContentSink;
    private readonly Logger logger;
    public event EventHandler? Started;
    public event EventHandler? Stopped;

    public RTMPPublishConnection(Stream input_stream, Stream output_stream, IRTMPContentSink rtmpContentSink)
      : base(input_stream, output_stream)
    {
      this.rtmpContentSink = rtmpContentSink;
      this.logger = new Logger(this.GetType());
    }
    protected override Task OnAudio(RTMPMessage msg, CancellationToken cancel_token)
    {
      rtmpContentSink.OnAudio(msg);
      return Task.CompletedTask;
    }

    protected override Task OnVideo(RTMPMessage msg, CancellationToken cancel_token)
    {
      rtmpContentSink.OnVideo(msg);
      return Task.CompletedTask;
    }

    protected override Task OnData(DataMessage msg, CancellationToken cancel_token)
    {
      rtmpContentSink.OnData(msg);
      return Task.CompletedTask;
    }
    protected override void FlushBuffer()
    {
      base.FlushBuffer();
    }

    protected override Task OnCommandDeleteStream(CommandMessage msg, CancellationToken cancel_token)
    {
      Stopped?.Invoke(this, EventArgs.Empty);
      return Task.CompletedTask;
    }

    protected override async Task OnCommandPublish(CommandMessage msg, CancellationToken cancel_token)
    {
      await base.OnCommandPublish(msg, cancel_token);
      var name = (string?)msg.Arguments[0];
      var type = (string?)msg.Arguments[1];
      logger.Debug($"publish: name {name}, type: {type}");
      await SendMessage(2, new UserControlMessage.StreamBeginMessage(this.Now, 0, msg.StreamId), cancel_token).ConfigureAwait(false);
      var status = CommandMessage.Create(
        ObjectEncoding,
        this.Now,
        msg.StreamId,
        "onStatus",
        0,
        AMF.AMFValue.Null,
        new AMF.AMFValue(new AMF.AMFObject {
          { "level",       "status" },
          { "code",        "NetStream.Publish.Start" },
          { "description", name ?? "" },
        })
      );
      await SendMessage(3, status, cancel_token).ConfigureAwait(false);
      var result = CommandMessage.Create(
        ObjectEncoding,
        this.Now,
        msg.StreamId,
        "_result",
        msg.TransactionId,
        AMF.AMFValue.Null
      );
      if (msg.TransactionId!=0) {
        await SendMessage(3, result, cancel_token).ConfigureAwait(false);
      }
      Started?.Invoke(this, EventArgs.Empty);
    }
  }

}
