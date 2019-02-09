using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace PeerCastStation.WPF
{
  internal class NotifyItemChangedEventArgs<TKey>
    : EventArgs
  {
    public TKey Key { get; private set; }
    public NotifyItemChangedEventArgs(TKey key)
    {
      Key = key;
    }
  }

  internal delegate void NotifyItemChangedEventHandler<TKey>(object sender, NotifyItemChangedEventArgs<TKey> args);

  internal interface INotifyItemChanged<TKey>
  {
    event NotifyItemChangedEventHandler<TKey> ItemChanged;
  }

  internal class ObservableDictionary<TKey, TValue>
    : IDictionary<TKey, TValue>
    , IReadOnlyDictionary<TKey, TValue>
    , INotifyItemChanged<TKey>
  {
    public IDictionary<TKey, TValue> Base { get; private set; }
    public TValue this[TKey key] {
      get { return Base[key]; }
      set {
        Base[key] = value;
        ItemChanged?.Invoke(this, new NotifyItemChangedEventArgs<TKey>(key));
      }
    }

    public ICollection<TKey> Keys {
      get { return Base.Keys; }
    }

    public ICollection<TValue> Values {
      get { return Base.Values; }
    }

    public int Count {
      get { return Base.Count; }
    }

    public bool IsReadOnly {
      get { return Base.IsReadOnly; }
    }

    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys {
      get { return Base.Keys; }
    }

    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values {
      get { return Base.Values; }
    }

    public event NotifyItemChangedEventHandler<TKey> ItemChanged;

    public ObservableDictionary(IDictionary<TKey, TValue> dic)
    {
      Base = dic;
    }

    public ObservableDictionary()
    {
      Base = new Dictionary<TKey, TValue>();
    }

    public void Add(TKey key, TValue value)
    {
      Base.Add(key, value);
      ItemChanged?.Invoke(this, new NotifyItemChangedEventArgs<TKey>(key));
    }

    public void Add(KeyValuePair<TKey, TValue> item)
    {
      Base.Add(item);
      ItemChanged?.Invoke(this, new NotifyItemChangedEventArgs<TKey>(item.Key));
    }

    public void Clear()
    {
      var keys = Base.Keys.ToArray();
      Base.Clear();
      foreach (var key in keys) {
        ItemChanged?.Invoke(this, new NotifyItemChangedEventArgs<TKey>(key));
      }
    }

    public bool Contains(KeyValuePair<TKey, TValue> item)
    {
      return Base.Contains(item);
    }

    public bool ContainsKey(TKey key)
    {
      return Base.ContainsKey(key);
    }

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
      Base.CopyTo(array, arrayIndex);
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
      return Base.GetEnumerator();
    }

    public bool Remove(TKey key)
    {
      if (Base.Remove(key)) {
        ItemChanged?.Invoke(this, new NotifyItemChangedEventArgs<TKey>(key));
        return true;
      }
      else {
        return false;
      }
    }

    public bool Remove(KeyValuePair<TKey, TValue> item)
    {
      if (Base.Remove(item)) {
        ItemChanged?.Invoke(this, new NotifyItemChangedEventArgs<TKey>(item.Key));
        return true;
      }
      else {
        return false;
      }
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
      return Base.TryGetValue(key, out value);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      return Base.GetEnumerator();
    }
  }

}
