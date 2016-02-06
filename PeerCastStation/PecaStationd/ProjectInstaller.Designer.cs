namespace PecaStationd
{
	partial class ProjectInstaller
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
			base.Dispose(disposing);
		}

		#region コンポーネント デザイナーで生成されたコード

		/// <summary>
		/// デザイナー サポートに必要なメソッドです。このメソッドの内容を
		/// コード エディターで変更しないでください。
		/// </summary>
		private void InitializeComponent()
		{
      this.peerCastStationServiceProcessInstaller = new System.ServiceProcess.ServiceProcessInstaller();
      this.peerCastStationServiceInstaller = new System.ServiceProcess.ServiceInstaller();
      // 
      // peerCastStationServiceProcessInstaller
      // 
      this.peerCastStationServiceProcessInstaller.Account = System.ServiceProcess.ServiceAccount.LocalService;
      this.peerCastStationServiceProcessInstaller.Password = null;
      this.peerCastStationServiceProcessInstaller.Username = null;
      // 
      // peerCastStationServiceInstaller
      // 
      this.peerCastStationServiceInstaller.Description = "PeerCastStation";
      this.peerCastStationServiceInstaller.DisplayName = "PeerCastStation Service";
      this.peerCastStationServiceInstaller.ServiceName = "PeerCastStationService";
      this.peerCastStationServiceInstaller.StartType = System.ServiceProcess.ServiceStartMode.Automatic;
      // 
      // ProjectInstaller
      // 
      this.Installers.AddRange(new System.Configuration.Install.Installer[] {
            this.peerCastStationServiceProcessInstaller,
            this.peerCastStationServiceInstaller});
      this.AfterInstall += new System.Configuration.Install.InstallEventHandler(this.ProjectInstaller_AfterInstall);

		}

		#endregion

		private System.ServiceProcess.ServiceProcessInstaller peerCastStationServiceProcessInstaller;
		private System.ServiceProcess.ServiceInstaller peerCastStationServiceInstaller;

	}
}