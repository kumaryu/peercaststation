using System;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace PeerCastStation.GUI
{
  public class VersionDescription
  {
    public DateTime PublishDate { get; set; }
    public Uri      Link        { get; set; }
    public string   Title       { get; set; }
  }

  public class NewVersionFoundEventArgs
  {
    public VersionDescription VersionDescription { get; private set; }
    public NewVersionFoundEventArgs(VersionDescription desc)
    {
      this.VersionDescription = desc;
    }
  }
  public delegate void NewVersionFoundEventHandler(object sender, NewVersionFoundEventArgs args);

  internal class AppCastReader
  {
    Uri url;
    WebClient client;
    DateTime currentVersion;
    public event NewVersionFoundEventHandler NewVersionFound;
    public AppCastReader(Uri url, DateTime current_version)
    {
      this.url = url;
      this.client = new WebClient();
      this.client.DownloadDataCompleted += OnDownloadDataCompleted;
      this.currentVersion = current_version;
    }

    public void CheckVersion()
    {
      this.client.DownloadDataAsync(url);
    }

    private void OnDownloadDataCompleted(object sender, DownloadDataCompletedEventArgs args)
    {
      if (!args.Cancelled && args.Error==null) {
        var data = System.Text.Encoding.UTF8.GetString(args.Result);
        var doc = XDocument.Parse(data);
        var cur = currentVersion;
        VersionDescription new_version = null;
        foreach (var item in doc.Descendants("item")) {
          var xtitle = item.Element("title");
          var xdate  = item.Element("pubDate");
          var xlink  = item.Element("link");
          DateTime date;
          Uri link = null;
          string title = null;
          if (xtitle!=null && xtitle.Value!=null) {
            title = xtitle.Value;
          }
          if (xlink!=null && xlink.Value!=null) {
            Uri.TryCreate(xlink.Value, UriKind.Absolute, out link);
          }
          if (xdate!=null && xdate.Value!=null && DateTime.TryParse(xdate.Value, out date)) {
            if (cur<date) {
              cur = date;
              new_version = new VersionDescription {
                Title       = title,
                PublishDate = date,
                Link        = link,
              };
            }
          }
        }
        if (new_version!=null && NewVersionFound!=null) {
          NewVersionFound(this, new NewVersionFoundEventArgs(new_version));
        }
      }
    }
  }
}
