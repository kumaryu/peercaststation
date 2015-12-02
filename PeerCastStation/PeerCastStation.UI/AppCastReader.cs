using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Net;
using System.Threading.Tasks;
using PeerCastStation.Core;
using System.Threading;

namespace PeerCastStation.UI
{
  internal class AppCastReader
  {
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
      this.client.DownloadDataAsync(source);
      return true;
    }

		public async Task<IEnumerable<VersionDescription>> DownloadVersionInfoTaskAsync(
			Uri source,
			CancellationToken cancel_token)
		{
			var client = new WebClient();
			cancel_token.Register(() => client.CancelAsync());
			return ParseAppCast(
				System.Text.Encoding.UTF8.GetString(
					await this.client.DownloadDataTaskAsync(source)));
		}

    private class ParseErrorException : ApplicationException {}
    private string GetStringValue(XElement src)
    {
      if (src==null) throw new ParseErrorException();
      return src.Value;
    }

    private string GetContents(XElement src)
    {
      if (src==null) throw new ParseErrorException();
      return String.Join("", src.Nodes().Select(child => child.ToString(SaveOptions.DisableFormatting)));
    }

    private string GetStringValue(XAttribute src)
    {
      if (src==null) throw new ParseErrorException();
      return src.Value;
    }

    private InstallerType GetInstallerTypeValue(XAttribute src)
    {
      if (src==null) return InstallerType.Unknown;
      InstallerType result;
      if (src==null ||
          !Enum.TryParse<InstallerType>(src.Value, true, out result)) {
        return InstallerType.Unknown;
      }
      return result;
    }

    private Uri GetUriValue(XElement src)
    {
      Uri result = null;
      if (!Uri.TryCreate(GetStringValue(src), UriKind.Absolute, out result)) {
        throw new ParseErrorException();
      }
      return result;
    }

    private Uri GetUriValue(XAttribute src)
    {
      Uri result = null;
      if (!Uri.TryCreate(GetStringValue(src), UriKind.Absolute, out result)) {
        throw new ParseErrorException();
      }
      return result;
    }

    private DateTime GetDateTimeValue(XElement src)
    {
      DateTime result;
      if (!DateTime.TryParse(GetStringValue(src), System.Globalization.DateTimeFormatInfo.InvariantInfo, System.Globalization.DateTimeStyles.None, out result)) {
        throw new ParseErrorException();
      }
      return result;
    }

    private long GetIntValue(XAttribute src)
    {
      long result;
      if (!Int64.TryParse(GetStringValue(src), out result)) {
        throw new ParseErrorException();
      }
      return result;
    }

		private IEnumerable<VersionDescription> ParseAppCast(string data)
		{
			var doc = XDocument.Parse(data);
			var versions = new List<VersionDescription>();
			foreach (var item in doc.Descendants("item")) {
				try {
					var ver = new VersionDescription {
						Title       = GetStringValue(item.Element("title")),
						PublishDate = GetDateTimeValue(item.Element("pubDate")),
						Link        = GetUriValue(item.Element("link")),
						Description = GetContents(item.Element("description")),
						Enclosures  = item.Elements("enclosure").Select(elt => 
							new VersionEnclosure {
								Url    = GetUriValue(elt.Attribute("url")),
								Length = GetIntValue(elt.Attribute("length")),
								Type   = GetStringValue(elt.Attribute("type")),
								Title  = GetStringValue(elt),
								InstallerType = GetInstallerTypeValue(elt.Attribute("installer-type")),
							}
						).ToArray(),
					};
					versions.Add(ver);
				}
				catch (ParseErrorException) {
					//Do nothing
				}
			}
			return versions;
		}

		private void OnDownloadDataCompleted(object sender, DownloadDataCompletedEventArgs args)
		{
			if (args.Cancelled || args.Error!=null) return;
			var result = ParseAppCast(System.Text.Encoding.UTF8.GetString(args.Result));
			if (result.Count()>0 && downloaded!=null) {
				downloaded(result);
			}
		}

  }
}
