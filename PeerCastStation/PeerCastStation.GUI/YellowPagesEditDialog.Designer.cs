namespace PeerCastStation.GUI
{
  partial class YellowPagesEditDialog
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
      System.Windows.Forms.Label label14;
      System.Windows.Forms.Label label16;
      System.Windows.Forms.Label label15;
      this.ypAddressText = new System.Windows.Forms.TextBox();
      this.ypProtocolList = new System.Windows.Forms.ComboBox();
      this.ypNameText = new System.Windows.Forms.TextBox();
      this.okButton = new System.Windows.Forms.Button();
      this.cancelButton = new System.Windows.Forms.Button();
      label14 = new System.Windows.Forms.Label();
      label16 = new System.Windows.Forms.Label();
      label15 = new System.Windows.Forms.Label();
      this.SuspendLayout();
      // 
      // label14
      // 
      label14.AutoSize = true;
      label14.Location = new System.Drawing.Point(12, 15);
      label14.Name = "label14";
      label14.Size = new System.Drawing.Size(47, 12);
      label14.TabIndex = 0;
      label14.Text = "YP名(&N)";
      label14.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // label16
      // 
      label16.AutoSize = true;
      label16.Location = new System.Drawing.Point(12, 40);
      label16.Name = "label16";
      label16.Size = new System.Drawing.Size(64, 12);
      label16.TabIndex = 2;
      label16.Text = "プロトコル(&P)";
      label16.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // label15
      // 
      label15.AutoSize = true;
      label15.Location = new System.Drawing.Point(12, 66);
      label15.Name = "label15";
      label15.Size = new System.Drawing.Size(57, 12);
      label15.TabIndex = 4;
      label15.Text = "アドレス(&D)";
      label15.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // ypAddressText
      // 
      this.ypAddressText.Location = new System.Drawing.Point(82, 63);
      this.ypAddressText.Name = "ypAddressText";
      this.ypAddressText.Size = new System.Drawing.Size(198, 19);
      this.ypAddressText.TabIndex = 5;
      // 
      // ypProtocolList
      // 
      this.ypProtocolList.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
      this.ypProtocolList.FormattingEnabled = true;
      this.ypProtocolList.Location = new System.Drawing.Point(82, 37);
      this.ypProtocolList.Name = "ypProtocolList";
      this.ypProtocolList.Size = new System.Drawing.Size(198, 20);
      this.ypProtocolList.TabIndex = 3;
      // 
      // ypNameText
      // 
      this.ypNameText.Location = new System.Drawing.Point(82, 12);
      this.ypNameText.Name = "ypNameText";
      this.ypNameText.Size = new System.Drawing.Size(198, 19);
      this.ypNameText.TabIndex = 1;
      // 
      // okButton
      // 
      this.okButton.Location = new System.Drawing.Point(124, 90);
      this.okButton.Name = "okButton";
      this.okButton.Size = new System.Drawing.Size(75, 23);
      this.okButton.TabIndex = 6;
      this.okButton.Text = "追加";
      this.okButton.UseVisualStyleBackColor = true;
      this.okButton.Click += new System.EventHandler(this.okButton_Click);
      // 
      // cancelButton
      // 
      this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
      this.cancelButton.Location = new System.Drawing.Point(205, 90);
      this.cancelButton.Name = "cancelButton";
      this.cancelButton.Size = new System.Drawing.Size(75, 23);
      this.cancelButton.TabIndex = 7;
      this.cancelButton.Text = "キャンセル";
      this.cancelButton.UseVisualStyleBackColor = true;
      // 
      // YellowPagesEditDialog
      // 
      this.AcceptButton = this.okButton;
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.CancelButton = this.cancelButton;
      this.ClientSize = new System.Drawing.Size(292, 125);
      this.Controls.Add(this.cancelButton);
      this.Controls.Add(this.okButton);
      this.Controls.Add(label14);
      this.Controls.Add(this.ypAddressText);
      this.Controls.Add(this.ypProtocolList);
      this.Controls.Add(label16);
      this.Controls.Add(this.ypNameText);
      this.Controls.Add(label15);
      this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
      this.MaximizeBox = false;
      this.MinimizeBox = false;
      this.Name = "YellowPagesEditDialog";
      this.ShowIcon = false;
      this.ShowInTaskbar = false;
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
      this.Text = "YellowPageの追加";
      this.ResumeLayout(false);
      this.PerformLayout();

    }

    #endregion

    private System.Windows.Forms.TextBox ypAddressText;
    private System.Windows.Forms.ComboBox ypProtocolList;
    private System.Windows.Forms.TextBox ypNameText;
    private System.Windows.Forms.Button okButton;
    private System.Windows.Forms.Button cancelButton;

  }
}