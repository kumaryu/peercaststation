using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PeerCastStation.Core;

namespace PeerCastStation.FLV.RTMP
{
	public class BufferedReadStream
		: System.IO.Stream
	{
		private RingbufferStream bufferStream;
		private Stream baseStream;
		public BufferedReadStream(Stream base_stream)
			: this(base_stream, 8192)
		{
		}

		public BufferedReadStream(Stream base_stream, int buffer_length)
			: this(base_stream, buffer_length, null)
		{
		}

		public BufferedReadStream(Stream base_stream, int buffer_length, byte[] header)
		{
			this.baseStream = base_stream;
			this.bufferStream = new RingbufferStream(buffer_length);
			if (header!=null) {
				this.bufferStream.Write(header, 0, header.Length);
			}
		}

    protected override void Dispose(bool disposing)
    {
      base.Dispose(disposing);
      if (disposing) {
        baseStream.Dispose();
      }
    }

		public override bool CanRead {
			get { return true; }
		}

		public override bool CanSeek {
			get { return false; }
		}

		public override bool CanWrite {
			get { return false; }
		}

		public override void Flush()
		{
			baseStream.Flush();
		}

		public override long Length {
			get { throw new NotSupportedException(); }
		}

		public override long Position {
			get { throw new NotSupportedException(); }
			set { throw new NotSupportedException(); }
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			var bufread = bufferStream.Read(buffer, offset, count);
			if (bufread>=count) {
				return bufread;
			}
			var buf = new byte[bufferStream.Capacity];
			var baseread = baseStream.Read(buf, 0, buf.Length);
			bufferStream.Write(buf, 0, baseread);
			var bufread2 = bufferStream.Read(buffer, bufread+offset, count-bufread);
			return bufread + bufread2;
		}

		public override long Seek(long offset, System.IO.SeekOrigin origin)
		{
			throw new NotSupportedException();
		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException();
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotSupportedException();
		}
	}

	public class RTMPOutputStream
		: IOutputStream
	{
		private PeerCast peerCast;
		private ConnectionStream inputStream;
		private ConnectionStream outputStream;
		private System.Net.EndPoint remoteEndPoint;
		private AccessControlInfo accessControl;
		private RTMPPlayConnection connection;
		private System.Threading.Tasks.Task<HandlerResult> connectionTask;
		private System.Threading.CancellationTokenSource cancelSource = new System.Threading.CancellationTokenSource();
		private Channel channel;

		public RTMPOutputStream(
				PeerCast peercast,
				System.IO.Stream input_stream,
				System.IO.Stream output_stream,
				System.Net.EndPoint remote_endpoint,
				AccessControlInfo access_control,
				Guid channel_id,
				byte[] header)
		{
			input_stream.ReadTimeout = System.Threading.Timeout.Infinite;
			this.peerCast       = peercast;
      var stream = new ConnectionStream(new BufferedReadStream(input_stream, 8192, header), output_stream);
			this.inputStream    = stream;
			this.outputStream   = stream;
      stream.WriteTimeout = 10000;
			this.remoteEndPoint = remote_endpoint;
			this.accessControl  = access_control;
			this.connection = new RTMPPlayConnection(this, this.inputStream, this.outputStream);
		}

		public ConnectionInfo GetConnectionInfo()
		{
      return new ConnectionInfoBuilder {
        ProtocolName     = "RTMP Output",
        Type             = ConnectionType.Direct,
        Status           = ConnectionStatus.Connected,
        RemoteName       = remoteEndPoint.ToString(),
        RemoteEndPoint   = remoteEndPoint as System.Net.IPEndPoint,
        RemoteHostStatus = RemoteHostStatus.Receiving,
        ContentPosition  = connection.ContentPosition,
        RecvRate         = (float)this.inputStream.ReadRate,
        SendRate         = (float)this.outputStream.WriteRate,
        AgentName        = connection.ClientName
      }.Build();
		}

		public OutputStreamType OutputStreamType {
			get { return Core.OutputStreamType.Play; }
		}

		public Channel RequestChannel(Guid channel_id, Uri tracker_uri)
		{
			var channel = peerCast.RequestChannel(channel_id, tracker_uri, true);
			this.channel = channel;
			if (channel!=null) {
				channel.AddOutputStream(this);
			}
			return channel;
		}

		public bool IsLocal {
			get {
				var endpoint = remoteEndPoint as System.Net.IPEndPoint;
				if (endpoint==null) return false;
				return endpoint.Address.IsSiteLocal();
			}
		}

		public int UpstreamRate {
			get {
				if (IsLocal || channel==null) return 0;
				return channel.ChannelInfo.Bitrate;
			}
		}

		public Task<HandlerResult> Start()
		{
			connectionTask =
				connection.Run(cancelSource.Token)
				.ContinueWith(task => {
					if (this.channel!=null) {
						this.channel.RemoveOutputStream(this);
					}
          return HandlerResult.Close;
				});
      return connectionTask;
		}

		public void Post(Host from, Atom packet)
		{
		}

		private StopReason stopReason = StopReason.None;
		public void Stop()
		{
			Stop(StopReason.UserShutdown);
		}

		public void Stop(StopReason reason)
		{
			stopReason = reason;
			cancelSource.Cancel();
		}

    public bool CheckAuthotization(string auth)
    {
      if (!accessControl.AuthorizationRequired || accessControl.AuthenticationKey==null) return true;
      if (auth==null) return false;
      var authorized = false;
      try {
        var authorization = System.Text.Encoding.ASCII.GetString(Convert.FromBase64String(auth)).Split(':');
        if (authorization.Length>=2) {
          var user = authorization[0];
          var pass = String.Join(":", authorization.Skip(1).ToArray());
          authorized = accessControl.CheckAuthorization(user, pass);
        }
      }
      catch (FormatException) {
      }
      catch (ArgumentException) {
      }
      return authorized;
    }

	}

	public class RTMPOutputStreamFactory
		: OutputStreamFactoryBase
	{
		public RTMPOutputStreamFactory(PeerCast peercast)
			: base(peercast)
		{
		}

		public override string Name
		{
			get { return "RTMP Output"; }
		}

		public override OutputStreamType OutputStreamType
		{
			get { return Core.OutputStreamType.Play; }
		}

		public override IOutputStream Create(
				System.IO.Stream input_stream,
				System.IO.Stream output_stream,
				System.Net.EndPoint remote_endpoint,
				AccessControlInfo access_control,
				Guid channel_id,
				byte[] header)
		{
			return new RTMPOutputStream(
					PeerCast,
					input_stream,
					output_stream,
					remote_endpoint,
					access_control,
					channel_id,
					header);
		}

		public override Guid? ParseChannelID(byte[] header)
		{
			if (header.Length>0 && header[0]==0x03) {
				return Guid.Empty;
			}
			else {
				return null;
			}
		}
	}

	[Plugin]
	public class RTMPOutputStreamPlugin
		: PluginBase
	{
		override public string Name { get { return "RTMP Output Stream"; } }

		private RTMPOutputStreamFactory factory;
		override protected void OnAttach()
		{
			if (factory==null) factory = new RTMPOutputStreamFactory(Application.PeerCast);
			Application.PeerCast.OutputStreamFactories.Add(factory);
		}

		override protected void OnDetach()
		{
			Application.PeerCast.OutputStreamFactories.Remove(factory);
		}
	}
}

