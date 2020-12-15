using System;
using System.Linq;

namespace PeerCastStation.Core
{
  internal static class ArrayExtension
  {
    public static T[] Add<T>(this T[] arr, T value)
    {
      var tmp = new T[arr.Length+1];
      arr.CopyTo(tmp, 0);
      tmp[arr.Length] = value;
      return tmp;
    }

    public static T[] Remove<T>(this T[] arr, T value)
    {
      return arr.Where(ent => !ent.Equals(value)).ToArray();
    }

    public static T[] Clear<T>(this T[] arr)
    {
      return Array.Empty<T>();
    }
  }

}
