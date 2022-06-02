using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Http;

namespace PeerCastStation.UI
{
  public class AppCastReader
  {
    public AppCastReader()
    {
    }

    public async Task<IEnumerable<VersionDescription>> DownloadVersionInfoTaskAsync(
      Uri source,
      CancellationToken cancel_token)
    {
      var client = new HttpClient(new HttpClientHandler() {
        AutomaticDecompression = DecompressionMethods.All,
      });
      client.DefaultRequestHeaders.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("deflate"));
      client.DefaultRequestHeaders.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("gzip"));
      try {
        return ParseAppCastString(await client.GetStringAsync(source, cancel_token).ConfigureAwait(false));
      }
      catch {
        return Enumerable.Empty<VersionDescription>();
      }
    }

    public bool DownloadVersionInfoAsync(Uri source, Action<IEnumerable<VersionDescription>> handler)
    {
      Task.Run(async () => {
        var result = await DownloadVersionInfoTaskAsync(source, CancellationToken.None).ConfigureAwait(false);
        if (result.Count()>0) {
          handler?.Invoke(result);
        }
      });
      return true;
    }

    private class ParseErrorException : ApplicationException {}
    private string GetStringValue(XElement? src)
    {
      if (src==null) throw new ParseErrorException();
      return src.Value;
    }

    private string GetContents(XElement? src)
    {
      if (src==null) throw new ParseErrorException();
      return String.Join("", src.Nodes().Select(child => child.ToString(SaveOptions.DisableFormatting)));
    }

    private string GetStringValue(XAttribute? src)
    {
      if (src==null) throw new ParseErrorException();
      return src.Value;
    }

    private InstallerType GetInstallerTypeValue(XAttribute? src)
    {
      InstallerType result;
      if (src==null ||
          !Enum.TryParse<InstallerType>(src.Value, true, out result)) {
        return InstallerType.Unknown;
      }
      return result;
    }

    private InstallerPlatform GetInstallerPlatformValue(XAttribute? src)
    {
      return Updater.ParsePlatformString(src?.Value ?? "unknown");
    }

    private Uri GetUriValue(XElement? src)
    {
      if (!Uri.TryCreate(GetStringValue(src), UriKind.Absolute, out var result)) {
        throw new ParseErrorException();
      }
      return result;
    }

    private Uri GetUriValue(XAttribute? src)
    {
      if (!Uri.TryCreate(GetStringValue(src), UriKind.Absolute, out var result)) {
        throw new ParseErrorException();
      }
      return result;
    }

    private DateTime GetDateTimeValue(XElement? src)
    {
      DateTime result;
      if (!DateTime.TryParse(GetStringValue(src), System.Globalization.DateTimeFormatInfo.InvariantInfo, System.Globalization.DateTimeStyles.None, out result)) {
        throw new ParseErrorException();
      }
      return result;
    }

    private long GetIntValue(XAttribute? src)
    {
      long result;
      if (!Int64.TryParse(GetStringValue(src), out result)) {
        throw new ParseErrorException();
      }
      return result;
    }

    public IEnumerable<VersionDescription> ParseAppCastString(string data)
    {
      var doc = XDocument.Parse(data);
      var versions = new List<VersionDescription>();
      foreach (var item in doc.Descendants("item")) {
        try {
          var ver = new VersionDescription(
            GetDateTimeValue(item.Element("pubDate")),
            GetUriValue(item.Element("link")),
            GetStringValue(item.Element("title")),
            GetContents(item.Element("description")),
            item.Elements("enclosure").Select(elt => 
              new VersionEnclosure(
                GetStringValue(elt),
                GetIntValue(elt.Attribute("length")),
                GetStringValue(elt.Attribute("type")),
                GetUriValue(elt.Attribute("url")),
                GetInstallerTypeValue(elt.Attribute("installer-type")),
                GetInstallerPlatformValue(elt.Attribute("installer-platform")),
                elt.Attribute("install-command")?.Value
              )
            )
          );
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
      return ParseAppCastString(System.Text.Encoding.UTF8.GetString(body));
    }

  }
}
