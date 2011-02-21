$: << File.join(File.dirname(__FILE__), '..', 'PeerCastStation.Core', 'bin', 'Debug')
require 'PeerCastStation.Core.dll'
require 'test/unit'
using_clr_extensions PeerCastStation::Core

class MockYellowPageFactory
  include PeerCastStation::Core::IYellowPageFactory
  
  def name
    'MockYellowPage'
  end
  
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
    addr = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7147)
    System::Uri.new("mock://#{addr}")
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
  
  def name
    'MockSourceStream'
  end
  
  def create(channel, uri)
    @log << [:create, channel, uri]
    MockSourceStream.new(channel, uri)
  end
end

class MockSourceStream
  include PeerCastStation::Core::ISourceStream
  
  def initialize(channel, tracker)
    @channel = channel
    @tracker = tracker
    @log = []
  end
  attr_reader :log, :tracker, :channel

  def post(from, packet)
    @log << [:post, from, packet]
  end
  
  def start
    @log << [:start]
  end
  
  def close
    @log << [:close]
  end
end

class MockOutputStream
  include PeerCastStation::Core::IOutputStream
  
  def initialize(type=0)
    @type = type
    @remote_endpoint = nil
    @upstream_rate = 0
    @is_local = false
    @log = []
  end
  attr_reader :log
  attr_accessor :remote_endpoint, :upstream_rate, :is_local

  def output_stream_type
    @type
  end

  def post(from, packet)
    @log << [:post, from, packet]
  end
  
  def start
    @log << [:start]
  end
  
  def close
    @log << [:close]
  end
end

class MockOutputStreamFactory
  include PeerCastStation::Core::IOutputStreamFactory
  
  def initialize
    @log = []
  end
  attr_reader :log
  
  def name
    'MockOutputStream'
  end
  
  def ParseChannelID(header)
    @log << [:parse_channel_id, header]
    header = header.to_a.pack('C*')
    if /^mock ([a-fA-F0-9]{32})/=~header then
      System::Guid.new($1.to_clr_string)
    else
      nil
    end
  end
  
  def create(stream, remote_endpoint, channel, header)
    @log << [:create, stream, remote_endpoint, channel, header]
    MockOutputStream.new
  end
end
  
class TC_CoreContent < Test::Unit::TestCase
  def test_construct
    obj = PeerCastStation::Core::Content.new(10, 'content')
    assert_equal(10, obj.position)
    assert_equal('content'.unpack('C*'), obj.data)
  end
end

class TC_CoreHost < Test::Unit::TestCase
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

class TC_CoreChannelInfo < Test::Unit::TestCase
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
    obj.tracker = System::Uri.new('mock://127.0.0.1:7147')
    obj.extra.add(PeerCastStation::Core::Atom.new(PeerCastStation::Core::ID4.new('test'.to_clr_string), 'foo'.to_clr_string))
    assert_equal(3, log.size)
    assert_equal('Name',    log[0])
    assert_equal('Tracker', log[1])
    assert_equal('Extra',   log[2])
  end
end

class TC_CoreNode < Test::Unit::TestCase
  def test_construct
    host = PeerCastStation::Core::Host.new
    obj = PeerCastStation::Core::Node.new(host)
    assert_equal(host, obj.host)
    assert_equal(0, obj.relay_count)
    assert_equal(0, obj.direct_count)
    assert(!obj.is_relay_full)
    assert(!obj.is_direct_full)
    assert(!obj.is_control_full)
    assert(!obj.is_receiving)
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
    obj.is_control_full = true
    obj.is_receiving = true
    obj.host = PeerCastStation::Core::Host.new
    obj.extra.add(PeerCastStation::Core::Atom.new(PeerCastStation::Core::ID4.new('test'.to_clr_string), 'foo'.to_clr_string))
    assert_equal(8, log.size)
    assert_equal('RelayCount',   log[0])
    assert_equal('DirectCount',  log[1])
    assert_equal('IsRelayFull',  log[2])
    assert_equal('IsDirectFull', log[3])
    assert_equal('IsControlFull',log[4])
    assert_equal('IsReceiving',  log[5])
    assert_equal('Host',         log[6])
    assert_equal('Extra',        log[7])
  end
end

class TC_OutputStreamCollection < Test::Unit::TestCase
  def test_count_relaying
    collection = PeerCastStation::Core::OutputStreamCollection.new
    assert_equal(0, collection.count)
    assert_equal(0, collection.count_relaying)
    collection.add(MockOutputStream.new(PeerCastStation::Core::OutputStreamType.play))
    collection.add(MockOutputStream.new(PeerCastStation::Core::OutputStreamType.relay))
    collection.add(MockOutputStream.new(PeerCastStation::Core::OutputStreamType.metadata))
    collection.add(MockOutputStream.new(
      PeerCastStation::Core::OutputStreamType.play |
      PeerCastStation::Core::OutputStreamType.relay))
    collection.add(MockOutputStream.new(
      PeerCastStation::Core::OutputStreamType.relay |
      PeerCastStation::Core::OutputStreamType.metadata))
    collection.add(MockOutputStream.new(
      PeerCastStation::Core::OutputStreamType.play |
      PeerCastStation::Core::OutputStreamType.metadata))
    collection.add(MockOutputStream.new(
      PeerCastStation::Core::OutputStreamType.play |
      PeerCastStation::Core::OutputStreamType.relay |
      PeerCastStation::Core::OutputStreamType.metadata))
    assert_equal(7, collection.count)
    assert_equal(4, collection.count_relaying)
  end

  def test_count_playing
    collection = PeerCastStation::Core::OutputStreamCollection.new
    assert_equal(0, collection.count)
    assert_equal(0, collection.count_playing)
    collection.add(MockOutputStream.new(PeerCastStation::Core::OutputStreamType.play))
    collection.add(MockOutputStream.new(PeerCastStation::Core::OutputStreamType.relay))
    collection.add(MockOutputStream.new(PeerCastStation::Core::OutputStreamType.metadata))
    collection.add(MockOutputStream.new(
      PeerCastStation::Core::OutputStreamType.play |
      PeerCastStation::Core::OutputStreamType.relay))
    collection.add(MockOutputStream.new(
      PeerCastStation::Core::OutputStreamType.relay |
      PeerCastStation::Core::OutputStreamType.metadata))
    collection.add(MockOutputStream.new(
      PeerCastStation::Core::OutputStreamType.play |
      PeerCastStation::Core::OutputStreamType.metadata))
    collection.add(MockOutputStream.new(
      PeerCastStation::Core::OutputStreamType.play |
      PeerCastStation::Core::OutputStreamType.relay |
      PeerCastStation::Core::OutputStreamType.metadata))
    assert_equal(7, collection.count)
    assert_equal(4, collection.count_playing)
  end
end

