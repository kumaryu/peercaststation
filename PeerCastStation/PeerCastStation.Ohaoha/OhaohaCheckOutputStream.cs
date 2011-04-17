using System;
using System.IO;
using System.Net;
using PeerCastStation.Core;
using PeerCastStation.HTTP;

namespace PeerCastStation.Ohaoha
{
  public class OhaohaCheckOutputStreamFactory
    : IOutputStreamFactory
  {
    public string Name
    {
      get { return "OhaohaCheck"; }
    }

    public IOutputStream Create(Stream stream, EndPoint remote_endpoint, Guid channel_id, byte[] header)
    {
      return new OhaohaCheckOutputStream(peercast, stream, remote_endpoint);
    }

    public Guid? ParseChannelID(byte[] header)
    {
      HTTPRequest res = null;
      var stream = new MemoryStream(header);
      try {
        res = HTTPRequestReader.Read(stream);
      }
      catch (EndOfStreamException) {
      }
      stream.Close();
      if (res!=null && res.Method=="GET" && res.Uri.AbsolutePath=="/" && res.Headers.Count==0) {
        return Guid.Empty;
      }
      else {
        return null;
      }
    }

    private PeerCast peercast;
    public OhaohaCheckOutputStreamFactory(PeerCast peercast)
    {
      this.peercast = peercast;
    }
  }

  public class OhaohaCheckOutputStream
    : IOutputStream
  {
    static Logger logger = new Logger(typeof(OhaohaCheckOutputStream));
    private PeerCast peercast;
    private Stream stream;

    public PeerCast PeerCast { get { return peercast; } }
    public Stream Stream { get { return stream; } }
    public bool IsLocal { get; private set; }
    public int UpstreamRate { get { return 0; } }

    public OhaohaCheckOutputStream(PeerCast peercast, Stream stream, EndPoint remote_endpoint)
    {
      logger.Debug("Initialized: Remote {0}", remote_endpoint);
      this.peercast = peercast;
      this.stream = stream;
      var ip = remote_endpoint as IPEndPoint;
      this.IsLocal = ip!=null ? Utils.IsSiteLocal(ip.Address) : true;
    }

    protected void WriteResponseHeader()
    {
    }

    public void Start()
    {
      logger.Debug("Started");
      var response = "HTTP/1.0 302 Found\r\nLocation: /html/index.html\r\n\r\n";
      var bytes = System.Text.Encoding.UTF8.GetBytes(response);
      stream.Write(bytes, 0, bytes.Length);
      stream.Close();
      logger.Debug("Finished");
    }

    public void Post(Host from, Atom packet)
    {
    }

    public void Close()
    {
      stream.Close();
    }

    public OutputStreamType OutputStreamType
    {
      get { return OutputStreamType.Metadata; }
    }
  }
}
