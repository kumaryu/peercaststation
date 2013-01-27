using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PeerCastStation.Core;
using PeerCastStation.WPF.Commons;

namespace PeerCastStation.WPF.CoreSettings
{
  class YellowPagesEditViewModel
  {
    private readonly YellowPageFactoryItem[] protocols;
    public YellowPageFactoryItem[] Protocols { get { return protocols; } }
    private readonly Command add;
    public Command Add { get { return add; } }

    public string Name { get; set; }
    public YellowPageFactoryItem SelectedProtocol { get; set; }
    public string Address { get; set; }

    public YellowPagesEditViewModel(PeerCast peerCast)
    {
      protocols = peerCast.YellowPageFactories
        .Select(factory => new YellowPageFactoryItem(factory)).ToArray();
      add = new Command(() =>
      {
        if (String.IsNullOrEmpty(Name)
            || SelectedProtocol == null)
          return;
        var factory = SelectedProtocol.Factory;
        var protocol = factory.Protocol;
        if (String.IsNullOrEmpty(protocol))
          return;

        Uri uri;
        var md = Regex.Match(Address, @"\A([^:/]+)(:(\d+))?\Z");
        if (md.Success &&
          Uri.CheckHostName(md.Groups[1].Value) != UriHostNameType.Unknown &&
          Uri.TryCreate(protocol + "://" + Address, UriKind.Absolute, out uri) &&
          factory.CheckURI(uri))
        {
        }
        else if (Uri.TryCreate(Address, UriKind.Absolute, out uri) &&
          factory.CheckURI(uri))
        {
        }
        else
        {
          return;
        }

        peerCast.AddYellowPage(protocol, Name, uri);
      });
    }
  }
}
