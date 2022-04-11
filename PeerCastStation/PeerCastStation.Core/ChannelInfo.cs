using System;

namespace PeerCastStation.Core
{
  /// <summary>
  /// チャンネルのメタデータを保持するクラスです
  /// </summary>
  public class ChannelInfo
  {
    /// <summary>
    /// チャンネル名を取得します
    /// </summary>
    public string? Name {
      get { return extra.GetChanInfoName(); }
    }

    /// <summary>
    /// チャンネルストリームの内容種類を取得します
    /// </summary>
    public string? ContentType {
      get { return extra.GetChanInfoType(); }
    }

    /// <summary>
    /// ジャンルを取得します
    /// </summary>
    public string? Genre {
      get { return extra.GetChanInfoGenre(); }
    }

    /// <summary>
    /// チャンネル詳細を取得します
    /// </summary>
    public string? Desc {
      get { return extra.GetChanInfoDesc(); }
    }

    /// <summary>
    /// 配信コメントを取得します
    /// </summary>
    public string? Comment {
      get { return extra.GetChanInfoComment(); }
    }

    /// <summary>
    /// コンタクトURLを取得します
    /// </summary>
    public string? URL {
      get { return extra.GetChanInfoURL(); }
    }

    /// <summary>
    /// 配信ビットレート情報を取得します
    /// </summary>
    public int Bitrate {
      get { return extra.GetChanInfoBitrate() ?? 0; }
    }

    /// <summary>
    /// ストリームのMIME Typeを取得します。
    /// </summary>
    public string MIMEType {
      get {
        var stream_type = extra.GetChanInfoStreamType();
        if (!String.IsNullOrEmpty(stream_type)) {
          return stream_type;
        }
        else {
          switch (ContentType) {
          case "MP3": return "audio/mpeg";
          case "OGG": return "audio/ogg";
          case "OGM": return "video/ogg";
          case "RAW": return "application/octet-stream";
          case "NSV": return "video/nsv";
          case "WMA": return "audio/x-ms-wma";
          case "WMV": return "video/x-ms-wmv";
          case "PLS": return "audio/mpegurl";
          case "M3U": return "audio/m3u";
          case "ASX": return "video/x-ms-asf";
          default: return "application/octet-stream";
          }
        }
      }
    }

    /// <summary>
    /// ストリームファイルの拡張子を取得します
    /// </summary>
    public string ContentExtension {
      get {
        var stream_ext = extra.GetChanInfoStreamExt();
        if (!String.IsNullOrEmpty(stream_ext)) {
          return stream_ext;
        }
        else {
          switch (ContentType) {
          case "MP3": return ".mp3";
          case "OGG": return ".ogg";
          case "OGM": return ".ogv";
          case "RAW": return "";
          case "NSV": return ".nsv";
          case "WMA": return ".wma";
          case "WMV": return ".wmv";
          case "PLS": return ".pls";
          case "M3U": return ".m3u";
          case "ASX": return ".asx";
          default: return "";
          }
        }
      }
    }

    private ReadOnlyAtomCollection extra;
    /// <summary>
    /// その他のチャンネル情報を保持するリストを取得します
    /// </summary>
    public IAtomCollection Extra { get { return extra; } }

    /// <summary>
    /// チャンネル情報を保持するAtomCollectionから新しいチャンネル情報を初期化します
    /// </summary>
    /// <param name="chaninfo">チャンネル情報を保持するAtomCollection</param>
    public ChannelInfo(IAtomCollection chaninfo)
    {
      extra = new ReadOnlyAtomCollection(new AtomCollection(chaninfo));
    }
  }

  /// <summary>
  /// チャンネルのトラック情報を保持するクラスです
  /// </summary>
  public class ChannelTrack
  {
    /// <summary>
    /// タイトルを取得します
    /// </summary>
    public string? Name {
      get { return extra.GetChanTrackTitle(); }
    }

    /// <summary>
    /// アルバム名を取得します
    /// </summary>
    public string? Album {
      get { return extra.GetChanTrackAlbum(); }
    }

    /// <summary>
    /// ジャンルを取得します
    /// </summary>
    public string? Genre {
      get { return extra.GetChanTrackGenre(); }
    }

    /// <summary>
    /// 作者名を取得します
    /// </summary>
    public string? Creator {
      get { return extra.GetChanTrackCreator(); }
    }

    /// <summary>
    /// トラック情報に関するURLを取得します
    /// </summary>
    public string? URL {
      get { return extra.GetChanTrackURL(); }
    }

    private ReadOnlyAtomCollection extra;
    /// <summary>
    /// その他のトラック情報を保持するリストを取得します
    /// </summary>
    public IAtomCollection Extra { get { return extra; } }

    /// <summary>
    /// トラック情報を保持するAtomCollectionから新しいトラック情報を初期化します
    /// </summary>
    /// <param name="chantrack">トラック情報を保持するAtomCollection</param>
    public ChannelTrack(IAtomCollection chantrack)
    {
      extra = new ReadOnlyAtomCollection(new AtomCollection(chantrack));
    }
  }
}
