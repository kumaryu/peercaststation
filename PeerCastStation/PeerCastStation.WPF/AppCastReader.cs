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
using System;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace PeerCastStation.WPF
{
  public class VersionDescription
  {
    public DateTime PublishDate { get; set; }
    public Uri      Link        { get; set; }
    public string   Title       { get; set; }
    public string   Description { get; set; }
  }

  public class NewVersionFoundEventArgs : EventArgs
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
            if (cur.Date<date.Date) {
              cur = date;
              new_version = new VersionDescription {
                Title       = title,
                PublishDate = date,
                Link        = link,
                Description = desc,
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
