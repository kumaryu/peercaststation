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
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.HTTP
{
  public class HTTPDummyOutputStreamFactory
    : OutputStreamFactoryBase
  {
    public override string Name
    {
      get { return "HTTPDummy"; }
    }

    public override OutputStreamType OutputStreamType
    {
      get {
        return
          OutputStreamType.Interface |
          OutputStreamType.Metadata |
          OutputStreamType.Relay |
          OutputStreamType.Play;
      }
    }

    public override IOutputStream Create(
      Stream input_stream,
      Stream output_stream,
      EndPoint remote_endpoint,
      AccessControlInfo access_control,
      Guid channel_id,
      byte[] header)
    {
      using (var stream = new MemoryStream(header)) {
        var req = HTTPRequestReader.Read(stream);
        return new HTTPDummyOutputStream(
          PeerCast,
          input_stream,
          output_stream,
          remote_endpoint,
          access_control,
          req);
      }
    }

    public override Guid? ParseChannelID(byte[] header)
    {
      using (var stream=new MemoryStream(header)) {
        var res = HTTPRequestReader.Read(stream);
        if (res!=null) return Guid.Empty;
        else           return null;
      }
    }

    public HTTPDummyOutputStreamFactory(PeerCast peercast)
      : base(peercast)
    {
    }

    public override int Priority
    {
      get { return int.MaxValue; }
    }
  }

  public class HTTPDummyOutputStream
    : OutputStreamBase
  {
    private HTTPRequest request;
    public HTTPDummyOutputStream(
      PeerCast peercast,
      Stream input_stream,
      Stream output_stream,
      EndPoint remote_endpoint,
      AccessControlInfo access_control,
      HTTPRequest req)
      : base(peercast, input_stream, output_stream, remote_endpoint, access_control, null, null)
    {
      Logger.Debug("Initialized: Remote {0}", remote_endpoint);
      this.request = req;
    }

    protected override async Task<StopReason> DoProcess(CancellationToken cancel_token)
    {
      var response = "HTTP/1.0 404 NotFound\r\n\r\n";
      var bytes = System.Text.Encoding.UTF8.GetBytes(response);
      await Connection.WriteAsync(bytes, cancel_token);
      return StopReason.OffAir;
    }

    public override OutputStreamType OutputStreamType
    {
      get { return OutputStreamType.Metadata; }
    }

    public override ConnectionInfo GetConnectionInfo()
    {
      return new ConnectionInfo(
        "No Protocol Matched",
        ConnectionType.Metadata,
        ConnectionStatus.Connected,
        RemoteEndPoint.ToString(),
        (IPEndPoint)RemoteEndPoint,
        IsLocal ? RemoteHostStatus.Local : RemoteHostStatus.None,
        null,
        Connection.ReadRate,
        Connection.WriteRate,
        null,
        null,
        request.Headers["USER-AGENT"]);
    }
  }

  [Plugin]
  class HTTPDummyOutputStreamPlugin
    : PluginBase
  {
    override public string Name { get { return "HTTP Dummy Output"; } }

    private HTTPDummyOutputStreamFactory factory;
    override protected void OnAttach()
    {
      if (factory==null) factory = new HTTPDummyOutputStreamFactory(Application.PeerCast);
      Application.PeerCast.OutputStreamFactories.Add(factory);
    }

    override protected void OnDetach()
    {
      Application.PeerCast.OutputStreamFactories.Remove(factory);
    }
  }
}

