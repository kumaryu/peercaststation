// PeerCastStation, a P2P streaming servent.
// Copyright (C) 2011 Ryuichi Sakamoto (kumaryu@kumaryu.net)
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using PeerCastStation.Core;

namespace PeerCastStation.HTTP
{
  public class HTTPDummyOutputStreamFactory
    : IOutputStreamFactory
  {
    public string Name
    {
      get { return "HTTPDummy"; }
    }

    public IOutputStream Create(Stream stream, EndPoint remote_endpoint, Guid channel_id, byte[] header)
    {
      return new HTTPDummyOutputStream(peercast, stream, remote_endpoint);
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
      if (res!=null) return Guid.Empty;
      else           return null;
    }

    private PeerCast peercast;
    public HTTPDummyOutputStreamFactory(PeerCast peercast)
    {
      this.peercast = peercast;
    }
  }

  public class HTTPDummyOutputStream
    : IOutputStream
  {
    static Logger logger = new Logger(typeof(HTTPDummyOutputStream));
    private PeerCast peercast;
    private Stream stream;

    public PeerCast PeerCast { get { return peercast; } }
    public Stream Stream { get { return stream; } }
    public bool IsLocal { get; private set; }
    public int UpstreamRate { get { return 0; } }

    public HTTPDummyOutputStream(PeerCast peercast, Stream stream, EndPoint remote_endpoint)
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
      var response = "HTTP/1.0 404 NotFound\r\n\r\n";
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

