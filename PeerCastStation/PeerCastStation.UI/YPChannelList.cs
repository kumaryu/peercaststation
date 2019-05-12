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
		}

		public IEnumerable<IYellowPageChannel> Update()
		{
			var task = UpdateAsync();
			task.Wait();
			return task.Result;
		}

    private CancellationTokenSource updateCancel = new CancellationTokenSource();
    public async Task<IEnumerable<IYellowPageChannel>> UpdateAsync()
    {
      try {
        if (updateTimer.IsRunning && updateTimer.ElapsedMilliseconds<18000) {
          return Channels.AsEnumerable();
        }
        updateCancel = new CancellationTokenSource(5000);
        var channels = await Task.WhenAll(this.Application.PeerCast.YellowPages.Select(async yp => {
          try {
            return await yp.GetChannelsAsync(updateCancel.Token).ConfigureAwait(false);
          }
          catch (Exception) {
            var msg = new NotificationMessage(
              yp.Name,
              "チャンネル一覧を取得できませんでした。",
              NotificationMessageType.Error);
            foreach (var ui in this.Application.Plugins.Where(p => p is IUserInterfacePlugin)) {
              ((IUserInterfacePlugin)ui).ShowNotificationMessage(msg);
            }
            return Enumerable.Empty<IYellowPageChannel>();
          }
        })).ConfigureAwait(false);
        updateTimer.Restart();
        Channels = channels.SelectMany(result => result).ToList();
        return Channels;
      }
      catch (Exception) {
        return Enumerable.Empty<IYellowPageChannel>();
      }
    }
	}

}
