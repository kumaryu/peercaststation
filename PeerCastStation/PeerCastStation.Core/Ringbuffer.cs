using System;
using System.Collections.Generic;
using System.Linq;

namespace PeerCastStation.Core
{
  public class Ringbuffer<T>
    : IList<T>
  {
    private int count = 0;
    private int top   = 0;
    private T[] buffer;

    public Ringbuffer(int capacity)
    {
      buffer = new T[capacity];
    }

    public int Capacity { get { return buffer.Length; } }

    public void Add(T item)
    {
      var last = (top+count) % Capacity;
      buffer[last] = item;
      count += 1;
      if (count>Capacity) {
        count = Capacity;
        top = (top+1) % Capacity;
      }
    }

    public void Clear()
    {
      top = 0;
      count = 0;
    }

    public bool Contains(T item)
    {
      return buffer.Concat(buffer).Skip(top).Take(count).Contains(item);
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
      if (array==null) throw new ArgumentNullException("array");
      if (arrayIndex<0) throw new ArgumentOutOfRangeException("arrayIndex");
      if (array.Length-arrayIndex>count) throw new ArgumentException();
      var firsthalf = Math.Min(Capacity-top, count);
      Array.Copy(buffer, top, array, arrayIndex, firsthalf);
      if (firsthalf<count) {
        Array.Copy(buffer, 0, array, arrayIndex+firsthalf, count-firsthalf);
      }
    }

    public T[] ToArray()
    {
      var arr = new T[count];
      CopyTo(arr, 0);
      return arr;
    }

    public int Count { get { return count; } }
    public bool IsReadOnly { get { return false; } }

    public bool Remove(T item)
    {
      var idx = IndexOf(item);
      if (idx>=0) {
        RemoveAt(idx);
        return true;
      }
      else {
        return false;
      }
    }

    private class Enumerator
      : IEnumerator<T>
    {
      private Ringbuffer<T> owner;
      private int pos = -1;
      internal Enumerator(Ringbuffer<T> owner)
      {
        this.owner = owner;
      }

      public void Dispose()
      {
        this.owner = null;
      }

      public T Current
      {
        get {
          return owner.buffer[(owner.top+pos) % owner.Capacity];
        }
      }

      object System.Collections.IEnumerator.Current { get { return this.Current; } }

      public bool MoveNext()
      {
        pos = Math.Min(pos+1, owner.Count);
        return pos<owner.Count;
      }

      public void Reset()
      {
        this.pos = -1;
      }
    }

    public IEnumerator<T> GetEnumerator()
    {
      return new Enumerator(this);
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
      return new Enumerator(this);
    }

    public int IndexOf(T item)
    {
      var idx = buffer.Concat(buffer)
        .Skip(top)
        .Take(count)
        .TakeWhile(entry => !EqualityComparer<T>.Default.Equals(entry, item))
        .Count();
      return idx>=count ? -1 : idx;
    }

    public void Insert(int index, T item)
    {
      var pos = (top+index) % Capacity;
      var first = buffer[top];
      if (index==count) {
        buffer[pos] = item;
      }
      else if (index==0) {
        first = item;
        top = (top-1+Capacity) % Capacity;
      }
      else if (pos<top) {
        Array.Copy(buffer, pos, buffer, pos+1, top-pos);
        buffer[pos] = item;
      }
      else {
        Array.Copy(buffer, top+1, buffer, top, index);
        buffer[pos-1] = item;
        top = (top-1+Capacity) % Capacity;
      }
      if (count<Capacity) {
        count += 1;
        buffer[top] = first;
      }
      else {
        top = (top+1) % Capacity;
      }
    }

    public void RemoveAt(int index)
    {
      if (index==0) {
        top = (top+1) % Capacity;
        count -= 1;
      }
      if (index==count-1) {
        count -= 1;
      }
      else if (index+top>=Capacity) {
        var pos = (index+top) % Capacity;
        Array.Copy(buffer, pos+1, buffer, pos, count-index);
        count -= 1;
      }
      else {
        var pos = (index+top) % Capacity;
        Array.Copy(buffer, top, buffer, top+1, index);
        top = (top+1) % Capacity;
        count -= 1;
      }
    }

    public T this[int index]
    {
      get { return buffer[(index+top) % Capacity]; }
      set { buffer[(index+top) % Capacity] = value; }
    }
  }
}
