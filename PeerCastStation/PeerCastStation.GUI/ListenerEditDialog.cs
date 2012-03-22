using System;
using System.Windows.Forms;
using PeerCastStation.Core;

namespace PeerCastStation.GUI
{
  public partial class ListenerEditDialog : Form
  {
    public int Port { get; set; }
    public System.Net.IPAddress Address { get; set; }
    public OutputStreamType LocalAccepts  { get; set; }
    public OutputStreamType GlobalAccepts { get; set; }
    public ListenerEditDialog()
    {
      InitializeComponent();
      addressText.SelectedIndex = 0;
    }

    private void addButton_Click(object sender, EventArgs e)
    {
      Port = (int)portNumber.Value;
      if (addressText.SelectedIndex==0) {
        Address = System.Net.IPAddress.Any;
      }
      else if (addressText.SelectedIndex==1) {
        Address = System.Net.IPAddress.IPv6Any;
      }
      else {
        Address = System.Net.IPAddress.Parse(addressText.Text);
      }
      LocalAccepts  = OutputStreamType.Metadata;
      GlobalAccepts = OutputStreamType.Metadata;
      if (portLocalDirect.Checked)     LocalAccepts |= OutputStreamType.Play;
      if (portLocalRelay.Checked)      LocalAccepts |= OutputStreamType.Relay;
      if (portLocalInterface.Checked)  LocalAccepts |= OutputStreamType.Interface;
      if (portGlobalDirect.Checked)    GlobalAccepts |= OutputStreamType.Play;
      if (portGlobalRelay.Checked)     GlobalAccepts |= OutputStreamType.Relay;
      if (portGlobalInterface.Checked) GlobalAccepts |= OutputStreamType.Interface;
      DialogResult = System.Windows.Forms.DialogResult.OK;
    }
  }
}
