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

    private class ChannelsCache
    {
      private static readonly long CacheLimit = 18000;
      private System.Diagnostics.Stopwatch cacheTimer = new System.Diagnostics.Stopwatch();
      private IEnumerable<IYellowPageChannel> channels = null;

      public bool IsValid {
        get { return channels!=null && cacheTimer.ElapsedMilliseconds<CacheLimit; }
      }

      public IEnumerable<IYellowPageChannel> Value {
        get {
          if (IsValid) {
            return channels;
          }
          else {
            return null;
          }
        }
        set {
          channels = value;
          cacheTimer.Restart();
        }
      }

    }
    private ChannelsCache channels = new ChannelsCache();
    private CancellationTokenSource updateCancel = new CancellationTokenSource();

    protected override void OnStart()
    {
    }

    protected override void OnStop()
    {
      updateCancel.Cancel();
    }

    public IEnumerable<IYellowPageChannel> Update()
    {
      var task = UpdateAsync();
      task.Wait();
      return task.Result;
    }

    public Task<IEnumerable<IYellowPageChannel>> UpdateAsync()
    {
      return UpdateAsync(CancellationToken.None);
    }

    public async Task<IEnumerable<IYellowPageChannel>> UpdateAsync(CancellationToken cancellationToken)
    {
      var list = channels.Value;
      if (list!=null) return list;
      using (var cancel=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, updateCancel.Token)) {
        cancel.CancelAfter(5000);
        try {
          channels.Value = 
            (
              await Task.WhenAll(
                Application.PeerCast.YellowPages.Select(async yp => {
                  try {
                    return await yp.GetChannelsAsync(cancel.Token).ConfigureAwait(false);
                  }
                  catch (Exception) {
                    Application.ShowNotificationMessage(new NotificationMessage(
                      yp.Name,
                      "チャンネル一覧を取得できませんでした。",
                      NotificationMessageType.Error)
                    );
                    return Enumerable.Empty<IYellowPageChannel>();
                  }
                })
              ).ConfigureAwait(false)
            )
            .SelectMany(lst => lst)
            .ToArray();
          return channels.Value;
        }
        catch (Exception) {
          return Enumerable.Empty<IYellowPageChannel>();
        }
      }
    }

  }

}

