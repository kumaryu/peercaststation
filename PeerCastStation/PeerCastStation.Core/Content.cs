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
using System.Linq;
using System.Collections.Generic;

namespace PeerCastStation.Core
{
  /// <summary>
  /// チャンネルのストリーム内容を表わすクラスです
  /// </summary>
  [Serializable]
  public class Content
  {
    /// <summary>
    /// コンテントのストリーム番号を取得します
    /// </summary>
    public int Stream { get; private set; } 
    /// <summary>
    /// コンテントのストリーム開始時点からの時刻を取得します。
    /// 時刻はコンテントストリームの論理時間と一致するとは限りません
    /// </summary>
    public TimeSpan Timestamp { get; private set; } 
    /// <summary>
    /// コンテントのストリーム開始時点からのバイト位置を取得します
    /// </summary>
    public long Position { get; private set; } 
    /// <summary>
    /// コンテントパケットの属性を取得します
    /// </summary>
    public PCPChanPacketContinuation ContFlag {  get; private set; }
    /// <summary>
    /// コンテントの内容を取得します
    /// </summary>
    public ReadOnlyMemory<byte> Data { get; private set; } 
    /// <summary>
    /// コンテントのContentCollectionへ追加された順番を取得および設定します
    /// </summary>
    public long Serial { get; private set; } 

    /// <summary>
    /// コンテントのストリーム番号、時刻、位置、内容を指定して初期化します
    /// </summary>
    /// <param name="stream">ストリーム番号</param>
    /// <param name="timestamp">時刻</param>
    /// <param name="pos">バイト位置</param>
    /// <param name="data">内容</param>
    /// <param name="cont">内容</param>
    public Content(int stream, TimeSpan timestamp, long pos, ReadOnlyMemory<byte> data, PCPChanPacketContinuation cont)
    {
      Stream    = stream;
      Timestamp = timestamp;
      Position  = pos;
      Data      = data;
      ContFlag  = cont;
      Serial    = -1;
    }

    public Content(int stream, TimeSpan timestamp, long pos, byte[] data, int offset, int length, PCPChanPacketContinuation cont)
    {
      Stream    = stream;
      Timestamp = timestamp;
      Position  = pos;
      var buf = new byte[length];
      Array.Copy(data, offset, buf, 0, length);
      Data      = buf;
      ContFlag  = cont;
      Serial    = -1;
    }

    public Content(Content other, long serial)
    {
      Stream    = other.Stream;
      Timestamp = other.Timestamp;
      Position  = other.Position;
      Data      = other.Data;
      ContFlag  = other.ContFlag;
      Serial    = serial;
    }
  }

  public class ContentCollection
  {
    private struct ContentKey
      : IComparable<ContentKey>
    {
      public int      Stream;
      public TimeSpan Timestamp;
      public long     Position;

      public ContentKey(int stream, TimeSpan timestamp, long position)
      {
        Stream = stream;
        Timestamp = timestamp;
        Position = position;
      }

      public int CompareTo(ContentKey other)
      {
        var s = Stream.CompareTo(other.Stream);
        if (s!=0) return s;
        var t = Timestamp.CompareTo(other.Timestamp);
        if (t!=0) return t;
        return Position.CompareTo(other.Position);
      }
    }

    private long serial = 0;
    private SortedList<ContentKey, Content> list = new SortedList<ContentKey, Content>();
    public TimeSpan PacketTimeLimit { get; set; } = TimeSpan.FromSeconds(3);
    private Channel owner;
    public ContentCollection(Channel owner)
    {
      this.owner = owner;
    }

    public int Count {
      get {
        lock (list) {
          return list.Count;
        }
      }
    }
    public bool IsReadOnly { get { return false; } }

    public void Add(Content item)
    {
      bool added = false;
      lock (list) {
        try {
          list.Add(new ContentKey(item.Stream, item.Timestamp, item.Position), new Content(item, serial++));
          added = true;
        }
        catch (ArgumentException) {}
        while (
          list.Count>1 &&
          (
           (list.First().Key.Stream<item.Stream) ||
           (list.First().Key.Stream==item.Stream &&
            item.Timestamp-list.First().Key.Timestamp>PacketTimeLimit)
          )
        ) {
          list.RemoveAt(0);
        }
      }
      if (added) {
        owner.OnContentAdded(item);
      }
    }

    public void Clear()
    {
      lock (list) {
        list.Clear();
      }
    }

    public bool Contains(Content item)
    {
      lock (list) {
        return list.ContainsKey(new ContentKey(item.Stream, item.Timestamp, item.Position));
      }
    }

    public void CopyTo(Content[] array, int arrayIndex)
    {
      lock (list) {
        list.Values.CopyTo(array, arrayIndex);
      }
    }

    public Content[] ToArray()
    {
      lock (list) {
        return list.Values.ToArray();
      }
    }

    public bool Remove(Content item)
    {
      bool res;
      lock (list) {
        res = list.Remove(new ContentKey(item.Stream, item.Timestamp, item.Position));
      }
      if (res) {
        return true;
      }
      else {
        return false;
      }
    }

    public Content? GetOldest(int stream)
    {
      lock (list) {
        return list.Values.Where(c => c.Stream>=stream).FirstOrDefault();
      }
    }

    public Content? GetNewest(int stream)
    {
      lock (list) {
        return list.Values.Where(c => c.Stream>=stream).LastOrDefault();
      }
    }

    public Content? Newest
    {
      get {
        lock (list) {
          return list.Values.LastOrDefault();
        }
      }
    }

    public Content? Oldest
    {
      get {
        lock (list) {
          return list.Values.FirstOrDefault();
        }
      }
    }

    private Content? GetFirstContent(int stream, TimeSpan t, long position)
    {
      lock (list) {
        return list.Values.LastOrDefault(c =>
          c.Stream>stream ||
          (c.Stream==stream &&
           c.ContFlag==PCPChanPacketContinuation.None &&
           (c.Timestamp>t ||
            (c.Timestamp==t && c.Position>position)
           )
          )
        ) ??
        list.Values.LastOrDefault(c =>
          c.Stream>stream ||
          (c.Stream==stream &&
           (c.Timestamp>t ||
            (c.Timestamp==t && c.Position>position)
           )
          )
        );
      }
    }

    public IList<Content> GetFirstContents(int stream, TimeSpan t, long position)
    {
      lock (list) {
        var fst = GetFirstContent(stream, t, position);
        if (fst==null) return new Content[0];
        return Enumerable.Concat(new Content[] { fst }, GetNewerContents(fst.Stream, fst.Timestamp, fst.Position)).ToArray();
      }
    }

    public IList<Content> GetNewerContents(int stream, TimeSpan t, long position)
    {
      lock (list) {
        return list.Values.Where(c =>
          c.Stream>stream ||
          (c.Stream==stream &&
           (c.Timestamp>t ||
            (c.Timestamp==t && c.Position>position)
           )
          )
        ).ToArray();
      }
    }

    public Content? GetNewerContent(Content content, out bool skipped)
    {
      Content? res;
      lock (list) {
        res = list.Values.Where(c =>
          c.Stream>content.Stream ||
          (c.Stream==content.Stream &&
           (c.Timestamp>content.Timestamp ||
            (c.Timestamp==content.Timestamp && c.Position>content.Position)
           )
          )
        ).FirstOrDefault();
      }
      skipped = res!=null && content.Serial>=0 && res.Serial>content.Serial+1;
      return res;
    }

    public Content? FindNextByPosition(int stream, long pos)
    {
      lock (list) {
        return list.Values.Where(c => c.Stream>=stream && pos<c.Position).FirstOrDefault();
      }
    }

  }
}
