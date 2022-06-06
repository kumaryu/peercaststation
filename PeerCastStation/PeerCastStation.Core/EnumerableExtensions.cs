using System;
using System.Collections.Generic;
using System.Linq;

namespace PeerCastStation.Core
{
  public static class EnumerableExtensions
  {
    public static IEnumerable<T> NotNull<T>(this IEnumerable<T?> self)
    {
      return self.Where(x => x != null).Select(x => x!);
    }
  }
}
