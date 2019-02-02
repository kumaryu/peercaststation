using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Threading;

namespace PeerCastStation.Updater
{
  public enum InstallerType {
    Unknown   = 0,
    Installer,
    Archive,
    ServiceInstaller,
    ServiceArchive,
  }

  public class VersionEnclosure
  {
    public string Title  { get; set; }
    public long   Length { get; set; }
    public string Type   { get; set; }
    public Uri    Url    { get; set; }
    public InstallerType InstallerType { get; set; }
  }

  public class VersionDescription
  {
    public DateTime PublishDate { get; set; }
    public Uri      Link        { get; set; }
    public string   Title       { get; set; }
    public string   Description { get; set; }
    public VersionEnclosure[] Enclosures { get; set; }
  }

  internal class AppCastReader
  {
    private WebClient client;
    private Action<IEnumerable<VersionDescription>> downloaded;
    public AppCastReader()
    {
      this.client = new WebClient();
      this.client.Headers.Add(HttpRequestHeader.AcceptEncoding, "deflate, gzip");
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
      using (var client = new WebClient())
      using (cancel_token.Register(() => client.CancelAsync(), false)) {
        client.Headers.Add(HttpRequestHeader.AcceptEncoding, "deflate, gzip");
        var body = await client.DownloadDataTaskAsync(source).ConfigureAwait(false);
        return ParseResponse(client.ResponseHeaders, body);
      }
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

    private IEnumerable<VersionDescription> ParseResponse(WebHeaderCollection header, byte[] body)
    {
      switch (header.Get("Content-Encoding")) {
      case "gzip":
        using (var dst=new System.IO.MemoryStream())
        using (var s=new System.IO.Compression.GZipStream(new System.IO.MemoryStream(body), System.IO.Compression.CompressionMode.Decompress)) {
          s.CopyTo(dst);
          dst.Flush();
          body = dst.ToArray();
        }
        break;
      case "deflate":
        using (var dst=new System.IO.MemoryStream())
        using (var s=new System.IO.Compression.DeflateStream(new System.IO.MemoryStream(body), System.IO.Compression.CompressionMode.Decompress)) {
          s.CopyTo(dst);
          dst.Flush();
          body = dst.ToArray();
        }
        break;
      default:
        break;
      }
      return ParseAppCast(System.Text.Encoding.UTF8.GetString(body));
    }

    private void OnDownloadDataCompleted(object sender, DownloadDataCompletedEventArgs args)
    {
      if (args.Cancelled || args.Error!=null) return;
      var result = ParseResponse(this.client.ResponseHeaders, args.Result);
      if (result.Count()>0 && downloaded!=null) {
        downloaded(result);
      }
    }

  }
}
