$: << File.join(File.dirname(__FILE__), '..', 'PeerCastStation.Core', 'bin', 'Debug')
require 'PeerCastStation.Core.dll'
require 'test/unit'
using_clr_extensions PeerCastStation::Core

class TC_CoreChannel < Test::Unit::TestCase
  def id4(s)
    PeerCastStation::Core::ID4.new(s.to_clr_string)
  end

  def setup
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7147)
    @peercast = PeerCastStation::Core::PeerCast.new(endpoint)
  end
  
  def teardown
    @peercast.close if @peercast and not @peercast.is_closed
  end

  def test_construct
    channel = PeerCastStation::Core::Channel.new(@peercast, System::Guid.empty, System::Uri.new('mock://localhost'))
    assert_nil(channel.source_stream)
    assert_equal(@peercast, channel.PeerCast)
    assert_equal('mock://localhost/', channel.source_uri.to_s)
    assert_equal(System::Guid.empty, channel.channel_info.ChannelID)
    assert_equal(PeerCastStation::Core::ChannelStatus.Idle, channel.status)
    assert_equal(0, channel.output_streams.count)
    assert_equal(0, channel.nodes.count)
    assert_nil(channel.content_header)
    assert_equal(0, channel.contents.count)
  end
  
  def test_changed
    property_log = []
    content_log = []
    channel = PeerCastStation::Core::Channel.new(@peercast, System::Guid.empty, System::Uri.new('mock://localhost'))
    channel.property_changed {|sender, e| property_log << e.property_name }
    channel.content_changed {|sender, e| content_log << 'content' }
    channel.status = PeerCastStation::Core::ChannelStatus.Connecting
    channel.source_stream = MockSourceStream.new(channel, channel.source_uri)
    channel.channel_info.name = 'bar'
    channel.output_streams.add(MockOutputStream.new)
    channel.nodes.add(PeerCastStation::Core::Node.new(PeerCastStation::Core::Host.new))
    channel.content_header = PeerCastStation::Core::Content.new(0, 'header')
    channel.contents.add(PeerCastStation::Core::Content.new(1, 'body'))
    assert_equal(7, property_log.size)
    assert_equal('Status',        property_log[0])
    assert_equal('SourceStream',  property_log[1])
    assert_equal('ChannelInfo',   property_log[2])
    assert_equal('OutputStreams', property_log[3])
    assert_equal('Nodes',         property_log[4])
    assert_equal('ContentHeader', property_log[5])
    assert_equal('Contents',      property_log[6])
    assert_equal(2, content_log.size)
    assert_equal('content', content_log[0])
    assert_equal('content', content_log[1])
  end
  
  def test_close
    log = []
    channel = PeerCastStation::Core::Channel.new(@peercast, System::Guid.empty, System::Uri.new('mock://localhost'))
    channel.closed { log << 'Closed' }
    channel.output_streams.add(MockOutputStream.new)
    channel.start(MockSourceStream.new(channel, channel.source_uri))
    sleep(1)
    channel.close
    assert_equal(PeerCastStation::Core::ChannelStatus.Closed, channel.status)
    assert_equal(:start, channel.source_stream.log[0][0])
    assert_equal(:close, channel.source_stream.log[1][0])
    assert_equal(:close, channel.output_streams[0].log[0][0])
    assert_equal('Closed', log[0])
  end

  def test_broadcast
    output = MockOutputStream.new
    channel = PeerCastStation::Core::Channel.new(@peercast, System::Guid.empty, System::Uri.new('mock://localhost'))
    channel.output_streams.add(output)
    source = MockSourceStream.new(channel, channel.source_uri)
    channel.start(source)
    sleep(1)
    from = PeerCastStation::Core::Host.new
    packet_trackers = PeerCastStation::Core::Atom.new(id4('test'), 'trackers'.to_clr_string)
    packet_relays   = PeerCastStation::Core::Atom.new(id4('test'), 'relays'.to_clr_string)
    channel.broadcast(from, packet_trackers, PeerCastStation::Core::BroadcastGroup.trackers)
    channel.broadcast(from, packet_relays,   PeerCastStation::Core::BroadcastGroup.relays)
    sleep(1)
    channel.close
    source_log = source.log.select {|log| log[0]==:post }
    output_log = output.log.select {|log| log[0]==:post }
    assert_equal(1, source_log.size)
    assert_equal(from,            source_log[0][1])
    assert_equal(packet_trackers, source_log[0][2])
    assert_equal(2, output_log.size)
    assert_equal(from,            output_log[0][1])
    assert_equal(packet_trackers, output_log[0][2])
    assert_equal(from,            output_log[1][1])
    assert_equal(packet_relays,   output_log[1][2])
  end

  class TestAccessController < PeerCastStation::Core::AccessController
    def self.new(peercast, relayable, playable)
      instance = super(peercast)
      instance.instance_eval do 
        @relayable = relayable
        @playable = playable
      end
      instance
    end

    def is_channel_playable(channel, output_stream=nil)
      @playable
    end

    def is_channel_relayable(channel, output_stream=nil)
      @relayable
    end
  end

  def test_is_relay_full
    channel = PeerCastStation::Core::Channel.new(
      @peercast, System::Guid.empty, System::Uri.new('mock://localhost'))
    @peercast.access_controller = TestAccessController.new(@peercast, true, true)
    assert(!channel.is_relay_full)
    @peercast.access_controller = TestAccessController.new(@peercast, false, true)
    assert(channel.is_relay_full)
  end

  def test_is_direct_full
    channel = PeerCastStation::Core::Channel.new(
      @peercast, System::Guid.empty, System::Uri.new('mock://localhost'))
    @peercast.access_controller = TestAccessController.new(@peercast, true, true)
    assert(!channel.is_direct_full)
    @peercast.access_controller = TestAccessController.new(@peercast, true, false)
    assert(channel.is_direct_full)
  end
end

