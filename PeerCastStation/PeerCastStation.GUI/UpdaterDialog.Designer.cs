namespace PeerCastStation.GUI
{
  partial class UpdaterDialog
  {
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
      if (disposing && (components != null)) {
        components.Dispose();
      }
      base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
      this.releaseNoteBrowser = new System.Windows.Forms.WebBrowser();
      this.newVersionLabel = new System.Windows.Forms.Label();
      this.closeButton = new System.Windows.Forms.Button();
      this.downloadButton = new System.Windows.Forms.Button();
      this.SuspendLayout();
      // 
      // releaseNoteBrowser
      // 
      this.releaseNoteBrowser.AllowNavigation = false;
      this.releaseNoteBrowser.AllowWebBrowserDrop = false;
      this.releaseNoteBrowser.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.releaseNoteBrowser.IsWebBrowserContextMenuEnabled = false;
      this.releaseNoteBrowser.Location = new System.Drawing.Point(12, 28);
      this.releaseNoteBrowser.MinimumSize = new System.Drawing.Size(20, 20);
      this.releaseNoteBrowser.Name = "releaseNoteBrowser";
      this.releaseNoteBrowser.ScriptErrorsSuppressed = true;
      this.releaseNoteBrowser.Size = new System.Drawing.Size(498, 243);
      this.releaseNoteBrowser.TabIndex = 0;
      this.releaseNoteBrowser.WebBrowserShortcutsEnabled = false;
      // 
      // newVersionLabel
      // 
      this.newVersionLabel.AutoSize = true;
      this.newVersionLabel.Location = new System.Drawing.Point(12, 9);
      this.newVersionLabel.Name = "newVersionLabel";
      this.newVersionLabel.Size = new System.Drawing.Size(202, 12);
      this.newVersionLabel.TabIndex = 1;
      this.newVersionLabel.Text = "新しいバージョンがダウンロード可能です！";
      // 
      // closeButton
      // 
      this.closeButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
      this.closeButton.Location = new System.Drawing.Point(435, 277);
      this.closeButton.Name = "closeButton";
      this.closeButton.Size = new System.Drawing.Size(75, 23);
      this.closeButton.TabIndex = 2;
      this.closeButton.Text = "閉じる";
      this.closeButton.UseVisualStyleBackColor = true;
      this.closeButton.Click += new System.EventHandler(this.closeButton_Click);
      // 
      // downloadButton
      // 
      this.downloadButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
      this.downloadButton.Location = new System.Drawing.Point(354, 277);
      this.downloadButton.Name = "downloadButton";
      this.downloadButton.Size = new System.Drawing.Size(75, 23);
      this.downloadButton.TabIndex = 3;
      this.downloadButton.Text = "ダウンロード";
      this.downloadButton.UseVisualStyleBackColor = true;
      this.downloadButton.Click += new System.EventHandler(this.downloadButton_Click);
      // 
      // UpdaterDialog
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.ClientSize = new System.Drawing.Size(522, 312);
      this.Controls.Add(this.downloadButton);
      this.Controls.Add(this.closeButton);
      this.Controls.Add(this.newVersionLabel);
      this.Controls.Add(this.releaseNoteBrowser);
      this.MaximizeBox = false;
      this.MinimizeBox = false;
      this.Name = "UpdaterDialog";
      this.ShowIcon = false;
      this.ShowInTaskbar = false;
      this.Text = "新しいバージョンのダウンロード";
      this.ResumeLayout(false);
      this.PerformLayout();

    }

    #endregion

    private System.Windows.Forms.WebBrowser releaseNoteBrowser;
    private System.Windows.Forms.Label newVersionLabel;
    private System.Windows.Forms.Button closeButton;
    private System.Windows.Forms.Button downloadButton;
  }
}