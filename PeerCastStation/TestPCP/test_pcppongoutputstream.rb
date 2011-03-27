$: << File.join(File.dirname(__FILE__), '..', 'PeerCastStation.Core', 'bin', 'Debug')
$: << File.join(File.dirname(__FILE__), '..', 'PeerCastStation.PCP', 'bin', 'Debug')
require 'PeerCastStation.Core.dll'
require 'PeerCastStation.PCP.dll'
require 'socket'
require 'peca'
require 'test/unit'
using_clr_extensions PeerCastStation::Core

PCSPCP = PeerCastStation::PCP
PCSCore = PeerCastStation::Core

class TC_PCPPongOutputStreamFactory < Test::Unit::TestCase
  def setup
    @endpoint   = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7147)
    @peercast   = PeerCastStation::Core::PeerCast.new
    @peercast.start_listen(@endpoint)
    @channel_id = System::Guid.empty
    @channel    = nil
  end
  
  def teardown
    @peercast.close if @peercast and not @peercast.is_closed
  end
  
  def test_construct
    factory = PCSPCP::PCPPongOutputStreamFactory.new(@peercast)
    assert_equal(factory.Name, 'PCPPong')
  end

  def test_parse_channel_id
    factory = PCSPCP::PCPPongOutputStreamFactory.new(@peercast)
    assert_nil(factory.ParseChannelID(<<EOS))
GET /channel/531DC8DFC7FB42928AC2C0A626517A87 HTTP/1.1\r
x-peercast-pcp:1\r
x-peercast-pos: 200000000\r
User-Agent: PeerCastStation/1.0\r
\r
EOS
    assert_equal(System::Guid.empty, factory.ParseChannelID(["pcp\n", 4, 1].pack('Z4VV')))
    assert_nil(factory.ParseChannelID(["pcp\n", 4, 0].pack('Z4VV')))
  end

  def test_create
    factory = PCSPCP::PCPPongOutputStreamFactory.new(@peercast)
    s = System::IO::MemoryStream.new
    output_stream = factory.create(s, @endpoint, @channel_id, nil)
    assert_kind_of(PCSPCP::PCPPongOutputStream, output_stream)
  end
end

class TC_PCPPongOutputStream < Test::Unit::TestCase
  class TestPCPPongOutputStream < PCSPCP::PCPPongOutputStream
    def self.new(*args)
      super.instance_eval {
        @sent_data = []
        self
      }
    end
    attr_reader :sent_data

    def Send(data)
      @sent_data << data
    end
  end

  def setup
    @endpoint   = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7147)
    @peercast   = PeerCastStation::Core::PeerCast.new
    @peercast.start_listen(@endpoint)
    @base_stream = System::IO::MemoryStream.new
  end
  
  def teardown
    @peercast.close if @peercast and not @peercast.is_closed
  end
  
  def test_construct
    stream = PCSPCP::PCPPongOutputStream.new(@peercast, @base_stream, @endpoint)
    assert_equal(@peercast,    stream.PeerCast)
    assert_equal(@base_stream, stream.Stream)
    assert(!stream.IsClosed)
    assert_equal(PCSCore::OutputStreamType.metadata, stream.output_stream_type)
  end

  def test_is_local
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('192.168.1.2'), 7144)
    stream = TestPCPPongOutputStream.new(@peercast, @base_stream, endpoint)
    assert(stream.is_local)

    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    stream = TestPCPPongOutputStream.new(@peercast, @base_stream, endpoint)
    assert(!stream.is_local)
  end
  
  def test_upstream_rate
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('192.168.1.2'), 7144)
    stream = TestPCPPongOutputStream.new(@peercast, @base_stream, endpoint)
    assert_equal(0, stream.upstream_rate)

    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    stream = TestPCPPongOutputStream.new(@peercast, @base_stream, endpoint)
    assert_equal(0, stream.upstream_rate)

    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    stream = TestPCPPongOutputStream.new(@peercast, @base_stream, endpoint)
    assert_equal(0, stream.upstream_rate)
  end

  def test_on_pcp_helo
    stream = TestPCPPongOutputStream.new(@peercast, @base_stream, @endpoint)
    helo = PCSCore::Atom.new(PCSCore::Atom.PCP_HELO, PCSCore::AtomCollection.new)
    stream.OnPCPHelo(helo)
    assert_equal(PCSCore::Atom.PCP_OLEH, stream.sent_data[0].name)
    assert_equal(PCSCore::Atom.PCP_QUIT, stream.sent_data[1].name)
    assert_equal(PCSCore::Atom.PCP_ERROR_QUIT+PCSCore::Atom.PCP_ERROR_NOTIDENTIFIED, stream.sent_data[1].get_int32)
    assert(stream.is_closed)

    session_id = System::Guid.new_guid
    stream = TestPCPPongOutputStream.new(@peercast, @base_stream, @endpoint)
    helo = PCSCore::Atom.new(PCSCore::Atom.PCP_HELO, PCSCore::AtomCollection.new)
    helo.children.SetHeloSessionID(session_id)
    stream.OnPCPHelo(helo)
    assert_equal(PCSCore::Atom.PCP_OLEH, stream.sent_data[0].name)
    assert_equal(1, stream.sent_data[0].children.count)
    assert_equal(@peercast.SessionID, stream.sent_data[0].children.GetHeloSessionID)
    assert(!stream.is_closed)
    stream.close

    session_id = System::Guid.new_guid
    stream = TestPCPPongOutputStream.new(@peercast, @base_stream, @endpoint)
    helo = PCSCore::Atom.new(PCSCore::Atom.PCP_HELO, PCSCore::AtomCollection.new)
    helo.children.SetHeloSessionID(session_id)
    helo.children.SetHeloVersion(1218)
    helo.children.SetHeloPort(7145)
    stream.OnPCPHelo(helo)
    assert_equal(PCSCore::Atom.PCP_OLEH, stream.sent_data[0].name)
    assert_equal(1, stream.sent_data[0].children.count)
    assert_equal(@peercast.SessionID, stream.sent_data[0].children.GetHeloSessionID)
    assert(!stream.is_closed)
    stream.close
  end

  def test_on_pcp_quit
    stream = TestPCPPongOutputStream.new(@peercast, @base_stream, @endpoint)
    assert(!stream.is_closed)
    quit = PCSCore::Atom.new(PCSCore::Atom.PCP_QUIT, PCSCore::Atom.PCP_ERROR_QUIT)
    stream.OnPCPQuit(quit)
    assert(stream.is_closed)
  end

  class TestProcessAtomPCPPongOutputStream < PCSPCP::PCPPongOutputStream
    def self.new(*args)
      super.instance_eval {
        @log = []
        self
      }
    end
    attr_reader :log

    def OnPCPHelo(atom); @log << [:helo]; end
    def OnPCPQuit(atom); @log << [:quit]; end
  end

  def test_process_atom
    atoms = [
      PCSCore::Atom.new(PCSCore::Atom.PCP_HELO, 0),
      PCSCore::Atom.new(PCSCore::Atom.PCP_OLEH, 0),
      PCSCore::Atom.new(PCSCore::Atom.PCP_OK, 0),
      PCSCore::Atom.new(PCSCore::Atom.PCP_CHAN, 0),
      PCSCore::Atom.new(PCSCore::Atom.PCP_CHAN_PKT, 0),
      PCSCore::Atom.new(PCSCore::Atom.PCP_CHAN_INFO, 0),
      PCSCore::Atom.new(PCSCore::Atom.PCP_CHAN_TRACK, 0),
      PCSCore::Atom.new(PCSCore::Atom.PCP_BCST, 0),
      PCSCore::Atom.new(PCSCore::Atom.PCP_HOST, 0),
      PCSCore::Atom.new(PCSCore::Atom.PCP_QUIT, 0),
    ]
    stream = TestProcessAtomPCPPongOutputStream.new(@peercast, @base_stream, @endpoint)
    atoms.each do |atom|
      stream.process_atom(atom)
    end
    assert_equal(2, stream.log.count)
    assert_equal([:helo], stream.log[0])
  end
end

