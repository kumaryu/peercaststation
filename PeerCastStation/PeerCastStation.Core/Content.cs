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
using System.Collections.Immutable;
using System.Threading;

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
    private class ContentComparer
      : IComparer<Content>
    {
      public int Compare(Content? x, Content? y)
      {
        if (x==y) {
          return 0;
        }
        else if (x==null) {
          return -1;
        }
        else if (y==null) {
          return 1;
        }
        var s = x.Stream.CompareTo(y.Stream);
        if (s!=0) return s;
        var t = x.Timestamp.CompareTo(y.Timestamp);
        if (t!=0) return t;
        return x.Position.CompareTo(y.Position);
      }
    }

    private long serial = 0;
    private ImmutableSortedSet<Content> list = ImmutableSortedSet.Create<Content>(new ContentComparer());
    public TimeSpan PacketTimeLimit { get; set; } = TimeSpan.FromSeconds(3);
    private Channel owner;
    public ContentCollection(Channel owner)
    {
      this.owner = owner;
    }

    private ImmutableSortedSet<Content> ModifyContentList(ref ImmutableSortedSet<Content> contents, Func<ImmutableSortedSet<Content>, ImmutableSortedSet<Content>> modifier)
    {
      ImmutableSortedSet<Content> org;
      ImmutableSortedSet<Content> old;
      do {
        org = contents;
        old = Interlocked.CompareExchange(ref contents, modifier(org), org);
      } while (!Object.ReferenceEquals(org, old));
      return old;
    }

    public void Add(Content item)
    {
      var new_content = new Content(item, Interlocked.Increment(ref serial));
      var old_list = ModifyContentList(ref list, contents => contents.Add(new_content));
      bool added = old_list!=list;
      ModifyContentList(ref list, contents => contents.Except(
        old_list.Where(content =>
           (content.Stream<item.Stream) ||
           (content.Stream==item.Stream &&
            item.Timestamp-content.Timestamp>PacketTimeLimit)
        )
      ));
      if (added) {
        owner.OnContentAdded(item);
      }
    }

    public void Clear()
    {
      ModifyContentList(ref list, contents => contents.Clear());
    }

    public IReadOnlyCollection<Content> ToReadOnlyCollection()
    {
      return list;
    }

    public Content? Newest
    {
      get {
        var contents = list;
        if (contents.Count==0) {
          return null;
        }
        else {
          return contents.Max;
        }
      }
    }

    public Content? Oldest
    {
      get {
        var contents = list;
        if (contents.Count==0) {
          return null;
        }
        else {
          return contents.Min;
        }
      }
    }

    public IEnumerable<Content> GetFirstContents(int stream, TimeSpan t, long position)
    {
      var contents = list;
      var fst = GetFirstContent(contents, stream, t, position);
      if (fst!=null) {
        yield return fst;
        foreach (var content in GetNewerContents(contents, fst.Stream, fst.Timestamp, fst.Position)) {
          yield return content;
        }
      }
    }

    private Content? GetFirstContent(ImmutableSortedSet<Content> contents, int stream, TimeSpan t, long position)
    {
      return contents.Reverse().FirstOrDefault(c =>
        c.Stream>stream ||
        (c.Stream==stream &&
         c.ContFlag==PCPChanPacketContinuation.None &&
         (c.Timestamp>t ||
          (c.Timestamp==t && c.Position>position)
         )
        )
      ) ??
      contents.Reverse().FirstOrDefault(c =>
        c.Stream>stream ||
        (c.Stream==stream &&
         (c.Timestamp>t ||
          (c.Timestamp==t && c.Position>position)
         )
        )
      );
    }

    private IEnumerable<Content> GetNewerContents(ImmutableSortedSet<Content> contents, int stream, TimeSpan t, long position)
    {
      return contents.Where(c =>
        c.Stream>stream ||
        (c.Stream==stream &&
         (c.Timestamp>t ||
          (c.Timestamp==t && c.Position>position)
         )
        )
      );
    }

  }
}
