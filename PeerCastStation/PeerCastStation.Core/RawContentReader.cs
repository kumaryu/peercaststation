using System;
using System.Collections.Generic;
using System.IO;

namespace PeerCastStation.Core
{
  /// <summary>
  /// 読み取ったデータをそのままコンテントとして流すクラスです
  /// </summary>
  public class RawContentReader
    : IContentReader
  {
    public ParsedContent Read(Channel channel, Stream stream)
    {
      if (stream.Length-stream.Position<=0) throw new EndOfStreamException();
      var res = new ParsedContent();
      var pos = channel.ContentPosition;
      if (channel.ContentHeader==null) {
        res.ContentHeader = new Content(pos, new byte[] { });
        var channel_info = new AtomCollection(channel.ChannelInfo.Extra);
        channel_info.SetChanInfoType("RAW");
        res.ChannelInfo = new ChannelInfo(channel_info);
      }
      res.Contents = new List<Content>();
      while (stream.Length-stream.Position>0) {
        var bytes = new byte[Math.Min(8192, stream.Length-stream.Position)];
        var sz = stream.Read(bytes, 0, bytes.Length);
        if (sz>0) {
          Array.Resize(ref bytes, sz);
          res.Contents.Add(new Content(pos, bytes));
          pos += sz;
        }
      }
      return res;
    }

    public string Name
    {
      get { return "RAW"; }
    }
  }
}
