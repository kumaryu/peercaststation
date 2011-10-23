using System;
using System.Threading;
using System.Runtime.Remoting.Channels.Ipc;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting;

namespace PeerCastStation.GUI
{
  public class OpenUriRequestedEventArgs : EventArgs
  {
    public string Uri { get; private set; }
    public OpenUriRequestedEventArgs(string uri)
    {
      this.Uri = uri;
    }
  }

  public class ProcessControl
    : MarshalByRefObject
  {
    static bool isFirstInstance = false;
    static Mutex sharedMutex;
    static public bool IsFirstInstance { get { return isFirstInstance; } }
    static public ProcessControl Instance { get; private set; }
    static public event EventHandler<OpenUriRequestedEventArgs> OpenUriRequested;
    static ProcessControl()
    {
      sharedMutex = new Mutex(false, "PeerCastStation.GUI.ProcessControl", out isFirstInstance);
    }

    static public void Start()
    {
      if (isFirstInstance) {
        var channel = new IpcServerChannel("PeerCastStation.GUI");
        ChannelServices.RegisterChannel(channel, false);
        RemotingConfiguration.RegisterWellKnownServiceType(typeof(ProcessControl), "ProcessControl", WellKnownObjectMode.Singleton);
      }
      else {
        IpcClientChannel channel = new IpcClientChannel();
        ChannelServices.RegisterChannel(channel, false);
        RemotingConfiguration.RegisterWellKnownClientType(typeof(ProcessControl), "ipc://PeerCastStation.GUI/ProcessControl");
        Instance = new ProcessControl();
      }
    }

    static public void Stop()
    {
      if (!isFirstInstance) {
        Instance = null;
      }
    }

    public void OpenUri(string uri)
    {
      if (OpenUriRequested!=null) {
        OpenUriRequested(this, new OpenUriRequestedEventArgs(uri));
      }
    }
  }
}
