$: << File.join(File.dirname(__FILE__), '..', 'PeerCastStation.Core', 'bin', 'Debug')
require 'PeerCastStation.Core.dll'
require 'socket'
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
    @log = []
  end
  attr_reader :log

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
  
  def create(stream, channel, header)
    @log << [:create, stream, channel, header]
    MockOutputStream.new
  end
end
  
class TestCoreID4 < Test::Unit::TestCase
  def test_construct
    id4 = PeerCastStation::Core::ID4.new('name'.to_clr_string)
    assert_equal('name', id4.to_string)
    assert_equal('name'.unpack('C*'), id4.get_bytes)
    
    id4 = PeerCastStation::Core::ID4.clr_ctor.overload(System::Array[System::Byte]).call('name')
    assert_equal('name', id4.to_string)
    assert_equal('name'.unpack('C*'), id4.get_bytes)
    
    id4 = PeerCastStation::Core::ID4.new('nagai_name', 0)
    assert_equal('naga', id4.to_string)
    assert_equal('naga'.unpack('C*'), id4.get_bytes)
  end
  
  def test_name_length
    assert_raise(System::ArgumentException) {
      id4 = PeerCastStation::Core::ID4.new('nagai_name'.to_clr_string)
    }
    assert_raise(System::ArgumentException) {
      id4 = PeerCastStation::Core::ID4.clr_ctor.overload(System::Array[System::Byte]).call('nagai_name')
    }
    assert_nothing_raised {
      id4 = PeerCastStation::Core::ID4.new('nagai_name', 4)
    }
  end
end

class TestCoreAtom < Test::Unit::TestCase
  def id4(s)
    PeerCastStation::Core::ID4.new(s.to_clr_string)
  end
  
  def test_construct_string
    obj = PeerCastStation::Core::Atom.new(id4('peer'), 'cast'.to_clr_string)
    assert_equal(id4('peer'), obj.name)
    assert(obj.has_value)
    assert(!obj.has_children)
    assert_equal('cast', obj.get_string)
  end
  
  def test_construct_byte
    obj = PeerCastStation::Core::Atom.clr_ctor.overload(PeerCastStation::Core::ID4, System::Byte).call(id4('peer'), 42)
    assert_equal(id4('peer'), obj.name)
    assert(obj.has_value)
    assert(!obj.has_children)
    assert_equal(42, obj.get_byte)
  end
  
  def test_construct_short
    obj = PeerCastStation::Core::Atom.clr_ctor.overload(PeerCastStation::Core::ID4, System::Int16).call(id4('peer'), 7144)
    assert_equal(id4('peer'), obj.name)
    assert(obj.has_value)
    assert(!obj.has_children)
    assert_equal(7144, obj.get_int16)
  end
  
  def test_construct_int
    obj = PeerCastStation::Core::Atom.clr_ctor.overload(PeerCastStation::Core::ID4, System::Int32).call(id4('peer'), 714400)
    assert_equal(id4('peer'), obj.name)
    assert(obj.has_value)
    assert(!obj.has_children)
    assert_equal(714400, obj.get_int32)
  end
  
  def test_construct_bytes
    obj = PeerCastStation::Core::Atom.clr_ctor.overload(PeerCastStation::Core::ID4, System::Array[System::Byte]).call(id4('peer'), 'cast')
    assert_equal(id4('peer'), obj.name)
    assert(obj.has_value)
    assert(!obj.has_children)
    assert_equal('cast'.unpack('C*'), obj.get_bytes.to_a)
  end
  
  def test_write_children
    children = PeerCastStation::Core::AtomCollection.new
    children.Add(PeerCastStation::Core::Atom.new(id4('c1'), 0))
    children.Add(PeerCastStation::Core::Atom.new(id4('c2'), 1))
    children.Add(PeerCastStation::Core::Atom.new(id4('c3'), 2))
    children.Add(PeerCastStation::Core::Atom.new(id4('c4'), 3))
    obj = PeerCastStation::Core::Atom.new(id4('peer'), children)
    assert_equal(id4('peer'), obj.name)
    assert(!obj.has_value)
    assert(obj.has_children)
    assert_equal(children, obj.children)
  end
end

class TestCoreAtomWriter < Test::Unit::TestCase
  def id4(s)
    PeerCastStation::Core::ID4.new(s.to_clr_string)
  end
  
  def test_construct
    stream = System::IO::MemoryStream.new
    writer = PeerCastStation::Core::AtomWriter.new(stream)
    assert_equal(stream, writer.base_stream)
    writer.close
  end
  
  def test_write_value
    stream = System::IO::MemoryStream.new
    writer = PeerCastStation::Core::AtomWriter.new(stream)
    atom = PeerCastStation::Core::Atom.new(id4('peer'), 7144)
    writer.write(atom)
    assert_equal(12, stream.Length)
    assert_equal(['peer', atom.get_bytes.Length].pack('Z4V').unpack('C*')+atom.get_bytes.to_a, stream.to_array.to_a)
    writer.close
  end
  
  def test_write_children
    children = PeerCastStation::Core::AtomCollection.new
    children.Add(PeerCastStation::Core::Atom.new(id4('c1'), 0))
    children.Add(PeerCastStation::Core::Atom.new(id4('c2'), 1))
    children.Add(PeerCastStation::Core::Atom.new(id4('c3'), 2))
    children.Add(PeerCastStation::Core::Atom.new(id4('c4'), 3))
    atom = PeerCastStation::Core::Atom.new(id4('peer'), children)
    stream = System::IO::MemoryStream.new
    writer = PeerCastStation::Core::AtomWriter.new(stream)
    writer.write(atom)
    assert_equal(8+12*4, stream.Length)
    assert_equal(
      ['peer', 0x80000000 | 4].pack('Z4V').unpack('C*')+
      ['c1', 4, 0].pack('Z4V2').unpack('C*')+
      ['c2', 4, 1].pack('Z4V2').unpack('C*')+
      ['c3', 4, 2].pack('Z4V2').unpack('C*')+
      ['c4', 4, 3].pack('Z4V2').unpack('C*'),
      stream.to_array.to_a
    )
    writer.close
  end
end

class TestCoreAtomCollectionExtensions < Test::Unit::TestCase
  def self.getter_method_name(name, method_name)
    unless method_name then
      'get_' + name.downcase
    else
      'Get' +method_name
    end
  end
  
  def self.setter_method_name(name, method_name)
    unless method_name then
      'set_' + name.downcase
    else
      'Set' + method_name
    end
  end
  
  def self.define_test_atom(name, method_name=nil)
    getter = getter_method_name(name, method_name)
    setter = setter_method_name(name, method_name)
    module_eval(<<EOS)
    def test_#{getter}
      collection = PeerCastStation::Core::AtomCollection.new
      assert_nil(collection.#{getter})
      value = PeerCastStation::Core::AtomCollection.new
      atom = PeerCastStation::Core::Atom.new(
        PeerCastStation::Core::Atom.PCP_#{name},
        value)
      collection.add(atom)
      assert_equal(value, collection.#{getter})
    end
    
    def test_#{setter}
      collection = PeerCastStation::Core::AtomCollection.new
      assert_equal(0, collection.count)
      value = PeerCastStation::Core::AtomCollection.new
      collection.#{setter}(value)
      assert_equal(1, collection.count)
      collection.#{setter}(value)
      assert_equal(1, collection.count)
      assert_equal(value, collection.#{getter})
    end
EOS
  end
  
  def self.define_test_string(name, method_name=nil)
    getter = getter_method_name(name, method_name)
    setter = setter_method_name(name, method_name)
    module_eval(<<EOS)
    def test_#{getter}
      collection = PeerCastStation::Core::AtomCollection.new
      assert_nil(collection.#{getter})
      atom = PeerCastStation::Core::Atom.new(
        PeerCastStation::Core::Atom.PCP_#{name},
        'test'.to_clr_string)
      collection.add(atom)
      assert_equal('test', collection.#{getter})
    end
    
    def test_#{setter}
      collection = PeerCastStation::Core::AtomCollection.new
      assert_equal(0, collection.count)
      value = 'test'
      collection.#{setter}(value)
      assert_equal(1, collection.count)
      collection.#{setter}(value)
      assert_equal(1, collection.count)
      assert_equal('test', collection.#{getter})
    end
EOS
  end
  
  def self.define_test_byte(name, method_name=nil)
    getter = getter_method_name(name, method_name)
    setter = setter_method_name(name, method_name)
    module_eval(<<EOS)
    def test_#{getter}
      collection = PeerCastStation::Core::AtomCollection.new
      assert_nil(collection.#{getter})
      atom = PeerCastStation::Core::Atom.clr_ctor.overload(PeerCastStation::Core::ID4, System::Byte).call(
        PeerCastStation::Core::Atom.PCP_#{name},
        71)
      collection.add(atom)
      assert_equal(71, collection.#{getter})
    end
    
    def test_#{setter}
      collection = PeerCastStation::Core::AtomCollection.new
      assert_equal(0, collection.count)
      value = 71
      collection.#{setter}(value)
      assert_equal(1, collection.count)
      collection.#{setter}(value)
      assert_equal(1, collection.count)
      assert_equal(71, collection.#{getter})
    end
EOS
  end
  
  def self.define_test_short(name, method_name=nil)
    getter = getter_method_name(name, method_name)
    setter = setter_method_name(name, method_name)
    module_eval(<<EOS)
    def test_#{getter}
      collection = PeerCastStation::Core::AtomCollection.new
      assert_nil(collection.#{getter})
      atom = PeerCastStation::Core::Atom.clr_ctor.overload(PeerCastStation::Core::ID4, System::Int16).call(
        PeerCastStation::Core::Atom.PCP_#{name},
        7144)
      collection.add(atom)
      assert_equal(7144, collection.#{getter})
    end
    
    def test_#{setter}
      collection = PeerCastStation::Core::AtomCollection.new
      assert_equal(0, collection.count)
      value = 7144
      collection.#{setter}(value)
      assert_equal(1, collection.count)
      collection.#{setter}(value)
      assert_equal(1, collection.count)
      assert_equal(7144, collection.#{getter})
    end
EOS
  end
  
  def self.define_test_int(name, method_name=nil)
    getter = getter_method_name(name, method_name)
    setter = setter_method_name(name, method_name)
    module_eval(<<EOS)
    def test_#{getter}
      collection = PeerCastStation::Core::AtomCollection.new
      assert_nil(collection.#{getter})
      atom = PeerCastStation::Core::Atom.clr_ctor.overload(PeerCastStation::Core::ID4, System::Int32).call(
        PeerCastStation::Core::Atom.PCP_#{name},
        714400)
      collection.add(atom)
      assert_equal(714400, collection.#{getter})
    end
    
    def test_#{setter}
      collection = PeerCastStation::Core::AtomCollection.new
      assert_equal(0, collection.count)
      value = 714400
      collection.#{setter}(value)
      assert_equal(1, collection.count)
      collection.#{setter}(value)
      assert_equal(1, collection.count)
      assert_equal(714400, collection.#{getter})
    end
EOS
  end
  
  def self.define_test_timespan(name, method_name=nil)
    getter = getter_method_name(name, method_name)
    setter = setter_method_name(name, method_name)
    module_eval(<<EOS)
    def test_#{getter}
      collection = PeerCastStation::Core::AtomCollection.new
      assert_nil(collection.#{getter})
      atom = PeerCastStation::Core::Atom.clr_ctor.overload(PeerCastStation::Core::ID4, System::Int32).call(
        PeerCastStation::Core::Atom.PCP_#{name},
        714400)
      collection.add(atom)
      assert_equal(714400, collection.#{getter}.total_seconds)
    end
    
    def test_#{setter}
      collection = PeerCastStation::Core::AtomCollection.new
      assert_equal(0, collection.count)
      value = System::TimeSpan.from_seconds(714400)
      collection.#{setter}(value)
      assert_equal(1, collection.count)
      collection.#{setter}(value)
      assert_equal(1, collection.count)
      assert_equal(714400, collection.#{getter}.total_seconds)
    end
EOS
  end
  
  def self.define_test_bytes(name, method_name=nil)
    getter = getter_method_name(name, method_name)
    setter = setter_method_name(name, method_name)
    module_eval(<<EOS)
    def test_#{getter}
      collection = PeerCastStation::Core::AtomCollection.new
      assert_nil(collection.#{getter})
      atom = PeerCastStation::Core::Atom.clr_ctor.overload(PeerCastStation::Core::ID4, System::Array[System::Byte]).call(
        PeerCastStation::Core::Atom.PCP_#{name},
        'bytes')
      collection.add(atom)
      assert_equal('bytes'.unpack('C*'), collection.#{getter}.to_a)
    end
    
    def test_#{setter}
      collection = PeerCastStation::Core::AtomCollection.new
      assert_equal(0, collection.count)
      value = 'bytes'
      collection.#{setter}(value)
      assert_equal(1, collection.count)
      collection.#{setter}(value)
      assert_equal(1, collection.count)
      assert_equal('bytes'.unpack('C*'), collection.#{getter}.to_a)
    end
EOS
  end
  
  def self.define_test_id(name, method_name=nil)
    getter = getter_method_name(name, method_name)
    setter = setter_method_name(name, method_name)
    module_eval(<<EOS)
    def test_#{getter}
      collection = PeerCastStation::Core::AtomCollection.new
      assert_nil(collection.#{getter})
      value = System::Guid.new_guid
      atom = PeerCastStation::Core::Atom.clr_ctor.overload(PeerCastStation::Core::ID4, System::Array[System::Byte]).call(
        PeerCastStation::Core::Atom.PCP_#{name},
        PeerCastStation::Core::AtomCollectionExtensions.IDToByteArray(value))
      collection.add(atom)
      assert_equal(value, collection.#{getter})
    end
    
    def test_#{setter}
      collection = PeerCastStation::Core::AtomCollection.new
      assert_equal(0, collection.count)
      value = System::Guid.new_guid
      collection.#{setter}(value)
      assert_equal(1, collection.count)
      collection.#{setter}(value)
      assert_equal(1, collection.count)
      assert_equal(value, collection.#{getter})
    end
EOS
  end
  
  def self.define_test_ip_address(name, method_name=nil)
    getter = getter_method_name(name, method_name)
    setter = setter_method_name(name, method_name)
    module_eval(<<EOS)
    def test_#{getter}
      collection = PeerCastStation::Core::AtomCollection.new
      assert_nil(collection.#{getter})
      value = System::Net::IPAddress.new([127, 0, 0, 1].pack('C*'))
      atom = PeerCastStation::Core::Atom.clr_ctor.overload(PeerCastStation::Core::ID4, System::Array[System::Byte]).call(
        PeerCastStation::Core::Atom.PCP_#{name},
        [1, 0, 0, 127].pack('C*'))
      collection.add(atom)
      assert_equal(value, collection.#{getter})
    end
    
    def test_#{setter}
      collection = PeerCastStation::Core::AtomCollection.new
      assert_equal(0, collection.count)
      value = System::Net::IPAddress.new([127, 0, 0, 1].pack('C*'))
      collection.#{setter}(value)
      assert_equal(1, collection.count)
      collection.#{setter}(value)
      assert_equal(1, collection.count)
      assert_equal(value, collection.#{getter})
    end
EOS
  end
  
  def self.define_test_id4(name, method_name=nil)
    getter = getter_method_name(name, method_name)
    setter = setter_method_name(name, method_name)
    module_eval(<<EOS)
    def test_#{getter}
      collection = PeerCastStation::Core::AtomCollection.new
      assert_nil(collection.#{getter})
      value = PeerCastStation::Core::ID4.new('peer'.to_clr_string)
      atom = PeerCastStation::Core::Atom.clr_ctor.overload(PeerCastStation::Core::ID4, System::Array[System::Byte]).call(
        PeerCastStation::Core::Atom.PCP_#{name},
        'peer')
      collection.add(atom)
      assert_equal(value, collection.#{getter})
    end
    
    def test_#{setter}
      collection = PeerCastStation::Core::AtomCollection.new
      assert_equal(0, collection.count)
      value = PeerCastStation::Core::ID4.new('peer'.to_clr_string)
      collection.#{setter}(value)
      assert_equal(1, collection.count)
      collection.#{setter}(value)
      assert_equal(1, collection.count)
      assert_equal(value, collection.#{getter})
    end
EOS
  end
  
  define_test_atom('HELO')
  define_test_string('HELO_AGENT', 'HeloAgent')
  define_test_id('HELO_BCID', 'HeloBCID')
  define_test_int('HELO_DISABLE')
  define_test_short('HELO_PING')
  define_test_short('HELO_PORT')
  define_test_ip_address('HELO_REMOTEIP', 'HeloRemoteIP')
  define_test_id('HELO_SESSIONID', 'HeloSessionID')
  define_test_int('HELO_VERSION')
  
  define_test_atom('BCST')
  define_test_id('BCST_CHANID', 'BcstChannelID')
  define_test_id('BCST_DEST', 'BcstDest')
  define_test_id('BCST_FROM', 'BcstFrom')
  define_test_byte('BCST_GROUP')
  define_test_byte('BCST_HOPS')
  define_test_byte('BCST_TTL', 'BcstTTL')
  define_test_int('BCST_VERSION')
  define_test_int('BCST_VERSION_VP', 'BcstVersionVP')
  define_test_short('BCST_VERSION_EX_NUMBER', 'BcstVersionEXNumber')
  define_test_bytes('BCST_VERSION_EX_PREFIX', 'BcstVersionEXPrefix')
  
  define_test_atom('CHAN')
  define_test_id('CHAN_BCID', 'ChanBCID')
  define_test_id('CHAN_ID', 'ChanID')
  define_test_atom('CHAN_INFO')
  define_test_int('CHAN_INFO_BITRATE')
  define_test_int('CHAN_INFO_PPFLAGS', 'ChanInfoPPFlags')
  define_test_string('CHAN_INFO_COMMENT')
  define_test_string('CHAN_INFO_DESC')
  define_test_string('CHAN_INFO_GENRE')
  define_test_string('CHAN_INFO_NAME')
  define_test_string('CHAN_INFO_TYPE')
  define_test_string('CHAN_INFO_URL', 'ChanInfoURL')
  define_test_atom('CHAN_PKT')
  define_test_bytes('CHAN_PKT_DATA')
  define_test_int('CHAN_PKT_POS')
  define_test_id4('CHAN_PKT_TYPE')
  define_test_atom('CHAN_TRACK')
  define_test_string('CHAN_TRACK_ALBUM')
  define_test_string('CHAN_TRACK_CREATOR')
  define_test_string('CHAN_TRACK_TITLE')
  define_test_string('CHAN_TRACK_URL', 'ChanTrackURL')
  
  define_test_atom('HOST')
  define_test_id('HOST_CHANID', 'HostChannelID')
  define_test_int('HOST_CLAP_PP', 'HostClapPP')
  define_test_byte('HOST_FLAGS1')
  define_test_ip_address('HOST_IP', 'HostIP')
  define_test_int('HOST_NEWPOS', 'HostNewPos')
  define_test_int('HOST_OLDPOS', 'HostOldPos')
  define_test_int('HOST_NUML', 'HostNumListeners')
  define_test_int('HOST_NUMR', 'HostNumRelays')
  define_test_short('HOST_PORT')
  define_test_id('HOST_ID', 'HostSessionID')
  define_test_byte('HOST_UPHOST_HOPS')
  define_test_ip_address('HOST_UPHOST_IP', 'HostUphostIP')
  define_test_int('HOST_UPHOST_PORT')
  define_test_timespan('HOST_UPTIME')
  define_test_int('HOST_VERSION')
  define_test_int('HOST_VERSION_VP', 'HostVersionVP')
  define_test_short('HOST_VERSION_EX_NUMBER', 'HostVersionEXNumber')
  define_test_bytes('HOST_VERSION_EX_PREFIX', 'HostVersionEXPrefix')
  
  define_test_int('OK')
  define_test_atom('OLEH')
  define_test_int('QUIT')
end
 
class TestCoreAtomReader < Test::Unit::TestCase
  def id4(s)
    PeerCastStation::Core::ID4.new(s.to_clr_string)
  end
  
  def test_construct
    stream = System::IO::MemoryStream.new
    reader = PeerCastStation::Core::AtomReader.new(stream)
    assert_equal(stream, reader.base_stream)
    reader.close
  end
  
  def test_read_value
    stream = System::IO::MemoryStream.new
    reader = PeerCastStation::Core::AtomReader.new(stream)
    data = ['peer', 4, 7144].pack('Z4V2')
    stream.write(data, 0, data.bytesize)
    stream.position = 0
    atom = reader.read()
    
    assert_equal(id4('peer'), atom.name)
    assert(atom.has_value)
    assert(!atom.has_children)
    assert_nothing_raised {
      assert_equal(7144, atom.get_int32)
    }
    reader.close
  end
  
  def test_read_children
    stream = System::IO::MemoryStream.new
    data = 
      ['peer', 0x80000000 | 4].pack('Z4V')+
      ['c1', 4, 0].pack('Z4V2')+['c2', 4, 1].pack('Z4V2')+
      ['c3', 4, 2].pack('Z4V2')+['c4', 4, 3].pack('Z4V2')
    stream.write(data, 0, data.bytesize)
    stream.position = 0
    reader = PeerCastStation::Core::AtomReader.new(stream)
    atom = reader.read()
    assert_equal(id4('peer'), atom.name)
    assert(!atom.has_value)
    assert(atom.has_children)
    assert_nothing_raised do
      4.times do |i|
        assert_equal(id4("c#{i+1}"), atom.children[i].name)
        assert_equal(i, atom.children[i].get_int32)
      end
    end
    reader.close
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
    obj.tracker = System::Uri.new('mock://127.0.0.1:7147')
    obj.extra.add(PeerCastStation::Core::Atom.new(PeerCastStation::Core::ID4.new('test'.to_clr_string), 'foo'.to_clr_string))
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

class MockPlugIn
  include PeerCastStation::Core::IPlugIn
  def initialize
    @log = []
  end
  attr_reader :log
  
  def name
    'MockPlugIn'
  end
  
  def description
    'Dummy plugin for test.'
  end
  
  def register(core)
    @log << [:register, core]
  end
  
  def unregister(core)
    @log << [:unregister, core]
  end
end

class MockPlugInLoader
  include PeerCastStation::Core::IPlugInLoader
  def initialize
    @log = []
  end
  attr_reader :log
  
  def name
    'MockPlugInLoader'
  end 
  
  def load(uri)
    @log << [:load, uri]
    if /mock/=~uri.to_s then
      MockPlugIn.new
    else
      nil
    end
  end
end 

class TestCore < Test::Unit::TestCase
  def setup
  end
  
  def teardown
    @peercast.close if @peercast and not @peercast.is_closed
  end
  
  def test_construct
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7147)
    @peercast = PeerCastStation::Core::PeerCast.new(endpoint)
    #assert_not_equal(0, obj.plug_in_loaders.count)
    assert_equal(0, @peercast.plug_ins.count)
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
  
  def test_plugin
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7147)
    @peercast = PeerCastStation::Core::PeerCast.new(endpoint)
    assert_nil(@peercast.load_plug_in(System::Uri.new('file://mock')))
    
    loader = MockPlugInLoader.new
    @peercast.plug_in_loaders.add(loader)
    plug_in = @peercast.load_plug_in(System::Uri.new('file://mock'))
    assert_equal([:load, System::Uri.new('file://mock')], loader.log[0])
    assert_not_nil(plug_in)
    assert_kind_of(MockPlugIn, plug_in)
    
    assert_equal(1, plug_in.log.size)
    assert_equal([:register, @peercast], plug_in.log[0])
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

class TestCoreChannel < Test::Unit::TestCase
  def id4(s)
    PeerCastStation::Core::ID4.new(s.to_clr_string)
  end

  def test_construct
    channel = PeerCastStation::Core::Channel.new(System::Guid.empty, System::Uri.new('mock://localhost'))
    assert_nil(channel.source_stream)
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
    channel = PeerCastStation::Core::Channel.new(System::Guid.empty, System::Uri.new('mock://localhost'))
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
    channel = PeerCastStation::Core::Channel.new(System::Guid.empty, System::Uri.new('mock://localhost'))
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
    channel = PeerCastStation::Core::Channel.new(System::Guid.empty, System::Uri.new('mock://localhost'))
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
end

