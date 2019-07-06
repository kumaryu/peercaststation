using System;
using System.Diagnostics.Tracing;

namespace PeerCastStation.Core
{
  public class EventLogger
    : EventSource
  {
    public class Keywords
    {
        public const EventKeywords OutputStream = (EventKeywords)1;
        public const EventKeywords SourceStream = (EventKeywords)2;
    }

    public EventLogger()
      : base("EventLogger")
    {
    }

    [Event(11, Keywords=Keywords.OutputStream)]
    public void SendHeaderPacket()
    {
      WriteEvent(11);
    }

    [Event(12, Keywords=Keywords.OutputStream)]
    public void SendContentPacket(long position)
    {
      WriteEvent(12, position);
    }

    [Event(13, Keywords=Keywords.OutputStream)]
    public void SendIllegalPacket(long position)
    {
      WriteEvent(13, position);
    }

    [Event(21, Keywords=Keywords.SourceStream)]
    public void RecvHeaderPacket()
    {
      WriteEvent(21);
    }

    [Event(22, Keywords=Keywords.SourceStream)]
    public void RecvContentPacket(long position)
    {
      WriteEvent(22, position);
    }
  }
}
