using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PeerCastStation.Core
{
  public class HTTPRequest
  {
    public string Method { get; set; }
    public Uri Uri     { get; set; }

    public HTTPRequest(IEnumerable<string> responses)
    {
      foreach (var res in responses) {
        Match match = null;
        if ((match = Regex.Match(res, @"^(\w+) (\S+) HTTP/1.\d$")).Success) {
          this.Method = match.Groups[1].Value;
          Uri uri;
          if (Uri.TryCreate(match.Groups[2].Value, UriKind.Absolute, out uri) ||
              Uri.TryCreate(new Uri("http://localhost/"), match.Groups[2].Value, out uri)) {
            this.Uri = uri;
          }
          else {
            this.Uri = null;
          }
        }
      }
    }
  }

  public static class HTTPRequestReader
  {
    public static HTTPRequest Read(Stream stream)
    {
      string line = null;
      var requests = new List<string>();
      var buf = new List<byte>();
      while (line!="") {
        var value = stream.ReadByte();
        if (value<0) {
          throw new EndOfStreamException();
        }
        buf.Add((byte)value);
        if (buf.Count >= 2 && buf[buf.Count - 2] == '\r' && buf[buf.Count - 1] == '\n') {
          line = System.Text.Encoding.UTF8.GetString(buf.ToArray(), 0, buf.Count - 2);
          if (line!="") requests.Add(line);
          buf.Clear();
        }
      }
      return new HTTPRequest(requests);
    }
  }

  public class HTTPOutputStreamFactory
    : IOutputStreamFactory
  {
    public string Name
    {
      get { return "HTTP"; }
    }

    public IOutputStream Create(Stream stream, Channel channel, byte[] header)
    {
      var request = ParseRequest(header);
      if (request!=null) {
        return new HTTPOutputStream(core, stream, channel, request);
      }
      else {
        return null;
      }
    }

    public Guid? ParseChannelID(byte[] header)
    {
      var request = ParseRequest(header);
      if (request!=null &&
          (request.Method=="GET" || request.Method=="HEAD") &&
          request.Uri!=null) {
        Match match = null;
        if ((match = Regex.Match(request.Uri.AbsolutePath, @"^/(stream/|pls/)([0-9A-Fa-f]{32}).*$")).Success) {
          return new Guid(match.Groups[2].Value);
        }
      }
      return null;
    }

    private Core core;
    public HTTPOutputStreamFactory(Core core)
    {
      this.core = core;
    }

    private HTTPRequest ParseRequest(byte[] header)
    {
      HTTPRequest res = null;
      var stream = new MemoryStream(header);
      try {
        res = HTTPRequestReader.Read(stream);
      }
      catch (EndOfStreamException) {
      }
      stream.Close();
      return res;
    }

  }

  public class HTTPOutputStream
    : IOutputStream
  {
    private Core core;
    private Stream stream;
    private Channel channel;
    private HTTPRequest request;
    private volatile bool closed = false;
    private System.Threading.AutoResetEvent changedEvent = new System.Threading.AutoResetEvent(true);

    public Core Core { get { return core; } }
    public Stream Stream { get { return stream; } }
    public Channel Channel { get { return channel; } }
    public bool IsClosed { get { return closed; } }

    public HTTPOutputStream(Core core, Stream stream, Channel channel, HTTPRequest request)
    {
      this.core = core;
      this.stream = stream;
      this.channel = channel;
      this.request = request;
      if (this.channel!=null) {
        this.channel.ContentChanged += (sender, e) => {
          this.changedEvent.Set();
        };
        this.channel.Closed += (sender, e) => {
          this.closed = true;
          this.changedEvent.Set();
        };
      }
    }

    public enum BodyType {
      None,
      Content,
      Playlist,
    }

    protected virtual BodyType GetBodyType()
    {
      if (channel==null) {
        return BodyType.None;
      }
      else if (Regex.IsMatch(request.Uri.AbsolutePath, @"^/stream/[0-9A-Fa-f]{32}.*$")) {
        return BodyType.Content;
      }
      else if (Regex.IsMatch(request.Uri.AbsolutePath, @"^/pls/[0-9A-Fa-f]{32}.*$")) {
        return BodyType.Playlist;
      }
      else {
        return BodyType.None;
      }
    }

    protected string CreateResponseHeader()
    {
      switch (GetBodyType()) {
      case BodyType.None:
        return "HTTP/1.0 404 NotFound\r\n";
      case BodyType.Content:
        {
          bool mms = 
            channel.ChannelInfo.ContentType=="WMV" ||
            channel.ChannelInfo.ContentType=="WMA" ||
            channel.ChannelInfo.ContentType=="ASX";
          if (mms) {
            return
              "HTTP/1.0 200 OK\r\n" +
              "Server: Rex/9.0.2980\r\n" +
              "Cache-Control: no-cache\r\n" +
              "Pragme: no-cache\r\n" +
              "Pragme: features=\"broadcast,playlist\"\r\n" +
              "Content-Type: application/x-mms-framed\r\n";
          }
          else {
            return
              "HTTP/1.0 200 OK\r\n" +
              "Content-Type: " + channel.ChannelInfo.MIMEType + "\r\n";
          }
        }
      case BodyType.Playlist:
        return "HTTP/1.0 404 NotFound\r\n";
      default:
        return "HTTP/1.0 404 NotFound\r\n";
      }
    }

    protected void WriteResponseHeader()
    {
      var response_header = CreateResponseHeader();
      var bytes = System.Text.Encoding.UTF8.GetBytes(response_header + "\r\n");
      stream.Write(bytes, 0, bytes.Length);
    }

    protected virtual void WaitContentChanged()
    {
      changedEvent.WaitOne();
    }

    protected virtual void WriteResponseBody()
    {
      switch (GetBodyType()) {
      case BodyType.None:
        break;
      case BodyType.Content:
        bool header_sent = false;
        long last_pos = -1;
        while (!closed) {
          WaitContentChanged();
          bool sent = true;
          while (sent) {
            if (!header_sent) {
              header_sent = WriteContentHeader();
              sent = header_sent;
            }
            else {
              long new_pos = WriteContent(last_pos);
              sent = last_pos!=new_pos;
              last_pos = new_pos;
            }
          }
        }
        break;
      case BodyType.Playlist:
        break;
      }
    }

    public void Start()
    {
      if (!closed) {
        WriteResponseHeader();
        if (request.Method=="GET") {
          WriteResponseBody();
        }
        this.stream.Close();
      }
    }

    protected virtual bool WriteContentHeader()
    {
      if (channel.ContentHeader!=null) {
        if (WriteBytes(channel.ContentHeader.Data)) {
          return true;
        }
        else {
          closed = true;
          return false;
        }
      }
      else {
        return false;
      }
    }

    protected long WriteContent(long last_pos)
    {
      var content = channel.Contents.NextOf(last_pos);
      if (content!=null) {
        if (WriteBytes(content.Data)) {
          return content.Position;
        }
        else {
          closed = true;
          return last_pos;
        }
      }
      else {
        return last_pos;
      }
    }

    protected virtual bool WriteBytes(byte[] bytes)
    {
      try {
        stream.Write(bytes, 0, bytes.Length);
      }
      catch (IOException) {
        return false;
      }
      catch (NotSupportedException) {
        return false;
      }
      catch (ObjectDisposedException) {
        return false;
      }
      return true;
    }

    public void Post(Host from, Atom packet)
    {
    }

    public void Close()
    {
      if (!closed) {
        closed = true;
        changedEvent.Set();
        this.stream.Close();
      }
    }

    public OutputStreamType OutputStreamType
    {
      get { return OutputStreamType.Play; }
    }
  }
}
