using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.ComponentModel;
using System.Collections.ObjectModel;

namespace PeerCastStation.Core
{
  public interface IPlugInLoader : IDisposable
  {
    string Name { get; }
    IPlugIn Load(Uri uri);
  }

  public interface IPlugIn : IDisposable
  {
    string Name { get; }
    ICollection<string> Extensions { get; }
    string Description { get; }
    Uri Contact { get; }
    void Register(Core core);
    void Unregister(Core core);
  }

  public interface IYellowPageFactory : IDisposable
  {
    IYellowPage Create(string name, Uri uri);
  }

  public class TrackerDescription
  {
    public Host Host { get; private set; }
    public string Protocol { get; private set; }

    public TrackerDescription(Host host, string protocol)
    {
      Host = host;
      Protocol = protocol;
    }
  }

  public interface IYellowPage : IDisposable
  {
    string Name { get; }
    Uri    Uri  { get; }
    TrackerDescription FindTracker(Guid channel_id);
    ICollection<ChannelInfo> ListChannels();
    void Announce(Channel channel);
  }

  public interface ISourceStreamFactory : IDisposable
  {
    ISourceStream Create();
  }

  public interface ISourceStream : IDisposable
  {
    void Start(Host tracker, Channel channel);
    void Close();
  }

  public interface IOutputStreamFactory : IDisposable
  {
    IOutputStream Create();
  }

  public interface IOutputStream : IDisposable
  {
    void Start(Stream stream, Channel channel);
    void Close();
  }

  public class Host
  {
    public IList<IPEndPoint> Addresses { get; private set; }
    public Guid SessionID { get; set; }
    public Guid BroadcastID { get; set; }
    public bool IsFirewalled { get; set; }
    public IList<string> Extensions { get; private set; }
    public AtomCollection Extra { get; private set; }

    public Host()
    {
      Addresses    = new List<IPEndPoint>();
      SessionID    = Guid.Empty;
      BroadcastID  = Guid.Empty;
      IsFirewalled = false;
      Extensions   = new List<string>();
      Extra        = new AtomCollection();
    }
  }

  public class Atom
  {
    public string Name  { get; private set; }
    public object Value { get; private set; }

    public Atom(string name, object value)
    {
      if (name.Length > 4) {
        throw new ArgumentException("Atom Name length must be 4 or less.");
      }
      Name = name;
      Value = value;
    }
  }

  public class AtomCollection : ObservableCollection<Atom>
  {
  }

  public class ChannelInfo
    : INotifyPropertyChanged
  {
    private Guid channelID;
    private Host tracker = null;
    private string name = "";
    public Host Tracker {
      get { return tracker; }
      set {
        tracker = value;
        OnPropertyChanged("Tracker");
      }
    }
    public Guid ChannelID {
      get { return channelID; }
    }
    public string Name {
      get { return name; }
      set {
        name = value;
        OnPropertyChanged("Name");
      }
    }
    private AtomCollection extra = new AtomCollection();
    public AtomCollection Extra { get { return extra; } }
    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged(string name)
    {
      if (PropertyChanged != null) {
        PropertyChanged(this, new PropertyChangedEventArgs(name));
      }
    }

    public ChannelInfo(Guid channel_id)
    {
      channelID = channel_id;
      extra.CollectionChanged += (sender, e) => {
        OnPropertyChanged("Extra");
      };
    }
  }

  public class Node
    : INotifyPropertyChanged
  {
    private Host host = null;
    private int relayCount = 0;
    private int directCount = 0;
    private bool isRelayFull = false;
    private bool isDirectFull = false;
    private AtomCollection extra = new AtomCollection();
    public Host Host {
      get { return host; }
      set
      {
        host = value;
        OnPropertyChanged("Host");
      }
    }
    public int RelayCount {
      get { return relayCount; }
      set
      {
        relayCount = value;
        OnPropertyChanged("RelayCount");
      }
    }
    public int DirectCount {
      get { return directCount; }
      set
      {
        directCount = value;
        OnPropertyChanged("DirectCount");
      }
    }
    public bool IsRelayFull {
      get { return isRelayFull; }
      set
      {
        isRelayFull = value;
        OnPropertyChanged("IsRelayFull");
      }
    }
    public bool IsDirectFull {
      get { return isDirectFull; }
      set
      {
        isDirectFull = value;
        OnPropertyChanged("IsDirectFull");
      }
    }
    public AtomCollection Extra { get { return extra; } }
    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged(string name)
    {
      if (PropertyChanged != null) {
        PropertyChanged(this, new PropertyChangedEventArgs(name));
      }
    }

    public Node(Host host)
    {
      Host = host;
      extra.CollectionChanged += (sender, e) => {
        OnPropertyChanged("Extra");
      };
    }
  }

  public class Channel
  {
    public enum ChannelStatus
    {
      Idle,
      Searching,
      Connecting,
      Receiving,
      Error,
      Closed
    }
    public ChannelStatus Status { get; set; }
    public ISourceStream SourceStream         { get; set; }
    public IList<IOutputStream> OutputStreams { get; private set; }
    public IList<Node> Nodes                  { get; private set; }
    public ChannelInfo ChannelInfo            { get; set; }
    public Content ContentHeader              { get; set; }
    public IList<Content> Contents            { get; private set; }

    public event EventHandler StatusChanged;
    public event EventHandler ChannelInfoUpdated;
    public event EventHandler NodeUpdated;
    public event EventHandler ContentUpdated;
    public event EventHandler Error;
    public event EventHandler Closed;

    public void Close()
    {
    }

    internal Channel(Guid channel_id, ISourceStream source)
    {
      SourceStream  = source;
      ChannelInfo   = new ChannelInfo(channel_id);
      ContentHeader = null;
      Contents      = new List<Content>();
      Nodes         = new List<Node>();
    }
  }

  public class Content
  {
    public Content(long pos, byte[] data)
    {
      Position = pos;
      Data = data;
    }

    public long Position { get; private set; } 
    public byte[] Data   { get; private set; } 
  }

  public class Core
  {
    public Host Host { get; set; }
    public IList<IPlugInLoader> PlugInLoaders { get; private set; }
    public ICollection<IPlugIn> PlugIns       { get; private set; }
    public IList<IYellowPage>   YellowPages   { get; private set; }
    public IDictionary<string, IYellowPageFactory>   YellowPageFactories   { get; private set; }
    public IDictionary<string, ISourceStreamFactory> SourceStreamFactories { get; private set; }
    public IDictionary<string, IOutputStreamFactory> OutputStreamFactories { get; private set; }
    public ICollection<Channel> Channels { get { return channels; } }
    private List<Channel> channels = new List<Channel>();
    public IPlugIn LoadPlugIn(Uri uri)
    {
      return null;
    }

    public Channel RelayChannel(Guid channel_id)
    {
      foreach (var yp in YellowPages) {
        var tracker = yp.FindTracker(channel_id);
        if (tracker!=null && tracker.Host!=null && tracker.Host.Addresses.Count>0) {
          return RelayChannel(channel_id, tracker.Protocol, tracker.Host);
        }
      }
      return null;
    }
    public Channel RelayChannel(Guid channel_id, string protocol, Host tracker)
    {
      ISourceStreamFactory source_factory = null;
      if (!SourceStreamFactories.TryGetValue(protocol, out source_factory)) {
        throw new ArgumentException(String.Format("Protocol `{0}' is not found", protocol));
      }
      var source_stream = source_factory.Create();
      var channel = new Channel(channel_id, source_stream);
      channels.Add(channel);
      source_stream.Start(tracker, channel);
      return channel;
    }
    public Channel BroadcastChannel(IYellowPage yp, Guid channel_id, string protocol, Uri source) { return null; }

    public void CloseChannel(Channel channel)
    {
      channel.Close();
      channels.Remove(channel);
    }

    public Core(IPEndPoint ip)
    {
      Host = new Host();
      Host.Addresses.Add(ip);
      Host.SessionID = Guid.NewGuid();

      PlugInLoaders = new List<IPlugInLoader>();
      PlugIns       = new List<IPlugIn>();
      YellowPages   = new List<IYellowPage>();
      YellowPageFactories = new Dictionary<string, IYellowPageFactory>();
      SourceStreamFactories = new Dictionary<string, ISourceStreamFactory>();
      OutputStreamFactories = new Dictionary<string, IOutputStreamFactory>();
    }
  }
}
