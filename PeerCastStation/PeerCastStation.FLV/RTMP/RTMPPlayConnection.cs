using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PeerCastStation.Core;
using PeerCastStation.FLV.AMF;

namespace PeerCastStation.FLV.RTMP
{
	public class RTMPPlayConnection
		: RTMPConnection
	{
		private static Logger logger = new Logger(typeof(RTMPPlayConnection));
		public RTMPOutputStream owner;
		public Channel  Channel  { get; private set; }
		public long     StreamId { get; private set; }

		public long? ContentPosition {
			get {
				if (lastPacket==null) return null;
				return lastPacket.Position;
			}
		}

		public RTMPPlayConnection(
			RTMPOutputStream owner,
			System.IO.Stream input_stream,
			System.IO.Stream output_stream)
			: base(input_stream, output_stream)
		{
			this.owner = owner;
		}

		protected override void OnClose()
		{
			base.OnClose();
			if (this.Channel!=null) {
				this.Channel.ContentChanged -= OnContentChanged;
			}
		}

    private class StreamName
    {
      public string Name { get; set; }
      public string Format { get; set; }
      public Dictionary<string,string> Parameters { get; set; }

      public StreamName()
      {
        this.Name = "";
        this.Format = "flv";
        this.Parameters = new Dictionary<string,string>();
      }

      public override string ToString()
      {
        return String.Join("?",
          String.Join(".",
            Uri.EscapeDataString(this.Name),
            this.Format
          ),
          String.Join("&",
            this.Parameters.Select(kv =>
              Uri.EscapeDataString(kv.Key) + "=" +
              Uri.EscapeDataString(kv.Value)
            )
          )
        );
      }

      public string GetParameter(string key)
      {
        string value;
        if (this.Parameters.TryGetValue(key, out value)) {
          return value;
        }
        else {
          return null;
        }
      }

      private static readonly System.Text.RegularExpressions.Regex namePattern =
        new System.Text.RegularExpressions.Regex(@"(?<name>[^.?]+)(?:\.(?<format>[^?]+))?(?:\?(?<params>\S+))?");
      public static StreamName Parse(string str)
      {
        var result = new StreamName();
        var match = namePattern.Match(str);
        if (!match.Success) {
          result.Name = str;
          return result;
        }
        result.Name = Uri.UnescapeDataString(match.Groups["name"].Value);
        if (match.Groups["format"].Success) {
          result.Format = Uri.UnescapeDataString(match.Groups["format"].Value);
        }
        if (match.Groups["params"].Success) {
          var params_str = match.Groups["params"].Value;
          foreach (var param_str in params_str.Split('&')) {
            var idx = param_str.IndexOf('=');
            if (idx<0) continue;
            var key = Uri.UnescapeDataString(param_str.Substring(0, idx));
            var val = Uri.UnescapeDataString(param_str.Substring(idx+1));
            result.Parameters[key] = val;
          }
        }
        return result;
      }
    }

    private async Task<Channel> RequestChannel(
      StreamName stream_name,
      CancellationToken cancel_token)
    {
      Guid channel_id;
      if (!Guid.TryParse(stream_name.Name, out channel_id)) {
        return null;
      }
      var tracker_uri =
        stream_name.Parameters.ContainsKey("tip") ?
        OutputStreamBase.CreateTrackerUri(channel_id, stream_name.Parameters["tip"]) :
        null;
      var channel = owner.RequestChannel(channel_id, tracker_uri);
      if (channel==null) return null;
      await Task.WhenAny(
        Task.Delay(10000),
        channel.WaitForReadyContentTypeAsync(cancel_token)).ConfigureAwait(false);
      if (channel.ChannelInfo==null ||
          String.IsNullOrEmpty(channel.ChannelInfo.ContentType) ||
          channel.ChannelInfo.ContentType!="FLV") {
        return null;
      }
      return channel;
    }

		private async Task SendOnStatus(
				long stream_id,
				int transaction,
				string level,
				string code,
				string description,
				CancellationToken cancel_token)
		{
			var status_command = CommandMessage.Create(
				this.ObjectEncoding,
				this.Now,
				stream_id,
				"onStatus",
				transaction,
				null,
				new AMFValue(new AMFObject {
					{ "level",       level },
					{ "code",        code },
					{ "description", description },
				})
			);
			await SendMessage(3, status_command, cancel_token).ConfigureAwait(false);
		}

    protected override async Task OnStopAsync(CancellationToken cancel_token)
    {
      if (this.Channel==null) return;
      await SendOnStatus(
        this.StreamId,
        0,
        "status",
        "NetStream.Play.UnpublishNotify",
        "",
        cancel_token).ConfigureAwait(false);
    }

    protected async Task OnCommandPlayStop(CommandMessage msg, CancellationToken cancel_token)
    {
      if (this.Channel==null) return;
      await SendOnStatus(
        this.StreamId,
        msg.TransactionId+1,
        "status",
        "NetStream.Play.Stop",
        "",
        cancel_token).ConfigureAwait(false);
      Close();
    }

    protected override async Task OnCommandPlay(CommandMessage msg, CancellationToken cancel_token)
    {
      if (msg.Arguments[0].Type==AMFValueType.Boolean && ((bool)msg.Arguments[0].Value)==false) {
        await OnCommandPlayStop(msg, cancel_token);
        return;
      }
      var stream_name = StreamName.Parse((string)msg.Arguments[0]);
      var start       = msg.Arguments.Count>1 ? (int)msg.Arguments[1] : -2;
      var duration    = msg.Arguments.Count>2 ? (int)msg.Arguments[2] : -1;
      var reset       = msg.Arguments.Count>3 ? (bool)msg.Arguments[3] : false;
      logger.Debug("Play: {0}, {1}, {2}, {3}", stream_name.ToString(), start, duration, reset);
      if (owner.CheckAuthotization(stream_name.GetParameter("auth"))) {
        this.Channel = await RequestChannel(stream_name, cancel_token).ConfigureAwait(false);
        this.StreamId = msg.StreamId;
        await SendMessage(2, new UserControlMessage.StreamBeginMessage(this.Now, 0, msg.StreamId), cancel_token).ConfigureAwait(false);
        if (this.Channel!=null) {
          if (reset) {
            await SendOnStatus(
              this.StreamId,
              msg.TransactionId+1,
              "status",
              "NetStream.Play.Reset",
              stream_name.ToString(),
              cancel_token).ConfigureAwait(false);
          }
          await SendOnStatus(
            this.StreamId,
            msg.TransactionId+1,
            "status",
            "NetStream.Play.Start",
            stream_name.ToString(),
            cancel_token).ConfigureAwait(false);
        }
        else {
          await SendOnStatus(
            this.StreamId,
            msg.TransactionId+1,
            "error",
            "NetStream.Play.StreamNotFound",
            stream_name.ToString(),
            cancel_token).ConfigureAwait(false);
        }
      }
      else {
        await SendOnStatus(
          this.StreamId,
          msg.TransactionId+1,
          "error",
          "NetStream.Play.Failed",
          "auth failed",
          cancel_token).ConfigureAwait(false);
      }
      if (msg.TransactionId!=0) {
        var result = CommandMessage.Create(
          this.ObjectEncoding,
          this.Now,
          msg.StreamId,
          "_result",
          msg.TransactionId,
          null
        );
        await SendMessage(3, result, cancel_token).ConfigureAwait(false);
      }
      if (this.Channel!=null) {
        await base.OnCommandPlay(msg, cancel_token).ConfigureAwait(false);
        this.Channel.ContentChanged += OnContentChanged;
        OnContentChanged(this, new EventArgs());
      }
      else {
        Close();
      }
    }

    protected override Task OnCommandClose(CommandMessage msg, CancellationToken cancel_token)
    {
      return base.OnCommandClose(msg, cancel_token);
    }

    private Content headerPacket = null;
    private Content lastPacket = null;
    private object locker = new object();
    private void OnContentChanged(object sender, EventArgs args)
    {
      lock (locker) {
        var new_header = Channel.ContentHeader;
        if (new_header!=headerPacket) {
          headerPacket = Channel.ContentHeader;
          if (headerPacket!=null) {
            PostContent(headerPacket);
          }
          lastPacket = headerPacket;
        }
        if (headerPacket==null) return;
        IEnumerable<Content> contents;
        contents = Channel.Contents.GetNewerContents(lastPacket.Stream, lastPacket.Timestamp, lastPacket.Position);
        foreach (var content in contents) {
          PostContent(content);
          lastPacket = content;
        }
      }
    }

		class RTMPContentSink
			: IRTMPContentSink
		{
			private RTMPPlayConnection connection;
			public RTMPContentSink(RTMPPlayConnection conn)
			{
				this.connection = conn;
			}

      public void OnFLVHeader(FLVFileHeader header)
      {
      }

			public void OnData(DataMessage msg)
			{
				this.connection.PostMessage(3,
					new RTMPMessage(
						msg.MessageType,
						msg.Timestamp,
						this.connection.StreamId,
						msg.Body)
				);
			}

      private long timestampBase = -1;
			public void OnVideo(RTMPMessage msg)
			{
        if (timestampBase<0 && msg.Timestamp>0) {
          timestampBase = msg.Timestamp;
        }
        this.connection.PostMessage(3,
          new RTMPMessage(
            msg.MessageType,
            msg.Timestamp - Math.Max(timestampBase, 0),
            this.connection.StreamId,
            msg.Body)
        );
			}

			public void OnAudio(RTMPMessage msg)
			{
        if (timestampBase<0 && msg.Timestamp>0) {
          timestampBase = msg.Timestamp;
        }
				this.connection.PostMessage(3,
					new RTMPMessage(
						msg.MessageType,
            msg.Timestamp - Math.Max(timestampBase, 0),
						this.connection.StreamId,
						msg.Body)
				);
			}
		}

		private System.IO.MemoryStream contentBuffer = new System.IO.MemoryStream();
		private FLVFileParser fileParser = new FLVFileParser();
		private RTMPContentSink contentSink;
		private void PostContent(Content content)
		{
			var pos = contentBuffer.Position;
			contentBuffer.Seek(0, System.IO.SeekOrigin.End);
			contentBuffer.Write(content.Data, 0, content.Data.Length);
			contentBuffer.Position = pos;
			if (contentSink==null) contentSink = new RTMPContentSink(this);
			fileParser.Read(contentBuffer, contentSink);
			if (contentBuffer.Position!=0) {
				var new_buf = new System.IO.MemoryStream();
				var trim_pos = contentBuffer.Position;
				contentBuffer.Close();
				var buf = contentBuffer.ToArray();
				new_buf.Write(buf, (int)trim_pos, (int)(buf.Length-trim_pos));
				new_buf.Position = 0;
				contentBuffer = new_buf;
			}
		}

	}
}
