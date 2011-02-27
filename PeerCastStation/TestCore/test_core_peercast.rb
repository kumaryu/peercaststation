$: << File.join(File.dirname(__FILE__), '..', 'PeerCastStation.Core', 'bin', 'Debug')
require 'PeerCastStation.Core.dll'
require 'socket'
require 'test/unit'
using_clr_extensions PeerCastStation::Core

class TC_CorePeerCast < Test::Unit::TestCase
  def setup
  end
  
  def teardown
    @peercast.close if @peercast and not @peercast.is_closed
  end
  
  def test_construct
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7147)
    @peercast = PeerCastStation::Core::PeerCast.new(endpoint)
    assert_not_nil(@peercast.access_controller)
    assert_equal(0, @peercast.yellow_pages.count)
    assert_equal(0, @peercast.yellow_page_factories.count)
    assert_equal(0, @peercast.source_stream_factories.count)
    assert_equal(0, @peercast.output_stream_factories.count)
    assert_equal(0, @peercast.channels.count)
    assert(!@peercast.is_closed)
    
    sleep(1)
    assert_equal(1, @peercast.host.addresses.count)
    assert_equal(endpoint, @peercast.host.addresses[0])
    assert_not_equal(System::Guid.empty, @peercast.host.SessionID)
    assert_equal(System::Guid.empty, @peercast.host.BroadcastID)
    assert(!@peercast.host.is_firewalled)
    assert_equal(0, @peercast.host.extensions.count)
    assert_equal(0, @peercast.host.extra.count)
    
    @peercast.close
    assert(@peercast.is_closed)
  end
  
  def test_relay_from_tracker
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7147)
    @peercast = PeerCastStation::Core::PeerCast.new(endpoint)
    @peercast.source_stream_factories['mock'] = MockSourceStreamFactory.new
    
    tracker = System::Uri.new('pcp://127.0.0.1:7147')
    channel_id = System::Guid.empty
    assert_raise(System::ArgumentException) {
      @peercast.relay_channel(channel_id, tracker);
    }
    
    tracker = System::Uri.new('mock://127.0.0.1:7147')
    channel = @peercast.relay_channel(channel_id, tracker);
    assert_not_nil(channel)
    assert_kind_of(MockSourceStream, channel.source_stream)
    source = channel.source_stream
    assert_equal(tracker, source.tracker)
    assert_equal(channel, source.channel)
    assert_equal(2, source.log.size)
    assert_equal(:start,  source.log[0][0])
    assert_equal(:close,  source.log[1][0])
    
    assert_equal(1, @peercast.channels.count)
    assert_equal(channel, @peercast.channels[0])
  end
  
  def test_relay_from_yp
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7147)
    @peercast = PeerCastStation::Core::PeerCast.new(endpoint)
    @peercast.yellow_page_factories['mock_yp'] = MockYellowPageFactory.new
    @peercast.source_stream_factories['mock'] = MockSourceStreamFactory.new
    @peercast.yellow_pages.add(@peercast.yellow_page_factories['mock_yp'].create('mock_yp', System::Uri.new('pcp:example.com:7147')))
    
    channel_id = System::Guid.empty
    channel = @peercast.relay_channel(channel_id)
    assert_not_nil(channel)
    assert_kind_of(MockSourceStream, channel.source_stream)
    source = channel.source_stream
    sleep(0.1) while channel.status!=PeerCastStation::Core::ChannelStatus.closed
    assert_equal('127.0.0.1',   source.tracker.host.to_s)
    assert_equal(endpoint.port, source.tracker.port)
    assert_equal(channel,       source.channel)
    assert_equal(2, source.log.size)
    assert_equal(:start,   source.log[0][0])
    assert_equal(:close,   source.log[1][0])
    
    assert_equal(1, @peercast.channels.count)
    assert_equal(channel, @peercast.channels[0])
  end

  def test_request_channel
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7147)
    @peercast = PeerCastStation::Core::PeerCast.new(endpoint)
    @peercast.yellow_page_factories['mock_yp'] = MockYellowPageFactory.new
    @peercast.source_stream_factories['mock'] = MockSourceStreamFactory.new
    @peercast.yellow_pages.add(@peercast.yellow_page_factories['mock_yp'].create('mock_yp', System::Uri.new('pcp:example.com:7147')))
    
    channel_id = System::Guid.new_guid
    assert_nil(@peercast.request_channel(channel_id, nil, false))

    channel = PeerCastStation::Core::Channel.new(@peercast, channel_id, System::Uri.new('mock://localhost'))
    @peercast.channels.add(channel)
    assert_equal(channel, @peercast.request_channel(channel_id, nil, false))

    channel_id = System::Guid.new_guid
    channel = @peercast.request_channel(channel_id, System::Uri.new('mock://localhost'), true)
    assert_not_nil(channel)
    assert_kind_of(MockSourceStream, channel.source_stream)

    channel_id = System::Guid.new_guid
    channel = @peercast.request_channel(channel_id, nil, true)
    assert_not_nil(channel)
    assert_kind_of(MockSourceStream, channel.source_stream)
  end
  
  def test_close_channel
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7147)
    tracker = System::Uri.new('mock://127.0.0.1:7147')
    @peercast = PeerCastStation::Core::PeerCast.new(endpoint)
    @peercast.source_stream_factories['mock'] = MockSourceStreamFactory.new
    channel_id = System::Guid.empty
    channel = @peercast.relay_channel(channel_id, tracker);
    assert_equal(1, @peercast.channels.count)
    @peercast.close_channel(channel)
    assert_equal(0, @peercast.channels.count)
  end
  
  def test_output_connection
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7147)
    @peercast = PeerCastStation::Core::PeerCast.new(endpoint)
    sleep(1)
    assert_equal(1, @peercast.host.addresses.count)
    assert_equal(System::Net::IPAddress.any, @peercast.host.addresses[0].address)
    assert_equal(7147, @peercast.host.addresses[0].port)
    
    output_stream_factory = MockOutputStreamFactory.new
    @peercast.output_stream_factories.add(output_stream_factory)
    
    sock = TCPSocket.new('localhost', 7147)
    sock.write('mock 9778E62BDC59DF56F9216D0387F80BF2')
    sock.close
    
    sleep(1)
    assert_equal(2, output_stream_factory.log.size)
    assert_equal(:parse_channel_id, output_stream_factory.log[0][0])
    assert_equal(:create,           output_stream_factory.log[1][0])
  end
end

