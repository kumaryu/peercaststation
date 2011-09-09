using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace PeerCastStation.GUI
{
  public partial class YellowPagesEditDialog : Form
  {
    public IList<YPSettings> YPSettingsList {
      get { return ypSettingsList; }
      set {
        ypSettingsList = new List<YPSettings>(value);
        ypSettingsBindingSource.DataSource = ypSettingsList;
      }
    }
    private IList<YPSettings> ypSettingsList = new List<YPSettings>();

    public YellowPagesEditDialog(PeerCastStation.Core.PeerCast peerCast)
    {
      InitializeComponent();
      ypProtocolList.Items.AddRange(peerCast.YellowPageFactories.Select(factory => factory.Name).ToArray());
      ypSettingsBindingSource.DataSource = ypSettingsList;
    }

    private void ypAddButton_Click(object sender, EventArgs e)
    {
      var new_item = new YPSettings() { Name = "新しいYP" };
      ypSettingsBindingSource.Add(new_item);
      ypSettingsBindingSource.Position = ypSettingsBindingSource.Count-1;
    }

    private void ypRemoveButton_Click(object sender, EventArgs e)
    {
      if (ypSettingsBindingSource.Current!=null) {
        ypSettingsBindingSource.RemoveCurrent();
      }
    }

    private void ypUpButton_Click(object sender, EventArgs e)
    {
      var current = ypSettingsBindingSource.Current;
      if (current!=null) {
        var idx = ypSettingsBindingSource.IndexOf(current);
        if (idx>0) {
          ypSettingsBindingSource.RemoveCurrent();
          ypSettingsBindingSource.Insert(idx-1, current);
          ypSettingsBindingSource.Position = idx-1;
        }
      }
    }

    private void ypDownButton_Click(object sender, EventArgs e)
    {
      var current = ypSettingsBindingSource.Current;
      if (current!=null) {
        var idx = ypSettingsBindingSource.IndexOf(current);
        if (idx<ypSettingsBindingSource.Count-1) {
          ypSettingsBindingSource.RemoveCurrent();
          ypSettingsBindingSource.Insert(idx+1, current);
          ypSettingsBindingSource.Position = idx+1;
        }
      }
    }
  }
}
