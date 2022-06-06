using System;
using System.Net;

namespace PeerCastStation.Core
{
  public class ConnectionInfoBuilder
  {
    public string     ProtocolName    { get; set; } = "";
    public ConnectionType   Type      { get; set; } = ConnectionType.None;
    public ConnectionStatus Status    { get; set; } = ConnectionStatus.Idle;
    public IPEndPoint? RemoteEndPoint  { get; set; } = null;
    public RemoteHostStatus RemoteHostStatus { get; set; } = RemoteHostStatus.None;
    public Guid?      RemoteSessionID { get; set; } = null;
    public long?      ContentPosition { get; set; } = null;
    public float?     RecvRate        { get; set; } = null;
    public float?     SendRate        { get; set; } = null;
    public int?       LocalRelays     { get; set; } = null;
    public int?       LocalDirects    { get; set; } = null;
    public string?    AgentName       { get; set; } = null;
    public string?    RemoteName      { get; set; } = null;

    public ConnectionInfoBuilder()
    {
    }

    public ConnectionInfoBuilder(ConnectionInfo other)
    {
      this.ProtocolName     = other.ProtocolName;
      this.Type             = other.Type;
      this.Status           = other.Status;
      this.RemoteName       = other.RemoteName;
      this.RemoteEndPoint   = other.RemoteEndPoint;
      this.RemoteHostStatus = other.RemoteHostStatus;
      this.RemoteSessionID  = other.RemoteSessionID;
      this.ContentPosition  = other.ContentPosition;
      this.RecvRate         = other.RecvRate;
      this.SendRate         = other.SendRate;
      this.LocalRelays      = other.LocalRelays;
      this.LocalDirects     = other.LocalDirects;
      this.AgentName        = other.AgentName;
    }

    public ConnectionInfo Build()
    {
      return new ConnectionInfo(
        this.ProtocolName,
        this.Type,
        this.Status,
        this.RemoteName,
        this.RemoteEndPoint,
        this.RemoteHostStatus,
        this.RemoteSessionID,
        this.ContentPosition,
        this.RecvRate,
        this.SendRate,
        this.LocalRelays,
        this.LocalDirects,
        this.AgentName);
    }

  }

}
