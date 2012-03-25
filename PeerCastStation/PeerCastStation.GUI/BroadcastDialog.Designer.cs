namespace PeerCastStation.GUI
{
  partial class BroadcastDialog
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
      System.Windows.Forms.GroupBox groupBox1;
      System.Windows.Forms.Label label1;
      System.Windows.Forms.Label label12;
      System.Windows.Forms.Label label11;
      System.Windows.Forms.Label label10;
      System.Windows.Forms.Label label9;
      System.Windows.Forms.GroupBox groupBox2;
      System.Windows.Forms.Label label2;
      System.Windows.Forms.Label label3;
      System.Windows.Forms.Label label4;
      System.Windows.Forms.Label label5;
      System.Windows.Forms.Label label14;
      System.Windows.Forms.GroupBox groupBox3;
      System.Windows.Forms.Label label13;
      System.Windows.Forms.Label label8;
      System.Windows.Forms.Label label7;
      System.Windows.Forms.Label label6;
      this.bcStart = new System.Windows.Forms.Button();
      this.bcComment = new System.Windows.Forms.TextBox();
      this.bcContactUrl = new System.Windows.Forms.TextBox();
      this.bcGenre = new System.Windows.Forms.TextBox();
      this.bcDescription = new System.Windows.Forms.TextBox();
      this.bcChannelName = new System.Windows.Forms.TextBox();
      this.bcTrackGenre = new System.Windows.Forms.TextBox();
      this.bcTrackURL = new System.Windows.Forms.TextBox();
      this.bcAlbum = new System.Windows.Forms.TextBox();
      this.bcCreator = new System.Windows.Forms.TextBox();
      this.bcTrackTitle = new System.Windows.Forms.TextBox();
      this.bcBitrate = new System.Windows.Forms.TextBox();
      this.bcYP = new System.Windows.Forms.ComboBox();
      this.bcStreamUrl = new System.Windows.Forms.TextBox();
      this.bcContentType = new System.Windows.Forms.ComboBox();
      button1 = new System.Windows.Forms.Button();
      groupBox1 = new System.Windows.Forms.GroupBox();
      label1 = new System.Windows.Forms.Label();
      label12 = new System.Windows.Forms.Label();
      label11 = new System.Windows.Forms.Label();
      label10 = new System.Windows.Forms.Label();
      label9 = new System.Windows.Forms.Label();
      groupBox2 = new System.Windows.Forms.GroupBox();
      label2 = new System.Windows.Forms.Label();
      label3 = new System.Windows.Forms.Label();
      label4 = new System.Windows.Forms.Label();
      label5 = new System.Windows.Forms.Label();
      label14 = new System.Windows.Forms.Label();
      groupBox3 = new System.Windows.Forms.GroupBox();
      label13 = new System.Windows.Forms.Label();
      label8 = new System.Windows.Forms.Label();
      label7 = new System.Windows.Forms.Label();
      label6 = new System.Windows.Forms.Label();
      groupBox1.SuspendLayout();
      groupBox2.SuspendLayout();
      groupBox3.SuspendLayout();
      this.SuspendLayout();
      // 
      // bcStart
      // 
      this.bcStart.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
      this.bcStart.Location = new System.Drawing.Point(262, 471);
      this.bcStart.Name = "bcStart";
      this.bcStart.Size = new System.Drawing.Size(90, 26);
      this.bcStart.TabIndex = 3;
      this.bcStart.Text = "配信開始";
      this.bcStart.UseVisualStyleBackColor = true;
      this.bcStart.Click += new System.EventHandler(this.BroadcastStart_Click);
      // 
      // button1
      // 
      button1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
      button1.DialogResult = System.Windows.Forms.DialogResult.Cancel;
      button1.Location = new System.Drawing.Point(358, 471);
      button1.Name = "button1";
      button1.Size = new System.Drawing.Size(90, 26);
      button1.TabIndex = 4;
      button1.Text = "キャンセル";
      button1.UseVisualStyleBackColor = true;
      // 
      // groupBox1
      // 
      groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      groupBox1.Controls.Add(this.bcComment);
      groupBox1.Controls.Add(label1);
      groupBox1.Controls.Add(this.bcContactUrl);
      groupBox1.Controls.Add(this.bcGenre);
      groupBox1.Controls.Add(this.bcDescription);
      groupBox1.Controls.Add(this.bcChannelName);
      groupBox1.Controls.Add(label12);
      groupBox1.Controls.Add(label11);
      groupBox1.Controls.Add(label10);
      groupBox1.Controls.Add(label9);
      groupBox1.Location = new System.Drawing.Point(10, 150);
      groupBox1.Name = "groupBox1";
      groupBox1.Size = new System.Drawing.Size(436, 151);
      groupBox1.TabIndex = 1;
      groupBox1.TabStop = false;
      groupBox1.Text = "チャンネル情報";
      // 
      // bcComment
      // 
      this.bcComment.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.bcComment.Location = new System.Drawing.Point(83, 93);
      this.bcComment.Name = "bcComment";
      this.bcComment.Size = new System.Drawing.Size(346, 19);
      this.bcComment.TabIndex = 7;
      // 
      // label1
      // 
      label1.AutoSize = true;
      label1.Location = new System.Drawing.Point(4, 96);
      label1.Name = "label1";
      label1.Size = new System.Drawing.Size(74, 12);
      label1.TabIndex = 6;
      label1.Text = "配信者コメント";
      // 
      // bcContactUrl
      // 
      this.bcContactUrl.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.bcContactUrl.Location = new System.Drawing.Point(83, 118);
      this.bcContactUrl.Name = "bcContactUrl";
      this.bcContactUrl.Size = new System.Drawing.Size(346, 19);
      this.bcContactUrl.TabIndex = 9;
      // 
      // bcGenre
      // 
      this.bcGenre.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.bcGenre.Location = new System.Drawing.Point(83, 43);
      this.bcGenre.Name = "bcGenre";
      this.bcGenre.Size = new System.Drawing.Size(346, 19);
      this.bcGenre.TabIndex = 3;
      // 
      // bcDescription
      // 
      this.bcDescription.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.bcDescription.Location = new System.Drawing.Point(83, 68);
      this.bcDescription.Name = "bcDescription";
      this.bcDescription.Size = new System.Drawing.Size(346, 19);
      this.bcDescription.TabIndex = 5;
      // 
      // bcChannelName
      // 
      this.bcChannelName.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.bcChannelName.Location = new System.Drawing.Point(83, 18);
      this.bcChannelName.Name = "bcChannelName";
      this.bcChannelName.Size = new System.Drawing.Size(346, 19);
      this.bcChannelName.TabIndex = 1;
      // 
      // label12
      // 
      label12.AutoSize = true;
      label12.Location = new System.Drawing.Point(4, 121);
      label12.Name = "label12";
      label12.Size = new System.Drawing.Size(68, 12);
      label12.TabIndex = 8;
      label12.Text = "コンタクトURL";
      // 
      // label11
      // 
      label11.AutoSize = true;
      label11.Location = new System.Drawing.Point(4, 71);
      label11.Name = "label11";
      label11.Size = new System.Drawing.Size(29, 12);
      label11.TabIndex = 4;
      label11.Text = "概要";
      // 
      // label10
      // 
      label10.AutoSize = true;
      label10.Location = new System.Drawing.Point(4, 46);
      label10.Name = "label10";
      label10.Size = new System.Drawing.Size(42, 12);
      label10.TabIndex = 2;
      label10.Text = "ジャンル";
      // 
      // label9
      // 
      label9.AutoSize = true;
      label9.Location = new System.Drawing.Point(4, 21);
      label9.Name = "label9";
      label9.Size = new System.Drawing.Size(63, 12);
      label9.TabIndex = 0;
      label9.Text = "チャンネル名";
      // 
      // groupBox2
      // 
      groupBox2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      groupBox2.Controls.Add(this.bcTrackGenre);
      groupBox2.Controls.Add(label2);
      groupBox2.Controls.Add(this.bcTrackURL);
      groupBox2.Controls.Add(this.bcAlbum);
      groupBox2.Controls.Add(this.bcCreator);
      groupBox2.Controls.Add(this.bcTrackTitle);
      groupBox2.Controls.Add(label3);
      groupBox2.Controls.Add(label4);
      groupBox2.Controls.Add(label5);
      groupBox2.Controls.Add(label14);
      groupBox2.Location = new System.Drawing.Point(10, 307);
      groupBox2.Name = "groupBox2";
      groupBox2.Size = new System.Drawing.Size(437, 148);
      groupBox2.TabIndex = 2;
      groupBox2.TabStop = false;
      groupBox2.Text = "トラック情報";
      // 
      // bcTrackGenre
      // 
      this.bcTrackGenre.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.bcTrackGenre.Location = new System.Drawing.Point(83, 93);
      this.bcTrackGenre.Name = "bcTrackGenre";
      this.bcTrackGenre.Size = new System.Drawing.Size(346, 19);
      this.bcTrackGenre.TabIndex = 7;
      // 
      // label2
      // 
      label2.AutoSize = true;
      label2.Location = new System.Drawing.Point(4, 96);
      label2.Name = "label2";
      label2.Size = new System.Drawing.Size(42, 12);
      label2.TabIndex = 6;
      label2.Text = "ジャンル";
      // 
      // bcTrackURL
      // 
      this.bcTrackURL.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.bcTrackURL.Location = new System.Drawing.Point(83, 118);
      this.bcTrackURL.Name = "bcTrackURL";
      this.bcTrackURL.Size = new System.Drawing.Size(346, 19);
      this.bcTrackURL.TabIndex = 9;
      // 
      // bcAlbum
      // 
      this.bcAlbum.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.bcAlbum.Location = new System.Drawing.Point(83, 43);
      this.bcAlbum.Name = "bcAlbum";
      this.bcAlbum.Size = new System.Drawing.Size(346, 19);
      this.bcAlbum.TabIndex = 3;
      // 
      // bcCreator
      // 
      this.bcCreator.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.bcCreator.Location = new System.Drawing.Point(83, 68);
      this.bcCreator.Name = "bcCreator";
      this.bcCreator.Size = new System.Drawing.Size(346, 19);
      this.bcCreator.TabIndex = 5;
      // 
      // bcTrackTitle
      // 
      this.bcTrackTitle.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.bcTrackTitle.Location = new System.Drawing.Point(83, 18);
      this.bcTrackTitle.Name = "bcTrackTitle";
      this.bcTrackTitle.Size = new System.Drawing.Size(346, 19);
      this.bcTrackTitle.TabIndex = 1;
      // 
      // label3
      // 
      label3.AutoSize = true;
      label3.Location = new System.Drawing.Point(4, 121);
      label3.Name = "label3";
      label3.Size = new System.Drawing.Size(27, 12);
      label3.TabIndex = 8;
      label3.Text = "URL";
      // 
      // label4
      // 
      label4.AutoSize = true;
      label4.Location = new System.Drawing.Point(4, 71);
      label4.Name = "label4";
      label4.Size = new System.Drawing.Size(57, 12);
      label4.TabIndex = 4;
      label4.Text = "アーティスト";
      // 
      // label5
      // 
      label5.AutoSize = true;
      label5.Location = new System.Drawing.Point(4, 46);
      label5.Name = "label5";
      label5.Size = new System.Drawing.Size(44, 12);
      label5.TabIndex = 2;
      label5.Text = "アルバム";
      // 
      // label14
      // 
      label14.AutoSize = true;
      label14.Location = new System.Drawing.Point(4, 21);
      label14.Name = "label14";
      label14.Size = new System.Drawing.Size(40, 12);
      label14.TabIndex = 0;
      label14.Text = "タイトル";
      // 
      // groupBox3
      // 
      groupBox3.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      groupBox3.Controls.Add(this.bcBitrate);
      groupBox3.Controls.Add(label13);
      groupBox3.Controls.Add(label8);
      groupBox3.Controls.Add(label7);
      groupBox3.Controls.Add(label6);
      groupBox3.Controls.Add(this.bcYP);
      groupBox3.Controls.Add(this.bcStreamUrl);
      groupBox3.Controls.Add(this.bcContentType);
      groupBox3.Location = new System.Drawing.Point(10, 12);
      groupBox3.Name = "groupBox3";
      groupBox3.Size = new System.Drawing.Size(435, 132);
      groupBox3.TabIndex = 0;
      groupBox3.TabStop = false;
      groupBox3.Text = "ストリーム情報";
      // 
      // bcBitrate
      // 
      this.bcBitrate.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.bcBitrate.Location = new System.Drawing.Point(83, 43);
      this.bcBitrate.Name = "bcBitrate";
      this.bcBitrate.Size = new System.Drawing.Size(346, 19);
      this.bcBitrate.TabIndex = 3;
      // 
      // label13
      // 
      label13.AutoSize = true;
      label13.Location = new System.Drawing.Point(4, 46);
      label13.Name = "label13";
      label13.Size = new System.Drawing.Size(55, 12);
      label13.TabIndex = 2;
      label13.Text = "ビットレート";
      // 
      // label8
      // 
      label8.AutoSize = true;
      label8.Location = new System.Drawing.Point(4, 97);
      label8.Name = "label8";
      label8.Size = new System.Drawing.Size(43, 12);
      label8.TabIndex = 6;
      label8.Text = "掲載YP";
      // 
      // label7
      // 
      label7.AutoSize = true;
      label7.Location = new System.Drawing.Point(4, 71);
      label7.Name = "label7";
      label7.Size = new System.Drawing.Size(31, 12);
      label7.TabIndex = 4;
      label7.Text = "タイプ";
      // 
      // label6
      // 
      label6.AutoSize = true;
      label6.Location = new System.Drawing.Point(4, 21);
      label6.Name = "label6";
      label6.Size = new System.Drawing.Size(71, 12);
      label6.TabIndex = 0;
      label6.Text = "ストリームURL";
      // 
      // bcYP
      // 
      this.bcYP.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.bcYP.DisplayMember = "Name";
      this.bcYP.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
      this.bcYP.FormattingEnabled = true;
      this.bcYP.Items.AddRange(new object[] {
            "掲載しない"});
      this.bcYP.Location = new System.Drawing.Point(83, 94);
      this.bcYP.Name = "bcYP";
      this.bcYP.Size = new System.Drawing.Size(346, 20);
      this.bcYP.TabIndex = 7;
      // 
      // bcStreamUrl
      // 
      this.bcStreamUrl.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.bcStreamUrl.Location = new System.Drawing.Point(83, 18);
      this.bcStreamUrl.Name = "bcStreamUrl";
      this.bcStreamUrl.Size = new System.Drawing.Size(346, 19);
      this.bcStreamUrl.TabIndex = 1;
      // 
      // bcContentType
      // 
      this.bcContentType.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.bcContentType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
      this.bcContentType.FormattingEnabled = true;
      this.bcContentType.Location = new System.Drawing.Point(83, 68);
      this.bcContentType.Name = "bcContentType";
      this.bcContentType.Size = new System.Drawing.Size(346, 20);
      this.bcContentType.TabIndex = 5;
      // 
      // BroadcastDialog
      // 
      this.AcceptButton = this.bcStart;
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.CancelButton = button1;
      this.ClientSize = new System.Drawing.Size(460, 509);
      this.Controls.Add(groupBox3);
      this.Controls.Add(groupBox2);
      this.Controls.Add(groupBox1);
      this.Controls.Add(button1);
      this.Controls.Add(this.bcStart);
      this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
      this.MaximizeBox = false;
      this.MinimizeBox = false;
      this.Name = "BroadcastDialog";
      this.ShowIcon = false;
      this.ShowInTaskbar = false;
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
      this.Text = "配信設定";
      groupBox1.ResumeLayout(false);
      groupBox1.PerformLayout();
      groupBox2.ResumeLayout(false);
      groupBox2.PerformLayout();
      groupBox3.ResumeLayout(false);
      groupBox3.PerformLayout();
      this.ResumeLayout(false);

    }

    #endregion

    private System.Windows.Forms.Button bcStart;
    private System.Windows.Forms.TextBox bcComment;
    private System.Windows.Forms.TextBox bcContactUrl;
    private System.Windows.Forms.TextBox bcGenre;
    private System.Windows.Forms.TextBox bcDescription;
    private System.Windows.Forms.TextBox bcChannelName;
    private System.Windows.Forms.TextBox bcTrackGenre;
    private System.Windows.Forms.TextBox bcTrackURL;
    private System.Windows.Forms.TextBox bcAlbum;
    private System.Windows.Forms.TextBox bcCreator;
    private System.Windows.Forms.TextBox bcTrackTitle;
    private System.Windows.Forms.TextBox bcBitrate;
    private System.Windows.Forms.ComboBox bcYP;
    private System.Windows.Forms.TextBox bcStreamUrl;
    private System.Windows.Forms.ComboBox bcContentType;
  }
}