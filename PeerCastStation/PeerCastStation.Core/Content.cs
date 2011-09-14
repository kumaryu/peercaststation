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
    : System.Collections.Specialized.INotifyCollectionChanged,
      ICollection<Content>
  {
    private SortedList<long, Content> list = new SortedList<long,Content>();
    public long LimitPackets { get; set; }
    public ContentCollection()
    {
      LimitPackets = 160;
    }

    public event NotifyCollectionChangedEventHandler CollectionChanged;

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
        if (list.ContainsKey(item.Position)) {
          list[item.Position] = item;
        }
        else {
          list.Add(item.Position, item);
        }
        while (list.Count>LimitPackets && list.Count>1) {
          list.RemoveAt(0);
        }
      }
      if (CollectionChanged!=null) {
        CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
      }
    }

    public void Clear()
    {
      lock (list) {
        list.Clear();
      }
      if (CollectionChanged!=null) {
        CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
      }
    }

    public bool Contains(Content item)
    {
      lock (list) {
        return list.ContainsValue(item);
      }
    }

    public void CopyTo(Content[] array, int arrayIndex)
    {
      lock (list) {
        list.Values.CopyTo(array, arrayIndex);
      }
    }

    public bool Remove(Content item)
    {
      bool res;
      lock (list) {
        res = list.Remove(item.Position);
      }
      if (res) {
        if (CollectionChanged!=null) {
          CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
        }
        return true;
      }
      else {
        return false;
      }
    }

    IEnumerator<Content> IEnumerable<Content>.GetEnumerator()
    {
      return list.Values.GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
      return list.Values.GetEnumerator();
    }

    public Content Oldest {
      get {
        lock (list) {
          if (list.Count>0) {
            return list.Values[0];
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
            return list.Values[list.Count-1];
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
        if (list.Count<1) {
          return 0;
        }
        if (list.Keys[0]>position) {
          return 0;
        }
        if (list.Keys[list.Count-1]<=position) {
          return list.Count;
        }
        var min = 0;
        var max = list.Count-1;
        var idx = (max+min)/2;
        while (true) {
          if (list.Keys[idx]==position) {
            return idx+1;
          }
          else if (list.Keys[idx]>position) {
            if (min>=max) {
              return idx;
            }
            max = idx-1;
            idx = (max+min)/2;
          }
          else if (list.Keys[idx]<position) {
            if (min>=max) {
              return idx+1;
            }
            min = idx+1;
            idx = (max+min)/2;
          }
        }
      }
    }

    public IList<Content> GetNewerContents(long position)
    {
      lock (list) {
        int idx = GetNewerPacketIndex(position);
        var res = new List<Content>(Math.Max(list.Count-idx, 0));
        for (var i=idx; i<list.Count; i++) {
          res.Add(list.Values[i]);
        }
        return res;
      }
    }

    public Content NextOf(long position)
    {
      lock (list) {
        int idx = GetNewerPacketIndex(position);
        if (idx>=list.Count) return null;
        else return list.Values[idx];
      }
    }

    public Content NextOf(Content item)
    {
      return NextOf(item.Position);
    }
  }

}
