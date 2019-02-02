using Owin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Owin.Builder;

namespace PeerCastStation.Core.Http
{
  class OwinApplicationRegistration
    : IDisposable
  {
    private OwinHost host = null;
    private int key = -1;

    internal OwinApplicationRegistration(OwinHost host, int key)
    {
      this.host = host;
      this.key = key;
    }

    public void Dispose()
    {
      if (host==null || key<0) return;
      host.Unregister(key);
      throw new NotImplementedException();
    }
  }

  class OwinHost
  {
    private struct Factory
    {
      public OutputStreamType Type;
      public Action<IAppBuilder> ConfigAction;
    }
    private SortedList<int,Factory> applicationFactories = new SortedList<int, Factory>();
    private int nextKey = 0;

    public OwinApplicationRegistration Register(OutputStreamType type, Action<IAppBuilder> configAction)
    {
      var key = nextKey++;
      applicationFactories.Add(key, new Factory { Type=type, ConfigAction=configAction });
      return new OwinApplicationRegistration(this, key);
    }

    internal void Unregister(int key)
    {
      applicationFactories.Remove(key);
    }

    public Func<IDictionary<string,object>, Task> Build(OutputStreamType type)
    {
      var builder = new Microsoft.Owin.Builder.AppBuilder();
      foreach (var factory in applicationFactories.Where(f => f.Value.Type.HasFlag(type))) {
        factory.Value.ConfigAction(builder);
      }
      return builder.Build<Func<IDictionary<string,object>, Task>>();
    }

  }

  class OwinHostOutputStream
  {
  }

  public class OWinHostOutputStreamFactory
    : IOutputStreamFactory
  {
    public string Name { get; private set; }

    public int Priority { get; private set; }

    public OutputStreamType OutputStreamType { get; private set; }

    public IList<Action<IAppBuilder>> ApplicationFactories { get; private set; }

    public OWinHostOutputStreamFactory(string name, OutputStreamType type, int priority)
    {
      Name = name;
      OutputStreamType = type;
      Priority = priority;
      ApplicationFactories = new List<Action<IAppBuilder>>();
    }

    public IOutputStream Create(Stream input_stream, Stream output_stream, EndPoint remote_endpoint, AccessControlInfo access_control, Guid channel_id, byte[] header)
    {
      throw new NotImplementedException();
    }

    public Guid? ParseChannelID(byte[] header)
    {
      var idx = Array.IndexOf(header, (byte)'\r');
      if (idx<0 ||
          idx==header.Length-1 ||
          header[idx+1]!='\n') {
        return null;
      }
      try {
        var reqline = HttpRequest.ParseRequestLine(System.Text.Encoding.ASCII.GetString(header, 0, idx));
        if (reqline!=null) {
          return Guid.Empty;
        }
        else {
          return null;
        }
      }
      catch (ArgumentException) {
        return null;
      }
    }
  }

}
