using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace PeerCastStation.Core
{
  public class SynchronizedList<T>
    : IList<T>, IReadOnlyList<T>
  {
    private ReaderWriterLockSlim locker = new ReaderWriterLockSlim();
    private List<T> items;

    public SynchronizedList()
    {
      items = new List<T>();
    }

    public T this[int index] {
      get {
        locker.EnterReadLock();
        try {
          return items[index];
        }
        finally {
          locker.ExitReadLock();
        }
      }

      set {
        locker.EnterWriteLock();
        try {
          items[index] = value;
        }
        finally {
          locker.ExitWriteLock();
        }
      }
    }

    public int Count {
      get {
        locker.EnterReadLock();
        try {
          return items.Count;
        }
        finally {
          locker.ExitReadLock();
        }
      }
    }

    public bool IsReadOnly {
      get { return false; }
    }

    public void Add(T item)
    {
      locker.EnterWriteLock();
      try {
        items.Add(item);
      }
      finally {
        locker.ExitWriteLock();
      }
    }

    public void Clear()
    {
      locker.EnterWriteLock();
      try {
        items.Clear();
      }
      finally {
        locker.ExitWriteLock();
      }
    }

    public bool Contains(T item)
    {
      locker.EnterReadLock();
      try {
        return items.Contains(item);
      }
      finally {
        locker.ExitReadLock();
      }
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
      locker.EnterReadLock();
      try {
        items.CopyTo(array, arrayIndex);
      }
      finally {
        locker.ExitReadLock();
      }
    }

    private sealed class SynchronizedEnumerator
      : IEnumerator<T>
    {
      private SynchronizedList<T> owner;
      private IEnumerator<T> enumerator;
      private bool disposed = false;
      public SynchronizedEnumerator(SynchronizedList<T> list)
      {
        owner = list;
        owner.locker.EnterReadLock();
        enumerator = owner.items.GetEnumerator();
      }

      public T Current {
        get { return enumerator.Current; }
      }

      object? IEnumerator.Current {
        get { return enumerator.Current; }
      }

      public void Dispose()
      {
        if (disposed) return;
        owner.locker.ExitReadLock();
        disposed = true;
      }

      public bool MoveNext()
      {
        return enumerator.MoveNext();
      }

      public void Reset()
      {
        enumerator.Reset();
      }
    }

    public IEnumerator<T> GetEnumerator()
    {
      return new SynchronizedEnumerator(this);
    }

    public int IndexOf(T item)
    {
      locker.EnterReadLock();
      try {
        return items.IndexOf(item);
      }
      finally {
        locker.ExitReadLock();
      }
    }

    public void Insert(int index, T item)
    {
      locker.EnterWriteLock();
      try {
        items.Insert(index, item);
      }
      finally {
        locker.ExitWriteLock();
      }
    }

    public bool Remove(T item)
    {
      locker.EnterWriteLock();
      try {
        return items.Remove(item);
      }
      finally {
        locker.ExitWriteLock();
      }
    }

    public void RemoveAt(int index)
    {
      locker.EnterWriteLock();
      try {
        items.RemoveAt(index);
      }
      finally {
        locker.ExitWriteLock();
      }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      return new SynchronizedEnumerator(this);
    }
  }
}
