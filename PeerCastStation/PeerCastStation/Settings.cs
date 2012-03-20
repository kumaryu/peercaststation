using System;
using PeerCastStation.Core;
using System.Collections.ObjectModel;
using System.Collections.Generic;

namespace PeerCastStation
{
  [Serializable]
  public class ListenerSettings
  {
    public System.Net.IPEndPoint EndPoint { get; set; }
    public OutputStreamType LocalAccepts  { get; set; }
    public OutputStreamType GlobalAccepts { get; set; }
  }

  [Serializable]
  public class AccessControllerSettings
  {
    public int MaxRelays            { get; set; }
    public int MaxDirects           { get; set; }
    public int MaxRelaysPerChannel  { get; set; }
    public int MaxDirectsPerChannel { get; set; }
    public int MaxUpstreamRate      { get; set; }
  }

  [Serializable]
  public class YellowPageSettings
  {
    public string Protocol { get; set; }
    public string Name     { get; set; }
    public Uri    Uri      { get; set; }
  }
}
