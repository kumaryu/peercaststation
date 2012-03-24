using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using PeerCastStation.Core;

namespace PeerCastStation.GUI
{
  public partial class YellowPagesEditDialog : Form
  {
    public string YPName   { get; set; }
    public Uri    Uri      { get; set; }
    public string Protocol { get; set; }

    public YellowPagesEditDialog(PeerCast peerCast)
    {
      InitializeComponent();
      if (MainForm.IsOSX) {
        this.Font = new System.Drawing.Font("Osaka", this.Font.SizeInPoints);
      }
      ypProtocolList.Items.AddRange(peerCast.YellowPageFactories.Select(factory => factory.Name).ToArray());
    }

    private void okButton_Click(object sender, EventArgs e)
    {
      YPName   = ypNameText.Text;
      Protocol = ypProtocolList.Text;
      Uri uri;
      if (!String.IsNullOrEmpty(YPName) &&
          !String.IsNullOrEmpty(Protocol) &&
          (Uri.TryCreate(ypAddressText.Text, UriKind.Absolute, out uri) ||
           Uri.TryCreate(Protocol + "://" + ypAddressText.Text, UriKind.Absolute, out uri))) {
        Uri = uri;
        DialogResult = DialogResult.OK;
      }
    }
  }
}
