$: << File.join(File.dirname(__FILE__), '..', 'PeerCastStation.Core', 'bin', 'Debug')
require 'PeerCastStation.Core.dll'
require 'test/unit'
using_clr_extensions PeerCastStation::Core

class TC_CoreID4 < Test::Unit::TestCase
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

class TC_CoreAtom < Test::Unit::TestCase
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

class TC_CoreAtomWriter < Test::Unit::TestCase
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

class TC_CoreAtomCollectionExtensions < Test::Unit::TestCase
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
 
class TC_CoreAtomReader < Test::Unit::TestCase
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

