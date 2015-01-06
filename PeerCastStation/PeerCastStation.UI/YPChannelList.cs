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
		public IReadOnlyList<YPChannel> Channels { get; private set; }
		public IList<YellowPage> YellowPages { get; private set; }

		public YPChannelList()
		{
			this.Channels = new List<YPChannel>().AsReadOnly(); 
			this.YellowPages = new List<YellowPage>();
			this.YellowPages.Add(new YellowPage("SP", new Uri("http://bayonet.ddo.jp/sp/index.txt")));
			this.YellowPages.Add(new YellowPage("TP", new Uri("http://temp.orz.hm/yp/index.txt")));
			this.YellowPages.Add(new YellowPage("芝", new Uri("http://peercast.takami98.net/turf-page/index.txt")));
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

		public IEnumerable<YPChannel> Update()
		{
			var task = UpdateAsync();
			task.Wait();
			return task.Result;
		}

		private Task<IEnumerable<YPChannel>> updateTask;
		private CancellationTokenSource updateCancel = new CancellationTokenSource();
		public async Task<IEnumerable<YPChannel>> UpdateAsync()
		{
			if (updateTimer.IsRunning && updateTimer.ElapsedMilliseconds<18000) return Channels;
			updateCancel = new CancellationTokenSource();
			updateTask = Task.WhenAll(this.YellowPages.Select(yp => yp.GetChannelsAsync(updateCancel.Token)))
				.ContinueWith(task => {
					updateTimer.Restart();
					if (task.IsCanceled || task.IsFaulted) return Enumerable.Empty<YPChannel>();
					Channels = task.Result.SelectMany(result => result).ToList();
					return Channels;
				});
			return await updateTask;
		}
	}

	public class YellowPage
	{
		public string Name { get; private set; }
		public Uri Uri { get; private set; }
		public YellowPage(string name, Uri uri)
		{
			this.Name = name;
			this.Uri = uri;
		}

		public async Task<IEnumerable<YPChannel>> GetChannelsAsync(CancellationToken cancel_token)
		{
			var client = new WebClient();
			client.Encoding = System.Text.Encoding.UTF8;
			cancel_token.Register(() => client.CancelAsync());
			try {
				using (var reader=new StringReader(await client.DownloadStringTaskAsync(this.Uri))) {
					var results = new List<YPChannel>();
					var line = reader.ReadLine();
					while (line!=null) {
						var tokens = line.Split(new string[] { "<>" }, StringSplitOptions.None);
						var channel = new YPChannel();
						channel.Source = this;
						if (tokens.Length> 0) channel.Name        = ParseStr(tokens[0]);  //1 CHANNEL_NAME チャンネル名
						if (tokens.Length> 1) channel.ChannelId   = ParseStr(tokens[1]);  //2 ID ID ユニーク値16進数32桁、制限チャンネルは全て0埋め
						if (tokens.Length> 2) channel.Tracker     = ParseStr(tokens[2]);  //3 TIP TIP ポートも含む。Push配信時はブランク、制限チャンネルは127.0.0.1
						if (tokens.Length> 3) channel.ContactUrl  = ParseStr(tokens[3]);  //4 CONTACT_URL コンタクトURL 基本的にURL、任意の文字列も可 CONTACT_URL
						if (tokens.Length> 4) channel.Genre       = ParseStr(tokens[4]);  //5 GENRE ジャンル
						if (tokens.Length> 5) channel.Description = ParseStr(tokens[5]);  //6 DETAIL 詳細
						if (tokens.Length> 6) channel.Listeners   = ParseInt(tokens[6]);  //7 LISTENER_NUM Listener数 -1は非表示、-1未満はサーバのメッセージ。ブランクもあるかも
						if (tokens.Length> 7) channel.Relays      = ParseInt(tokens[7]);  //8 RELAY_NUM Relay数 同上 
						if (tokens.Length> 8) channel.Bitrate     = ParseInt(tokens[8]);  //9 BITRATE Bitrate 単位は kbps 
						if (tokens.Length> 9) channel.ContentType = ParseStr(tokens[9]);  //10 TYPE Type たぶん大文字 
						if (tokens.Length>10) channel.Artist      = ParseStr(tokens[10]); //11 TRACK_ARTIST トラック アーティスト 
						if (tokens.Length>11) channel.Album       = ParseStr(tokens[11]); //12 TRACK_ALBUM トラック アルバム 
						if (tokens.Length>12) channel.TrackTitle  = ParseStr(tokens[12]); //13 TRACK_TITLE トラック タイトル 
						if (tokens.Length>13) channel.TrackUrl    = ParseStr(tokens[13]); //14 TRACK_CONTACT_URL トラック コンタクトURL 基本的にURL、任意の文字列も可 
						if (tokens.Length>15) channel.Uptime      = ParseUptime(tokens[15]); //16 BROADCAST_TIME 配信時間 000〜99999 
						if (tokens.Length>17) channel.Comment     = ParseStr(tokens[17]); //18 COMMENT コメント 
						results.Add(channel);
						line = reader.ReadLine();
					}
					return results;
				}
			}
			catch (Exception e) {
				System.Diagnostics.Debug.WriteLine(e);
				return Enumerable.Empty<YPChannel>();
			}
		}

		private int? ParseUptime(string token)
		{
			if (String.IsNullOrWhiteSpace(token)) return null;
			var times = token.Split(':');
			if (times.Length<2) return ParseInt(times[0]);
			var hours   = ParseInt(times[0]);
			var minutes = ParseInt(times[1]);
			if (!hours.HasValue || !minutes.HasValue) return null;
			return (hours*60 + minutes)*60;
		}

		private string ParseStr(string token)
		{
			if (String.IsNullOrWhiteSpace(token)) return token;
			return System.Net.WebUtility.HtmlDecode(token);
		}

		private int? ParseInt(string token)
		{
			int result;
			if (token==null || !Int32.TryParse(token, out result)) return null;
			return result;
		}
	}

	public class YPChannel
	{
		public YellowPage Source      { get; set; }
		public string     Name        { get; set; }
		public string     ChannelId   { get; set; }
		public string     Tracker     { get; set; }
		public string     ContentType { get; set; }
		public int?       Listeners   { get; set; }
		public int?       Relays      { get; set; }
		public int?       Bitrate     { get; set; }
		public int?       Uptime      { get; set; }
		public string     ContactUrl  { get; set; }
		public string     Genre       { get; set; }
		public string     Description { get; set; }
		public string     Comment     { get; set; }
		public string     Artist      { get; set; }
		public string     TrackTitle  { get; set; }
		public string     Album       { get; set; }
		public string     TrackUrl    { get; set; }
	}

}
