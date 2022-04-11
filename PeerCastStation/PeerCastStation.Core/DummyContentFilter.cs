
using System;

namespace PeerCastStation.Core
{
  public class DummyContentFilter
    : IContentFilter
  {
    public string Name { get { return "Dummy"; } }
    public IContentSink Activate(IContentSink sink)
    {
      return new DummyContentFilterSink(sink);
    }

    public class DummyContentFilterSink
      : IContentSink
    {
      private IContentSink targetSink;
      public DummyContentFilterSink(IContentSink sink)
      {
        targetSink = sink;
      }

      public void OnChannelInfo(ChannelInfo channel_info)
      {
        targetSink.OnChannelInfo(channel_info);
      }

      public void OnChannelTrack(ChannelTrack channel_track)
      {
        targetSink.OnChannelTrack(channel_track);
      }

      public void OnContent(Content content)
      {
        targetSink.OnContent(content);
      }

      public void OnContentHeader(Content content_header)
      {
        targetSink.OnContentHeader(content_header);
      }

      public void OnStop(StopReason reason)
      {
        targetSink.OnStop(reason);
      }
    }

  }

  [Plugin]
  public class DummyContentFilterPlugin
    : PluginBase
  {
    public override string Name {
      get { return "DummyContentFilter"; }
    }

    private DummyContentFilter filter = new DummyContentFilter();
    protected override void OnAttach(PeerCastApplication application)
    {
      application.PeerCast.ContentFilters.Add(filter);
    }

    protected override void OnDetach(PeerCastApplication application)
    {
      application.PeerCast.ContentFilters.Remove(filter);
    }
  }

}
