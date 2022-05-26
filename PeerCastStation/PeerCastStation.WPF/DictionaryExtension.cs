using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace PeerCastStation.WPF
{
  internal static class DictionaryExtension
  {
    internal class DictionaryWithDefaultValue<TKey, TValue>
      : IDictionary<TKey, TValue>
      , IReadOnlyDictionary<TKey, TValue>
    {
      public IDictionary<TKey,TValue> BaseDictionary { get; private set; }
      public Func<TKey,TValue> DefaultValue { get; private set; }
      public DictionaryWithDefaultValue(IDictionary<TKey,TValue> dic, Func<TKey,TValue> defaultValueFunc)
      {
        BaseDictionary = dic;
        DefaultValue = defaultValueFunc;
      }

      public TValue this[TKey key] {
        get {
          if (BaseDictionary.TryGetValue(key, out var value)) {
            return value;
          }
          else {
            return DefaultValue(key);
          }
        }
        set { BaseDictionary[key] = value; }
      }

      public ICollection<TKey> Keys {
        get { return BaseDictionary.Keys; }
      }

      public ICollection<TValue> Values {
        get { return BaseDictionary.Values; }
      }

      public int Count {
        get { return BaseDictionary.Count; }
      }

      public bool IsReadOnly {
        get { return BaseDictionary.IsReadOnly; }
      }

      IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys {
        get { return ((IReadOnlyDictionary<TKey,TValue>)BaseDictionary).Keys; }
      }

      IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values {
        get { return ((IReadOnlyDictionary<TKey,TValue>)BaseDictionary).Values; }
      }

      public void Add(TKey key, TValue value)
      {
        BaseDictionary.Add(key, value);
      }

      public void Add(KeyValuePair<TKey, TValue> item)
      {
        BaseDictionary.Add(item);
      }

      public void Clear()
      {
        BaseDictionary.Clear();
      }

      public bool Contains(KeyValuePair<TKey, TValue> item)
      {
        return BaseDictionary.Contains(item);
      }

      public bool ContainsKey(TKey key)
      {
        return BaseDictionary.ContainsKey(key);
      }

      public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
      {
        BaseDictionary.CopyTo(array, arrayIndex);
      }

      public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
      {
        return BaseDictionary.GetEnumerator();
      }

      public bool Remove(TKey key)
      {
        return BaseDictionary.Remove(key);
      }

      public bool Remove(KeyValuePair<TKey, TValue> item)
      {
        return BaseDictionary.Remove(item);
      }

      public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
      {
        return BaseDictionary.TryGetValue(key, out value);
      }

      IEnumerator IEnumerable.GetEnumerator()
      {
        return ((IEnumerable)BaseDictionary).GetEnumerator();
      }
    }

    public static IDictionary<TKey,TValue?> WithDefaultValue<TKey,TValue>(this IDictionary<TKey,TValue?> dic)
    {
      return new DictionaryWithDefaultValue<TKey,TValue?>(dic, key => default(TValue?));
    }

    public static IDictionary<TKey,TValue> WithDefaultValue<TKey,TValue>(this IDictionary<TKey,TValue> dic, Func<TKey,TValue> defaultValueFunc)
    {
      return new DictionaryWithDefaultValue<TKey,TValue>(dic, defaultValueFunc);
    }

  }

}

