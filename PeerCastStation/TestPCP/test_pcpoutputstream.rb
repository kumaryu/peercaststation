$: << File.join(File.dirname(__FILE__), '..', 'PeerCastStation.Core', 'bin', 'Debug')
$: << File.join(File.dirname(__FILE__), '..', 'PeerCastStation.PCP', 'bin', 'Debug')
require 'PeerCastStation.Core.dll'
require 'PeerCastStation.PCP.dll'
require 'socket'
require 'peca'
require 'test/unit'
using_clr_extensions PeerCastStation::Core

PCSPCP = PeerCastStation::PCP

class TC_RelayRequest < Test::Unit::TestCase
  def test_construct
    data = System::Array[System::String].new([
      'GET /channel/9778E62BDC59DF56F9216D0387F80BF2 HTTP/1.1',
      'x-peercast-pcp:1',
      'x-peercast-pos: 200000000',
      'User-Agent: PeerCastStation/1.0',
      'foo:bar',
    ])
    res = PeerCastStation::PCP::RelayRequest.new(data)
    assert_equal(
      System::Uri.new('http://localhost/channel/9778E62BDC59DF56F9216D0387F80BF2'),
      res.uri)
    assert_equal(1,          res.PCPVersion)
    assert_equal(200000000,  res.stream_pos)
    assert_equal('PeerCastStation/1.0', res.user_agent)
  end
end

class TC_RelayRequestReader < Test::Unit::TestCase
  def test_read
    data = System::IO::MemoryStream.new(<<EOS)
GET /channel/9778E62BDC59DF56F9216D0387F80BF2 HTTP/1.1\r
x-peercast-pcp:1\r
x-peercast-pos: 200000000\r
User-Agent: PeerCastStation/1.0\r
\r
EOS
    res = nil
    assert_nothing_raised {
      res = PeerCastStation::PCP::RelayRequestReader.read(data)
    }
    assert_equal(
      System::Uri.new('http://localhost/channel/9778E62BDC59DF56F9216D0387F80BF2'),
      res.uri)
    assert_equal(1,          res.PCPVersion)
    assert_equal(200000000,  res.stream_pos)
    assert_equal('PeerCastStation/1.0', res.user_agent)
  end

  def test_read_failed
    assert_raise(System::IO::EndOfStreamException) {
      data = System::IO::MemoryStream.new(<<EOS)
GET /channel/9778E62BDC59DF56F9216D0387F80BF2 HTTP/1.1\r
x-peercast-pcp:1\r
x-peercast-pos: 200000000\r
User-Agent: PeerCastStation/1.0\r
EOS
      res = PeerCastStation::PCP::RelayRequestReader.read(data)
    }
  end
end

class TC_PCPOutputStreamFactory < Test::Unit::TestCase
  def setup
    @session_id = System::Guid.new_guid
    @endpoint   = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7147)
    @peercast   = PeerCastStation::Core::PeerCast.new
    @peercast.start_listen(@endpoint)
    @channel_id = System::Guid.parse('531dc8dfc7fb42928ac2c0a626517a87')
    @channel    = PeerCastStation::Core::Channel.new(@peercast, @channel_id, System::Uri.new('http://localhost:7146'))
  end
  
  def teardown
    @peercast.close if @peercast and not @peercast.is_closed
  end
  
  def test_construct
    factory = PCSPCP::PCPOutputStreamFactory.new(@peercast)
    assert_equal(factory.Name, 'PCP')
  end

  def test_create
    factory = PCSPCP::PCPOutputStreamFactory.new(@peercast)
    s = System::IO::MemoryStream.new
    header = <<EOS
GET /channel/531DC8DFC7FB42928AC2C0A626517A87 HTTP/1.1\r
x-peercast-pcp:1\r
x-peercast-pos: 200000000\r
User-Agent: PeerCastStation/1.0\r
\r
EOS
    output_stream = factory.create(s, @endpoint, @channel_id, header)
    assert_kind_of(PCSPCP::PCPOutputStream, output_stream)
  end
end

class TC_PCPOutputStream < Test::Unit::TestCase
  class TestPCPOutputStream < PCSPCP::PCPOutputStream
    def self.new(*args)
      super.instance_eval {
        @sent_data = []
        @ok = 0
        self
      }
    end
    attr_reader :sent_data, :ok

    def Send(data)
      @sent_data << data
    end

    def OnPCPOk(atom)
      super
      @ok += 1
    end
  end

  def setup
    @session_id = System::Guid.new_guid
    @endpoint   = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7147)
    @peercast   = PeerCastStation::Core::PeerCast.new
    @peercast.start_listen(@endpoint)
    @channel_id = System::Guid.parse('531dc8dfc7fb42928ac2c0a626517a87')
    @channel    = PeerCastStation::Core::Channel.new(@peercast, @channel_id, System::Uri.new('http://localhost:7146'))
    @channel.channel_info.name = 'Test Channel' 
    @channel.channel_info.extra.set_chan_info_bitrate(7144)
    @channel.channel_info.extra.set_chan_info_genre('Test')
    @channel.channel_info.extra.set_chan_info_desc('this is a test channel')
    @channel.channel_info.extra.SetChanInfoURL('http://www.example.com/')
    @request    = PeerCastStation::PCP::RelayRequest.new(
      System::Array[System::String].new([
        'GET /channel/9778E62BDC59DF56F9216D0387F80BF2 HTTP/1.1',
        'x-peercast-pcp:1',
        'x-peercast-pos: 200000000',
        'User-Agent: PeerCastStation/1.0',
        'foo:bar',
      ])
    )
    @base_stream = System::IO::MemoryStream.new
  end
  
  def teardown
    @peercast.close if @peercast and not @peercast.is_closed
  end
  
  def test_construct
    stream = PCSPCP::PCPOutputStream.new(
      @peercast,
      @base_stream,
      @endpoint,
      @channel,
      @request)
    assert_equal(@peercast,    stream.PeerCast)
    assert_equal(@base_stream, stream.stream)
    assert_equal(@channel,     stream.Channel)
    assert_equal(200000000,    stream.StreamPosition)
    assert_equal(PCSCore::OutputStreamType.relay, stream.output_stream_type)
  end

  def test_is_local
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('192.168.1.2'), 7144)
    stream = TestPCPOutputStream.new(@peercast, @base_stream, endpoint, nil, @request)
    assert(stream.is_local)

    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    stream = TestPCPOutputStream.new(@peercast, @base_stream, endpoint, nil, @request)
    assert(!stream.is_local)
  end
  
  def test_upstream_rate
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('192.168.1.2'), 7144)
    stream = TestPCPOutputStream.new(@peercast, @base_stream, endpoint, @channel, @request)
    assert_equal(0, stream.upstream_rate)

    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    stream = TestPCPOutputStream.new(@peercast, @base_stream, endpoint, nil, @request)
    assert_equal(0, stream.upstream_rate)

    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    stream = TestPCPOutputStream.new(@peercast, @base_stream, endpoint, @channel, @request)
    assert_equal(7144, stream.upstream_rate)
  end

  class TestChannel < PeerCastStation::Core::Channel
    def Status
      @status ||= PeerCastStation::Core::SourceStreamStatus.idle
    end
    
    def Status=(value)
      @status = value
    end
  end
  def test_create_relay_response
    channel = TestChannel.new(@peercast, @channel_id, System::Uri.new('http://localhost:7146'))
    stream = TestPCPOutputStream.new(@peercast, @base_stream, @endpoint, @channel, @request)
    res = stream.create_relay_response(nil, true)
    assert_equal(<<EOS, res.to_s)
HTTP/1.0 404 Not Found.\r
\r
EOS
    
    channel.Status = PeerCastStation::Core::SourceStreamStatus.idle
    assert_equal(<<EOS, res.to_s)
HTTP/1.0 404 Not Found.\r
\r
EOS

    channel.Status = PeerCastStation::Core::SourceStreamStatus.recieving
    res = stream.create_relay_response(channel, false)
    assert_equal(<<EOS, res.to_s)
HTTP/1.0 200 OK\r
Server: #{@peercast.agent_name}\r
Accept-Ranges: none\r
x-audiocast-name: Test Channel\r
x-audiocast-bitrate: 7144\r
x-audiocast-genre: Test\r
x-audiocast-description: this is a test channel\r
x-audiocast-url: http://www.example.com/\r
x-peercast-channelid: 531DC8DFC7FB42928AC2C0A626517A87\r
Content-Type:application/x-peercast-pcp\r
\r
EOS

    res = stream.create_relay_response(channel, true)
    assert_equal(<<EOS, res.to_s)
HTTP/1.0 503 Temporary Unavailable.\r
Server: #{@peercast.agent_name}\r
Accept-Ranges: none\r
x-audiocast-name: Test Channel\r
x-audiocast-bitrate: 7144\r
x-audiocast-genre: Test\r
x-audiocast-description: this is a test channel\r
x-audiocast-url: http://www.example.com/\r
x-peercast-channelid: 531DC8DFC7FB42928AC2C0A626517A87\r
Content-Type:application/x-peercast-pcp\r
\r
EOS
  end

  def test_create_content_header_packet
    content = PCSCore::Content.new(0, 'foobar')
    chan_info = PCSCore::AtomCollection.new
    @channel.channel_info.extra.set_chan_info(chan_info)
    atom = TestPCPOutputStream.create_content_header_packet(@channel, content)
    assert_equal(PCSCore::Atom.PCP_CHAN, atom.name)
    assert(atom.has_children)
    chan_pkt = atom.children.get_chan_pkt
    assert_equal(PCSCore::Atom.PCP_CHAN_PKT_HEAD, chan_pkt.get_chan_pkt_type)
    assert_equal(0, chan_pkt.get_chan_pkt_pos)
    assert_equal('foobar', chan_pkt.get_chan_pkt_data.to_a.pack('C*'))
    assert_equal(chan_info, atom.children.get_chan_info)
  end

  def test_create_content_body_packet
    content = PCSCore::Content.new(10000000, 'foobar')
    chan_info = PCSCore::AtomCollection.new
    @channel.channel_info.extra.set_chan_info(chan_info)
    atom = TestPCPOutputStream.create_content_body_packet(@channel, content)
    assert_equal(PCSCore::Atom.PCP_CHAN, atom.name)
    assert(atom.has_children)
    chan_pkt = atom.children.get_chan_pkt
    assert_equal(PCSCore::Atom.PCP_CHAN_PKT_DATA, chan_pkt.get_chan_pkt_type)
    assert_equal(10000000, chan_pkt.get_chan_pkt_pos)
    assert_equal('foobar', chan_pkt.get_chan_pkt_data.to_a.pack('C*'))
    assert_equal(nil, atom.children.get_chan_info)
  end

  def int64?(value)
    value ? System::Nullable[System::Int64].new(value) : nil
  end

  def test_create_content_packet
    atom, header_pos, content_pos = TestPCPOutputStream.create_content_packet(@channel, int64?(nil), int64?(nil))
    assert_nil(atom)
    assert_nil(header_pos)
    assert_nil(content_pos)

    @channel.content_header = PCSCore::Content.new(0, 'header')
    atom, header_pos, content_pos = TestPCPOutputStream.create_content_packet(@channel, int64?(nil), int64?(nil))
    assert_equal(0, header_pos)
    assert_nil(content_pos)
    assert_not_nil(atom)
    assert_equal(PCSCore::Atom.PCP_CHAN, atom.name)
    assert_equal(PCSCore::Atom.PCP_CHAN_PKT_HEAD, atom.children.get_chan_pkt.get_chan_pkt_type)

    @channel.content_header = PCSCore::Content.new(6, 'header')
    atom, header_pos, content_pos = TestPCPOutputStream.create_content_packet(@channel, int64?(0), int64?(nil))
    assert_equal(6, header_pos)
    assert_nil(content_pos)
    assert_not_nil(atom)
    assert_equal(PCSCore::Atom.PCP_CHAN, atom.name)
    assert_equal(PCSCore::Atom.PCP_CHAN_PKT_HEAD, atom.children.get_chan_pkt.get_chan_pkt_type)

    @channel.contents.add(PCSCore::Content.new(12, 'data'))
    atom, header_pos, content_pos = TestPCPOutputStream.create_content_packet(@channel, int64?(6), int64?(nil))
    assert_equal(6,  header_pos)
    assert_equal(12, content_pos)
    assert_not_nil(atom)
    assert_equal(PCSCore::Atom.PCP_CHAN, atom.name)
    assert_equal(PCSCore::Atom.PCP_CHAN_PKT_DATA, atom.children.get_chan_pkt.get_chan_pkt_type)

    atom, header_pos, content_pos = TestPCPOutputStream.create_content_packet(@channel, int64?(6), int64?(12))
    assert_equal(6,  header_pos)
    assert_equal(12, content_pos)
    assert_nil(atom)

    @channel.contents.add(PCSCore::Content.new(16, 'data2'))
    atom, header_pos, content_pos = TestPCPOutputStream.create_content_packet(@channel, int64?(6), int64?(12))
    assert_equal(6,  header_pos)
    assert_equal(16, content_pos)
    assert_not_nil(atom)
    assert_equal(PCSCore::Atom.PCP_CHAN, atom.name)
    assert_equal(PCSCore::Atom.PCP_CHAN_PKT_DATA, atom.children.get_chan_pkt.get_chan_pkt_type)

    @channel.content_header = PCSCore::Content.new(21, 'header')
    @channel.contents.add(PCSCore::Content.new(27,     'data3'))
    atom, header_pos, content_pos = TestPCPOutputStream.create_content_packet(@channel, int64?(6), int64?(16))
    assert_equal(21, header_pos)
    assert_equal(16, content_pos)
    assert_not_nil(atom)
    assert_equal(PCSCore::Atom.PCP_CHAN, atom.name)
    assert_equal(PCSCore::Atom.PCP_CHAN_PKT_HEAD, atom.children.get_chan_pkt.get_chan_pkt_type)

    atom, header_pos, content_pos = TestPCPOutputStream.create_content_packet(@channel, int64?(21), int64?(16))
    assert_equal(21, header_pos)
    assert_equal(27, content_pos)
    assert_not_nil(atom)
    assert_equal(PCSCore::Atom.PCP_CHAN, atom.name)
    assert_equal(PCSCore::Atom.PCP_CHAN_PKT_DATA, atom.children.get_chan_pkt.get_chan_pkt_type)

    atom, header_pos, content_pos = TestPCPOutputStream.create_content_packet(@channel, int64?(21), int64?(nil))
    assert_equal(21, header_pos)
    assert_equal(27, content_pos)
    assert_not_nil(atom)
    assert_equal(PCSCore::Atom.PCP_CHAN, atom.name)
    assert_equal(PCSCore::Atom.PCP_CHAN_PKT_DATA, atom.children.get_chan_pkt.get_chan_pkt_type)
  end

  def test_content_changed
    stream = TestPCPOutputStream.new(@peercast, @base_stream, @endpoint, @channel, @request)
    assert(stream.is_content_changed)

    stream.set_content_changed
    assert(stream.is_content_changed)
    assert(!stream.is_content_changed)

    stream.set_content_changed
    stream.set_content_changed
    assert(stream.is_content_changed)
    assert(!stream.is_content_changed)
  end

  def test_on_pcp_helo
    stream = TestPCPOutputStream.new(@peercast, @base_stream, @endpoint, @channel, @request)
    assert_nil(stream.downhost)
    helo = PCSCore::Atom.new(PCSCore::Atom.PCP_HELO, PCSCore::AtomCollection.new)
    stream.OnPCPHelo(helo)
    assert_nil(stream.downhost)
    assert_equal(PCSCore::Atom.PCP_OLEH, stream.sent_data[0].name)
    assert_equal(PCSCore::Atom.PCP_QUIT, stream.sent_data[1].name)
    assert_equal(PCSCore::Atom.PCP_ERROR_QUIT+PCSCore::Atom.PCP_ERROR_NOTIDENTIFIED, stream.sent_data[1].get_int32)
    assert(stream.is_closed)

    session_id = System::Guid.new_guid
    stream = TestPCPOutputStream.new(@peercast, @base_stream, @endpoint, @channel, @request)
    assert_nil(stream.downhost)
    helo = PCSCore::Atom.new(PCSCore::Atom.PCP_HELO, PCSCore::AtomCollection.new)
    helo.children.SetHeloSessionID(session_id)
    stream.OnPCPHelo(helo)
    assert(stream.downhost.is_firewalled)
    assert_nil(stream.downhost.local_end_point)
    assert_nil(stream.downhost.global_end_point)
    assert_equal(PCSCore::Atom.PCP_OLEH, stream.sent_data[0].name)
    assert_equal(PCSCore::Atom.PCP_QUIT, stream.sent_data[1].name)
    assert_equal(PCSCore::Atom.PCP_ERROR_QUIT+PCSCore::Atom.PCP_ERROR_BADAGENT, stream.sent_data[1].get_int32)
    assert(stream.is_closed)

    session_id = System::Guid.new_guid
    stream = TestPCPOutputStream.new(@peercast, @base_stream, @endpoint, @channel, @request)
    stream.is_relay_full = false
    assert_nil(stream.downhost)
    helo = PCSCore::Atom.new(PCSCore::Atom.PCP_HELO, PCSCore::AtomCollection.new)
    helo.children.SetHeloSessionID(session_id)
    helo.children.SetHeloVersion(1218)
    helo.children.SetHeloPort(7145)
    stream.OnPCPHelo(helo)
    assert(!stream.downhost.is_firewalled)
    assert_not_nil(stream.downhost.global_end_point)
    assert_equal(@endpoint.address, stream.downhost.global_end_point.address)
    assert_equal(7145,              stream.downhost.global_end_point.port)
    assert_equal(PCSCore::Atom.PCP_OLEH, stream.sent_data[0].name)
    oleh = stream.sent_data[0]
    assert_equal(@endpoint.address,              oleh.children.GetHeloRemoteIP)
    assert_equal(@peercast.AgentName,            oleh.children.GetHeloAgent)
    assert_equal(1218,                           oleh.children.GetHeloVersion)
    assert_equal(@peercast.local_end_point.port, oleh.children.GetHeloPort)
    assert_equal(PCSCore::Atom.PCP_OK, stream.sent_data[1].name)
    assert(!stream.is_closed)
    stream.close

    session_id = System::Guid.new_guid
    stream = TestPCPOutputStream.new(@peercast, @base_stream, @endpoint, @channel, @request)
    stream.is_relay_full = true
    node = PCSCore::Node.new(PCSCore::Host.new)
    node.host.SessionID = session_id
    node.host.global_end_point = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7149)
    node.host.is_firewalled = false
    node.is_relay_full      = false
    node.is_direct_full     = false
    node.is_receiving       = true
    @channel.nodes.add(node)
    assert_nil(stream.downhost)
    helo = PCSCore::Atom.new(PCSCore::Atom.PCP_HELO, PCSCore::AtomCollection.new)
    helo.children.SetHeloSessionID(session_id)
    helo.children.SetHeloVersion(1218)
    helo.children.SetHeloPort(7145)
    stream.OnPCPHelo(helo)
    assert(!stream.downhost.is_firewalled)
    assert_not_nil(stream.downhost.global_end_point)
    assert_equal(@endpoint.address, stream.downhost.global_end_point.address)
    assert_equal(7145,              stream.downhost.global_end_point.port)
    assert_equal(PCSCore::Atom.PCP_OLEH, stream.sent_data[0].name)
    assert_equal(PCSCore::Atom.PCP_HOST, stream.sent_data[1].name)
    host = stream.sent_data[1]
    assert_equal(node.host.SessionID,                host.children.GetHostSessionID)
    assert_equal(node.host.global_end_point.address, host.children.GetHostIP)
    assert_equal(node.host.global_end_point.port,    host.children.GetHostPort)
    assert_equal(@channel.channel_info.ChannelID,host.children.GetHostChannelID)
    assert_equal(
      PCSCore::PCPHostFlags1.relay |
      PCSCore::PCPHostFlags1.direct |
      PCSCore::PCPHostFlags1.receiving,
      host.children.GetHostFlags1)
    assert_equal(PCSCore::Atom.PCP_QUIT, stream.sent_data[2].name)
    assert_equal(PCSCore::Atom.PCP_ERROR_QUIT+PCSCore::Atom.PCP_ERROR_UNAVAILABLE, stream.sent_data[2].get_int32)
    assert(stream.is_closed)

    session_id = System::Guid.new_guid
    stream = TestPCPOutputStream.new(@peercast, @base_stream, @endpoint, @channel, @request)
    stream.is_relay_full = true
    16.times do
      @channel.nodes.add(PCSCore::Node.new(PCSCore::Host.new))
    end
    assert_nil(stream.downhost)
    helo = PCSCore::Atom.new(PCSCore::Atom.PCP_HELO, PCSCore::AtomCollection.new)
    helo.children.SetHeloSessionID(session_id)
    helo.children.SetHeloVersion(1218)
    helo.children.SetHeloPort(7145)
    stream.OnPCPHelo(helo)
    assert(!stream.downhost.is_firewalled)
    assert_not_nil(stream.downhost.global_end_point)
    assert_equal(@endpoint.address, stream.downhost.global_end_point.address)
    assert_equal(7145,              stream.downhost.global_end_point.port)
    assert_equal(PCSCore::Atom.PCP_OLEH, stream.sent_data[0].name)
    8.times do |i|
      assert_equal(PCSCore::Atom.PCP_HOST, stream.sent_data[1+i].name)
    end
    assert_equal(PCSCore::Atom.PCP_QUIT, stream.sent_data[9].name)
    assert_equal(PCSCore::Atom.PCP_ERROR_QUIT+PCSCore::Atom.PCP_ERROR_UNAVAILABLE, stream.sent_data[9].get_int32)
    assert(stream.is_closed)
  end

  def test_pcp_bcst
    stream = TestPCPOutputStream.new(@peercast, @base_stream, @endpoint, @channel, @request)
    stream.downhost = PCSCore::Host.new
    stream.downhost.SessionID = System::Guid.new_guid
    stream.downhost.global_end_point = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7149)
    stream.downhost.is_firewalled = false
    output = MockOutputStream.new
    @channel.output_streams.add(output)
    bcst = PCSCore::Atom.new(PCSCore::Atom.PCP_BCST, PCSCore::AtomCollection.new)
    bcst.children.SetBcstTTL(11)
    bcst.children.SetBcstHops(0)
    bcst.children.SetBcstFrom(@session_id)
    bcst.children.SetBcstGroup(PCSCore::BroadcastGroup.relays)
    bcst.children.SetBcstChannelID(@channel_id)
    bcst.children.SetBcstVersion(1218)
    bcst.children.SetBcstVersionVP(27)
    bcst.children.SetOk(42)
    stream.OnPCPBcst(bcst)

    post_log = output.log.select {|log| log[0]==:post }
    assert_equal(1, post_log.size)
    assert_equal(PCSCore::Atom.PCP_BCST,post_log[0][2].name)
    assert_equal(10,                    post_log[0][2].children.GetBcstTTL)
    assert_equal(@session_id,           post_log[0][2].children.GetBcstFrom)
    assert_equal(PCP_BCST_GROUP_RELAYS, post_log[0][2].children.GetBcstGroup)
    assert_equal(@channel_id,           post_log[0][2].children.GetBcstChannelID)
    assert_equal(1218,                  post_log[0][2].children.GetBcstVersion)
    assert_equal(27,                    post_log[0][2].children.GetBcstVersionVP)
    assert_equal(42,                    post_log[0][2].children.GetOk)
    assert_equal(1, stream.ok)
  end

  def test_pcp_bcst_dest
    stream = TestPCPOutputStream.new(@peercast, @base_stream, @endpoint, @channel, @request)
    stream.downhost = PCSCore::Host.new
    stream.downhost.SessionID = System::Guid.new_guid
    stream.downhost.global_end_point = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7149)
    stream.downhost.is_firewalled = false
    output = MockOutputStream.new
    @channel.output_streams.add(output)
    
    bcst = PCSCore::Atom.new(PCSCore::Atom.PCP_BCST, PCSCore::AtomCollection.new)
    bcst.children.SetBcstTTL(11)
    bcst.children.SetBcstHops(0)
    bcst.children.SetBcstFrom(@session_id)
    bcst.children.SetBcstDest(@peercast.SessionID)
    bcst.children.SetBcstGroup(PCSCore::BroadcastGroup.relays)
    bcst.children.SetBcstChannelID(@channel_id)
    bcst.children.SetBcstVersion(1218)
    bcst.children.SetBcstVersionVP(27)
    bcst.children.SetOk(42)
    stream.OnPCPBcst(bcst)

    post_log = output.log.select {|log| log[0]==:post }
    assert_equal(0, post_log.size)
    assert_equal(1, stream.ok)
  end

  def test_pcp_bcst_no_ttl
    stream = TestPCPOutputStream.new(@peercast, @base_stream, @endpoint, @channel, @request)
    stream.downhost = PCSCore::Host.new
    stream.downhost.SessionID = System::Guid.new_guid
    stream.downhost.global_end_point = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7149)
    stream.downhost.is_firewalled = false
    output = MockOutputStream.new
    @channel.output_streams.add(output)
    
    bcst = PCSCore::Atom.new(PCSCore::Atom.PCP_BCST, PCSCore::AtomCollection.new)
    bcst.children.SetBcstTTL(1)
    bcst.children.SetBcstHops(0)
    bcst.children.SetBcstFrom(@session_id)
    bcst.children.SetBcstGroup(PCSCore::BroadcastGroup.relays)
    bcst.children.SetBcstChannelID(@channel_id)
    bcst.children.SetBcstVersion(1218)
    bcst.children.SetBcstVersionVP(27)
    bcst.children.SetOk(42)
    stream.OnPCPBcst(bcst)

    post_log = output.log.select {|log| log[0]==:post }
    assert_equal(0, post_log.size)
    assert_equal(1, stream.ok)
  end

  def test_on_pcp_quit
    stream = TestPCPOutputStream.new(@peercast, @base_stream, @endpoint, @channel, @request)
    assert(!stream.is_closed)
    quit = PCSCore::Atom.new(PCSCore::Atom.PCP_QUIT, PCSCore::Atom.PCP_ERROR_QUIT)
    stream.OnPCPQuit(quit)
    assert(stream.is_closed)
  end

  class TestProcessAtomPCPOutputStream < PCSPCP::PCPOutputStream
    def self.new(*args)
      super.instance_eval {
        @log = []
        self
      }
    end
    attr_reader :log

    def OnPCPHelo(atom);      @log << [:helo]; end
    def OnPCPOleh(atom);      @log << [:oleh]; end
    def OnPCPOk(atom);        @log << [:ok]; end
    def OnPCPChan(atom);      @log << [:chan]; end
    def OnPCPChanPkt(atom);   @log << [:chan_pkt]; end
    def OnPCPChanInfo(atom);  @log << [:chan_info]; end
    def OnPCPChanTrack(atom); @log << [:chan_track]; end
    def OnPCPBcst(atom);      @log << [:bcst]; end
    def OnPCPHost(atom);      @log << [:host]; end
    def OnPCPQuit(atom);      @log << [:quit]; end
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
    stream = TestProcessAtomPCPOutputStream.new(@peercast, @base_stream, @endpoint, @channel, @request)
    atoms.each do |atom|
      stream.process_atom(atom)
    end
    assert_equal(1, stream.log.count)
    assert_equal([:helo], stream.log[0])

    stream = TestProcessAtomPCPOutputStream.new(@peercast, @base_stream, @endpoint, @channel, @request)
    stream.downhost = PCSCore::Host.new
    stream.downhost.SessionID = System::Guid.new_guid
    stream.downhost.global_end_point = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7149)
    stream.downhost.is_firewalled = false
    atoms.each do |atom|
      stream.process_atom(atom)
    end
    assert_equal(10, stream.log.count)
    assert_equal([:helo],       stream.log[0])
    assert_equal([:oleh],       stream.log[1])
    assert_equal([:ok],         stream.log[2])
    assert_equal([:chan],       stream.log[3])
    assert_equal([:chan_pkt],   stream.log[4])
    assert_equal([:chan_info],  stream.log[5])
    assert_equal([:chan_track], stream.log[6])
    assert_equal([:bcst],       stream.log[7])
    assert_equal([:host],       stream.log[8])
    assert_equal([:quit],       stream.log[9])
  end

  def test_pcp_host
    stream = TestPCPOutputStream.new(@peercast, @base_stream, @endpoint, @channel, @request)
    assert_equal(0, @channel.nodes.count)

    node = PCSCore::Node.new(PCSCore::Host.new)
    node.host.SessionID = System::Guid.new_guid
    node.host.global_end_point = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7149)
    node.host.is_firewalled = false
    node.is_relay_full      = false
    node.is_direct_full     = false
    node.is_receiving       = true
    node.direct_count = 10
    node.relay_count = 38
    host = PCSCore::Atom.new(PCSCore::Atom.PCP_HOST, PCSCore::AtomCollection.new)
    host.children.SetHostSessionID(node.host.SessionID)
    host.children.AddHostIP(node.host.global_end_point.address)
    host.children.AddHostPort(node.host.global_end_point.port)
    host.children.SetHostNumRelays(node.relay_count)
    host.children.SetHostNumListeners(node.direct_count)
    host.children.SetHostFlags1(
      (node.host.is_firewalled ? PCSCore::PCPHostFlags1.firewalled : PCSCore::PCPHostFlags1.none) |
      (node.is_relay_full      ? PCSCore::PCPHostFlags1.none       : PCSCore::PCPHostFlags1.relay) |
      (node.is_direct_full     ? PCSCore::PCPHostFlags1.none       : PCSCore::PCPHostFlags1.direct) |
      (node.is_receiving       ? PCSCore::PCPHostFlags1.receiving  : PCSCore::PCPHostFlags1.none) |
      (node.is_control_full    ? PCSCore::PCPHostFlags1.none       : PCSCore::PCPHostFlags1.control_in))
    stream.OnPCPHost(host)
    sleep(0.1)

    assert_equal(1, @channel.nodes.count)
    node = @channel.nodes.find {|n| n.host.SessionID.eql?(host.children.GetHostSessionID) }
    assert(node)
    assert_equal(host.children.GetHostNumListeners, node.direct_count)
    assert_equal(host.children.GetHostNumRelays,    node.relay_count)
    flags1 = host.children.GetHostFlags1
    assert_equal((flags1 & PCSCore::PCPHostFlags1.firewalled)!=PCSCore::PCPHostFlags1.none, node.host.is_firewalled)
    assert_equal((flags1 & PCSCore::PCPHostFlags1.relay)     ==PCSCore::PCPHostFlags1.none, node.is_relay_full)
    assert_equal((flags1 & PCSCore::PCPHostFlags1.direct)    ==PCSCore::PCPHostFlags1.none, node.is_direct_full)
    assert_equal((flags1 & PCSCore::PCPHostFlags1.receiving) !=PCSCore::PCPHostFlags1.none, node.is_receiving) 
    assert_equal((flags1 & PCSCore::PCPHostFlags1.control_in)==PCSCore::PCPHostFlags1.none, node.is_control_full)
    assert_not_nil(node.host.global_end_point)
  end

end

