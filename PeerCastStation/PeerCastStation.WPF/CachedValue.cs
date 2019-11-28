using System;
using System.Diagnostics;

namespace PeerCastStation.WPF
{
  class CachedValue<T>
  {
    public TimeSpan Lifetime {
      get { return TimeSpan.FromSeconds((double)lifetime / Stopwatch.Frequency); }
    }

    public T Value {
      get {
        var ts = Stopwatch.GetTimestamp();
        if (ts - timeStamp > lifetime) {
          value = getValueFunc();
          timeStamp = ts;
        }
        return value;
      }
    }

    private long timeStamp;
    private long lifetime;
    private T value;
    private Func<T> getValueFunc;

    public CachedValue(Func<T> valueFunc, TimeSpan lifetime)
    {
      getValueFunc = valueFunc;
      this.lifetime = (long)(lifetime.TotalSeconds * Stopwatch.Frequency);
    }

  }
}

