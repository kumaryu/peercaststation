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
      foreach (var asm in app.Plugins.Select(type => type.Assembly).Distinct()) {
        var info = FileVersionInfo.GetVersionInfo(asm.Location);
        versionsList.Items.Add(
          new ListViewItem(
            new string[] {
            Path.GetFileName(info.FileName),
            info.FileVersion,
            asm.FullName,
            info.LegalCopyright}));
      }
    }
  }
}
