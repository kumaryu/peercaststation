using System;

namespace PeerCastStation.Core
{
  public class RateCounter
  {
    private long count;
    private int begin;
    private float rate;
    public int Duration { get; private set; }
    public float Rate { get { Check(); return rate; } }

    public RateCounter(int duration)
    {
      this.Duration = duration;
      this.begin = Environment.TickCount-duration;
    }

    public void Add(int value)
    {
      count += value;
      Check();
    }

    public void Reset()
    {
      rate = 0;
      count = 0;
      begin = Environment.TickCount;
    }

    private void Check()
    {
      var t = Environment.TickCount;
      var d = Math.Abs(t-begin);
      if (d>Duration) {
        rate = count*1000 / (float)d;
        count = 0;
        begin = Environment.TickCount;
      }
    }
  }
}
