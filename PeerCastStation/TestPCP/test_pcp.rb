$: << File.join(File.dirname(__FILE__), '..', 'PeerCastStation.Core', 'bin', 'Debug')
$: << File.join(File.dirname(__FILE__), '..', 'PeerCastStation.PCP', 'bin', 'Debug')
require 'PeerCastStation.Core.dll'
require 'PeerCastStation.PCP.dll'
require 'socket'
require 'peca'
require 'test/unit'
using_clr_extensions PeerCastStation::Core

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
  
  def start(stream, channel)
    @log << [:start, stream, channel]
  end
  
  def close
    @log << [:close]
  end
end

class MockPCPServer
  def initialize(*args, &client_proc)
    @client_proc = nil
    @server = TCPServer.new(*args)
    @client_proc = client_proc
    @client_threads = []
    @thread = Thread.new {
      sock = @server.accept
      if @client_proc then
        @client_threads.push(Thread.new {
          @client_proc.call(sock)
          sock.close
        })
      else
        sock.close
      end
    }
  end
  attr_accessor :client_proc
  attr_reader :thread
  
  def close
    @thread.join
    @server.close
  end
end

class System::Guid
  def to_s
    self.to_byte_array.to_a.collect {|v| v.to_s(16) }.join
  end
end

class TestPCPSourceStream < PeerCastStation::PCP::PCPSourceStream
  def self.new(core, channel, tracker)
    inst = super
    inst.instance_eval do
      @log = []
      @on_pcp_ok = nil
    end
    inst
  end
  attr_accessor :on_pcp_ok, :log

  def Post(from, atom)
    super
    @log << [:post, from, atom]
  end

  def Close(reason)
    @log << [:close, reason]
    super
  end

  def OnPCPOk(atom)
    @on_pcp_ok.call(atom) if @on_pcp_ok
    super
  end
end

class TC_RelayRequestResponse < Test::Unit::TestCase
  def test_construct
    data = System::Array[System::String].new([
      'HTTP/1.1 200 OK',
      'x-peercast-pcp:1',
      'x-peercast-pos: 200000000',
      'Content-Type: application/x-peercast-pcp',
      'foo:bar',
    ])
    res = PeerCastStation::PCP::RelayRequestResponse.new(data)
    assert_equal(200,       res.status_code)
    assert_equal(1,         res.PCPVersion)
    assert_equal(200000000, res.stream_pos)
    assert_equal('application/x-peercast-pcp', res.content_type)
  end
end

class TC_RelayRequestResponseReader < Test::Unit::TestCase
  def test_read
    data = System::IO::MemoryStream.new(<<EOS)
HTTP/1.1 200 OK\r
x-peercast-pcp:1\r
x-peercast-pos: 200000000\r
Content-Type: application/x-peercast-pcp\r
foo:bar\r
\r
EOS
    res = nil
    assert_nothing_raised {
      res = PeerCastStation::PCP::RelayRequestResponseReader.read(data)
    }
    assert_equal(200,       res.status_code)
    assert_equal(1,         res.PCPVersion)
    assert_equal(200000000, res.stream_pos)
    assert_equal('application/x-peercast-pcp', res.content_type)
  end

  def test_read_failed
    assert_raise(System::IO::EndOfStreamException) {
      data = System::IO::MemoryStream.new(<<EOS)
HTTP/1.1 200 OK\r
x-peercast-pcp:1\r
x-peercast-pos: 200000000\r
Content-Type: application/x-peercast-pcp\r
EOS
      res = PeerCastStation::PCP::RelayRequestResponseReader.read(data)
    }
  end
end

class TC_PCPSourceStream < Test::Unit::TestCase
  def id4(s)
    PeerCastStation::Core::ID4.new(s.to_clr_string)
  end
  
  def setup
    @session_id = System::Guid.new_guid
    @endpoint   = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7147)
    @core       = PeerCastStation::Core::Core.new(@endpoint)
    @channel_id = System::Guid.parse('531dc8dfc7fb42928ac2c0a626517a87')
    @channel    = PeerCastStation::Core::Channel.new(@channel_id, System::Uri.new('http://localhost:7146'))
  end
  
  def teardown
    @core.close if @core and not @core.is_closed
  end
  
  def test_construct
    source = PeerCastStation::PCP::PCPSourceStream.new(@core, @channel, @channel.source_uri)
  end
  
  def test_create_broadcast_packet
    source = PeerCastStation::PCP::PCPSourceStream.new(@core, @channel, @channel.source_uri)
    bcst = source.create_broadcast_packet(
      PeerCastStation::Core::BroadcastGroup.relays | PeerCastStation::Core::BroadcastGroup.trackers,
      PeerCastStation::Core::Atom.new(id4('test'), 42)
    )
    assert(bcst)
    assert_equal(PCP_BCST, bcst.name.to_s)
    assert(bcst.has_children)
    assert_equal(@channel_id, bcst.children.GetBcstChannelID)
    assert_equal(11, bcst.children.GetBcstTTL)
    assert_equal(0, bcst.children.GetBcstHops)
    assert_equal(@core.host.SessionID, bcst.children.GetBcstFrom)
    assert_equal(
      PeerCastStation::Core::BroadcastGroup.relays | PeerCastStation::Core::BroadcastGroup.trackers,
      bcst.children.GetBcstGroup)
    assert_equal(1218, bcst.children.GetBcstVersion)
    assert_equal(27, bcst.children.GetBcstVersionVP)
    assert_equal('PP'.unpack('CC'), bcst.children.GetBcstVersionEXPrefix)
    assert_equal(23, bcst.children.GetBcstVersionEXNumber)
    assert(bcst.children.to_a.any? {|atom| atom.name.to_s=='test' && atom.GetInt32==42 })
  end

  def test_create_host_packet
    source = PeerCastStation::PCP::PCPSourceStream.new(@core, @channel, @channel.source_uri)
    source.uphost = @channel.source_host
    @channel.output_streams.add(MockOutputStream.new(PeerCastStation::Core::OutputStreamType.play))
    @channel.contents.add(PeerCastStation::Core::Content.new(7144, 'foobar'))
    host = source.create_host_packet
    assert(host)
    assert_equal(PCP_HOST, host.name.to_s)
    assert(host.has_children)
    assert_equal(@channel_id, host.children.GetHostChannelID)
    assert_equal(@core.host.SessionID, host.children.GetHostSessionID)
    assert(host.children.to_a.any? {|atom| atom.name.to_s==PCP_HOST_IP })
    assert(host.children.to_a.any? {|atom| atom.name.to_s==PCP_HOST_PORT })
    assert_equal(1, host.children.GetHostNumListeners)
    assert_equal(0, host.children.GetHostNumRelays)
    assert(host.children.GetHostUptime)
    assert_equal(7144, host.children.GetHostOldPos)
    assert_equal(7144, host.children.GetHostNewPos)
    assert_equal(1218, host.children.GetHostVersion)
    assert_equal(27, host.children.GetHostVersionVP)
    assert_equal('PP'.unpack('CC'), host.children.GetHostVersionEXPrefix)
    assert_equal(23, host.children.GetHostVersionEXNumber)
    assert(source.uphost.Addresses.to_a.any? {|addr| addr.Address==host.children.GetHostUphostIP })
    assert(source.uphost.Addresses.to_a.any? {|addr| addr.Port==host.children.GetHostUphostPort })
    assert(host.children.GetHostFlags1)
  end

	def test_connect
    source = PeerCastStation::PCP::PCPSourceStream.new(@core, @channel, @channel.source_uri)
    assert(!source.is_connected)
    connected = 0
    server = MockPCPServer.new('localhost', 7146) {|sock| connected += 1 }
    host = PeerCastStation::Core::Host.new
    host.addresses.add(System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7146))
    sleep(0.1)
    assert(source.connect(host))
    assert(source.is_connected)
    server.close
    assert_equal(1, connected)
	end

  def test_connect_failed
    source = PeerCastStation::PCP::PCPSourceStream.new(@core, @channel, @channel.source_uri)
    host = PeerCastStation::Core::Host.new
    host.addresses.add(System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7146))
    assert(!source.connect(host))
    assert(!source.is_connected)
  end

  def test_close
    source = PeerCastStation::PCP::PCPSourceStream.new(@core, @channel, @channel.source_uri)
    assert(!source.is_connected)
    server = MockPCPServer.new('localhost', 7146)
    host = PeerCastStation::Core::Host.new
    host.addresses.add(System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7146))
    sleep(0.1)
    assert(source.connect(host))
    assert(source.is_connected)
    source.close(PeerCastStation::PCP::CloseReason.user_shutdown)
    assert(!source.is_connected)
    server.close
  end

  def test_set_close
    source = PeerCastStation::PCP::PCPSourceStream.new(@core, @channel, @channel.source_uri)
    assert(!source.is_connected)
    assert_nil(source.state)
    source.close
    assert_nil(source.state)

    server = MockPCPServer.new('localhost', 7146)
    host = PeerCastStation::Core::Host.new
    host.addresses.add(System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7146))
    source.connect(host)
    assert_nil(source.state)
    source.close
    assert_kind_of(PeerCastStation::PCP::PCPSourceClosedState, source.state)
  end

  def test_ignore_host
    source = PeerCastStation::PCP::PCPSourceStream.new(@core, @channel, @channel.source_uri)
    host = PeerCastStation::Core::Host.new
    assert_equal(0, @channel.ignored_hosts.count)
    source.ignore_host(host)
    assert_equal(1, @channel.ignored_hosts.count)
    assert(@channel.ignored_hosts.include?(host))
  end

  def test_send_relay_request
    source = TestPCPSourceStreamNoSend.new(@core, @channel, @channel.source_uri)
    source.send_relay_request
    assert(source.log)
    assert_equal(1, source.log.size)
    assert_equal(:send, source.log[0][0])
    assert_equal("GET /channel/#{@channel.channel_info.ChannelID.ToString('N')} HTTP/1.0",
                 source.log[0][1].to_a.pack('C*').split(/\r\n/)[0])
    assert(source.log[0][1].to_a.pack('C*').split(/\r\n/).include?("x-peercast-pcp:1"))
  end

  def test_send_pcp_helo
    source = TestPCPSourceStreamNoSend.new(@core, @channel, @channel.source_uri)
    source.SendPCPHelo
    assert(source.log)
    assert_equal(1, source.log.size)
    assert_equal(:send, source.log[0][0])
    assert_equal(id4(PCP_HELO), source.log[0][1].name)
    assert(source.log[0][1].has_children)
  end

  class TestStreamState
    include PeerCastStation::PCP::IStreamState

    def initialize
      @log = []
    end
    attr_reader :log
    def process
      @log << :process
      nil
    end
  end

  def test_process_state
    state = TestStreamState.new
    source = TestPCPSourceStreamNoSend.new(@core, @channel, @channel.source_uri)
    source.state = state
    assert_equal(0, state.log.size)
    source.process_state
    assert_equal(1, state.log.size)
    assert_equal(:process, state.log[0])
    assert_nil(source.state)
  end

  class TestPCPSourceStreamNoSend < PeerCastStation::PCP::PCPSourceStream
    def Send(data)
      (@log ||= []) << [:send, data]
    end
    attr_reader :log
  end

  def test_post
    source = TestPCPSourceStreamNoSend.new(@core, @channel, @channel.source_uri)
    source.post(
      PeerCastStation::Core::Host.new,
      PeerCastStation::Core::Atom.new(id4('test'), 'hogehoge'.to_clr_string))
    source.process_events
    assert(source.log)
    assert_equal(1, source.log.size)
    assert_equal(:send,       source.log[0][0])
    assert_equal(id4('test'), source.log[0][1].name)
    assert_equal('hogehoge',  source.log[0][1].get_string.to_s)
  end

  def test_pcp_bcst
    source = TestPCPSourceStream.new(@core, @channel, @channel.source_uri)
    ok = 0
    source.on_pcp_ok = proc {
      ok += 1
    }
    output = MockOutputStream.new
    @channel.output_streams.add(output)
    bcst = PeerCastStation::Core::Atom.new(
      id4(PCP_BCST),
      PeerCastStation::Core::AtomCollection.new)
    bcst.children.SetBcstTTL(11)
    bcst.children.SetBcstHops(0)
    bcst.children.SetBcstFrom(@session_id)
    bcst.children.SetBcstGroup(PeerCastStation::Core::BroadcastGroup.relays)
    bcst.children.SetBcstChannelID(@channel_id)
    bcst.children.SetBcstVersion(1218)
    bcst.children.SetBcstVersionVP(27)
    bcst.children.SetOk(42)
    source.OnPCPBcst(bcst)

    post_log = output.log.select {|log| log[0]==:post }
    assert_equal(1, post_log.size)
    assert_equal(id4(PCP_BCST),         post_log[0][2].name)
    assert_equal(10,                    post_log[0][2].children.GetBcstTTL)
    assert_equal(@session_id,           post_log[0][2].children.GetBcstFrom)
    assert_equal(PCP_BCST_GROUP_RELAYS, post_log[0][2].children.GetBcstGroup)
    assert_equal(@channel_id,           post_log[0][2].children.GetBcstChannelID)
    assert_equal(1218,                  post_log[0][2].children.GetBcstVersion)
    assert_equal(27,                    post_log[0][2].children.GetBcstVersionVP)
    assert_equal(42,                    post_log[0][2].children.GetOk)
    assert_equal(1, ok)
    post_log = source.log.select {|log| log[0]==:post }
    assert_equal(0, post_log.size)
  end

  def test_pcp_bcst_dest
    source = TestPCPSourceStream.new(@core, @channel, @channel.source_uri)
    ok = 0
    source.on_pcp_ok = proc {
      ok += 1
    }
    output = MockOutputStream.new
    @channel.output_streams.add(output)
    
    bcst = PeerCastStation::Core::Atom.new(
      id4(PCP_BCST),
      PeerCastStation::Core::AtomCollection.new)
    bcst.children.SetBcstTTL(11)
    bcst.children.SetBcstHops(0)
    bcst.children.SetBcstFrom(@session_id)
    bcst.children.SetBcstDest(@core.host.SessionID)
    bcst.children.SetBcstGroup(PeerCastStation::Core::BroadcastGroup.relays)
    bcst.children.SetBcstChannelID(@channel_id)
    bcst.children.SetBcstVersion(1218)
    bcst.children.SetBcstVersionVP(27)
    bcst.children.SetOk(42)
    source.OnPCPBcst(bcst)

    post_log = output.log.select {|log| log[0]==:post }
    assert_equal(0, post_log.size)
    assert_equal(1, ok)
    post_log = source.log.select {|log| log[0]==:post }
    assert_equal(0, post_log.size)
  end

  def test_pcp_bcst_no_ttl
    source = TestPCPSourceStream.new(@core, @channel, @channel.source_uri)
    ok = 0
    source.on_pcp_ok = proc {
      ok += 1
    }
    output = MockOutputStream.new
    @channel.output_streams.add(output)
    
    bcst = PeerCastStation::Core::Atom.new(
      id4(PCP_BCST),
      PeerCastStation::Core::AtomCollection.new)
    bcst.children.SetBcstTTL(1)
    bcst.children.SetBcstHops(0)
    bcst.children.SetBcstFrom(@session_id)
    bcst.children.SetBcstGroup(PeerCastStation::Core::BroadcastGroup.relays)
    bcst.children.SetBcstChannelID(@channel_id)
    bcst.children.SetBcstVersion(1218)
    bcst.children.SetBcstVersionVP(27)
    bcst.children.SetOk(42)
    source.OnPCPBcst(bcst)

    post_log = output.log.select {|log| log[0]==:post }
    assert_equal(0, post_log.size)
    assert_equal(1, ok)
    post_log = source.log.select {|log| log[0]==:post }
    assert_equal(0, post_log.size)
  end

  def test_pcp_chan
    source = TestPCPSourceStream.new(@core, @channel, @channel.source_uri)
    ok = 0
    source.on_pcp_ok = proc { ok += 1 }
    chan = PeerCastStation::Core::Atom.new(id4(PCP_CHAN), PeerCastStation::Core::AtomCollection.new)
    chan.children.SetOk(42)
    assert_nil(source.OnPCPChan(chan))
    assert_equal(1, ok)
  end

  def test_pcp_chan_pkt
    source = TestPCPSourceStream.new(@core, @channel, @channel.source_uri)
    assert_nil(@channel.content_header)
    assert_equal(0, @channel.contents.count)
    chan_pkt = PeerCastStation::Core::Atom.new(id4(PCP_CHAN_PKT), PeerCastStation::Core::AtomCollection.new)
    chan_pkt.children.SetChanPktType(PeerCastStation::Core::Atom.PCP_CHAN_PKT_TYPE_HEAD)
    chan_pkt.children.SetChanPktData('foobar')
    assert_nil(source.OnPCPChanPkt(chan_pkt))
    sleep(0.1)
    assert(@channel.content_header)
    assert_equal(0,        @channel.content_header.position)
    assert_equal('foobar', @channel.content_header.data.to_a.pack('C*'))
    assert_equal(0, @channel.contents.count)

    chan_pkt = PeerCastStation::Core::Atom.new(id4(PCP_CHAN_PKT), PeerCastStation::Core::AtomCollection.new)
    chan_pkt.children.SetChanPktType(PeerCastStation::Core::Atom.PCP_CHAN_PKT_TYPE_DATA)
    chan_pkt.children.SetChanPktPos(6)
    chan_pkt.children.SetChanPktData('hogefuga')
    assert_nil(source.OnPCPChanPkt(chan_pkt))
    sleep(0.1)
    assert_equal(1,          @channel.contents.count)
    assert_equal('hogefuga', @channel.contents.newest.data.to_a.pack('C*'))
    assert_equal(6,          @channel.contents.newest.position)

    chan_pkt = PeerCastStation::Core::Atom.new(id4(PCP_CHAN_PKT), PeerCastStation::Core::AtomCollection.new)
    chan_pkt.children.SetChanPktType(PeerCastStation::Core::Atom.PCP_CHAN_PKT_TYPE_META)
    chan_pkt.children.SetChanPktPos(10000)
    chan_pkt.children.SetChanPktData('meta')
    assert_nil(source.OnPCPChanPkt(chan_pkt))
    sleep(0.1)
    assert_equal(0, @channel.content_header.position)
    assert_equal(6, @channel.contents.newest.position)
  end

  def test_pcp_chan_info
    source = TestPCPSourceStream.new(@core, @channel, @channel.source_uri)
    assert_equal('', @channel.channel_info.name)
    assert_equal(0, @channel.channel_info.extra.count)

    chan_info = PeerCastStation::Core::Atom.new(id4(PCP_CHAN_INFO), PeerCastStation::Core::AtomCollection.new)
    chan_info.children.SetChanInfoBitrate(7144)
    chan_info.children.SetChanInfoURL('http://example.com')
    chan_info.children.SetChanInfoType('WMV')
    chan_info.children.SetChanInfoGenre('Genre')
    chan_info.children.SetChanInfoDesc('Desc')
    chan_info.children.SetChanInfoComment('Comment')
    assert_nil(source.OnPCPChanInfo(chan_info))
    sleep(0.1)
    assert_equal('', @channel.channel_info.name)
    assert_equal(1,         @channel.channel_info.extra.count)
    info = @channel.channel_info.extra.GetChanInfo
    assert_equal(6,                    info.count)
    assert_equal(7144,                 info.GetChanInfoBitrate)
    assert_equal('http://example.com', info.GetChanInfoURL)
    assert_equal('WMV',                info.GetChanInfoType)
    assert_equal('Genre',              info.GetChanInfoGenre)
    assert_equal('Desc',               info.GetChanInfoDesc)
    assert_equal('Comment',            info.GetChanInfoComment)

    chan_info = PeerCastStation::Core::Atom.new(id4(PCP_CHAN_INFO), PeerCastStation::Core::AtomCollection.new)
    chan_info.children.SetChanInfoName('foobar')
    chan_info.children.SetChanInfoType('OGM')
    assert_nil(source.OnPCPChanInfo(chan_info))
    sleep(0.1)
    assert_equal('foobar',  @channel.channel_info.name)
    assert_equal(1, @channel.channel_info.extra.count)
    info = @channel.channel_info.extra.GetChanInfo
    assert_equal(2,        info.count)
    assert_equal('foobar', info.GetChanInfoName)
    assert_equal('OGM',    info.GetChanInfoType)
  end

  def test_pcp_chan_track
    source = TestPCPSourceStream.new(@core, @channel, @channel.source_uri)
    assert_equal('', @channel.channel_info.name)
    assert_equal(0, @channel.channel_info.extra.count)

    chan_track = PeerCastStation::Core::Atom.new(id4(PCP_CHAN_TRACK), PeerCastStation::Core::AtomCollection.new)
    chan_track.children.SetChanTrackURL('http://example.com')
    chan_track.children.SetChanTrackTitle('Title')
    chan_track.children.SetChanTrackAlbum('Album')
    chan_track.children.SetChanTrackCreator('Creator')
    assert_nil(source.OnPCPChanTrack(chan_track))
    sleep(0.1)
    assert_equal('', @channel.channel_info.name)
    assert_equal(1, @channel.channel_info.extra.count)
    track = @channel.channel_info.extra.GetChanTrack
    assert_equal(4,                    track.count)
    assert_equal('http://example.com', track.GetChanTrackURL)
    assert_equal('Title',              track.GetChanTrackTitle)
    assert_equal('Album',              track.GetChanTrackAlbum)
    assert_equal('Creator',            track.GetChanTrackCreator)

    chan_track = PeerCastStation::Core::Atom.new(id4(PCP_CHAN_TRACK), PeerCastStation::Core::AtomCollection.new)
    chan_track.children.SetChanTrackURL('http://example.com')
    chan_track.children.SetChanTrackTitle('Title')
    assert_nil(source.OnPCPChanTrack(chan_track))
    sleep(0.1)
    assert_equal('', @channel.channel_info.name)
    assert_equal(1, @channel.channel_info.extra.count)
    track = @channel.channel_info.extra.GetChanTrack
    assert_equal(2,                    track.count)
    assert_equal('http://example.com', track.GetChanTrackURL)
    assert_equal('Title',              track.GetChanTrackTitle)
  end

  def test_pcp_helo
    source = TestPCPSourceStreamNoSend.new(@core, @channel, @channel.source_uri)
    helo = PeerCastStation::Core::Atom.new(id4(PCP_HELO), PeerCastStation::Core::AtomCollection.new)
    helo.children.SetHeloSessionID(@session_id)
    helo.children.SetHeloAgent('IronRuby')
    helo.children.SetHeloVersion(1218)
    assert_nil(source.OnPCPHelo(helo))

    assert(source.log)
    assert_equal(1, source.log.size)
    assert_equal(:send, source.log[0][0])
    assert_equal(id4(PCP_OLEH), source.log[0][1].name)
    assert(source.log[0][1].children.GetHeloAgent)
    assert_equal(@core.host.SessionID,         source.log[0][1].children.GetHeloSessionID)
    assert_equal(@core.host.addresses[0].port, source.log[0][1].children.GetHeloPort)
    assert_equal(1218,                         source.log[0][1].children.GetHeloVersion)
  end

  def test_pcp_oleh
    source = TestPCPSourceStream.new(@core, @channel, @channel.source_uri)
    oleh = PeerCastStation::Core::Atom.new(id4(PCP_OLEH), PeerCastStation::Core::AtomCollection.new)
    oleh.children.SetHeloRemoteIP(System::Net::IPAddress.parse('0.0.0.0'))
    oleh.children.SetHeloSessionID(@session_id)
    oleh.children.SetHeloAgent('IronRuby')
    oleh.children.SetHeloVersion(1218)
    assert_equal(1, @core.host.addresses.count)
    assert_nil(source.OnPCPOleh(oleh))
    sleep(0.1)
    assert_equal(2, @core.host.addresses.count)

    assert_nil(source.OnPCPOleh(oleh))
    sleep(0.1)
    assert_equal(2, @core.host.addresses.count)
  end

  def test_pcp_host
    source = TestPCPSourceStream.new(@core, @channel, @channel.source_uri)
    assert_equal(0, @channel.nodes.count)
    node = @channel.nodes.find {|n| n.host.SessionID.eql?(@core.host.SessionID) }
    assert_nil(node)

    host = source.create_host_packet
    assert_nil(source.OnPCPHost(host))
    sleep(0.1)

    assert_equal(1, @channel.nodes.count)
    node = @channel.nodes.find {|n| n.host.SessionID.eql?(@core.host.SessionID) }
    assert(node)
    assert_equal(host.children.GetHostNumListeners, node.direct_count)
    assert_equal(host.children.GetHostNumRelays,    node.relay_count)
    flags1 = host.children.GetHostFlags1
    assert_equal((flags1 & PeerCastStation::Core::PCPHostFlags1.firewalled)!=PeerCastStation::Core::PCPHostFlags1.none, node.host.is_firewalled)
    assert_equal((flags1 & PeerCastStation::Core::PCPHostFlags1.relay)     ==PeerCastStation::Core::PCPHostFlags1.none, node.is_relay_full)
    assert_equal((flags1 & PeerCastStation::Core::PCPHostFlags1.direct)    ==PeerCastStation::Core::PCPHostFlags1.none, node.is_direct_full)
    assert_equal((flags1 & PeerCastStation::Core::PCPHostFlags1.receiving) !=PeerCastStation::Core::PCPHostFlags1.none, node.is_receiving) 
    assert_equal((flags1 & PeerCastStation::Core::PCPHostFlags1.control_in)==PeerCastStation::Core::PCPHostFlags1.none, node.is_control_full)
    assert_equal(1, node.host.addresses.count)
  end

  def test_pcp_quit
    source = TestPCPSourceStream.new(@core, @channel, @channel.source_uri)
    quit = PeerCastStation::Core::Atom.new(id4(PCP_QUIT), PCP_ERROR_QUIT+PCP_ERROR_UNAVAILABLE)
    res = source.OnPCPQuit(quit)
    assert(res)
    assert_kind_of(PeerCastStation::PCP::PCPSourceClosedState, res)
    assert_equal(source, res.owner)
    assert_equal(PeerCastStation::PCP::CloseReason.unavailable, res.close_reason)

    quit = PeerCastStation::Core::Atom.new(id4(PCP_QUIT), PCP_ERROR_QUIT+PCP_ERROR_OFFAIR)
    res = source.OnPCPQuit(quit)
    assert(res)
    assert_kind_of(PeerCastStation::PCP::PCPSourceClosedState, res)
    assert_equal(source, res.owner)
    assert_equal(PeerCastStation::PCP::CloseReason.channel_exit, res.close_reason)
  end
end

PCSCore = PeerCastStation::Core
PCSPCP  = PeerCastStation::PCP

class TC_PCPSourceClosedState < Test::Unit::TestCase
  class TestPCPSourceStreamNoIgnore < PeerCastStation::PCP::PCPSourceStream
    def self.new(core, channel, tracker)
      inst = super
      inst.instance_eval do
        @log = []
      end
      inst
    end
    attr_accessor :on_pcp_ok, :log

    def Close(reason)
      @log << [:close, reason]
      super
    end

    def IgnoreHost(host)
      @log << [:ignore_host, host]
      super
    end

    def SelectSourceHost
      @log << [:select_source_host]
      super
    end
  end

  def id4(s)
    PeerCastStation::Core::ID4.new(s.to_clr_string)
  end
  
  def setup
    @session_id = System::Guid.new_guid
    @endpoint   = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7147)
    @core       = PeerCastStation::Core::Core.new(@endpoint)
    @channel_id = System::Guid.parse('531dc8dfc7fb42928ac2c0a626517a87')
    @channel    = PeerCastStation::Core::Channel.new(@channel_id, System::Uri.new('http://localhost:7146'))
    @source     = TestPCPSourceStreamNoIgnore.new(@core, @channel, @channel.source_uri)
  end
  
  def teardown
    @core.close if @core and not @core.is_closed
  end
  
  def test_construct
    state = PCSPCP::PCPSourceClosedState.new(@source, PCSPCP::CloseReason.channel_exit)
    assert_equal(@source, state.owner)
    assert_equal(PCSPCP::CloseReason.channel_exit, state.close_reason)
  end

  def test_process
    assert_equal(0, @source.log.size)

    state = PCSPCP::PCPSourceClosedState.new(@source, PCSPCP::CloseReason.user_shutdown)
    assert_nil(state.process)
    assert_equal(1, @source.log.size)
    assert_equal(:close, @source.log[0][0])
    assert_equal(PCSPCP::CloseReason.user_shutdown, @source.log[0][1])
    @source.log.clear

    state = PCSPCP::PCPSourceClosedState.new(@source, PCSPCP::CloseReason.node_not_found)
    assert_nil(state.process)
    assert_equal(1, @source.log.size)
    assert_equal(:close, @source.log[0][0])
    assert_equal(PCSPCP::CloseReason.node_not_found, @source.log[0][1])
    @source.log.clear

    state = PCSPCP::PCPSourceClosedState.new(@source, PCSPCP::CloseReason.unavailable)
    connect_state = state.process
    assert_equal(3, @source.log.size)
    assert_equal(:ignore_host,        @source.log[0][0])
    assert_equal(:select_source_host, @source.log[1][0])
    assert_equal(:close,              @source.log[2][0])
    assert_equal(PCSPCP::CloseReason.unavailable, @source.log[2][1])
    assert_kind_of(PCSPCP::PCPSourceConnectState, connect_state)
    @source.log.clear

    [
      PCSPCP::CloseReason.ChannelExit,
      PCSPCP::CloseReason.ConnectionError,
      PCSPCP::CloseReason.AccessDenied,
      PCSPCP::CloseReason.ChannelNotFound,
    ].each do |reason|
      state = PCSPCP::PCPSourceClosedState.new(@source, reason)
      @source.uphost = @channel.source_host
      assert_nil(state.process)
      assert_equal(1, @source.log.size)
      assert_equal(:close, @source.log[0][0])
      assert_equal(reason, @source.log[0][1])
      assert_kind_of(PCSPCP::PCPSourceConnectState, connect_state)
      @source.log.clear
      @source.uphost = PCSCore::Host.new
      connect_state = state.process
      assert_equal(3, @source.log.size)
      assert_equal(:ignore_host,        @source.log[0][0])
      assert_equal(:select_source_host, @source.log[1][0])
      assert_equal(:close,              @source.log[2][0])
      assert_equal(reason,              @source.log[2][1])
      assert_kind_of(PCSPCP::PCPSourceConnectState, connect_state)
      @source.log.clear
    end
  end
end


