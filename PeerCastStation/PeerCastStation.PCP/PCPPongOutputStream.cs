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
using System.Collections.Generic;
using System.Net;
using System.IO;

using PeerCastStation.Core;

namespace PeerCastStation.PCP
{
  public class PCPPongOutputStreamFactory
    : OutputStreamFactoryBase
  {
    public override string Name
    {
      get { return "PCPPong"; }
    }

    public override OutputStreamType OutputStreamType
    {
      get { return OutputStreamType.Metadata; }
    }

    public PCPPongOutputStreamFactory(PeerCast peercast)
      : base(peercast)
    {
    }

    public override IOutputStream Create(
      Stream input_stream,
      Stream output_stream,
      EndPoint remote_endpoint,
      AccessControlInfo access_control,
      Guid channel_id,
      byte[] header)
    {
      return new PCPPongOutputStream(PeerCast, input_stream, output_stream, (IPEndPoint)remote_endpoint, access_control, header);
    }

    public override Guid? ParseChannelID(byte[] header)
    {
      if (header.Length>=12 && 
          header[0]=='p' && 
          header[1]=='c' && 
          header[2]=='p' && 
          header[3]=='\n' &&
          header[4]==4 && 
          header[5]==0 && 
          header[6]==0 && 
          header[7]==0 &&
          header[8]==1 && 
          header[9]==0 && 
          header[10]==0 && 
          header[11]==0) {
        return Guid.Empty;
      }
      else {
        return null;
      }
    }
  }

  public class PCPPongOutputStream
    : OutputStreamBase
  {
    public override string ToString()
    {
      return String.Format("PCP(PONG) {0}", RemoteEndPoint);
    }

    public PCPPongOutputStream(
      PeerCast peercast,
      Stream input_stream,
      Stream output_stream,
      IPEndPoint endpoint,
      AccessControlInfo access_control,
      byte[] header)
      : base(peercast, input_stream, output_stream, endpoint, access_control, null, header)
    {
      Logger.Debug("Initialized: Remote {0}", endpoint);
    }

    protected override void OnStarted()
    {
      Logger.Debug("Starting");
     	base.OnStarted();
    }

    protected override void OnIdle()
    {
      base.OnIdle();
      Atom atom = null;
      while ((atom = RecvAtom())!=null) {
        ProcessAtom(atom);
      }
    }

    protected override void OnStopped()
    {
      base.OnStopped();
      Logger.Debug("Finished");
    }

    protected virtual void ProcessAtom(Atom atom)
    {
           if (atom.Name==Atom.PCP_HELO) OnPCPHelo(atom);
      else if (atom.Name==Atom.PCP_QUIT) OnPCPQuit(atom);
    }

    protected virtual void OnPCPHelo(Atom atom)
    {
      var session_id = atom.Children.GetHeloSessionID();
      var oleh = new AtomCollection();
      oleh.SetHeloSessionID(PeerCast.SessionID);
      Send(new Atom(Atom.PCP_OLEH, oleh));
      if (session_id==null) {
        Logger.Info("Helo has no SessionID");
        Stop(StopReason.NotIdentifiedError);
      }
      else {
        Logger.Debug("Helo from {0}", PeerCast.SessionID.ToString("N"));
        Stop(StopReason.None);
      }
    }

    protected virtual void OnPCPQuit(Atom atom)
    {
      Stop(StopReason.None);
    }

    public override OutputStreamType OutputStreamType
    {
      get { return OutputStreamType.Metadata;  }
    }

    public override ConnectionInfo GetConnectionInfo()
    {
      ConnectionStatus status = ConnectionStatus.Connected;
      if (IsStopped) {
        status = HasError ? ConnectionStatus.Error : ConnectionStatus.Idle;
      }
      return new ConnectionInfo(
        "PCP Pong",
        ConnectionType.Metadata,
        status,
        RemoteEndPoint.ToString(),
        (IPEndPoint)RemoteEndPoint,
        IsLocal ? RemoteHostStatus.Local : RemoteHostStatus.None,
        null,
        RecvRate,
        SendRate,
        null,
        null,
        null);
    }
  }

  [Plugin]
  class PCPPongOutputStreamPlugin
    : PluginBase
  {
    override public string Name { get { return "PCP Pong"; } }

    private PCPPongOutputStreamFactory factory;
    override protected void OnAttach()
    {
      if (factory==null) factory = new PCPPongOutputStreamFactory(Application.PeerCast);
      Application.PeerCast.OutputStreamFactories.Add(factory);
    }

    override protected void OnDetach()
    {
      Application.PeerCast.OutputStreamFactories.Remove(factory);
    }
  }
}
