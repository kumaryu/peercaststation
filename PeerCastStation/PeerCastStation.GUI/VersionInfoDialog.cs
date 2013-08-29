using System;
using System.Linq;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;

namespace PeerCastStation.GUI
{
  public partial class VersionInfoDialog : Form
  {
    public VersionInfoDialog(PeerCastStation.Core.PeerCastApplication app)
    {
      InitializeComponent();
      foreach (var plugin in app.Plugins) {
        var info = plugin.GetVersionInfo();
        versionsList.Items.Add(
          new ListViewItem(
            new string[] {
              plugin.Name,
              plugin.IsUsable.ToString(),
              info.FileName,
              info.Version,
              info.AssemblyName,
              info.Copyright,
            }
          )
        );
      }
    }
  }
}
