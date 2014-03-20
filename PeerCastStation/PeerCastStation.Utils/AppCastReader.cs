using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Net;
using PeerCastStation.Core;

namespace PeerCastStation.Utils
{
  internal class AppCastReader
  {
    private Uri url;
    private WebClient client;
    private Action<IEnumerable<VersionDescription>> downloaded;
    public AppCastReader()
    {
      this.client = new WebClient();
      this.client.DownloadDataCompleted += OnDownloadDataCompleted;
    }

    public bool DownloadVersionInfoAsync(Uri source, Action<IEnumerable<VersionDescription>> handler)
    {
      if (this.client.IsBusy) return false;
      this.downloaded = handler;
      this.client.DownloadDataAsync(url);
      return true;
    }

    private void OnDownloadDataCompleted(object sender, DownloadDataCompletedEventArgs args)
    {
      if (args.Cancelled || args.Error!=null) return;
      var data = System.Text.Encoding.UTF8.GetString(args.Result);
      var doc = XDocument.Parse(data);
      var versions = new List<VersionDescription>();
      foreach (var item in doc.Descendants("item")) {
        var xtitle = item.Element("title");
        var xdate  = item.Element("pubDate");
        var xlink  = item.Element("link");
        var xdesc  = item.Element("description");
        DateTime date;
        Uri    link  = null;
        string title = null;
        string desc  = null;
        if (xtitle!=null && xtitle.Value!=null) {
          title = xtitle.Value;
        }
        if (xlink!=null && xlink.Value!=null) {
          Uri.TryCreate(xlink.Value, UriKind.Absolute, out link);
        }
        if (xdesc!=null && xdesc.Value!=null) {
          desc = xdesc.ToString();
        }
        if (xdate!=null && xdate.Value!=null && DateTime.TryParse(xdate.Value, out date)) {
          versions.Add(new VersionDescription {
            Title       = title,
            PublishDate = date,
            Link        = link,
            Description = desc,
          });
        }
        if (downloaded!=null) {
          downloaded(versions);
        }
      }
    }

  }
}
