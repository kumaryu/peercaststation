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
  public class PCPPongOutputStreamFactory : IOutputStreamFactory
  {
    public string Name
    {
      get { return "PCPPong"; }
    }

    public PeerCast PeerCast { get; private set; }
    public PCPPongOutputStreamFactory(PeerCast peercast)
    {
      PeerCast = peercast;
    }

    public IOutputStream Create(
      Stream stream,
      EndPoint remote_endpoint,
      Guid channel_id,
      byte[] header)
    {
      return new PCPPongOutputStream(PeerCast, stream, (IPEndPoint)remote_endpoint, header);
    }

    public Guid? ParseChannelID(byte[] header)
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

  public class PCPPongOutputStream : IOutputStream
  {
    static private Logger logger = new Logger(typeof(PCPPongOutputStream));
    public PeerCast PeerCast { get; private set; }
    public Stream Stream { get; private set; }
    public bool IsClosed { get; private set; }
    private EndPoint remoteEndPoint = null;
    private QueuedSynchronizationContext syncContext = null;

    public override string ToString()
    {
      return String.Format("PCP(PONG) {0} ({1})", remoteEndPoint);
    }

    public PCPPongOutputStream(PeerCast peercast, Stream stream, IPEndPoint endpoint, byte[] header)
    {
      logger.Debug("Initialized: Remote {0}", endpoint);
      PeerCast = peercast;
      Stream = stream;
      remoteEndPoint = endpoint;
      recvStream.Write(header, 0, header.Length);
    }

    public bool IsLocal
    {
      get {
        var ip = remoteEndPoint as IPEndPoint;
        if (ip!=null) {
          return PeerCastStation.Core.Utils.IsSiteLocal(ip.Address);
        }
        else {
          return true;
        }
      }
    }

    public int UpstreamRate
    {
      get { return 0; }
    }

    public void Start()
    {
      logger.Debug("Starting");
      if (this.syncContext == null) {
        this.syncContext = new QueuedSynchronizationContext();
        System.Threading.SynchronizationContext.SetSynchronizationContext(this.syncContext);
      }
      StartReceive();
      while (!IsClosed) {
        Atom atom = null;
        while ((atom = RecvAtom())!=null) {
          ProcessAtom(atom);
        }
        ProcessSend();
        if (syncContext!=null) syncContext.ProcessAll();
      }
      Close();
      if (syncContext!=null) syncContext.ProcessAll();
      logger.Debug("Finished");
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
        logger.Info("Helo has no SessionID");
        //相手のセッションIDが無かったらエラー終了
        var quit = new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT+Atom.PCP_ERROR_NOTIDENTIFIED);
        Send(quit);
        Close();
      }
      else {
        logger.Debug("Helo from {0}", PeerCast.SessionID.ToString("N"));
      }
    }

    protected virtual void OnPCPQuit(Atom atom)
    {
      Close();
    }

    public void Post(Host from, Atom packet)
    {
      //Do nothing
    }

    MemoryStream recvStream = new MemoryStream();
    byte[] recvBuffer = new byte[8192];
    private void StartReceive()
    {
      if (!IsClosed) {
        try {
          Stream.BeginRead(recvBuffer, 0, recvBuffer.Length, (ar) => {
            Stream s = (Stream)ar.AsyncState;
            try {
              int bytes = s.EndRead(ar);
              if (bytes > 0) {
                syncContext.Post(x => {
                  recvStream.Seek(0, SeekOrigin.End);
                  recvStream.Write(recvBuffer, 0, bytes);
                  recvStream.Seek(0, SeekOrigin.Begin);
                  StartReceive();
                }, null);
              }
              else {
                Close();
              }
            }
            catch (ObjectDisposedException) {}
            catch (IOException) {
              Close();
            }
          }, Stream);
        }
        catch (ObjectDisposedException) {}
        catch (IOException) {
          Close();
        }
      }
    }

    MemoryStream sendStream = new MemoryStream(8192);
    IAsyncResult sendResult = null;
    private void ProcessSend()
    {
      if (sendResult!=null && sendResult.IsCompleted) {
        try {
          Stream.EndWrite(sendResult);
        }
        catch (ObjectDisposedException) {
        }
        catch (IOException) {
          Close();
        }
        sendResult = null;
      }
      if (!IsClosed && sendResult==null && sendStream.Length>0) {
        var buf = sendStream.ToArray();
        sendStream.SetLength(0);
        sendStream.Position = 0;
        try {
          sendResult = Stream.BeginWrite(buf, 0, buf.Length, null, null);
        }
        catch (ObjectDisposedException) {
        }
        catch (IOException) {
          Close();
        }
      }
    }

    protected virtual void Send(byte[] bytes)
    {
      sendStream.Write(bytes, 0, bytes.Length);
    }

    protected virtual void Send(Atom atom)
    {
      AtomWriter.Write(sendStream, atom);
    }

    protected Atom RecvAtom()
    {
      Atom res = null;
      if (recvStream.Length>=8 && Recv(s => { res = AtomReader.Read(s); })) {
        return res;
      }
      else {
        return null;
      }
    }

    protected bool Recv(Action<Stream> proc)
    {
      bool res = false;
      recvStream.Seek(0, SeekOrigin.Begin);
      try {
        proc(recvStream);
        recvStream = dropStream(recvStream);
        res = true;
      }
      catch (EndOfStreamException) {
      }
      return res;
    }

    static private MemoryStream dropStream(MemoryStream s)
    {
      var res = new MemoryStream((int)Math.Max(8192, s.Length - s.Position));
      res.Write(s.GetBuffer(), (int)s.Position, (int)(s.Length - s.Position));
      res.Position = 0;
      return res;
    }

    private void DoClose()
    {
      IsClosed = true;
      if (sendResult!=null) {
        try {
          Stream.EndWrite(sendResult);
        }
        catch (ObjectDisposedException) {}
        catch (IOException) {}
        sendResult = null;
      }
      Stream.Close();
      sendStream.SetLength(0);
      sendStream.Position = 0;
      recvStream.SetLength(0);
      recvStream.Position = 0;
    }

    public void Close()
    {
      if (!IsClosed) {
        if (syncContext!=null) {
          syncContext.Post(x => {
            DoClose();
          }, null);
        }
        else {
          DoClose();
        }
      }
    }

    public OutputStreamType OutputStreamType
    {
      get { return OutputStreamType.Metadata;  }
    }
  }
}
