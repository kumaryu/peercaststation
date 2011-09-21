namespace PeerCastStation.GUI
{
  partial class VersionInfoDialog
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
      System.Windows.Forms.Button button1;
      System.Windows.Forms.ColumnHeader versionColumn;
      System.Windows.Forms.ColumnHeader fileColumn;
      System.Windows.Forms.ColumnHeader assemblyName;
      System.Windows.Forms.ColumnHeader copyrightColumn;
      this.versionsList = new System.Windows.Forms.ListView();
      button1 = new System.Windows.Forms.Button();
      versionColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
      fileColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
      assemblyName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
      copyrightColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
      this.SuspendLayout();
      // 
      // button1
      // 
      button1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
      button1.DialogResult = System.Windows.Forms.DialogResult.OK;
      button1.Location = new System.Drawing.Point(317, 257);
      button1.Name = "button1";
      button1.Size = new System.Drawing.Size(75, 23);
      button1.TabIndex = 1;
      button1.Text = "閉じる";
      button1.UseVisualStyleBackColor = true;
      // 
      // versionColumn
      // 
      versionColumn.Text = "バージョン";
      // 
      // fileColumn
      // 
      fileColumn.Text = "ファイル";
      fileColumn.Width = 180;
      // 
      // assemblyName
      // 
      assemblyName.Text = "アセンブリ名";
      // 
      // copyrightColumn
      // 
      copyrightColumn.Text = "著作権";
      // 
      // versionsList
      // 
      this.versionsList.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.versionsList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            fileColumn,
            versionColumn,
            assemblyName,
            copyrightColumn});
      this.versionsList.FullRowSelect = true;
      this.versionsList.GridLines = true;
      this.versionsList.Location = new System.Drawing.Point(12, 12);
      this.versionsList.MultiSelect = false;
      this.versionsList.Name = "versionsList";
      this.versionsList.Size = new System.Drawing.Size(380, 239);
      this.versionsList.TabIndex = 2;
      this.versionsList.UseCompatibleStateImageBehavior = false;
      this.versionsList.View = System.Windows.Forms.View.Details;
      // 
      // VersionInfoDialog
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.ClientSize = new System.Drawing.Size(404, 292);
      this.Controls.Add(this.versionsList);
      this.Controls.Add(button1);
      this.MaximizeBox = false;
      this.MinimizeBox = false;
      this.Name = "VersionInfoDialog";
      this.ShowIcon = false;
      this.ShowInTaskbar = false;
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
      this.Text = "バージョン情報";
      this.ResumeLayout(false);

    }

    #endregion

    private System.Windows.Forms.ListView versionsList;
  }
}