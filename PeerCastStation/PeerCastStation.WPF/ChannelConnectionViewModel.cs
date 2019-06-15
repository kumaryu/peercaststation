using PeerCastStation.Core;
using System;
using System.ComponentModel;

namespace PeerCastStation.WPF
{
  public abstract class ChannelConnectionViewModel
    : INotifyPropertyChanged
  {
    public virtual bool IsDisconnectable { get { return false; } }
    public virtual void Disconnect()
    {
      throw new InvalidOperationException(); 
    }

    public virtual bool IsReconnectable  { get { return false; } }
    public virtual void Reconnect()
    {
      throw new InvalidOperationException(); 
    }

    public abstract ConnectionStatus ConnectionStatus { get; }
    public abstract string Protocol           { get; }
    public abstract string Status             { get; }
    public abstract string RemoteName         { get; }
    public abstract string Bitrate            { get; }
    public abstract string ContentPosition    { get; }
    public abstract string Connections        { get; }
    public abstract string AgentName          { get; }
    public abstract object Connection         { get; }

    public virtual void Update()
    {
      if (PropertyChanged!=null) {
        PropertyChanged(this, new PropertyChangedEventArgs("ConnectionStatus"));
        PropertyChanged(this, new PropertyChangedEventArgs("Status"));
        PropertyChanged(this, new PropertyChangedEventArgs("RemoteName"));
        PropertyChanged(this, new PropertyChangedEventArgs("Bitrate"));
        PropertyChanged(this, new PropertyChangedEventArgs("ContentPosition"));
        PropertyChanged(this, new PropertyChangedEventArgs("Connections"));
        PropertyChanged(this, new PropertyChangedEventArgs("AgentName"));
      }
    }
    public event PropertyChangedEventHandler PropertyChanged;

    public override bool Equals(object obj)
    {
      if (obj==null || obj.GetType()!=this.GetType()) return false;
      return Connection.Equals(((ChannelConnectionViewModel)obj).Connection);
    }

    public override int GetHashCode()
    {
      if (Connection==null) return 0;
      return Connection.GetHashCode();
    }

    protected string GetRemoteName(ConnectionInfo connection_info)
    {
      var settings = PeerCastApplication.Current.Settings.Get<WPFSettings>();
      switch (settings.RemoteNodeName) {
      case RemoteNodeName.EndPoint:
        return connection_info.RemoteEndPoint!=null ?
               connection_info.RemoteEndPoint.ToString() :
               connection_info.RemoteName;
      case RemoteNodeName.SessionID:
        return connection_info.RemoteSessionID.HasValue ?
               connection_info.RemoteSessionID.Value.ToString("N").ToUpperInvariant() :
               connection_info.RemoteName;
      case RemoteNodeName.Uri:
      default:
        return connection_info.RemoteName;
      }
    }
  }

  public class SourceChannelConnectionViewModel
    : ChannelConnectionViewModel
  {
    private ISourceStream sourceStream;
    public SourceChannelConnectionViewModel(ISourceStream ss)
    {
      sourceStream = ss;
    }

    public override ConnectionStatus ConnectionStatus {
      get {
        var info = sourceStream.GetConnectionInfo();
        if ((info.RemoteHostStatus & RemoteHostStatus.Root)!=0) {
          return ConnectionStatus.ConnectionToRoot;
        }
        if ((info.RemoteHostStatus & RemoteHostStatus.Tracker)!=0) {
          return ConnectionStatus.ConnectionToTracker;
        }
        return ConnectionStatus.Unknown;
      }
    }

    public override string Protocol {
      get { return sourceStream.GetConnectionInfo().ProtocolName; }
    }

    public override string Status {
      get { return sourceStream.GetConnectionInfo().Status.ToString(); }
    }

    public override string RemoteName {
      get { return GetRemoteName(sourceStream.GetConnectionInfo()); }
    }

    public override string Bitrate {
      get {
        var info = sourceStream.GetConnectionInfo();
        var bitrate = (int)(((info.RecvRate ?? 0) + (info.SendRate ?? 0))*8/1000);
        return String.Format("{0}kbps", bitrate);
      }
    }

    public override string ContentPosition {
      get { return ""; }
    }

    public override string Connections {
      get { return ""; }
    }

    public override string AgentName {
      get { return sourceStream.GetConnectionInfo().AgentName; }
    }

    public override object Connection {
      get { return sourceStream; }
    }
  }

  class OutputChannelConnectionViewModel
    : ChannelConnectionViewModel
  {
    private IChannelSink outputStream;
    public OutputChannelConnectionViewModel(IChannelSink os)
    {
      outputStream = os;
    }

    public override bool IsDisconnectable { get { return true; } }
    public override void Disconnect()
    {
      outputStream.OnStopped(StopReason.UserShutdown);
    }

    public override ConnectionStatus ConnectionStatus {
      get {
        var info = outputStream.GetConnectionInfo();
        if (info.Type!=ConnectionType.Relay) {
          return ConnectionStatus.Unknown;
        }
        if ((info.RemoteHostStatus & RemoteHostStatus.Receiving)==0) {
          return ConnectionStatus.NotReceiving;
        }
        if ((info.RemoteHostStatus & RemoteHostStatus.Firewalled)!=0 &&
            (info.RemoteHostStatus & RemoteHostStatus.Local)==0) {
          if ((info.LocalRelays ?? 0)>0) {
            return ConnectionStatus.FirewalledRelaying;
          }
          else {
            return ConnectionStatus.Firewalled;
          }
        }
        else if ((info.RemoteHostStatus & RemoteHostStatus.RelayFull)!=0) {
          if ((info.LocalRelays ?? 0)>0) {
            return ConnectionStatus.RelayFull;
          }
          else {
            return ConnectionStatus.NotRelayable;
          }
        }
        else {
          return ConnectionStatus.Relayable;
        }
      }
    }

    public override string Protocol {
      get { return outputStream.GetConnectionInfo().ProtocolName; }
    }

    public override string Status {
      get { return outputStream.GetConnectionInfo().Status.ToString(); }
    }

    public override string RemoteName {
      get { return GetRemoteName(outputStream.GetConnectionInfo()); }
    }

    public override string Bitrate {
      get {
        var info = outputStream.GetConnectionInfo();
        var bitrate = (int)(((info.RecvRate ?? 0) + (info.SendRate ?? 0))*8/1000);
        return String.Format("{0}kbps", bitrate);
      }
    }

    public override string ContentPosition {
      get { return (outputStream.GetConnectionInfo().ContentPosition ?? 0).ToString(); }
    }

    public override string Connections {
      get {
        var info = outputStream.GetConnectionInfo();
        if (info.Type==ConnectionType.Relay) {
          return String.Format("[{0}/{1}]", info.LocalDirects ?? 0, info.LocalRelays ?? 0);
        }
        else {
          return "";
        }
      }
    }

    public override string AgentName {
      get { return outputStream.GetConnectionInfo().AgentName; }
    }

    public override object Connection { get { return outputStream; } }
  }

  class AnnounceChannelConnectionViewModel
    : ChannelConnectionViewModel
  {
    private IAnnouncingChannel announcingChannel;
    public AnnounceChannelConnectionViewModel(IAnnouncingChannel ac)
    {
      announcingChannel = ac;
    }

    public override bool IsReconnectable { get { return true; } }
    public override void Reconnect()
    {
      announcingChannel.YellowPage.RestartAnnounce(announcingChannel);
    }

    public override ConnectionStatus ConnectionStatus {
      get {
        var info = announcingChannel.GetConnectionInfo();
        if ((info.RemoteHostStatus & RemoteHostStatus.Root)!=0) {
          return ConnectionStatus.ConnectionToRoot;
        }
        if ((info.RemoteHostStatus & RemoteHostStatus.Tracker)!=0) {
          return ConnectionStatus.ConnectionToTracker;
        }
        return ConnectionStatus.Unknown;
      }
    }

    public override string Protocol {
      get { return announcingChannel.GetConnectionInfo().ProtocolName; }
    }

    public override string Status {
      get { return announcingChannel.GetConnectionInfo().Status.ToString(); }
    }

    public override string RemoteName {
      get { return GetRemoteName(announcingChannel.GetConnectionInfo()); }
    }

    public override string Bitrate {
      get {
        var info = announcingChannel.GetConnectionInfo();
        var bitrate = (int)(((info.RecvRate ?? 0) + (info.SendRate ?? 0))*8/1000);
        return String.Format("{0}kbps", bitrate);
      }
    }

    public override string ContentPosition {
      get { return ""; }
    }

    public override string Connections {
      get { return ""; }
    }

    public override string AgentName {
      get { return announcingChannel.GetConnectionInfo().AgentName; }
    }

    public override object Connection { get { return announcingChannel; } }
  }
}
