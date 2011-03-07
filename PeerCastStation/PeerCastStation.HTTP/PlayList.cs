using System;
using System.Collections.Generic;
using PeerCastStation.Core;

namespace PeerCastStation.HTTP
{
  /// <summary>
  /// プレイリストを表すインターフェースです
  /// </summary>
  public interface IPlayList
  {
    /// <summary>
    /// プレイリスト自体のContent-Typeを取得します
    /// </summary>
    string MIMEType { get; }

    /// <summary>
    /// プレイリストに含まれるチャンネルのコレクションを取得します
    /// </summary>
    IList<ChannelInfo> Channels { get; }

    /// <summary>
    /// Channelsを参照してプレイリストを作成し文字列で返します
    /// </summary>
    /// <param name="baseuri">ベースとなるURI</param>
    /// <returns>作成したプレイリスト</returns>
    string CreatePlayList(Uri baseuri);
  }

  /// <summary>
  /// URLを列挙するだけの簡単なプレイリストを作成するクラスです
  /// </summary>
  public class PLSPlayList
    : IPlayList
  {
    public string MIMEType { get { return "audio/x-mpegurl"; } }
    public IList<ChannelInfo> Channels { get; private set; }

    public PLSPlayList()
    {
      Channels = new List<ChannelInfo>();
    }

    public string CreatePlayList(Uri baseuri)
    {
      var res = new System.Text.StringBuilder();
      foreach (var info in Channels) {
        var url = new Uri(baseuri, info.ChannelID.ToString("N").ToUpper() + info.ContentExtension);
        res.AppendLine(url.ToString());
      }
      return res.ToString();
    }
  }

  /// <summary>
  /// ASX形式のプレイリストを作成するクラスです
  /// </summary>
  public class ASXPlayList
    : IPlayList
  {
    public string MIMEType { get { return "video/x-ms-asf"; } }
    public IList<ChannelInfo> Channels { get; private set; }

    public ASXPlayList()
    {
      Channels = new List<ChannelInfo>();
    }

    public string CreatePlayList(Uri baseuri)
    {
      var stream = new System.IO.StringWriter();
      var xml = new System.Xml.XmlTextWriter(stream);
      xml.Formatting = System.Xml.Formatting.Indented;
      xml.WriteStartElement("ASX");
      xml.WriteAttributeString("version", "3.0");
      if (Channels.Count>0) {
        xml.WriteElementString("Title", Channels[0].Name);
      }
      foreach (var info in Channels) {
        string name = info.Name;
        string contact_url = null;
        if (info.Extra.GetChanInfo()!=null) {
          contact_url = info.Extra.GetChanInfo().GetChanInfoURL();
        }
        var stream_url = new Uri(baseuri, info.ChannelID.ToString("N").ToUpper() + info.ContentExtension);
        xml.WriteStartElement("Entry");
        xml.WriteElementString("Title", name);
        if (contact_url!=null && contact_url!="") {
          xml.WriteStartElement("MoreInfo");
          xml.WriteAttributeString("href", contact_url);
          xml.WriteEndElement();
        }
        xml.WriteStartElement("Ref");
        xml.WriteAttributeString("href", stream_url.ToString());
        xml.WriteEndElement();
        xml.WriteEndElement();
      }
      xml.WriteEndElement();
      xml.Close();
      return stream.ToString();
    }
  }
}
