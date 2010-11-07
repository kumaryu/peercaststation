$: << File.join(File.dirname(__FILE__), '..', 'PeerCastStation.Core', 'bin', 'Debug')
require 'PeerCastStation.Core.dll'
require 'test/unit'

class MockYellowPageFactory
  include PeerCastStation::Core::IYellowPageFactory
  def create(name, uri)
    MockYellowPage.new(name, uri)
  end
end

class MockYellowPage
  include PeerCastStation::Core::IYellowPage
  def initialize(name, uri)
    @name = name
    @uri = uri
    @log = []
  end
  attr_reader :name, :uri, :log
  
  def find_tracker(channel_id)
    @log << [:find_tracker, channel_id]
    addr = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('0.0.0.0'), 7144)
    host = PeerCastStation::Core::Host.new()
    host.addresses.add(addr)
    PeerCastStation::Core::TrackerDescription.new(host, 'mock')
  end
  
  def list_channels
    raise NotImplementError, 'Not implemented yet'
  end
  
  def announce(channel)
    raise NotImplementError, 'Not implemented yet'
  end
end

class MockSourceStreamFactory
  include PeerCastStation::Core::ISourceStreamFactory
  def initialize
    @log = []
  end
  attr_reader :log
  
  def create
    @log << [:create]
    MockSourceStream.new
  end
end

class MockSourceStream
  include PeerCastStation::Core::ISourceStream
  
  def initialize
    @log = []
  end
  attr_reader :log
  
  def start(tracker, channel)
    @log << [:start, tracker, channel]
  end
  
  def close
    @log << [:close]
  end
end

class MockOutputStream
  include PeerCastStation::Core::IOutputStream
  
  def initialize
    @log = []
  end
  attr_reader :log
  
  def start(stream, channel)
    @log << [:start, stream, channel]
  end
  
  def close
    @log << [:close]
  end
end
  
class TestCoreAtom < Test::Unit::TestCase
  def test_construct
    obj = PeerCastStation::Core::Atom.new('peer', 'cast')
    assert_equal('peer', obj.name)
    assert_equal('cast', obj.value)
  end
  
  def test_name_length
    assert_raise(System::ArgumentException) {
      obj = PeerCastStation::Core::Atom.new('nagai_name', 'cast')
    }
  end
end

class TestCoreContent < Test::Unit::TestCase
  def test_construct
    obj = PeerCastStation::Core::Content.new(10, 'content')
    assert_equal(10, obj.position)
    assert_equal('content'.unpack('C*'), obj.data)
  end
end

class TestCoreHost < Test::Unit::TestCase
  def test_construct
    obj = PeerCastStation::Core::Host.new
    assert(obj.addresses)
    assert_equal(0, obj.addresses.count)
    assert_equal(System::Guid.empty, obj.SessionID)
    assert_equal(System::Guid.empty, obj.BroadcastID)
    assert(!obj.is_firewalled)
    assert(obj.extensions)
    assert_equal(0, obj.extensions.count)
    assert(obj.extra)
    assert_equal(0, obj.extra.count)
  end
end

class TestCoreChannelInfo < Test::Unit::TestCase
  def test_construct
    obj = PeerCastStation::Core::ChannelInfo.new(System::Guid.empty)
    assert_equal(System::Guid.empty, obj.ChannelID)
    assert_nil(obj.tracker)
    assert_equal('', obj.name)
    assert_not_nil(obj.extra)
    assert_equal(0, obj.extra.count)
  end
  
  def test_changed
    log = []
    obj = PeerCastStation::Core::ChannelInfo.new(System::Guid.empty)
    obj.property_changed {|sender, e| log << e.property_name }
    obj.name = 'test'
    obj.tracker = PeerCastStation::Core::Host.new
    obj.extra.add(PeerCastStation::Core::Atom.new('test', 'foo'))
    assert_equal(3, log.size)
    assert_equal('Name',    log[0])
    assert_equal('Tracker', log[1])
    assert_equal('Extra',   log[2])
  end
end

class TestCoreNode < Test::Unit::TestCase
  def test_construct
    host = PeerCastStation::Core::Host.new
    obj = PeerCastStation::Core::Node.new(host)
    assert_equal(host, obj.host)
    assert_equal(0, obj.relay_count)
    assert_equal(0, obj.direct_count)
    assert(!obj.is_relay_full)
    assert(!obj.is_direct_full)
    assert_not_nil(obj.extra)
    assert_equal(0, obj.extra.count)
  end
  
  def test_changed
    log = []
    obj = PeerCastStation::Core::Node.new(PeerCastStation::Core::Host.new)
    obj.property_changed {|sender, e| log << e.property_name }
    obj.relay_count = 1
    obj.direct_count = 1
    obj.is_relay_full = true
    obj.is_direct_full = true
    obj.host = PeerCastStation::Core::Host.new
    obj.extra.add(PeerCastStation::Core::Atom.new('test', 'foo'))
    assert_equal(6, log.size)
    assert_equal('RelayCount',   log[0])
    assert_equal('DirectCount',  log[1])
    assert_equal('IsRelayFull',  log[2])
    assert_equal('IsDirectFull', log[3])
    assert_equal('Host',         log[4])
    assert_equal('Extra',        log[5])
  end
end

class TestCore < Test::Unit::TestCase
  def test_construct
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('0.0.0.0'), 7144)
    obj = PeerCastStation::Core::Core.new(endpoint)
    #assert_not_equal(0, obj.plug_in_loaders.count)
    assert_equal(0, obj.plug_ins.count)
    assert_equal(0, obj.yellow_pages.count)
    assert_equal(0, obj.yellow_page_factories.count)
    assert_equal(0, obj.source_stream_factories.count)
    assert_equal(0, obj.output_stream_factories.count)
    assert_equal(0, obj.channels.count)
    
    assert_equal(1, obj.host.addresses.count)
    assert_equal(endpoint, obj.host.addresses[0])
    assert_not_equal(System::Guid.empty, obj.host.SessionID)
    assert_equal(System::Guid.empty, obj.host.BroadcastID)
    assert(!obj.host.is_firewalled)
    assert_equal(0, obj.host.extensions.count)
    assert_equal(0, obj.host.extra.count)
  end
  
  def test_relay_from_tracker
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('0.0.0.0'), 7144)
    tracker = PeerCastStation::Core::Host.new()
    tracker.addresses.add(endpoint)
    core = PeerCastStation::Core::Core.new(endpoint)
    core.source_stream_factories['mock'] = MockSourceStreamFactory.new
    
    channel_id = System::Guid.empty
    assert_raise(System::ArgumentException) {
      core.relay_channel(channel_id, 'pcp', tracker);
    }
    
    channel = core.relay_channel(channel_id, 'mock', tracker);
    assert_not_nil(channel)
    assert_kind_of(MockSourceStream, channel.source_stream)
    source = channel.source_stream
    assert_equal(1, source.log.size)
    assert_equal(:start,  source.log[0][0])
    assert_equal(tracker, source.log[0][1])
    assert_equal(channel,  source.log[0][2])
    
    assert_equal(1, core.channels.count)
    assert_equal(channel, core.channels[0])
  end
  
  def test_relay_from_yp
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('0.0.0.0'), 7144)
    core = PeerCastStation::Core::Core.new(endpoint)
    core.yellow_page_factories['mock_yp'] = MockYellowPageFactory.new
    core.source_stream_factories['mock'] = MockSourceStreamFactory.new
    core.yellow_pages.add(core.yellow_page_factories['mock_yp'].create('mock_yp', System::Uri.new('pcp:example.com:7144')))
    
    channel_id = System::Guid.empty
    channel = core.relay_channel(channel_id)
    assert_not_nil(channel)
    assert_kind_of(MockSourceStream, channel.source_stream)
    source = channel.source_stream
    assert_equal(1, source.log.size)
    assert_equal(:start,   source.log[0][0])
    assert_equal(endpoint, source.log[0][1].addresses[0])
    assert_equal(channel,  source.log[0][2])
    
    assert_equal(1, core.channels.count)
    assert_equal(channel, core.channels[0])
  end
  
  def test_close_channel
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('0.0.0.0'), 7144)
    tracker = PeerCastStation::Core::Host.new()
    tracker.addresses.add(endpoint)
    core = PeerCastStation::Core::Core.new(endpoint)
    core.source_stream_factories['mock'] = MockSourceStreamFactory.new
    channel_id = System::Guid.empty
    channel = core.relay_channel(channel_id, 'mock', tracker);
    assert_equal(1, core.channels.count)
    core.close_channel(channel)
    assert_equal(0, core.channels.count)
  end
end

class TestCoreChannel < Test::Unit::TestCase
  def test_construct
    channel = PeerCastStation::Core::Channel.new(System::Guid.empty, MockSourceStream.new)
    assert_kind_of(MockSourceStream, channel.source_stream)
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
    channel = PeerCastStation::Core::Channel.new(System::Guid.empty, MockSourceStream.new)
    channel.property_changed {|sender, e| property_log << e.property_name }
    channel.content_changed {|sender, e| content_log << 'content' }
    channel.status = PeerCastStation::Core::ChannelStatus.Connecting
    channel.source_stream = nil
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
    channel = PeerCastStation::Core::Channel.new(System::Guid.empty, MockSourceStream.new)
    channel.closed { log << 'Closed' }
    channel.output_streams.add(MockOutputStream.new)
    channel.close
    assert_equal(PeerCastStation::Core::ChannelStatus.Closed, channel.status)
    assert_equal([[:close]], channel.source_stream.log)
    assert_equal([[:close]], channel.output_streams[0].log)
    assert_equal(['Closed'], log)
  end
end
