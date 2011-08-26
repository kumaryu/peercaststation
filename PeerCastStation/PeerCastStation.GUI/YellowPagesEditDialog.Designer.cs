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
      this.components = new System.ComponentModel.Container();
      System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
      System.Windows.Forms.Panel panel3;
      System.Windows.Forms.Panel panel1;
      System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
      System.Windows.Forms.Button ypRemoveButton;
      System.Windows.Forms.Button ypAddButton;
      System.Windows.Forms.Label label17;
      System.Windows.Forms.Button ypDownButton;
      System.Windows.Forms.Button ypUpButton;
      System.Windows.Forms.Panel panel2;
      System.Windows.Forms.Label label18;
      System.Windows.Forms.Label label16;
      System.Windows.Forms.Label label15;
      System.Windows.Forms.Label label14;
      this.button2 = new System.Windows.Forms.Button();
      this.button1 = new System.Windows.Forms.Button();
      this.ypList = new System.Windows.Forms.ListBox();
      this.ypSettingsBindingSource = new System.Windows.Forms.BindingSource(this.components);
      this.ypEnabledCheck = new System.Windows.Forms.CheckBox();
      this.ypProtocolList = new System.Windows.Forms.ComboBox();
      this.ypAddressText = new System.Windows.Forms.TextBox();
      this.ypNameText = new System.Windows.Forms.TextBox();
      tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
      panel3 = new System.Windows.Forms.Panel();
      panel1 = new System.Windows.Forms.Panel();
      tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
      ypRemoveButton = new System.Windows.Forms.Button();
      ypAddButton = new System.Windows.Forms.Button();
      label17 = new System.Windows.Forms.Label();
      ypDownButton = new System.Windows.Forms.Button();
      ypUpButton = new System.Windows.Forms.Button();
      panel2 = new System.Windows.Forms.Panel();
      label18 = new System.Windows.Forms.Label();
      label16 = new System.Windows.Forms.Label();
      label15 = new System.Windows.Forms.Label();
      label14 = new System.Windows.Forms.Label();
      tableLayoutPanel1.SuspendLayout();
      panel3.SuspendLayout();
      panel1.SuspendLayout();
      tableLayoutPanel2.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)(this.ypSettingsBindingSource)).BeginInit();
      panel2.SuspendLayout();
      this.SuspendLayout();
      // 
      // tableLayoutPanel1
      // 
      tableLayoutPanel1.ColumnCount = 2;
      tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
      tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
      tableLayoutPanel1.Controls.Add(panel3, 0, 1);
      tableLayoutPanel1.Controls.Add(panel1, 0, 0);
      tableLayoutPanel1.Controls.Add(panel2, 1, 0);
      tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
      tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
      tableLayoutPanel1.Name = "tableLayoutPanel1";
      tableLayoutPanel1.RowCount = 2;
      tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
      tableLayoutPanel1.Size = new System.Drawing.Size(521, 338);
      tableLayoutPanel1.TabIndex = 14;
      // 
      // panel3
      // 
      panel3.AutoSize = true;
      panel3.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
      tableLayoutPanel1.SetColumnSpan(panel3, 2);
      panel3.Controls.Add(this.button2);
      panel3.Controls.Add(this.button1);
      panel3.Dock = System.Windows.Forms.DockStyle.Fill;
      panel3.Location = new System.Drawing.Point(3, 306);
      panel3.Name = "panel3";
      panel3.Size = new System.Drawing.Size(515, 29);
      panel3.TabIndex = 7;
      // 
      // button2
      // 
      this.button2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
      this.button2.DialogResult = System.Windows.Forms.DialogResult.Cancel;
      this.button2.Location = new System.Drawing.Point(437, 3);
      this.button2.Name = "button2";
      this.button2.Size = new System.Drawing.Size(75, 23);
      this.button2.TabIndex = 1;
      this.button2.Text = "キャンセル";
      this.button2.UseVisualStyleBackColor = true;
      // 
      // button1
      // 
      this.button1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
      this.button1.DialogResult = System.Windows.Forms.DialogResult.OK;
      this.button1.Location = new System.Drawing.Point(356, 3);
      this.button1.Name = "button1";
      this.button1.Size = new System.Drawing.Size(75, 23);
      this.button1.TabIndex = 0;
      this.button1.Text = "OK";
      this.button1.UseVisualStyleBackColor = true;
      // 
      // panel1
      // 
      panel1.Controls.Add(tableLayoutPanel2);
      panel1.Controls.Add(label17);
      panel1.Controls.Add(ypDownButton);
      panel1.Controls.Add(ypUpButton);
      panel1.Controls.Add(this.ypList);
      panel1.Dock = System.Windows.Forms.DockStyle.Fill;
      panel1.Location = new System.Drawing.Point(0, 0);
      panel1.Margin = new System.Windows.Forms.Padding(0);
      panel1.Name = "panel1";
      panel1.Size = new System.Drawing.Size(260, 303);
      panel1.TabIndex = 0;
      // 
      // tableLayoutPanel2
      // 
      tableLayoutPanel2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      tableLayoutPanel2.ColumnCount = 2;
      tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
      tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
      tableLayoutPanel2.Controls.Add(ypRemoveButton, 0, 0);
      tableLayoutPanel2.Controls.Add(ypAddButton, 0, 0);
      tableLayoutPanel2.Location = new System.Drawing.Point(4, 270);
      tableLayoutPanel2.Margin = new System.Windows.Forms.Padding(0);
      tableLayoutPanel2.Name = "tableLayoutPanel2";
      tableLayoutPanel2.RowCount = 1;
      tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      tableLayoutPanel2.Size = new System.Drawing.Size(219, 26);
      tableLayoutPanel2.TabIndex = 4;
      // 
      // ypRemoveButton
      // 
      ypRemoveButton.Dock = System.Windows.Forms.DockStyle.Fill;
      ypRemoveButton.Location = new System.Drawing.Point(112, 3);
      ypRemoveButton.Name = "ypRemoveButton";
      ypRemoveButton.Size = new System.Drawing.Size(104, 20);
      ypRemoveButton.TabIndex = 1;
      ypRemoveButton.Text = "削除(&R)";
      ypRemoveButton.UseVisualStyleBackColor = true;
      ypRemoveButton.Click += new System.EventHandler(this.ypRemoveButton_Click);
      // 
      // ypAddButton
      // 
      ypAddButton.Dock = System.Windows.Forms.DockStyle.Fill;
      ypAddButton.Location = new System.Drawing.Point(3, 3);
      ypAddButton.Name = "ypAddButton";
      ypAddButton.Size = new System.Drawing.Size(103, 20);
      ypAddButton.TabIndex = 0;
      ypAddButton.Text = "追加(&A)";
      ypAddButton.UseVisualStyleBackColor = true;
      ypAddButton.Click += new System.EventHandler(this.ypAddButton_Click);
      // 
      // label17
      // 
      label17.AutoSize = true;
      label17.Location = new System.Drawing.Point(4, 5);
      label17.Name = "label17";
      label17.Size = new System.Drawing.Size(101, 12);
      label17.TabIndex = 0;
      label17.Text = "YellowPage一覧(&L)";
      // 
      // ypDownButton
      // 
      ypDownButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
      ypDownButton.AutoSize = true;
      ypDownButton.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
      ypDownButton.Location = new System.Drawing.Point(229, 49);
      ypDownButton.Name = "ypDownButton";
      ypDownButton.Size = new System.Drawing.Size(27, 22);
      ypDownButton.TabIndex = 3;
      ypDownButton.Text = "↓";
      ypDownButton.UseVisualStyleBackColor = true;
      ypDownButton.Click += new System.EventHandler(this.ypDownButton_Click);
      // 
      // ypUpButton
      // 
      ypUpButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
      ypUpButton.AutoSize = true;
      ypUpButton.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
      ypUpButton.Location = new System.Drawing.Point(229, 20);
      ypUpButton.Name = "ypUpButton";
      ypUpButton.Size = new System.Drawing.Size(27, 22);
      ypUpButton.TabIndex = 2;
      ypUpButton.Text = "↑";
      ypUpButton.UseVisualStyleBackColor = true;
      ypUpButton.Click += new System.EventHandler(this.ypUpButton_Click);
      // 
      // ypList
      // 
      this.ypList.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.ypList.DataSource = this.ypSettingsBindingSource;
      this.ypList.DisplayMember = "Name";
      this.ypList.FormattingEnabled = true;
      this.ypList.IntegralHeight = false;
      this.ypList.ItemHeight = 12;
      this.ypList.Location = new System.Drawing.Point(4, 20);
      this.ypList.Name = "ypList";
      this.ypList.Size = new System.Drawing.Size(219, 244);
      this.ypList.TabIndex = 1;
      // 
      // ypSettingsBindingSource
      // 
      this.ypSettingsBindingSource.DataSource = typeof(PeerCastStation.GUI.YPSettings);
      // 
      // panel2
      // 
      panel2.Controls.Add(this.ypEnabledCheck);
      panel2.Controls.Add(label18);
      panel2.Controls.Add(this.ypProtocolList);
      panel2.Controls.Add(label16);
      panel2.Controls.Add(this.ypAddressText);
      panel2.Controls.Add(label15);
      panel2.Controls.Add(label14);
      panel2.Controls.Add(this.ypNameText);
      panel2.Dock = System.Windows.Forms.DockStyle.Fill;
      panel2.Location = new System.Drawing.Point(260, 0);
      panel2.Margin = new System.Windows.Forms.Padding(0);
      panel2.Name = "panel2";
      panel2.Size = new System.Drawing.Size(261, 303);
      panel2.TabIndex = 1;
      // 
      // ypEnabledCheck
      // 
      this.ypEnabledCheck.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.ypEnabledCheck.AutoSize = true;
      this.ypEnabledCheck.DataBindings.Add(new System.Windows.Forms.Binding("Checked", this.ypSettingsBindingSource, "Enabled", true));
      this.ypEnabledCheck.Location = new System.Drawing.Point(72, 96);
      this.ypEnabledCheck.Name = "ypEnabledCheck";
      this.ypEnabledCheck.Size = new System.Drawing.Size(123, 16);
      this.ypEnabledCheck.TabIndex = 6;
      this.ypEnabledCheck.Text = "このYPを使用する(&E)";
      this.ypEnabledCheck.UseVisualStyleBackColor = true;
      // 
      // label18
      // 
      label18.AutoSize = true;
      label18.Location = new System.Drawing.Point(2, 5);
      label18.Name = "label18";
      label18.Size = new System.Drawing.Size(102, 12);
      label18.TabIndex = 0;
      label18.Text = "YellowPage設定(&S)";
      // 
      // ypProtocolList
      // 
      this.ypProtocolList.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.ypProtocolList.DataBindings.Add(new System.Windows.Forms.Binding("SelectedItem", this.ypSettingsBindingSource, "Protocol", true));
      this.ypProtocolList.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
      this.ypProtocolList.FormattingEnabled = true;
      this.ypProtocolList.Location = new System.Drawing.Point(72, 45);
      this.ypProtocolList.Name = "ypProtocolList";
      this.ypProtocolList.Size = new System.Drawing.Size(181, 20);
      this.ypProtocolList.TabIndex = 4;
      // 
      // label16
      // 
      label16.AutoSize = true;
      label16.Location = new System.Drawing.Point(2, 48);
      label16.Name = "label16";
      label16.Size = new System.Drawing.Size(64, 12);
      label16.TabIndex = 3;
      label16.Text = "プロトコル(&P)";
      // 
      // ypAddressText
      // 
      this.ypAddressText.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.ypAddressText.DataBindings.Add(new System.Windows.Forms.Binding("Text", this.ypSettingsBindingSource, "Address", true));
      this.ypAddressText.Location = new System.Drawing.Point(72, 71);
      this.ypAddressText.Name = "ypAddressText";
      this.ypAddressText.Size = new System.Drawing.Size(181, 19);
      this.ypAddressText.TabIndex = 0;
      // 
      // label15
      // 
      label15.AutoSize = true;
      label15.Location = new System.Drawing.Point(3, 74);
      label15.Name = "label15";
      label15.Size = new System.Drawing.Size(57, 12);
      label15.TabIndex = 5;
      label15.Text = "アドレス(&D)";
      // 
      // label14
      // 
      label14.AutoSize = true;
      label14.Location = new System.Drawing.Point(3, 23);
      label14.Name = "label14";
      label14.Size = new System.Drawing.Size(47, 12);
      label14.TabIndex = 1;
      label14.Text = "YP名(&N)";
      // 
      // ypNameText
      // 
      this.ypNameText.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.ypNameText.DataBindings.Add(new System.Windows.Forms.Binding("Text", this.ypSettingsBindingSource, "Name", true));
      this.ypNameText.Location = new System.Drawing.Point(72, 20);
      this.ypNameText.Name = "ypNameText";
      this.ypNameText.Size = new System.Drawing.Size(181, 19);
      this.ypNameText.TabIndex = 2;
      // 
      // YellowPagesEditDialog
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.ClientSize = new System.Drawing.Size(521, 338);
      this.Controls.Add(tableLayoutPanel1);
      this.MaximizeBox = false;
      this.MinimizeBox = false;
      this.Name = "YellowPagesEditDialog";
      this.ShowIcon = false;
      this.ShowInTaskbar = false;
      this.Text = "YellowPage設定の編集";
      tableLayoutPanel1.ResumeLayout(false);
      tableLayoutPanel1.PerformLayout();
      panel3.ResumeLayout(false);
      panel1.ResumeLayout(false);
      panel1.PerformLayout();
      tableLayoutPanel2.ResumeLayout(false);
      ((System.ComponentModel.ISupportInitialize)(this.ypSettingsBindingSource)).EndInit();
      panel2.ResumeLayout(false);
      panel2.PerformLayout();
      this.ResumeLayout(false);

    }

    #endregion

    private System.Windows.Forms.ListBox ypList;
    private System.Windows.Forms.CheckBox ypEnabledCheck;
    private System.Windows.Forms.ComboBox ypProtocolList;
    private System.Windows.Forms.TextBox ypAddressText;
    private System.Windows.Forms.TextBox ypNameText;
    private System.Windows.Forms.Button button2;
    private System.Windows.Forms.Button button1;
    private System.Windows.Forms.BindingSource ypSettingsBindingSource;
  }
}