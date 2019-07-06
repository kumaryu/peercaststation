using System;

namespace PeerCastStation.Core
{
  public struct Timestamp
  {
    public static readonly long Frequency = System.Diagnostics.Stopwatch.Frequency;
    public readonly long Tick;
    public Timestamp(long tick)
    {
      this.Tick = tick;
    }

    public static Timestamp Now {
      get { return new Timestamp(System.Diagnostics.Stopwatch.GetTimestamp()); }
    }

    public static TimeSpan operator - (Timestamp a, Timestamp b)
    {
      return TimeSpan.FromTicks((a.Tick-b.Tick) * TimeSpan.TicksPerSecond / Frequency);
    }
  }

}

