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
    public PeerCast PeerCast { get; private set; }
    public Stream Stream { get; private set; }
    public bool IsClosed { get; private set; }
    private EndPoint remoteEndPoint = null;
    private QueuedSynchronizationContext syncContext = null;

    public PCPPongOutputStream(PeerCast peercast, Stream stream, IPEndPoint endpoint, byte[] header)
    {
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
    }

    protected virtual void ProcessAtom(Atom atom)
    {
           if (atom.Name==Atom.PCP_HELO) OnPCPHelo(atom);
      else if (atom.Name==Atom.PCP_QUIT) OnPCPQuit(atom);
    }

    protected virtual void OnPCPHelo(Atom atom)
    {
      var session_id = atom.Children.GetHeloSessionID();
      var res = new Atom(Atom.PCP_OLEH, new AtomCollection());
      res.Children.SetHeloSessionID(PeerCast.SessionID);
      Send(res);
      if (session_id==null) {
        //相手のセッションIDが無かったらエラー終了
        var quit = new Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT+Atom.PCP_ERROR_NOTIDENTIFIED);
        Send(quit);
        Close();
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
