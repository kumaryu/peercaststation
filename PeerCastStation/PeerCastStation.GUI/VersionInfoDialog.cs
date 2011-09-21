using System;
using System.Linq;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;

namespace PeerCastStation.GUI
{
  public partial class VersionInfoDialog : Form
  {
    public VersionInfoDialog(string[] asm_patterns)
    {
      InitializeComponent();
      foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) {
        var name = asm.GetName();
        if (asm_patterns.Any(pattern => System.Text.RegularExpressions.Regex.IsMatch(name.Name, pattern))) {
          var info = FileVersionInfo.GetVersionInfo(asm.Location);
          versionsList.Items.Add(
            new ListViewItem(
              new string[] {
              Path.GetFileName(info.FileName),
              info.FileVersion,
              name.FullName,
              info.LegalCopyright}));
        }
      }
    }
  }
}
