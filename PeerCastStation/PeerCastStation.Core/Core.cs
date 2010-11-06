using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

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
    IEnumerable<string> Extensions { get; }
    string Description { get; }
    Uri Contact { get; }
    void Register(Core core);
    void Unregister(Core core);
  }

  public interface IYellowPageFactory : IDisposable
  {
    string Name { get; }
    IYellowPage Create(string name, Uri uri);
  }
  
  public interface IYellowPage : IDisposable
  {
    string Name { get; }
    Uri    Uri  { get; }
    Host FindTracker(Guid channel_id);
    IEnumerable<ChannelInfo> ListChannels();
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
    public IList<Atom> Extra { get; set; }

    public Host()
    {
      Addresses = new List<IPEndPoint>();
      SessionID = Guid.Empty;
      BroadcastID = Guid.Empty;
      IsFirewalled = false;
      Extensions = new List<string>();
      Extra = new List<Atom>();
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

  public class Channel
  {
    public ISourceStream SourceStream { get; set; }
    public IList<IOutputStream> OutputStreams { get; private set; }
    public IList<Node> Nodes       { get; private set; }
    public ChannelInfo ChannelInfo { get; set; }
    public IList<Content> Contents { get; private set; }

    public EventHandler StatusChanged;
    public EventHandler ChannelInfoUpdated;
    public EventHandler NodeUpdated;
    public EventHandler ContentUpdated;
    public EventHandler Error;
  }

  public class Node
  {
    public Host Host         { get; set; }
    public int RelayCount    { get; set; }
    public int DirectCount   { get; set; }
    public bool IsRelayFull  { get; set; }
    public bool IsDirectFull { get; set; }
    public IList<Atom> Extra { get; set; }
  }

  public class ChannelInfo
  {
    public Host Tracker      { get; set; }
    public Guid ChannelID    { get; set; }
    public string Name       { get; set; }
    public IList<Atom> Extra { get; set; }
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
    public IEnumerable<IPlugIn> PlugIns       { get; private set; }
    public IList<IYellowPage>   YellowPages   { get; private set; }
    public IList<IYellowPageFactory>   YellowPageFactories   { get; private set; }
    public IList<ISourceStreamFactory> SourceStreamFactories { get; private set; }
    public IList<IOutputStreamFactory> OutputStreamFactories { get; private set; }
    public Channel RelayChannel(Guid channel_id) { return null; }
    public Channel RelayChannel(Guid channel_id, string protocol, IPEndPoint tracker) { return null; }
    public Channel BroadcastChannel(IYellowPage yp, Guid channel_id, string protocol, Uri source) { return null; }
  }
}
