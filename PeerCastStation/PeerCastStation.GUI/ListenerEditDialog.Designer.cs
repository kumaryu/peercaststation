namespace PeerCastStation.GUI
{
  partial class ListenerEditDialog
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
      System.Windows.Forms.Label label1;
      System.Windows.Forms.Label label2;
      System.Windows.Forms.Label label27;
      System.Windows.Forms.Label label28;
      this.addressText = new System.Windows.Forms.ComboBox();
      this.portNumber = new System.Windows.Forms.NumericUpDown();
      this.portGlobalInterface = new System.Windows.Forms.CheckBox();
      this.portLocalInterface = new System.Windows.Forms.CheckBox();
      this.portLocalDirect = new System.Windows.Forms.CheckBox();
      this.portLocalRelay = new System.Windows.Forms.CheckBox();
      this.portGlobalDirect = new System.Windows.Forms.CheckBox();
      this.portGlobalRelay = new System.Windows.Forms.CheckBox();
      this.addButton = new System.Windows.Forms.Button();
      this.cancelButton = new System.Windows.Forms.Button();
      label1 = new System.Windows.Forms.Label();
      label2 = new System.Windows.Forms.Label();
      label27 = new System.Windows.Forms.Label();
      label28 = new System.Windows.Forms.Label();
      ((System.ComponentModel.ISupportInitialize)(this.portNumber)).BeginInit();
      this.SuspendLayout();
      // 
      // label1
      // 
      label1.AutoSize = true;
      label1.Location = new System.Drawing.Point(12, 15);
      label1.Name = "label1";
      label1.Size = new System.Drawing.Size(41, 12);
      label1.TabIndex = 0;
      label1.Text = "アドレス";
      // 
      // addressText
      // 
      this.addressText.FormattingEnabled = true;
      this.addressText.Items.AddRange(new object[] {
            "IPv4 Any",
            "IPv6 Any"});
      this.addressText.Location = new System.Drawing.Point(141, 12);
      this.addressText.Name = "addressText";
      this.addressText.Size = new System.Drawing.Size(151, 20);
      this.addressText.TabIndex = 1;
      // 
      // portNumber
      // 
      this.portNumber.Location = new System.Drawing.Point(141, 38);
      this.portNumber.Maximum = new decimal(new int[] {
            65535,
            0,
            0,
            0});
      this.portNumber.Minimum = new decimal(new int[] {
            1024,
            0,
            0,
            0});
      this.portNumber.Name = "portNumber";
      this.portNumber.Size = new System.Drawing.Size(151, 19);
      this.portNumber.TabIndex = 2;
      this.portNumber.Value = new decimal(new int[] {
            7144,
            0,
            0,
            0});
      // 
      // label2
      // 
      label2.AutoSize = true;
      label2.Location = new System.Drawing.Point(12, 40);
      label2.Name = "label2";
      label2.Size = new System.Drawing.Size(57, 12);
      label2.TabIndex = 3;
      label2.Text = "ポート番号";
      // 
      // portGlobalInterface
      // 
      this.portGlobalInterface.AutoSize = true;
      this.portGlobalInterface.Location = new System.Drawing.Point(251, 85);
      this.portGlobalInterface.Name = "portGlobalInterface";
      this.portGlobalInterface.Size = new System.Drawing.Size(48, 16);
      this.portGlobalInterface.TabIndex = 17;
      this.portGlobalInterface.Text = "操作";
      this.portGlobalInterface.UseVisualStyleBackColor = true;
      // 
      // label27
      // 
      label27.AutoSize = true;
      label27.Location = new System.Drawing.Point(12, 64);
      label27.Name = "label27";
      label27.Size = new System.Drawing.Size(124, 12);
      label27.TabIndex = 10;
      label27.Text = "LAN内からの接続を許可";
      label27.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // portLocalInterface
      // 
      this.portLocalInterface.AutoSize = true;
      this.portLocalInterface.Checked = true;
      this.portLocalInterface.CheckState = System.Windows.Forms.CheckState.Checked;
      this.portLocalInterface.Location = new System.Drawing.Point(251, 63);
      this.portLocalInterface.Name = "portLocalInterface";
      this.portLocalInterface.Size = new System.Drawing.Size(48, 16);
      this.portLocalInterface.TabIndex = 13;
      this.portLocalInterface.Text = "操作";
      this.portLocalInterface.UseVisualStyleBackColor = true;
      // 
      // label28
      // 
      label28.AutoSize = true;
      label28.Location = new System.Drawing.Point(12, 86);
      label28.Name = "label28";
      label28.Size = new System.Drawing.Size(115, 12);
      label28.TabIndex = 14;
      label28.Text = "WANからの接続を許可";
      label28.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // portLocalDirect
      // 
      this.portLocalDirect.AutoSize = true;
      this.portLocalDirect.Checked = true;
      this.portLocalDirect.CheckState = System.Windows.Forms.CheckState.Checked;
      this.portLocalDirect.Location = new System.Drawing.Point(197, 63);
      this.portLocalDirect.Name = "portLocalDirect";
      this.portLocalDirect.Size = new System.Drawing.Size(48, 16);
      this.portLocalDirect.TabIndex = 12;
      this.portLocalDirect.Text = "視聴";
      this.portLocalDirect.UseVisualStyleBackColor = true;
      // 
      // portLocalRelay
      // 
      this.portLocalRelay.AutoSize = true;
      this.portLocalRelay.Checked = true;
      this.portLocalRelay.CheckState = System.Windows.Forms.CheckState.Checked;
      this.portLocalRelay.Location = new System.Drawing.Point(141, 63);
      this.portLocalRelay.Name = "portLocalRelay";
      this.portLocalRelay.Size = new System.Drawing.Size(50, 16);
      this.portLocalRelay.TabIndex = 11;
      this.portLocalRelay.Text = "リレー";
      this.portLocalRelay.UseVisualStyleBackColor = true;
      // 
      // portGlobalDirect
      // 
      this.portGlobalDirect.AutoSize = true;
      this.portGlobalDirect.Location = new System.Drawing.Point(197, 85);
      this.portGlobalDirect.Name = "portGlobalDirect";
      this.portGlobalDirect.Size = new System.Drawing.Size(48, 16);
      this.portGlobalDirect.TabIndex = 16;
      this.portGlobalDirect.Text = "視聴";
      this.portGlobalDirect.UseVisualStyleBackColor = true;
      // 
      // portGlobalRelay
      // 
      this.portGlobalRelay.AutoSize = true;
      this.portGlobalRelay.Checked = true;
      this.portGlobalRelay.CheckState = System.Windows.Forms.CheckState.Checked;
      this.portGlobalRelay.Location = new System.Drawing.Point(141, 86);
      this.portGlobalRelay.Name = "portGlobalRelay";
      this.portGlobalRelay.Size = new System.Drawing.Size(50, 16);
      this.portGlobalRelay.TabIndex = 15;
      this.portGlobalRelay.Text = "リレー";
      this.portGlobalRelay.UseVisualStyleBackColor = true;
      // 
      // addButton
      // 
      this.addButton.Location = new System.Drawing.Point(136, 115);
      this.addButton.Name = "addButton";
      this.addButton.Size = new System.Drawing.Size(75, 23);
      this.addButton.TabIndex = 18;
      this.addButton.Text = "追加";
      this.addButton.UseVisualStyleBackColor = true;
      this.addButton.Click += new System.EventHandler(this.addButton_Click);
      // 
      // cancelButton
      // 
      this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
      this.cancelButton.Location = new System.Drawing.Point(217, 115);
      this.cancelButton.Name = "cancelButton";
      this.cancelButton.Size = new System.Drawing.Size(75, 23);
      this.cancelButton.TabIndex = 19;
      this.cancelButton.Text = "キャンセル";
      this.cancelButton.UseVisualStyleBackColor = true;
      // 
      // ListenerEditDialog
      // 
      this.AcceptButton = this.addButton;
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.CancelButton = this.cancelButton;
      this.ClientSize = new System.Drawing.Size(305, 148);
      this.Controls.Add(this.cancelButton);
      this.Controls.Add(this.addButton);
      this.Controls.Add(this.portGlobalInterface);
      this.Controls.Add(label27);
      this.Controls.Add(this.portLocalInterface);
      this.Controls.Add(label28);
      this.Controls.Add(this.portLocalDirect);
      this.Controls.Add(this.portLocalRelay);
      this.Controls.Add(this.portGlobalDirect);
      this.Controls.Add(this.portGlobalRelay);
      this.Controls.Add(label2);
      this.Controls.Add(this.portNumber);
      this.Controls.Add(this.addressText);
      this.Controls.Add(label1);
      this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
      this.MaximizeBox = false;
      this.MinimizeBox = false;
      this.Name = "ListenerEditDialog";
      this.ShowIcon = false;
      this.ShowInTaskbar = false;
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
      this.Text = "ポートの追加";
      ((System.ComponentModel.ISupportInitialize)(this.portNumber)).EndInit();
      this.ResumeLayout(false);
      this.PerformLayout();

    }

    #endregion

    private System.Windows.Forms.ComboBox addressText;
    private System.Windows.Forms.NumericUpDown portNumber;
    private System.Windows.Forms.CheckBox portGlobalInterface;
    private System.Windows.Forms.CheckBox portLocalInterface;
    private System.Windows.Forms.CheckBox portLocalDirect;
    private System.Windows.Forms.CheckBox portLocalRelay;
    private System.Windows.Forms.CheckBox portGlobalDirect;
    private System.Windows.Forms.CheckBox portGlobalRelay;
    private System.Windows.Forms.Button addButton;
    private System.Windows.Forms.Button cancelButton;

  }
}