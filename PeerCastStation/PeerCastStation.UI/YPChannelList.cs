using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PeerCastStation.Core;

namespace PeerCastStation.UI
{
	[Plugin]
	public class YPChannelList
		: PluginBase
	{
		public override string Name {
			get { return "YP Channel List"; }
		}

		private System.Diagnostics.Stopwatch updateTimer = new System.Diagnostics.Stopwatch();
		public IReadOnlyList<IYellowPageChannel> Channels { get; private set; }

		public YPChannelList()
		{
			this.Channels = new List<IYellowPageChannel>().AsReadOnly(); 
		}

		protected override void OnStart()
		{
			base.OnStart();
		}

		protected override void OnStop()
		{
			base.OnStop();
			updateCancel.Cancel();
			if (updateTask!=null) {
				updateTask.Wait();
			}
		}

		public IEnumerable<IYellowPageChannel> Update()
		{
			var task = UpdateAsync();
			task.Wait();
			return task.Result;
		}

		private Task<IEnumerable<IYellowPageChannel>> updateTask;
		private CancellationTokenSource updateCancel = new CancellationTokenSource();
		public async Task<IEnumerable<IYellowPageChannel>> UpdateAsync()
		{
			if (updateTimer.IsRunning && updateTimer.ElapsedMilliseconds<18000) return Channels;
			updateCancel = new CancellationTokenSource(5000);
			updateTask = Task.WhenAll(this.Application.PeerCast.YellowPages.Select(yp => yp.GetChannelsAsync(updateCancel.Token)))
				.ContinueWith(task => {
					updateTimer.Restart();
					if (task.IsCanceled || task.IsFaulted) return Enumerable.Empty<IYellowPageChannel>();
					Channels = task.Result.SelectMany(result => result).ToList();
					return Channels;
				});
			return await updateTask;
		}
	}

}
