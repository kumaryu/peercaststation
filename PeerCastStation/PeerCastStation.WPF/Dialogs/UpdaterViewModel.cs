﻿// PeerCastStation, a P2P streaming servent.
// Copyright (C) 2013 PROGRE (djyayutto@gmail.com)
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
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PeerCastStation.UI;
using PeerCastStation.WPF.Commons;
using PeerCastStation.Core;

namespace PeerCastStation.WPF.Dialogs
{
	internal class UpdaterViewModel
		: ViewModelBase
	{
		private IEnumerable<VersionDescription> versionInfo = Enumerable.Empty<VersionDescription>();
		public IEnumerable<VersionDescription> VersionInfo {
			get { return versionInfo; }
			private set {
				if (SetProperty("VersionInfo", ref versionInfo, value)) {
					OnPropertyChanged("Descriptions");
					OnPropertyChanged("Enclosures");
				}
			}
		}

		public string Descriptions {
			get { return String.Join("\n", versionInfo.Select(v => v.Description).ToArray()); }
		}

		internal enum UpdateActionState {
			Idle,
			Checking,
			NoUpdates,
			NewVersionFound,
			Downloading,
			Downloaded,
			Aborted,
		};
		private UpdateActionState state = UpdateActionState.Idle;
		public UpdateActionState State {
			get { return state; }
			private set {
				if (state==value) return;
				state = value;
				OnPropertyChanged("State");
			}
		}
		private double progress = 0.0;
		public double Progress {
			get { return progress; }
			private set {
				if (progress==value) return;
				progress = value;
				OnPropertyChanged("Progress");
			}
		}

		public UpdaterViewModel(Updater updater)
		{
      versionChecker = updater;
		}

		private Updater versionChecker;
		public async Task<IEnumerable<VersionDescription>> DoCheckUpdate()
		{
			try {
				var results = await versionChecker.CheckVersionTaskAsync(cancelSource.Token);
				this.VersionInfo = results;
				if (results!=null && results.Count()>0) {
          if (versionChecker.CurrentInstallerType==InstallerType.Archive ||
              versionChecker.CurrentInstallerType==InstallerType.ServiceArchive) {
            var enclosure = VersionInfo.First().Enclosures.First(e => e.InstallerType==versionChecker.CurrentInstallerType);
            downloadPath = enclosure.Url.AbsolutePath;
            this.State = UpdateActionState.Downloaded;
          }
          else {
            this.State = UpdateActionState.NewVersionFound;
          }
				}
				else {
					this.State = UpdateActionState.NoUpdates;
				}
				return results;
			}
			catch (System.Net.WebException) {
				this.State = UpdateActionState.NoUpdates;
				return Enumerable.Empty<VersionDescription>();
			}
		}

    private string downloadPath;
    public async Task DoDownload()
    {
      var enclosure = VersionInfo.First().Enclosures.First(e => e.InstallerType==versionChecker.CurrentInstallerType);
      if (versionChecker.CurrentInstallerType==InstallerType.Archive ||
          versionChecker.CurrentInstallerType==InstallerType.ServiceArchive) {
        downloadPath = enclosure.Url.AbsolutePath;
        this.State = UpdateActionState.Downloaded;
        return;
      }
      var client = new System.Net.WebClient();
      client.DownloadProgressChanged += (sender, args) => {
        this.Progress = args.ProgressPercentage/100.0;
      };
      cancelSource.Token.Register(() => {
        client.CancelAsync();
      }, true);
      this.State = UpdateActionState.Downloading;
      try {
        downloadPath = System.IO.Path.Combine(
          Shell.GetKnownFolder(Shell.KnownFolder.Downloads),
          System.IO.Path.GetFileName(enclosure.Url.AbsolutePath));
        await client.DownloadFileTaskAsync(
          enclosure.Url.ToString(),
          downloadPath);
        this.State = UpdateActionState.Downloaded;
      }
      catch (System.Net.WebException) {
        this.State = UpdateActionState.Aborted;
      }
    }

    private void DoInstall()
    {
      if (downloadPath==null) return;
      switch (System.IO.Path.GetExtension(downloadPath).ToLowerInvariant()) {
      case ".msi":
      case ".exe":
        Process.Start(downloadPath);
        PeerCastApplication.Current.Stop();
        break;
      case ".zip":
        PeerCastApplication.Current.Stop(3);
        break;
      default:
        Process.Start("explorer.exe", $"/select,\"{downloadPath}\"");
        PeerCastApplication.Current.Stop();
        break;
      }
    }

		private System.Threading.CancellationTokenSource cancelSource =
			new System.Threading.CancellationTokenSource();
		public async void Execute()
		{
			switch (this.State) {
			case UpdateActionState.Idle:
			case UpdateActionState.NoUpdates:
				await DoCheckUpdate();
				break;
			case UpdateActionState.NewVersionFound:
			case UpdateActionState.Aborted:
				await DoDownload();
				break;
			case UpdateActionState.Checking:
			case UpdateActionState.Downloading:
				cancelSource.Cancel();
				break;
			case UpdateActionState.Downloaded:
				DoInstall();
				break;
			}
		}

	}
}
