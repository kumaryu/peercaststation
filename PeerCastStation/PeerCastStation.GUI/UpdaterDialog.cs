using System;
using System.Reflection;
using System.Windows.Forms;
using System.Diagnostics;

namespace PeerCastStation.GUI
{
  public partial class UpdaterDialog : Form
  {
    private VersionDescription versionInfo;
    public UpdaterDialog(VersionDescription vinfo)
    {
      versionInfo = vinfo;
      InitializeComponent();
      releaseNoteBrowser.DocumentText = versionInfo.Description;
      var cur = FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly().Location);
      newVersionLabel.Text = String.Format("新しいバージョンがダウンロードできます！(現在は{0})", cur.ProductVersion);
    }

    private void closeButton_Click(object sender, EventArgs e)
    {
      Close();
    }

    private void downloadButton_Click(object sender, EventArgs e)
    {
      System.Diagnostics.Process.Start(versionInfo.Link.ToString());
    }
  }
}
