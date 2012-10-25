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
using System.Collections.Generic;
using System.Collections.Specialized;

namespace PeerCastStation.Core
{
  /// <summary>
  /// チャンネルのストリーム内容を表わすクラスです
  /// </summary>
  [Serializable]
  public class Content
  {
    /// <summary>
    /// コンテントの位置を取得します。
    /// 位置はバイト数や時間とか関係なくソースの出力したパケット番号です
    /// </summary>
    public long Position { get; private set; } 
    /// <summary>
    /// コンテントの内容を取得します
    /// </summary>
    public byte[] Data   { get; private set; } 

    /// <summary>
    /// コンテントの位置と内容を指定して初期化します
    /// </summary>
    /// <param name="pos">位置</param>
    /// <param name="data">内容</param>
    public Content(long pos, byte[] data)
    {
      Position = pos;
      Data = data;
    }
  }

  public class ContentCollection
    : MarshalByRefObject,
      ICollection<Content>
  {
    private List<Content> list = new List<Content>();
    public long LimitPackets { get; set; }
    public ContentCollection()
    {
      LimitPackets = 100;
    }

    public event EventHandler ContentChanged;
    private void OnContentChanged()
    {
      if (ContentChanged!=null) ContentChanged(this, new EventArgs());
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
      lock (list) {
        list.Add(item);
        while (list.Count>LimitPackets && list.Count>1) {
          list.RemoveAt(0);
        }
      }
      OnContentChanged();
    }

    public void Clear()
    {
      lock (list) {
        list.Clear();
      }
      OnContentChanged();
    }

    public bool Contains(Content item)
    {
      lock (list) {
        return list.Contains(item);
      }
    }

    public void CopyTo(Content[] array, int arrayIndex)
    {
      lock (list) {
        list.CopyTo(array, arrayIndex);
      }
    }

    public bool Remove(Content item)
    {
      bool res;
      lock (list) {
        res = list.Remove(item);
      }
      if (res) {
        OnContentChanged();
        return true;
      }
      else {
        return false;
      }
    }

    IEnumerator<Content> IEnumerable<Content>.GetEnumerator()
    {
      return list.GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
      return list.GetEnumerator();
    }

    public Content Oldest {
      get {
        lock (list) {
          if (list.Count>0) {
            return list[0];
          }
          else {
            return null;
          }
        }
      }
    }

    public Content Newest {
      get {
        lock (list) {
          if (list.Count>0) {
            return list[list.Count-1];
          }
          else {
            return null;
          }
        }
      }
    }

    private int GetNewerPacketIndex(long position)
    {
      lock (list) {
        for (var i=0; i<list.Count; i++) {
          if (list[i].Position>position || list[i].Position<position-0x80000000) {
            return i;
          }
        }
        return list.Count;
      }
    }

    public IList<Content> GetNewerContents(long position)
    {
      lock (list) {
        int idx = GetNewerPacketIndex(position);
        var res = new List<Content>(Math.Max(list.Count-idx, 0));
        for (var i=idx; i<list.Count; i++) {
          res.Add(list[i]);
        }
        return res;
      }
    }

    public Content NextOf(long position)
    {
      lock (list) {
        int idx = GetNewerPacketIndex(position);
        if (idx>=list.Count) return null;
        else return list[idx];
      }
    }

    public Content NextOf(Content item)
    {
      return NextOf(item.Position);
    }
  }

}
