// PeerCastStation, a P2P streaming servent.
// Copyright (C) 2011 Ryuichi Sakamoto (kumaryu@kumaryu.net)
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
namespace PeerCastStation.GUI
{
  partial class MainForm
  {
    /// <summary>
    /// 必要なデザイナー変数です。
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// 使用中のリソースをすべてクリーンアップします。
    /// </summary>
    /// <param name="disposing">マネージ リソースが破棄される場合 true、破棄されない場合は false です。</param>
    protected override void Dispose(bool disposing)
    {
      if (disposing && (components != null)) {
        components.Dispose();
      }
      if (disposing && (logFileWriter != null)) {
        logFileWriter.Close();
      }
      base.Dispose(disposing);
    }

    #region Windows フォーム デザイナーで生成されたコード

    /// <summary>
    /// デザイナー サポートに必要なメソッドです。このメソッドの内容を
    /// コード エディターで変更しないでください。
    /// </summary>
    private void InitializeComponent()
    {
      this.components = new System.ComponentModel.Container();
      System.Windows.Forms.ToolStripMenuItem showGUIMenuItem;
      System.Windows.Forms.ToolStripMenuItem quitMenuItem;
      System.Windows.Forms.Label label21;
      System.Windows.Forms.Label label20;
      System.Windows.Forms.Label label19;
      System.Windows.Forms.Label label18;
      System.Windows.Forms.Label label17;
      System.Windows.Forms.Label label16;
      System.Windows.Forms.Label label15;
      System.Windows.Forms.Label label22;
      System.Windows.Forms.Label label23;
      System.Windows.Forms.Label label24;
      System.Windows.Forms.Label label26;
      System.Windows.Forms.Panel panel1;
      System.Windows.Forms.Label label25;
      System.Windows.Forms.Label label34;
      System.Windows.Forms.Label label35;
      System.Windows.Forms.Label label3;
      System.Windows.Forms.Label label33;
      System.Windows.Forms.Label label1;
      System.Windows.Forms.Label label2;
      System.Windows.Forms.Label label27;
      System.Windows.Forms.Label label28;
      System.Windows.Forms.Label label4;
      System.Windows.Forms.ToolStripMenuItem showHTMLUIMenuItem;
      System.Windows.Forms.ToolStripMenuItem playMenu;
      System.Windows.Forms.ToolStripMenuItem openContactURLMenu;
      System.Windows.Forms.ToolStripSeparator toolStripMenuItem2;
      System.Windows.Forms.ToolStripMenuItem copyStreamURLMenu;
      System.Windows.Forms.ToolStripMenuItem copyContactURLMenu;
      System.Windows.Forms.Label label6;
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
      this.chanInfoUpdateButton = new System.Windows.Forms.Button();
      this.notifyIconMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
      this.versionCheckMenuItem = new System.Windows.Forms.ToolStripMenuItem();
      this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
      this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
      this.mainTab = new System.Windows.Forms.TabControl();
      this.tabChannels = new System.Windows.Forms.TabPage();
      this.splitContainer1 = new System.Windows.Forms.SplitContainer();
      this.channelStart = new System.Windows.Forms.Button();
      this.channelBump = new System.Windows.Forms.Button();
      this.channelClose = new System.Windows.Forms.Button();
      this.channelPlay = new System.Windows.Forms.Button();
      this.channelList = new System.Windows.Forms.ListBox();
      this.channelListMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
      this.tabControl2 = new System.Windows.Forms.TabControl();
      this.tabPage1 = new System.Windows.Forms.TabPage();
      this.connectionReconnect = new System.Windows.Forms.Button();
      this.connectionClose = new System.Windows.Forms.Button();
      this.connectionList = new System.Windows.Forms.ListBox();
      this.tabPage3 = new System.Windows.Forms.TabPage();
      this.groupBox2 = new System.Windows.Forms.GroupBox();
      this.chanTrackGenre = new System.Windows.Forms.TextBox();
      this.chanTrackContactURL = new System.Windows.Forms.TextBox();
      this.chanTrackAlbum = new System.Windows.Forms.TextBox();
      this.chanTrackTitle = new System.Windows.Forms.TextBox();
      this.chanTrackArtist = new System.Windows.Forms.TextBox();
      this.groupBox1 = new System.Windows.Forms.GroupBox();
      this.chanInfoComment = new System.Windows.Forms.TextBox();
      this.chanInfoBitrate = new System.Windows.Forms.TextBox();
      this.chanInfoContentType = new System.Windows.Forms.TextBox();
      this.chanInfoContactURL = new System.Windows.Forms.TextBox();
      this.chanInfoGenre = new System.Windows.Forms.TextBox();
      this.chanInfoDesc = new System.Windows.Forms.TextBox();
      this.chanInfoChannelID = new System.Windows.Forms.TextBox();
      this.chanInfoChannelName = new System.Windows.Forms.TextBox();
      this.tabPage2 = new System.Windows.Forms.TabPage();
      this.relayTreeUpdate = new System.Windows.Forms.Button();
      this.relayTree = new System.Windows.Forms.TreeView();
      this.tabSettings = new System.Windows.Forms.TabPage();
      this.panel2 = new System.Windows.Forms.Panel();
      this.groupBox6 = new System.Windows.Forms.GroupBox();
      this.showWindowOnStartup = new System.Windows.Forms.CheckBox();
      this.groupBox3 = new System.Windows.Forms.GroupBox();
      this.button1 = new System.Windows.Forms.Button();
      this.button2 = new System.Windows.Forms.Button();
      this.yellowPagesList = new System.Windows.Forms.ListBox();
      this.groupBox5 = new System.Windows.Forms.GroupBox();
      this.applySettings = new System.Windows.Forms.Button();
      this.tableLayoutPanel3 = new System.Windows.Forms.TableLayoutPanel();
      this.maxUpstreamRate = new System.Windows.Forms.NumericUpDown();
      this.maxDirectsPerChannel = new System.Windows.Forms.NumericUpDown();
      this.maxDirects = new System.Windows.Forms.NumericUpDown();
      this.maxRelaysPerChannel = new System.Windows.Forms.NumericUpDown();
      this.maxRelays = new System.Windows.Forms.NumericUpDown();
      this.channelCleanerLimit = new System.Windows.Forms.NumericUpDown();
      this.label7 = new System.Windows.Forms.Label();
      this.groupBox4 = new System.Windows.Forms.GroupBox();
      this.portRemoveButton = new System.Windows.Forms.Button();
      this.portAddButton = new System.Windows.Forms.Button();
      this.portsList = new System.Windows.Forms.ListBox();
      this.portGlobalInterface = new System.Windows.Forms.CheckBox();
      this.portLocalInterface = new System.Windows.Forms.CheckBox();
      this.portLocalDirect = new System.Windows.Forms.CheckBox();
      this.portLocalRelay = new System.Windows.Forms.CheckBox();
      this.portGlobalDirect = new System.Windows.Forms.CheckBox();
      this.portGlobalRelay = new System.Windows.Forms.CheckBox();
      this.tabLog = new System.Windows.Forms.TabPage();
      this.versionInfoButton = new System.Windows.Forms.Button();
      this.logToGUICheck = new System.Windows.Forms.CheckBox();
      this.logToConsoleCheck = new System.Windows.Forms.CheckBox();
      this.logClearButton = new System.Windows.Forms.Button();
      this.selectLogFileName = new System.Windows.Forms.Button();
      this.logToFileCheck = new System.Windows.Forms.CheckBox();
      this.logText = new System.Windows.Forms.TextBox();
      this.logFileNameText = new System.Windows.Forms.TextBox();
      this.logLevelList = new System.Windows.Forms.ComboBox();
      this.label5 = new System.Windows.Forms.Label();
      this.logSaveFileDialog = new System.Windows.Forms.SaveFileDialog();
      this.statusBar = new System.Windows.Forms.StatusStrip();
      this.portLabel = new System.Windows.Forms.ToolStripStatusLabel();
      this.portOpenedLabel = new System.Windows.Forms.ToolStripStatusLabel();
      showGUIMenuItem = new System.Windows.Forms.ToolStripMenuItem();
      quitMenuItem = new System.Windows.Forms.ToolStripMenuItem();
      label21 = new System.Windows.Forms.Label();
      label20 = new System.Windows.Forms.Label();
      label19 = new System.Windows.Forms.Label();
      label18 = new System.Windows.Forms.Label();
      label17 = new System.Windows.Forms.Label();
      label16 = new System.Windows.Forms.Label();
      label15 = new System.Windows.Forms.Label();
      label22 = new System.Windows.Forms.Label();
      label23 = new System.Windows.Forms.Label();
      label24 = new System.Windows.Forms.Label();
      label26 = new System.Windows.Forms.Label();
      panel1 = new System.Windows.Forms.Panel();
      label25 = new System.Windows.Forms.Label();
      label34 = new System.Windows.Forms.Label();
      label35 = new System.Windows.Forms.Label();
      label3 = new System.Windows.Forms.Label();
      label33 = new System.Windows.Forms.Label();
      label1 = new System.Windows.Forms.Label();
      label2 = new System.Windows.Forms.Label();
      label27 = new System.Windows.Forms.Label();
      label28 = new System.Windows.Forms.Label();
      label4 = new System.Windows.Forms.Label();
      showHTMLUIMenuItem = new System.Windows.Forms.ToolStripMenuItem();
      playMenu = new System.Windows.Forms.ToolStripMenuItem();
      openContactURLMenu = new System.Windows.Forms.ToolStripMenuItem();
      toolStripMenuItem2 = new System.Windows.Forms.ToolStripSeparator();
      copyStreamURLMenu = new System.Windows.Forms.ToolStripMenuItem();
      copyContactURLMenu = new System.Windows.Forms.ToolStripMenuItem();
      label6 = new System.Windows.Forms.Label();
      panel1.SuspendLayout();
      this.notifyIconMenu.SuspendLayout();
      this.mainTab.SuspendLayout();
      this.tabChannels.SuspendLayout();
      this.splitContainer1.Panel1.SuspendLayout();
      this.splitContainer1.Panel2.SuspendLayout();
      this.splitContainer1.SuspendLayout();
      this.channelListMenu.SuspendLayout();
      this.tabControl2.SuspendLayout();
      this.tabPage1.SuspendLayout();
      this.tabPage3.SuspendLayout();
      this.groupBox2.SuspendLayout();
      this.groupBox1.SuspendLayout();
      this.tabPage2.SuspendLayout();
      this.tabSettings.SuspendLayout();
      this.panel2.SuspendLayout();
      this.groupBox6.SuspendLayout();
      this.groupBox3.SuspendLayout();
      this.groupBox5.SuspendLayout();
      this.tableLayoutPanel3.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)(this.maxUpstreamRate)).BeginInit();
      ((System.ComponentModel.ISupportInitialize)(this.maxDirectsPerChannel)).BeginInit();
      ((System.ComponentModel.ISupportInitialize)(this.maxDirects)).BeginInit();
      ((System.ComponentModel.ISupportInitialize)(this.maxRelaysPerChannel)).BeginInit();
      ((System.ComponentModel.ISupportInitialize)(this.maxRelays)).BeginInit();
      ((System.ComponentModel.ISupportInitialize)(this.channelCleanerLimit)).BeginInit();
      this.groupBox4.SuspendLayout();
      this.tabLog.SuspendLayout();
      this.statusBar.SuspendLayout();
      this.SuspendLayout();
      // 
      // showGUIMenuItem
      // 
      showGUIMenuItem.AutoToolTip = true;
      showGUIMenuItem.Name = "showGUIMenuItem";
      showGUIMenuItem.Size = new System.Drawing.Size(202, 22);
      showGUIMenuItem.Text = "GUIを表示(&G)";
      showGUIMenuItem.ToolTipText = "PeerCastStationのGUIを表示します";
      showGUIMenuItem.Click += new System.EventHandler(this.showGUIMenuItem_Click);
      // 
      // quitMenuItem
      // 
      quitMenuItem.AutoToolTip = true;
      quitMenuItem.Name = "quitMenuItem";
      quitMenuItem.Size = new System.Drawing.Size(202, 22);
      quitMenuItem.Text = "終了(&Q)";
      quitMenuItem.ToolTipText = "PeerCastStationを終了します";
      quitMenuItem.Click += new System.EventHandler(this.quitMenuItem_Click);
      // 
      // label21
      // 
      label21.AutoSize = true;
      label21.Location = new System.Drawing.Point(19, 196);
      label21.Name = "label21";
      label21.Size = new System.Drawing.Size(57, 12);
      label21.TabIndex = 21;
      label21.Text = "ビットレート:";
      label21.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // label20
      // 
      label20.AutoSize = true;
      label20.Location = new System.Drawing.Point(45, 171);
      label20.Name = "label20";
      label20.Size = new System.Drawing.Size(31, 12);
      label20.TabIndex = 20;
      label20.Text = "形式:";
      label20.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // label19
      // 
      label19.AutoSize = true;
      label19.Location = new System.Drawing.Point(12, 146);
      label19.Name = "label19";
      label19.Size = new System.Drawing.Size(64, 12);
      label19.TabIndex = 19;
      label19.Text = "チャンネルID:";
      label19.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // label18
      // 
      label18.AutoSize = true;
      label18.Location = new System.Drawing.Point(6, 96);
      label18.Name = "label18";
      label18.Size = new System.Drawing.Size(70, 12);
      label18.TabIndex = 18;
      label18.Text = "コンタクトURL:";
      label18.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // label17
      // 
      label17.AutoSize = true;
      label17.Location = new System.Drawing.Point(45, 71);
      label17.Name = "label17";
      label17.Size = new System.Drawing.Size(31, 12);
      label17.TabIndex = 17;
      label17.Text = "概要:";
      label17.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // label16
      // 
      label16.AutoSize = true;
      label16.Location = new System.Drawing.Point(32, 46);
      label16.Name = "label16";
      label16.Size = new System.Drawing.Size(44, 12);
      label16.TabIndex = 15;
      label16.Text = "ジャンル:";
      label16.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // label15
      // 
      label15.AutoSize = true;
      label15.Location = new System.Drawing.Point(11, 21);
      label15.Name = "label15";
      label15.Size = new System.Drawing.Size(65, 12);
      label15.TabIndex = 14;
      label15.Text = "チャンネル名:";
      label15.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // label22
      // 
      label22.AutoSize = true;
      label22.Location = new System.Drawing.Point(17, 71);
      label22.Name = "label22";
      label22.Size = new System.Drawing.Size(59, 12);
      label22.TabIndex = 4;
      label22.Text = "アーティスト:";
      label22.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // label23
      // 
      label23.AutoSize = true;
      label23.Location = new System.Drawing.Point(34, 21);
      label23.Name = "label23";
      label23.Size = new System.Drawing.Size(42, 12);
      label23.TabIndex = 0;
      label23.Text = "タイトル:";
      label23.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // label24
      // 
      label24.AutoSize = true;
      label24.Location = new System.Drawing.Point(30, 46);
      label24.Name = "label24";
      label24.Size = new System.Drawing.Size(46, 12);
      label24.TabIndex = 2;
      label24.Text = "アルバム:";
      label24.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // label26
      // 
      label26.AutoSize = true;
      label26.Location = new System.Drawing.Point(47, 121);
      label26.Name = "label26";
      label26.Size = new System.Drawing.Size(29, 12);
      label26.TabIndex = 8;
      label26.Text = "URL:";
      label26.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // panel1
      // 
      panel1.AutoSize = true;
      panel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
      panel1.Controls.Add(this.chanInfoUpdateButton);
      panel1.Dock = System.Windows.Forms.DockStyle.Top;
      panel1.Location = new System.Drawing.Point(0, 385);
      panel1.Name = "panel1";
      panel1.Size = new System.Drawing.Size(406, 32);
      panel1.TabIndex = 21;
      // 
      // chanInfoUpdateButton
      // 
      this.chanInfoUpdateButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
      this.chanInfoUpdateButton.Enabled = false;
      this.chanInfoUpdateButton.Location = new System.Drawing.Point(328, 6);
      this.chanInfoUpdateButton.Name = "chanInfoUpdateButton";
      this.chanInfoUpdateButton.Size = new System.Drawing.Size(75, 23);
      this.chanInfoUpdateButton.TabIndex = 30;
      this.chanInfoUpdateButton.Text = "更新";
      this.chanInfoUpdateButton.UseVisualStyleBackColor = true;
      this.chanInfoUpdateButton.Click += new System.EventHandler(this.chanInfoUpdateButton_Click);
      // 
      // label25
      // 
      label25.AutoSize = true;
      label25.Location = new System.Drawing.Point(0, 121);
      label25.Name = "label25";
      label25.Size = new System.Drawing.Size(76, 12);
      label25.TabIndex = 28;
      label25.Text = "配信者コメント:";
      label25.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // label34
      // 
      label34.AutoSize = true;
      label34.Dock = System.Windows.Forms.DockStyle.Fill;
      label34.Location = new System.Drawing.Point(286, 25);
      label34.Name = "label34";
      label34.Size = new System.Drawing.Size(65, 25);
      label34.TabIndex = 8;
      label34.Text = "チャンネル毎:";
      label34.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // label35
      // 
      label35.AutoSize = true;
      label35.Dock = System.Windows.Forms.DockStyle.Fill;
      label35.Location = new System.Drawing.Point(181, 25);
      label35.Name = "label35";
      label35.Size = new System.Drawing.Size(31, 25);
      label35.TabIndex = 6;
      label35.Text = "合計:";
      label35.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // label3
      // 
      label3.AutoSize = true;
      label3.Dock = System.Windows.Forms.DockStyle.Fill;
      label3.Location = new System.Drawing.Point(3, 25);
      label3.Name = "label3";
      label3.Size = new System.Drawing.Size(172, 25);
      label3.TabIndex = 5;
      label3.Text = "最大視聴数:";
      label3.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // label33
      // 
      label33.AutoSize = true;
      label33.Dock = System.Windows.Forms.DockStyle.Fill;
      label33.Location = new System.Drawing.Point(286, 0);
      label33.Name = "label33";
      label33.Size = new System.Drawing.Size(65, 25);
      label33.TabIndex = 3;
      label33.Text = "チャンネル毎:";
      label33.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // label1
      // 
      label1.AutoSize = true;
      label1.Dock = System.Windows.Forms.DockStyle.Fill;
      label1.Location = new System.Drawing.Point(181, 0);
      label1.Name = "label1";
      label1.Size = new System.Drawing.Size(31, 25);
      label1.TabIndex = 1;
      label1.Text = "合計:";
      label1.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // label2
      // 
      label2.AutoSize = true;
      label2.Dock = System.Windows.Forms.DockStyle.Fill;
      label2.Location = new System.Drawing.Point(3, 0);
      label2.Name = "label2";
      label2.Size = new System.Drawing.Size(172, 25);
      label2.TabIndex = 0;
      label2.Text = "最大リレー数:";
      label2.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // label27
      // 
      label27.AutoSize = true;
      label27.Location = new System.Drawing.Point(6, 101);
      label27.Name = "label27";
      label27.Size = new System.Drawing.Size(126, 12);
      label27.TabIndex = 2;
      label27.Text = "LAN内からの接続を許可:";
      label27.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // label28
      // 
      label28.AutoSize = true;
      label28.Location = new System.Drawing.Point(6, 123);
      label28.Name = "label28";
      label28.Size = new System.Drawing.Size(117, 12);
      label28.TabIndex = 6;
      label28.Text = "WANからの接続を許可:";
      label28.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // label4
      // 
      label4.AutoSize = true;
      label4.Dock = System.Windows.Forms.DockStyle.Fill;
      label4.Location = new System.Drawing.Point(3, 50);
      label4.Name = "label4";
      label4.Size = new System.Drawing.Size(172, 20);
      label4.TabIndex = 10;
      label4.Text = "最大上り帯域(kbps):";
      label4.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // showHTMLUIMenuItem
      // 
      showHTMLUIMenuItem.Name = "showHTMLUIMenuItem";
      showHTMLUIMenuItem.Size = new System.Drawing.Size(202, 22);
      showHTMLUIMenuItem.Text = "HTML UIを表示(&H)";
      showHTMLUIMenuItem.Click += new System.EventHandler(this.showHTMLUIMenuItem_Click);
      // 
      // playMenu
      // 
      playMenu.Name = "playMenu";
      playMenu.Size = new System.Drawing.Size(226, 22);
      playMenu.Text = "再生(&P)";
      playMenu.Click += new System.EventHandler(this.channelPlay_Click);
      // 
      // openContactURLMenu
      // 
      openContactURLMenu.Name = "openContactURLMenu";
      openContactURLMenu.Size = new System.Drawing.Size(226, 22);
      openContactURLMenu.Text = "コンタクトURLを開く(&U)";
      openContactURLMenu.Click += new System.EventHandler(this.openContactURLMenu_Click);
      // 
      // toolStripMenuItem2
      // 
      toolStripMenuItem2.Name = "toolStripMenuItem2";
      toolStripMenuItem2.Size = new System.Drawing.Size(223, 6);
      // 
      // copyStreamURLMenu
      // 
      copyStreamURLMenu.Name = "copyStreamURLMenu";
      copyStreamURLMenu.Size = new System.Drawing.Size(226, 22);
      copyStreamURLMenu.Text = "ストリームURLをコピー(&S)";
      copyStreamURLMenu.Click += new System.EventHandler(this.copyStreamURLMenu_Click);
      // 
      // copyContactURLMenu
      // 
      copyContactURLMenu.Name = "copyContactURLMenu";
      copyContactURLMenu.Size = new System.Drawing.Size(226, 22);
      copyContactURLMenu.Text = "コンタクトURLをコピー(&C)";
      copyContactURLMenu.Click += new System.EventHandler(this.copyContactURLMenu_Click);
      // 
      // label6
      // 
      label6.AutoSize = true;
      label6.Location = new System.Drawing.Point(32, 96);
      label6.Name = "label6";
      label6.Size = new System.Drawing.Size(44, 12);
      label6.TabIndex = 6;
      label6.Text = "ジャンル:";
      label6.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // notifyIconMenu
      // 
      this.notifyIconMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.versionCheckMenuItem,
            this.toolStripSeparator1,
            showHTMLUIMenuItem,
            showGUIMenuItem,
            this.toolStripMenuItem1,
            quitMenuItem});
      this.notifyIconMenu.Name = "notifyIconMenu";
      this.notifyIconMenu.ShowImageMargin = false;
      this.notifyIconMenu.Size = new System.Drawing.Size(203, 104);
      // 
      // versionCheckMenuItem
      // 
      this.versionCheckMenuItem.Name = "versionCheckMenuItem";
      this.versionCheckMenuItem.Size = new System.Drawing.Size(202, 22);
      this.versionCheckMenuItem.Text = "アップデートのチェック(&U)";
      this.versionCheckMenuItem.Click += new System.EventHandler(this.versionCheckMenuItem_Click);
      // 
      // toolStripSeparator1
      // 
      this.toolStripSeparator1.Name = "toolStripSeparator1";
      this.toolStripSeparator1.Size = new System.Drawing.Size(199, 6);
      // 
      // toolStripMenuItem1
      // 
      this.toolStripMenuItem1.Name = "toolStripMenuItem1";
      this.toolStripMenuItem1.Size = new System.Drawing.Size(199, 6);
      // 
      // mainTab
      // 
      this.mainTab.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.mainTab.Controls.Add(this.tabChannels);
      this.mainTab.Controls.Add(this.tabSettings);
      this.mainTab.Controls.Add(this.tabLog);
      this.mainTab.Location = new System.Drawing.Point(0, 0);
      this.mainTab.Name = "mainTab";
      this.mainTab.SelectedIndex = 0;
      this.mainTab.Size = new System.Drawing.Size(445, 516);
      this.mainTab.TabIndex = 0;
      this.mainTab.SelectedIndexChanged += new System.EventHandler(this.mainTab_SelectedIndexChanged);
      // 
      // tabChannels
      // 
      this.tabChannels.Controls.Add(this.splitContainer1);
      this.tabChannels.Location = new System.Drawing.Point(4, 22);
      this.tabChannels.Name = "tabChannels";
      this.tabChannels.Padding = new System.Windows.Forms.Padding(3);
      this.tabChannels.Size = new System.Drawing.Size(437, 490);
      this.tabChannels.TabIndex = 1;
      this.tabChannels.Text = "チャンネル一覧";
      this.tabChannels.UseVisualStyleBackColor = true;
      // 
      // splitContainer1
      // 
      this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
      this.splitContainer1.Location = new System.Drawing.Point(3, 3);
      this.splitContainer1.Name = "splitContainer1";
      this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
      // 
      // splitContainer1.Panel1
      // 
      this.splitContainer1.Panel1.Controls.Add(this.channelStart);
      this.splitContainer1.Panel1.Controls.Add(this.channelBump);
      this.splitContainer1.Panel1.Controls.Add(this.channelClose);
      this.splitContainer1.Panel1.Controls.Add(this.channelPlay);
      this.splitContainer1.Panel1.Controls.Add(this.channelList);
      // 
      // splitContainer1.Panel2
      // 
      this.splitContainer1.Panel2.Controls.Add(this.tabControl2);
      this.splitContainer1.Size = new System.Drawing.Size(431, 484);
      this.splitContainer1.SplitterDistance = 172;
      this.splitContainer1.TabIndex = 9;
      // 
      // channelStart
      // 
      this.channelStart.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
      this.channelStart.Location = new System.Drawing.Point(365, 108);
      this.channelStart.Name = "channelStart";
      this.channelStart.Size = new System.Drawing.Size(61, 30);
      this.channelStart.TabIndex = 4;
      this.channelStart.Text = "配信...";
      this.channelStart.UseVisualStyleBackColor = true;
      this.channelStart.Click += new System.EventHandler(this.channelStart_Click);
      // 
      // channelBump
      // 
      this.channelBump.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
      this.channelBump.Location = new System.Drawing.Point(365, 72);
      this.channelBump.Name = "channelBump";
      this.channelBump.Size = new System.Drawing.Size(61, 30);
      this.channelBump.TabIndex = 3;
      this.channelBump.Text = "再接続";
      this.channelBump.UseVisualStyleBackColor = true;
      this.channelBump.Click += new System.EventHandler(this.channelBump_Click);
      // 
      // channelClose
      // 
      this.channelClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
      this.channelClose.Location = new System.Drawing.Point(365, 36);
      this.channelClose.Name = "channelClose";
      this.channelClose.Size = new System.Drawing.Size(61, 30);
      this.channelClose.TabIndex = 2;
      this.channelClose.Text = "切断";
      this.channelClose.UseVisualStyleBackColor = true;
      this.channelClose.Click += new System.EventHandler(this.channelClose_Click);
      // 
      // channelPlay
      // 
      this.channelPlay.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
      this.channelPlay.Location = new System.Drawing.Point(365, 0);
      this.channelPlay.Name = "channelPlay";
      this.channelPlay.Size = new System.Drawing.Size(61, 30);
      this.channelPlay.TabIndex = 1;
      this.channelPlay.Text = "再生";
      this.channelPlay.UseVisualStyleBackColor = true;
      this.channelPlay.Click += new System.EventHandler(this.channelPlay_Click);
      // 
      // channelList
      // 
      this.channelList.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.channelList.ContextMenuStrip = this.channelListMenu;
      this.channelList.IntegralHeight = false;
      this.channelList.ItemHeight = 12;
      this.channelList.Location = new System.Drawing.Point(0, 0);
      this.channelList.Name = "channelList";
      this.channelList.Size = new System.Drawing.Size(361, 172);
      this.channelList.TabIndex = 0;
      this.channelList.SelectedIndexChanged += new System.EventHandler(this.channelList_SelectedIndexChanged);
      // 
      // channelListMenu
      // 
      this.channelListMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            playMenu,
            openContactURLMenu,
            toolStripMenuItem2,
            copyStreamURLMenu,
            copyContactURLMenu});
      this.channelListMenu.Name = "channelListMenu";
      this.channelListMenu.Size = new System.Drawing.Size(227, 98);
      // 
      // tabControl2
      // 
      this.tabControl2.Controls.Add(this.tabPage1);
      this.tabControl2.Controls.Add(this.tabPage3);
      this.tabControl2.Controls.Add(this.tabPage2);
      this.tabControl2.Dock = System.Windows.Forms.DockStyle.Fill;
      this.tabControl2.Location = new System.Drawing.Point(0, 0);
      this.tabControl2.Name = "tabControl2";
      this.tabControl2.SelectedIndex = 0;
      this.tabControl2.Size = new System.Drawing.Size(431, 308);
      this.tabControl2.TabIndex = 0;
      this.tabControl2.SelectedIndexChanged += new System.EventHandler(this.channelList_SelectedIndexChanged);
      // 
      // tabPage1
      // 
      this.tabPage1.Controls.Add(this.connectionReconnect);
      this.tabPage1.Controls.Add(this.connectionClose);
      this.tabPage1.Controls.Add(this.connectionList);
      this.tabPage1.Location = new System.Drawing.Point(4, 22);
      this.tabPage1.Name = "tabPage1";
      this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
      this.tabPage1.Size = new System.Drawing.Size(423, 282);
      this.tabPage1.TabIndex = 0;
      this.tabPage1.Text = "接続一覧";
      this.tabPage1.UseVisualStyleBackColor = true;
      // 
      // connectionReconnect
      // 
      this.connectionReconnect.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
      this.connectionReconnect.Enabled = false;
      this.connectionReconnect.Location = new System.Drawing.Point(361, 42);
      this.connectionReconnect.Name = "connectionReconnect";
      this.connectionReconnect.Size = new System.Drawing.Size(61, 30);
      this.connectionReconnect.TabIndex = 4;
      this.connectionReconnect.Text = "再接続";
      this.connectionReconnect.UseVisualStyleBackColor = true;
      this.connectionReconnect.Click += new System.EventHandler(this.connectionReconnect_Click);
      // 
      // connectionClose
      // 
      this.connectionClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
      this.connectionClose.Enabled = false;
      this.connectionClose.Location = new System.Drawing.Point(361, 6);
      this.connectionClose.Name = "connectionClose";
      this.connectionClose.Size = new System.Drawing.Size(61, 30);
      this.connectionClose.TabIndex = 3;
      this.connectionClose.Text = "切断";
      this.connectionClose.UseVisualStyleBackColor = true;
      this.connectionClose.Click += new System.EventHandler(this.connectionClose_Click);
      // 
      // connectionList
      // 
      this.connectionList.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.connectionList.FormattingEnabled = true;
      this.connectionList.IntegralHeight = false;
      this.connectionList.ItemHeight = 12;
      this.connectionList.Location = new System.Drawing.Point(6, 6);
      this.connectionList.Name = "connectionList";
      this.connectionList.Size = new System.Drawing.Size(351, 270);
      this.connectionList.TabIndex = 2;
      this.connectionList.SelectedIndexChanged += new System.EventHandler(this.connectionList_SelectedIndexChanged);
      // 
      // tabPage3
      // 
      this.tabPage3.AutoScroll = true;
      this.tabPage3.Controls.Add(panel1);
      this.tabPage3.Controls.Add(this.groupBox2);
      this.tabPage3.Controls.Add(this.groupBox1);
      this.tabPage3.Location = new System.Drawing.Point(4, 22);
      this.tabPage3.Name = "tabPage3";
      this.tabPage3.Size = new System.Drawing.Size(423, 282);
      this.tabPage3.TabIndex = 2;
      this.tabPage3.Text = "チャンネル情報";
      this.tabPage3.UseVisualStyleBackColor = true;
      // 
      // groupBox2
      // 
      this.groupBox2.AutoSize = true;
      this.groupBox2.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
      this.groupBox2.Controls.Add(label6);
      this.groupBox2.Controls.Add(this.chanTrackGenre);
      this.groupBox2.Controls.Add(label26);
      this.groupBox2.Controls.Add(label24);
      this.groupBox2.Controls.Add(label23);
      this.groupBox2.Controls.Add(label22);
      this.groupBox2.Controls.Add(this.chanTrackContactURL);
      this.groupBox2.Controls.Add(this.chanTrackAlbum);
      this.groupBox2.Controls.Add(this.chanTrackTitle);
      this.groupBox2.Controls.Add(this.chanTrackArtist);
      this.groupBox2.Dock = System.Windows.Forms.DockStyle.Top;
      this.groupBox2.Location = new System.Drawing.Point(0, 230);
      this.groupBox2.Name = "groupBox2";
      this.groupBox2.Size = new System.Drawing.Size(406, 155);
      this.groupBox2.TabIndex = 20;
      this.groupBox2.TabStop = false;
      this.groupBox2.Text = "トラック情報";
      // 
      // chanTrackGenre
      // 
      this.chanTrackGenre.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.chanTrackGenre.Location = new System.Drawing.Point(82, 93);
      this.chanTrackGenre.Name = "chanTrackGenre";
      this.chanTrackGenre.ReadOnly = true;
      this.chanTrackGenre.Size = new System.Drawing.Size(321, 19);
      this.chanTrackGenre.TabIndex = 7;
      // 
      // chanTrackContactURL
      // 
      this.chanTrackContactURL.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.chanTrackContactURL.Location = new System.Drawing.Point(82, 118);
      this.chanTrackContactURL.Name = "chanTrackContactURL";
      this.chanTrackContactURL.ReadOnly = true;
      this.chanTrackContactURL.Size = new System.Drawing.Size(321, 19);
      this.chanTrackContactURL.TabIndex = 9;
      // 
      // chanTrackAlbum
      // 
      this.chanTrackAlbum.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.chanTrackAlbum.Location = new System.Drawing.Point(82, 43);
      this.chanTrackAlbum.Name = "chanTrackAlbum";
      this.chanTrackAlbum.ReadOnly = true;
      this.chanTrackAlbum.Size = new System.Drawing.Size(321, 19);
      this.chanTrackAlbum.TabIndex = 3;
      // 
      // chanTrackTitle
      // 
      this.chanTrackTitle.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.chanTrackTitle.Location = new System.Drawing.Point(82, 18);
      this.chanTrackTitle.Name = "chanTrackTitle";
      this.chanTrackTitle.ReadOnly = true;
      this.chanTrackTitle.Size = new System.Drawing.Size(321, 19);
      this.chanTrackTitle.TabIndex = 1;
      // 
      // chanTrackArtist
      // 
      this.chanTrackArtist.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.chanTrackArtist.Location = new System.Drawing.Point(82, 68);
      this.chanTrackArtist.Name = "chanTrackArtist";
      this.chanTrackArtist.ReadOnly = true;
      this.chanTrackArtist.Size = new System.Drawing.Size(321, 19);
      this.chanTrackArtist.TabIndex = 5;
      // 
      // groupBox1
      // 
      this.groupBox1.AutoSize = true;
      this.groupBox1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
      this.groupBox1.Controls.Add(this.chanInfoComment);
      this.groupBox1.Controls.Add(label25);
      this.groupBox1.Controls.Add(this.chanInfoBitrate);
      this.groupBox1.Controls.Add(this.chanInfoContentType);
      this.groupBox1.Controls.Add(this.chanInfoContactURL);
      this.groupBox1.Controls.Add(this.chanInfoGenre);
      this.groupBox1.Controls.Add(this.chanInfoDesc);
      this.groupBox1.Controls.Add(this.chanInfoChannelID);
      this.groupBox1.Controls.Add(label21);
      this.groupBox1.Controls.Add(label20);
      this.groupBox1.Controls.Add(label19);
      this.groupBox1.Controls.Add(label18);
      this.groupBox1.Controls.Add(label17);
      this.groupBox1.Controls.Add(this.chanInfoChannelName);
      this.groupBox1.Controls.Add(label16);
      this.groupBox1.Controls.Add(label15);
      this.groupBox1.Dock = System.Windows.Forms.DockStyle.Top;
      this.groupBox1.Location = new System.Drawing.Point(0, 0);
      this.groupBox1.Name = "groupBox1";
      this.groupBox1.Size = new System.Drawing.Size(406, 230);
      this.groupBox1.TabIndex = 19;
      this.groupBox1.TabStop = false;
      this.groupBox1.Text = "チャンネル情報";
      // 
      // chanInfoComment
      // 
      this.chanInfoComment.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.chanInfoComment.Location = new System.Drawing.Point(82, 118);
      this.chanInfoComment.Name = "chanInfoComment";
      this.chanInfoComment.ReadOnly = true;
      this.chanInfoComment.Size = new System.Drawing.Size(321, 19);
      this.chanInfoComment.TabIndex = 29;
      // 
      // chanInfoBitrate
      // 
      this.chanInfoBitrate.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.chanInfoBitrate.Location = new System.Drawing.Point(82, 193);
      this.chanInfoBitrate.Name = "chanInfoBitrate";
      this.chanInfoBitrate.ReadOnly = true;
      this.chanInfoBitrate.Size = new System.Drawing.Size(321, 19);
      this.chanInfoBitrate.TabIndex = 27;
      // 
      // chanInfoContentType
      // 
      this.chanInfoContentType.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.chanInfoContentType.Location = new System.Drawing.Point(82, 168);
      this.chanInfoContentType.Name = "chanInfoContentType";
      this.chanInfoContentType.ReadOnly = true;
      this.chanInfoContentType.Size = new System.Drawing.Size(321, 19);
      this.chanInfoContentType.TabIndex = 26;
      // 
      // chanInfoContactURL
      // 
      this.chanInfoContactURL.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.chanInfoContactURL.Location = new System.Drawing.Point(82, 93);
      this.chanInfoContactURL.Name = "chanInfoContactURL";
      this.chanInfoContactURL.ReadOnly = true;
      this.chanInfoContactURL.Size = new System.Drawing.Size(321, 19);
      this.chanInfoContactURL.TabIndex = 25;
      // 
      // chanInfoGenre
      // 
      this.chanInfoGenre.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.chanInfoGenre.Location = new System.Drawing.Point(82, 43);
      this.chanInfoGenre.Name = "chanInfoGenre";
      this.chanInfoGenre.ReadOnly = true;
      this.chanInfoGenre.Size = new System.Drawing.Size(321, 19);
      this.chanInfoGenre.TabIndex = 24;
      // 
      // chanInfoDesc
      // 
      this.chanInfoDesc.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.chanInfoDesc.Location = new System.Drawing.Point(82, 68);
      this.chanInfoDesc.Name = "chanInfoDesc";
      this.chanInfoDesc.ReadOnly = true;
      this.chanInfoDesc.Size = new System.Drawing.Size(321, 19);
      this.chanInfoDesc.TabIndex = 23;
      // 
      // chanInfoChannelID
      // 
      this.chanInfoChannelID.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.chanInfoChannelID.Location = new System.Drawing.Point(82, 143);
      this.chanInfoChannelID.Name = "chanInfoChannelID";
      this.chanInfoChannelID.ReadOnly = true;
      this.chanInfoChannelID.Size = new System.Drawing.Size(321, 19);
      this.chanInfoChannelID.TabIndex = 22;
      // 
      // chanInfoChannelName
      // 
      this.chanInfoChannelName.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.chanInfoChannelName.Location = new System.Drawing.Point(82, 18);
      this.chanInfoChannelName.Name = "chanInfoChannelName";
      this.chanInfoChannelName.ReadOnly = true;
      this.chanInfoChannelName.Size = new System.Drawing.Size(321, 19);
      this.chanInfoChannelName.TabIndex = 16;
      // 
      // tabPage2
      // 
      this.tabPage2.Controls.Add(this.relayTreeUpdate);
      this.tabPage2.Controls.Add(this.relayTree);
      this.tabPage2.Location = new System.Drawing.Point(4, 22);
      this.tabPage2.Name = "tabPage2";
      this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
      this.tabPage2.Size = new System.Drawing.Size(423, 282);
      this.tabPage2.TabIndex = 1;
      this.tabPage2.Text = "リレーツリー";
      this.tabPage2.UseVisualStyleBackColor = true;
      // 
      // relayTreeUpdate
      // 
      this.relayTreeUpdate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
      this.relayTreeUpdate.Location = new System.Drawing.Point(361, 6);
      this.relayTreeUpdate.Name = "relayTreeUpdate";
      this.relayTreeUpdate.Size = new System.Drawing.Size(61, 30);
      this.relayTreeUpdate.TabIndex = 6;
      this.relayTreeUpdate.Text = "更新";
      this.relayTreeUpdate.UseVisualStyleBackColor = true;
      this.relayTreeUpdate.Click += new System.EventHandler(this.relayTreeUpdate_Click);
      // 
      // relayTree
      // 
      this.relayTree.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.relayTree.Location = new System.Drawing.Point(6, 6);
      this.relayTree.Name = "relayTree";
      this.relayTree.Size = new System.Drawing.Size(351, 270);
      this.relayTree.TabIndex = 5;
      // 
      // tabSettings
      // 
      this.tabSettings.Controls.Add(this.panel2);
      this.tabSettings.Location = new System.Drawing.Point(4, 22);
      this.tabSettings.Name = "tabSettings";
      this.tabSettings.Size = new System.Drawing.Size(437, 490);
      this.tabSettings.TabIndex = 2;
      this.tabSettings.Text = "設定";
      this.tabSettings.UseVisualStyleBackColor = true;
      // 
      // panel2
      // 
      this.panel2.AutoScroll = true;
      this.panel2.Controls.Add(this.groupBox6);
      this.panel2.Controls.Add(this.groupBox3);
      this.panel2.Controls.Add(this.groupBox5);
      this.panel2.Controls.Add(this.groupBox4);
      this.panel2.Dock = System.Windows.Forms.DockStyle.Fill;
      this.panel2.Location = new System.Drawing.Point(0, 0);
      this.panel2.Name = "panel2";
      this.panel2.Padding = new System.Windows.Forms.Padding(4);
      this.panel2.Size = new System.Drawing.Size(437, 490);
      this.panel2.TabIndex = 1;
      // 
      // groupBox6
      // 
      this.groupBox6.AutoSize = true;
      this.groupBox6.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
      this.groupBox6.Controls.Add(this.showWindowOnStartup);
      this.groupBox6.Dock = System.Windows.Forms.DockStyle.Top;
      this.groupBox6.Location = new System.Drawing.Point(4, 430);
      this.groupBox6.Name = "groupBox6";
      this.groupBox6.Size = new System.Drawing.Size(429, 52);
      this.groupBox6.TabIndex = 4;
      this.groupBox6.TabStop = false;
      this.groupBox6.Text = "UI設定";
      // 
      // showWindowOnStartup
      // 
      this.showWindowOnStartup.AutoSize = true;
      this.showWindowOnStartup.Checked = true;
      this.showWindowOnStartup.CheckState = System.Windows.Forms.CheckState.Checked;
      this.showWindowOnStartup.Location = new System.Drawing.Point(8, 18);
      this.showWindowOnStartup.Name = "showWindowOnStartup";
      this.showWindowOnStartup.Size = new System.Drawing.Size(164, 16);
      this.showWindowOnStartup.TabIndex = 10;
      this.showWindowOnStartup.Text = "起動時にウィンドウを表示する";
      this.showWindowOnStartup.UseVisualStyleBackColor = true;
      // 
      // groupBox3
      // 
      this.groupBox3.AutoSize = true;
      this.groupBox3.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
      this.groupBox3.Controls.Add(this.button1);
      this.groupBox3.Controls.Add(this.button2);
      this.groupBox3.Controls.Add(this.yellowPagesList);
      this.groupBox3.Dock = System.Windows.Forms.DockStyle.Top;
      this.groupBox3.Location = new System.Drawing.Point(4, 318);
      this.groupBox3.Name = "groupBox3";
      this.groupBox3.Size = new System.Drawing.Size(429, 112);
      this.groupBox3.TabIndex = 3;
      this.groupBox3.TabStop = false;
      this.groupBox3.Text = "YP一覧";
      // 
      // button1
      // 
      this.button1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
      this.button1.Location = new System.Drawing.Point(347, 47);
      this.button1.Name = "button1";
      this.button1.Size = new System.Drawing.Size(75, 23);
      this.button1.TabIndex = 17;
      this.button1.Text = "削除";
      this.button1.UseVisualStyleBackColor = true;
      this.button1.Click += new System.EventHandler(this.yellowPageRemoveButton_Click);
      // 
      // button2
      // 
      this.button2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
      this.button2.Location = new System.Drawing.Point(347, 18);
      this.button2.Name = "button2";
      this.button2.Size = new System.Drawing.Size(75, 23);
      this.button2.TabIndex = 16;
      this.button2.Text = "追加...";
      this.button2.UseVisualStyleBackColor = true;
      this.button2.Click += new System.EventHandler(this.yellowPageAddButton_Click);
      // 
      // yellowPagesList
      // 
      this.yellowPagesList.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.yellowPagesList.FormattingEnabled = true;
      this.yellowPagesList.ItemHeight = 12;
      this.yellowPagesList.Location = new System.Drawing.Point(6, 18);
      this.yellowPagesList.Name = "yellowPagesList";
      this.yellowPagesList.Size = new System.Drawing.Size(335, 76);
      this.yellowPagesList.TabIndex = 15;
      // 
      // groupBox5
      // 
      this.groupBox5.AutoSize = true;
      this.groupBox5.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
      this.groupBox5.Controls.Add(this.applySettings);
      this.groupBox5.Controls.Add(this.tableLayoutPanel3);
      this.groupBox5.Dock = System.Windows.Forms.DockStyle.Top;
      this.groupBox5.Location = new System.Drawing.Point(4, 161);
      this.groupBox5.Name = "groupBox5";
      this.groupBox5.Size = new System.Drawing.Size(429, 157);
      this.groupBox5.TabIndex = 2;
      this.groupBox5.TabStop = false;
      this.groupBox5.Text = "接続数設定";
      // 
      // applySettings
      // 
      this.applySettings.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
      this.applySettings.Location = new System.Drawing.Point(347, 113);
      this.applySettings.Name = "applySettings";
      this.applySettings.Size = new System.Drawing.Size(73, 26);
      this.applySettings.TabIndex = 54;
      this.applySettings.Text = "適用";
      this.applySettings.UseVisualStyleBackColor = true;
      this.applySettings.Click += new System.EventHandler(this.applySettings_Click);
      // 
      // tableLayoutPanel3
      // 
      this.tableLayoutPanel3.AutoSize = true;
      this.tableLayoutPanel3.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
      this.tableLayoutPanel3.ColumnCount = 5;
      this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
      this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
      this.tableLayoutPanel3.Controls.Add(this.maxUpstreamRate, 1, 2);
      this.tableLayoutPanel3.Controls.Add(label4, 0, 2);
      this.tableLayoutPanel3.Controls.Add(this.maxDirectsPerChannel, 4, 1);
      this.tableLayoutPanel3.Controls.Add(label34, 3, 1);
      this.tableLayoutPanel3.Controls.Add(this.maxDirects, 2, 1);
      this.tableLayoutPanel3.Controls.Add(label35, 1, 1);
      this.tableLayoutPanel3.Controls.Add(label3, 0, 1);
      this.tableLayoutPanel3.Controls.Add(this.maxRelaysPerChannel, 4, 0);
      this.tableLayoutPanel3.Controls.Add(label33, 3, 0);
      this.tableLayoutPanel3.Controls.Add(this.maxRelays, 2, 0);
      this.tableLayoutPanel3.Controls.Add(label1, 1, 0);
      this.tableLayoutPanel3.Controls.Add(label2, 0, 0);
      this.tableLayoutPanel3.Controls.Add(this.channelCleanerLimit, 0, 3);
      this.tableLayoutPanel3.Controls.Add(this.label7, 0, 3);
      this.tableLayoutPanel3.Dock = System.Windows.Forms.DockStyle.Top;
      this.tableLayoutPanel3.Location = new System.Drawing.Point(3, 15);
      this.tableLayoutPanel3.Margin = new System.Windows.Forms.Padding(0);
      this.tableLayoutPanel3.Name = "tableLayoutPanel3";
      this.tableLayoutPanel3.RowCount = 4;
      this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
      this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
      this.tableLayoutPanel3.Size = new System.Drawing.Size(423, 90);
      this.tableLayoutPanel3.TabIndex = 53;
      // 
      // maxUpstreamRate
      // 
      this.maxUpstreamRate.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.tableLayoutPanel3.SetColumnSpan(this.maxUpstreamRate, 4);
      this.maxUpstreamRate.Location = new System.Drawing.Point(181, 53);
      this.maxUpstreamRate.Maximum = new decimal(new int[] {
            1000000,
            0,
            0,
            0});
      this.maxUpstreamRate.Name = "maxUpstreamRate";
      this.maxUpstreamRate.Size = new System.Drawing.Size(239, 19);
      this.maxUpstreamRate.TabIndex = 11;
      // 
      // maxDirectsPerChannel
      // 
      this.maxDirectsPerChannel.Dock = System.Windows.Forms.DockStyle.Fill;
      this.maxDirectsPerChannel.Location = new System.Drawing.Point(357, 28);
      this.maxDirectsPerChannel.Name = "maxDirectsPerChannel";
      this.maxDirectsPerChannel.Size = new System.Drawing.Size(63, 19);
      this.maxDirectsPerChannel.TabIndex = 9;
      // 
      // maxDirects
      // 
      this.maxDirects.Dock = System.Windows.Forms.DockStyle.Fill;
      this.maxDirects.Location = new System.Drawing.Point(218, 28);
      this.maxDirects.Name = "maxDirects";
      this.maxDirects.Size = new System.Drawing.Size(62, 19);
      this.maxDirects.TabIndex = 7;
      // 
      // maxRelaysPerChannel
      // 
      this.maxRelaysPerChannel.Dock = System.Windows.Forms.DockStyle.Fill;
      this.maxRelaysPerChannel.Location = new System.Drawing.Point(357, 3);
      this.maxRelaysPerChannel.Name = "maxRelaysPerChannel";
      this.maxRelaysPerChannel.Size = new System.Drawing.Size(63, 19);
      this.maxRelaysPerChannel.TabIndex = 4;
      // 
      // maxRelays
      // 
      this.maxRelays.Dock = System.Windows.Forms.DockStyle.Fill;
      this.maxRelays.Location = new System.Drawing.Point(218, 3);
      this.maxRelays.Name = "maxRelays";
      this.maxRelays.Size = new System.Drawing.Size(62, 19);
      this.maxRelays.TabIndex = 2;
      // 
      // channelCleanerLimit
      // 
      this.tableLayoutPanel3.SetColumnSpan(this.channelCleanerLimit, 4);
      this.channelCleanerLimit.Dock = System.Windows.Forms.DockStyle.Fill;
      this.channelCleanerLimit.Location = new System.Drawing.Point(181, 73);
      this.channelCleanerLimit.Name = "channelCleanerLimit";
      this.channelCleanerLimit.Size = new System.Drawing.Size(239, 19);
      this.channelCleanerLimit.TabIndex = 55;
      // 
      // label7
      // 
      this.label7.AutoSize = true;
      this.label7.Dock = System.Windows.Forms.DockStyle.Fill;
      this.label7.Location = new System.Drawing.Point(3, 70);
      this.label7.Name = "label7";
      this.label7.Size = new System.Drawing.Size(172, 20);
      this.label7.TabIndex = 56;
      this.label7.Text = "チャンネル自動切断までの時間(分)";
      this.label7.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // groupBox4
      // 
      this.groupBox4.AutoSize = true;
      this.groupBox4.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
      this.groupBox4.Controls.Add(this.portRemoveButton);
      this.groupBox4.Controls.Add(this.portAddButton);
      this.groupBox4.Controls.Add(this.portsList);
      this.groupBox4.Controls.Add(this.portGlobalInterface);
      this.groupBox4.Controls.Add(label27);
      this.groupBox4.Controls.Add(this.portLocalInterface);
      this.groupBox4.Controls.Add(label28);
      this.groupBox4.Controls.Add(this.portLocalDirect);
      this.groupBox4.Controls.Add(this.portLocalRelay);
      this.groupBox4.Controls.Add(this.portGlobalDirect);
      this.groupBox4.Controls.Add(this.portGlobalRelay);
      this.groupBox4.Dock = System.Windows.Forms.DockStyle.Top;
      this.groupBox4.Location = new System.Drawing.Point(4, 4);
      this.groupBox4.Name = "groupBox4";
      this.groupBox4.Size = new System.Drawing.Size(429, 157);
      this.groupBox4.TabIndex = 0;
      this.groupBox4.TabStop = false;
      this.groupBox4.Text = "ポート";
      // 
      // portRemoveButton
      // 
      this.portRemoveButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
      this.portRemoveButton.Location = new System.Drawing.Point(347, 47);
      this.portRemoveButton.Name = "portRemoveButton";
      this.portRemoveButton.Size = new System.Drawing.Size(75, 23);
      this.portRemoveButton.TabIndex = 14;
      this.portRemoveButton.Text = "削除";
      this.portRemoveButton.UseVisualStyleBackColor = true;
      this.portRemoveButton.Click += new System.EventHandler(this.portRemoveButton_Click);
      // 
      // portAddButton
      // 
      this.portAddButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
      this.portAddButton.Location = new System.Drawing.Point(347, 18);
      this.portAddButton.Name = "portAddButton";
      this.portAddButton.Size = new System.Drawing.Size(75, 23);
      this.portAddButton.TabIndex = 13;
      this.portAddButton.Text = "追加...";
      this.portAddButton.UseVisualStyleBackColor = true;
      this.portAddButton.Click += new System.EventHandler(this.portAddButton_Click);
      // 
      // portsList
      // 
      this.portsList.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.portsList.FormattingEnabled = true;
      this.portsList.ItemHeight = 12;
      this.portsList.Location = new System.Drawing.Point(6, 18);
      this.portsList.Name = "portsList";
      this.portsList.Size = new System.Drawing.Size(335, 76);
      this.portsList.TabIndex = 11;
      this.portsList.SelectedIndexChanged += new System.EventHandler(this.portsList_SelectedIndexChanged);
      // 
      // portGlobalInterface
      // 
      this.portGlobalInterface.AutoSize = true;
      this.portGlobalInterface.Location = new System.Drawing.Point(245, 122);
      this.portGlobalInterface.Name = "portGlobalInterface";
      this.portGlobalInterface.Size = new System.Drawing.Size(48, 16);
      this.portGlobalInterface.TabIndex = 9;
      this.portGlobalInterface.Text = "操作";
      this.portGlobalInterface.UseVisualStyleBackColor = true;
      this.portGlobalInterface.CheckedChanged += new System.EventHandler(this.portGlobalInterface_CheckedChanged);
      // 
      // portLocalInterface
      // 
      this.portLocalInterface.AutoSize = true;
      this.portLocalInterface.Location = new System.Drawing.Point(245, 100);
      this.portLocalInterface.Name = "portLocalInterface";
      this.portLocalInterface.Size = new System.Drawing.Size(48, 16);
      this.portLocalInterface.TabIndex = 5;
      this.portLocalInterface.Text = "操作";
      this.portLocalInterface.UseVisualStyleBackColor = true;
      this.portLocalInterface.CheckedChanged += new System.EventHandler(this.portLocalInterface_CheckedChanged);
      // 
      // portLocalDirect
      // 
      this.portLocalDirect.AutoSize = true;
      this.portLocalDirect.Location = new System.Drawing.Point(191, 100);
      this.portLocalDirect.Name = "portLocalDirect";
      this.portLocalDirect.Size = new System.Drawing.Size(48, 16);
      this.portLocalDirect.TabIndex = 4;
      this.portLocalDirect.Text = "視聴";
      this.portLocalDirect.UseVisualStyleBackColor = true;
      this.portLocalDirect.CheckedChanged += new System.EventHandler(this.portLocalDirect_CheckedChanged);
      // 
      // portLocalRelay
      // 
      this.portLocalRelay.AutoSize = true;
      this.portLocalRelay.Location = new System.Drawing.Point(135, 100);
      this.portLocalRelay.Name = "portLocalRelay";
      this.portLocalRelay.Size = new System.Drawing.Size(50, 16);
      this.portLocalRelay.TabIndex = 3;
      this.portLocalRelay.Text = "リレー";
      this.portLocalRelay.UseVisualStyleBackColor = true;
      this.portLocalRelay.CheckedChanged += new System.EventHandler(this.portLocalRelay_CheckedChanged);
      // 
      // portGlobalDirect
      // 
      this.portGlobalDirect.AutoSize = true;
      this.portGlobalDirect.Location = new System.Drawing.Point(191, 122);
      this.portGlobalDirect.Name = "portGlobalDirect";
      this.portGlobalDirect.Size = new System.Drawing.Size(48, 16);
      this.portGlobalDirect.TabIndex = 8;
      this.portGlobalDirect.Text = "視聴";
      this.portGlobalDirect.UseVisualStyleBackColor = true;
      this.portGlobalDirect.CheckedChanged += new System.EventHandler(this.portGlobalDirect_CheckedChanged);
      // 
      // portGlobalRelay
      // 
      this.portGlobalRelay.AutoSize = true;
      this.portGlobalRelay.Location = new System.Drawing.Point(135, 123);
      this.portGlobalRelay.Name = "portGlobalRelay";
      this.portGlobalRelay.Size = new System.Drawing.Size(50, 16);
      this.portGlobalRelay.TabIndex = 7;
      this.portGlobalRelay.Text = "リレー";
      this.portGlobalRelay.UseVisualStyleBackColor = true;
      this.portGlobalRelay.CheckedChanged += new System.EventHandler(this.portGlobalRelay_CheckedChanged);
      // 
      // tabLog
      // 
      this.tabLog.Controls.Add(this.versionInfoButton);
      this.tabLog.Controls.Add(this.logToGUICheck);
      this.tabLog.Controls.Add(this.logToConsoleCheck);
      this.tabLog.Controls.Add(this.logClearButton);
      this.tabLog.Controls.Add(this.selectLogFileName);
      this.tabLog.Controls.Add(this.logToFileCheck);
      this.tabLog.Controls.Add(this.logText);
      this.tabLog.Controls.Add(this.logFileNameText);
      this.tabLog.Controls.Add(this.logLevelList);
      this.tabLog.Controls.Add(this.label5);
      this.tabLog.Location = new System.Drawing.Point(4, 22);
      this.tabLog.Name = "tabLog";
      this.tabLog.Padding = new System.Windows.Forms.Padding(3);
      this.tabLog.Size = new System.Drawing.Size(437, 490);
      this.tabLog.TabIndex = 3;
      this.tabLog.Text = "ログ";
      this.tabLog.UseVisualStyleBackColor = true;
      // 
      // versionInfoButton
      // 
      this.versionInfoButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
      this.versionInfoButton.AutoSize = true;
      this.versionInfoButton.Location = new System.Drawing.Point(8, 403);
      this.versionInfoButton.Name = "versionInfoButton";
      this.versionInfoButton.Size = new System.Drawing.Size(90, 26);
      this.versionInfoButton.TabIndex = 13;
      this.versionInfoButton.Text = "バージョン情報...";
      this.versionInfoButton.UseVisualStyleBackColor = true;
      this.versionInfoButton.Click += new System.EventHandler(this.versionInfoButton_Click);
      // 
      // logToGUICheck
      // 
      this.logToGUICheck.AutoSize = true;
      this.logToGUICheck.Location = new System.Drawing.Point(8, 38);
      this.logToGUICheck.Name = "logToGUICheck";
      this.logToGUICheck.Size = new System.Drawing.Size(76, 16);
      this.logToGUICheck.TabIndex = 10;
      this.logToGUICheck.Text = "GUIに出力";
      this.logToGUICheck.UseVisualStyleBackColor = true;
      this.logToGUICheck.CheckedChanged += new System.EventHandler(this.logToGUICheck_CheckedChanged);
      // 
      // logToConsoleCheck
      // 
      this.logToConsoleCheck.AutoSize = true;
      this.logToConsoleCheck.Location = new System.Drawing.Point(90, 38);
      this.logToConsoleCheck.Name = "logToConsoleCheck";
      this.logToConsoleCheck.Size = new System.Drawing.Size(103, 16);
      this.logToConsoleCheck.TabIndex = 9;
      this.logToConsoleCheck.Text = "コンソールに出力";
      this.logToConsoleCheck.UseVisualStyleBackColor = true;
      this.logToConsoleCheck.CheckedChanged += new System.EventHandler(this.logToConsoleCheck_CheckedChanged);
      // 
      // logClearButton
      // 
      this.logClearButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
      this.logClearButton.AutoSize = true;
      this.logClearButton.Location = new System.Drawing.Point(337, 403);
      this.logClearButton.Name = "logClearButton";
      this.logClearButton.Size = new System.Drawing.Size(90, 26);
      this.logClearButton.TabIndex = 8;
      this.logClearButton.Text = "クリア";
      this.logClearButton.UseVisualStyleBackColor = true;
      this.logClearButton.Click += new System.EventHandler(this.logClearButton_Click);
      // 
      // selectLogFileName
      // 
      this.selectLogFileName.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
      this.selectLogFileName.Location = new System.Drawing.Point(406, 35);
      this.selectLogFileName.Name = "selectLogFileName";
      this.selectLogFileName.Size = new System.Drawing.Size(21, 18);
      this.selectLogFileName.TabIndex = 7;
      this.selectLogFileName.Text = "...";
      this.selectLogFileName.UseVisualStyleBackColor = true;
      this.selectLogFileName.Click += new System.EventHandler(this.selectLogFileName_Click);
      // 
      // logToFileCheck
      // 
      this.logToFileCheck.AutoSize = true;
      this.logToFileCheck.Location = new System.Drawing.Point(199, 37);
      this.logToFileCheck.Name = "logToFileCheck";
      this.logToFileCheck.Size = new System.Drawing.Size(91, 16);
      this.logToFileCheck.TabIndex = 6;
      this.logToFileCheck.Text = "ファイルに出力";
      this.logToFileCheck.UseVisualStyleBackColor = true;
      this.logToFileCheck.CheckedChanged += new System.EventHandler(this.logToFileCheck_CheckedChanged);
      // 
      // logText
      // 
      this.logText.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.logText.Location = new System.Drawing.Point(8, 60);
      this.logText.Multiline = true;
      this.logText.Name = "logText";
      this.logText.ReadOnly = true;
      this.logText.ScrollBars = System.Windows.Forms.ScrollBars.Both;
      this.logText.Size = new System.Drawing.Size(419, 340);
      this.logText.TabIndex = 5;
      this.logText.WordWrap = false;
      // 
      // logFileNameText
      // 
      this.logFileNameText.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.logFileNameText.Location = new System.Drawing.Point(296, 35);
      this.logFileNameText.Name = "logFileNameText";
      this.logFileNameText.Size = new System.Drawing.Size(104, 19);
      this.logFileNameText.TabIndex = 4;
      this.logFileNameText.Validated += new System.EventHandler(this.logFileNameText_Validated);
      // 
      // logLevelList
      // 
      this.logLevelList.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.logLevelList.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
      this.logLevelList.FormattingEnabled = true;
      this.logLevelList.Items.AddRange(new object[] {
            "なし",
            "致命的エラーのみ",
            "エラー全般",
            "エラーと警告",
            "通知メッセージも含む",
            "デバッグメッセージも含む"});
      this.logLevelList.Location = new System.Drawing.Point(105, 9);
      this.logLevelList.Name = "logLevelList";
      this.logLevelList.Size = new System.Drawing.Size(322, 20);
      this.logLevelList.TabIndex = 2;
      this.logLevelList.SelectedIndexChanged += new System.EventHandler(this.logLevelList_SelectedIndexChanged);
      // 
      // label5
      // 
      this.label5.AutoSize = true;
      this.label5.Location = new System.Drawing.Point(8, 12);
      this.label5.Name = "label5";
      this.label5.Size = new System.Drawing.Size(76, 12);
      this.label5.TabIndex = 1;
      this.label5.Text = "ログ出力レベル";
      // 
      // logSaveFileDialog
      // 
      this.logSaveFileDialog.DefaultExt = "txt";
      this.logSaveFileDialog.Filter = "ログファイル(*.txt;*.log)|*.txt;*.log|全てのファイル(*.*)|*.*";
      this.logSaveFileDialog.Title = "ログ記録ファイルの選択";
      // 
      // statusBar
      // 
      this.statusBar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.portLabel,
            this.portOpenedLabel});
      this.statusBar.Location = new System.Drawing.Point(0, 514);
      this.statusBar.Name = "statusBar";
      this.statusBar.Size = new System.Drawing.Size(443, 23);
      this.statusBar.TabIndex = 1;
      // 
      // portLabel
      // 
      this.portLabel.Name = "portLabel";
      this.portLabel.Size = new System.Drawing.Size(49, 18);
      this.portLabel.Text = "ポート:";
      // 
      // portOpenedLabel
      // 
      this.portOpenedLabel.Name = "portOpenedLabel";
      this.portOpenedLabel.Size = new System.Drawing.Size(134, 18);
      this.portOpenedLabel.Text = "toolStripStatusLabel1";
      // 
      // MainForm
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.ClientSize = new System.Drawing.Size(443, 537);
      this.Controls.Add(this.statusBar);
      this.Controls.Add(this.mainTab);
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.Name = "MainForm";
      this.Text = "PeerCastStation";
      this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
      this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.MainForm_FormClosed);
      this.Load += new System.EventHandler(this.MainForm_Load);
      panel1.ResumeLayout(false);
      this.notifyIconMenu.ResumeLayout(false);
      this.mainTab.ResumeLayout(false);
      this.tabChannels.ResumeLayout(false);
      this.splitContainer1.Panel1.ResumeLayout(false);
      this.splitContainer1.Panel2.ResumeLayout(false);
      this.splitContainer1.ResumeLayout(false);
      this.channelListMenu.ResumeLayout(false);
      this.tabControl2.ResumeLayout(false);
      this.tabPage1.ResumeLayout(false);
      this.tabPage3.ResumeLayout(false);
      this.tabPage3.PerformLayout();
      this.groupBox2.ResumeLayout(false);
      this.groupBox2.PerformLayout();
      this.groupBox1.ResumeLayout(false);
      this.groupBox1.PerformLayout();
      this.tabPage2.ResumeLayout(false);
      this.tabSettings.ResumeLayout(false);
      this.panel2.ResumeLayout(false);
      this.panel2.PerformLayout();
      this.groupBox6.ResumeLayout(false);
      this.groupBox6.PerformLayout();
      this.groupBox3.ResumeLayout(false);
      this.groupBox5.ResumeLayout(false);
      this.groupBox5.PerformLayout();
      this.tableLayoutPanel3.ResumeLayout(false);
      this.tableLayoutPanel3.PerformLayout();
      ((System.ComponentModel.ISupportInitialize)(this.maxUpstreamRate)).EndInit();
      ((System.ComponentModel.ISupportInitialize)(this.maxDirectsPerChannel)).EndInit();
      ((System.ComponentModel.ISupportInitialize)(this.maxDirects)).EndInit();
      ((System.ComponentModel.ISupportInitialize)(this.maxRelaysPerChannel)).EndInit();
      ((System.ComponentModel.ISupportInitialize)(this.maxRelays)).EndInit();
      ((System.ComponentModel.ISupportInitialize)(this.channelCleanerLimit)).EndInit();
      this.groupBox4.ResumeLayout(false);
      this.groupBox4.PerformLayout();
      this.tabLog.ResumeLayout(false);
      this.tabLog.PerformLayout();
      this.statusBar.ResumeLayout(false);
      this.statusBar.PerformLayout();
      this.ResumeLayout(false);
      this.PerformLayout();

    }

    #endregion

    private System.Windows.Forms.TabControl mainTab;
    private System.Windows.Forms.TabPage tabChannels;
    private System.Windows.Forms.TabPage tabSettings;
    private System.Windows.Forms.TabPage tabLog;
    private System.Windows.Forms.ComboBox logLevelList;
    private System.Windows.Forms.Label label5;
    private System.Windows.Forms.Button selectLogFileName;
    private System.Windows.Forms.CheckBox logToFileCheck;
    private System.Windows.Forms.TextBox logText;
    private System.Windows.Forms.TextBox logFileNameText;
    private System.Windows.Forms.SaveFileDialog logSaveFileDialog;
    private System.Windows.Forms.Button logClearButton;
    private System.Windows.Forms.StatusStrip statusBar;
    private System.Windows.Forms.ToolStripStatusLabel portLabel;
    private System.Windows.Forms.ToolStripStatusLabel portOpenedLabel;
    private System.Windows.Forms.CheckBox logToGUICheck;
    private System.Windows.Forms.CheckBox logToConsoleCheck;
    private System.Windows.Forms.SplitContainer splitContainer1;
    private System.Windows.Forms.Button channelBump;
    private System.Windows.Forms.Button channelClose;
    private System.Windows.Forms.Button channelPlay;
    private System.Windows.Forms.ListBox channelList;
    private System.Windows.Forms.TabControl tabControl2;
    private System.Windows.Forms.TabPage tabPage1;
    private System.Windows.Forms.TabPage tabPage3;
    private System.Windows.Forms.TabPage tabPage2;
    private System.Windows.Forms.TreeView relayTree;
    private System.Windows.Forms.ToolStripSeparator toolStripMenuItem1;
    private System.Windows.Forms.ContextMenuStrip notifyIconMenu;
    private System.Windows.Forms.GroupBox groupBox2;
		private System.Windows.Forms.TextBox chanTrackContactURL;
    private System.Windows.Forms.TextBox chanTrackAlbum;
    private System.Windows.Forms.TextBox chanTrackTitle;
    private System.Windows.Forms.TextBox chanTrackArtist;
    private System.Windows.Forms.GroupBox groupBox1;
    private System.Windows.Forms.TextBox chanInfoBitrate;
    private System.Windows.Forms.TextBox chanInfoContentType;
    private System.Windows.Forms.TextBox chanInfoContactURL;
    private System.Windows.Forms.TextBox chanInfoGenre;
    private System.Windows.Forms.TextBox chanInfoDesc;
    private System.Windows.Forms.TextBox chanInfoChannelID;
    private System.Windows.Forms.TextBox chanInfoChannelName;
    private System.Windows.Forms.Button chanInfoUpdateButton;
		private System.Windows.Forms.TextBox chanInfoComment;
    private System.Windows.Forms.Button versionInfoButton;
    private System.Windows.Forms.Panel panel2;
    private System.Windows.Forms.GroupBox groupBox5;
    private System.Windows.Forms.TableLayoutPanel tableLayoutPanel3;
    private System.Windows.Forms.NumericUpDown maxDirectsPerChannel;
    private System.Windows.Forms.NumericUpDown maxDirects;
    private System.Windows.Forms.NumericUpDown maxRelaysPerChannel;
    private System.Windows.Forms.NumericUpDown maxRelays;
    private System.Windows.Forms.GroupBox groupBox4;
    private System.Windows.Forms.CheckBox portGlobalInterface;
    private System.Windows.Forms.CheckBox portLocalInterface;
    private System.Windows.Forms.CheckBox portLocalDirect;
    private System.Windows.Forms.CheckBox portLocalRelay;
    private System.Windows.Forms.CheckBox portGlobalDirect;
    private System.Windows.Forms.CheckBox portGlobalRelay;
    private System.Windows.Forms.NumericUpDown maxUpstreamRate;
    private System.Windows.Forms.Button portRemoveButton;
    private System.Windows.Forms.Button portAddButton;
    private System.Windows.Forms.ListBox portsList;
    private System.Windows.Forms.Button applySettings;
    private System.Windows.Forms.GroupBox groupBox3;
    private System.Windows.Forms.Button button1;
    private System.Windows.Forms.Button button2;
    private System.Windows.Forms.ListBox yellowPagesList;
    private System.Windows.Forms.Button channelStart;
    private System.Windows.Forms.Button relayTreeUpdate;
    private System.Windows.Forms.Button connectionClose;
    private System.Windows.Forms.ListBox connectionList;
    private System.Windows.Forms.ContextMenuStrip channelListMenu;
    private System.Windows.Forms.Button connectionReconnect;
    private System.Windows.Forms.TextBox chanTrackGenre;
    private System.Windows.Forms.ToolStripMenuItem versionCheckMenuItem;
    private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
    private System.Windows.Forms.GroupBox groupBox6;
    private System.Windows.Forms.CheckBox showWindowOnStartup;
    private System.Windows.Forms.NumericUpDown channelCleanerLimit;
    private System.Windows.Forms.Label label7;

  }
}

