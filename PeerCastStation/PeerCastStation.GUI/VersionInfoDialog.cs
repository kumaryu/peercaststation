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
        var asm = plugin.GetType().Assembly;
        var info = FileVersionInfo.GetVersionInfo(asm.Location);
        versionsList.Items.Add(
          new ListViewItem(
            new string[] {
              plugin.Name,
              plugin.IsUsable.ToString(),
              Path.GetFileName(info.FileName),
              info.FileVersion,
              asm.FullName,
              info.LegalCopyright,
            }
          )
        );
      }
    }
  }
}
