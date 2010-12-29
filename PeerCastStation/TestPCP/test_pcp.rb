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
  def initialize(*args)
    @client_proc = nil
    @server = TCPServer.new(*args)
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

  def OnPCPOk(atom)
    @on_pcp_ok.call(atom) if @on_pcp_ok
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
    @server = nil
  end
  
  def teardown
    @server.close if @server
  end
  
  def test_construct
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7144)
    @core = PeerCastStation::Core::Core.new(endpoint)
    channel_id = System::Guid.parse('531dc8dfc7fb42928ac2c0a626517a87')
    channel = PeerCastStation::Core::Channel.new(channel_id, System::Uri.new('http://localhost:7146'))
    source = PeerCastStation::PCP::PCPSourceStream.new(@core, channel, channel.source_uri)
  end
  
  def teardown
    @core.close if @core and not @core.is_closed
  end

  def test_create_broadcast_packet
    @core = PeerCastStation::Core::Core.new(System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7144))
    channel_id = System::Guid.parse('531dc8dfc7fb42928ac2c0a626517a87')
    channel = PeerCastStation::Core::Channel.new(channel_id, System::Uri.new('http://localhost:7146'))
    source = PeerCastStation::PCP::PCPSourceStream.new(@core, channel, channel.source_uri)
    bcst = source.create_broadcast_packet(
      PeerCastStation::Core::BroadcastGroup.relays | PeerCastStation::Core::BroadcastGroup.trackers,
      PeerCastStation::Core::Atom.new(id4('test'), 42)
    )
    assert(bcst)
    assert_equal(PCP_BCST, bcst.name.to_s)
    assert(bcst.has_children)
    assert_equal(channel_id, bcst.children.GetBcstChannelID)
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
    @core = PeerCastStation::Core::Core.new(System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7144))
    channel_id = System::Guid.parse('531dc8dfc7fb42928ac2c0a626517a87')
    channel = PeerCastStation::Core::Channel.new(channel_id, System::Uri.new('http://localhost:7146'))
    source = PeerCastStation::PCP::PCPSourceStream.new(@core, channel, channel.source_uri)
    source.uphost = channel.source_host
    channel.output_streams.add(MockOutputStream.new(PeerCastStation::Core::OutputStreamType.play))
    channel.contents.add(PeerCastStation::Core::Content.new(7144, 'foobar'))
    host = source.create_host_packet
    assert(host)
    assert_equal(PCP_HOST, host.name.to_s)
    assert(host.has_children)
    assert_equal(channel_id, host.children.GetHostChannelID)
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

  def test_start
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7144)
    @core = PeerCastStation::Core::Core.new(endpoint)
    channel_id = System::Guid.parse('531dc8dfc7fb42928ac2c0a626517a87')
    channel = PeerCastStation::Core::Channel.new(channel_id, System::Uri.new('http://localhost:7146'))
    source = PeerCastStation::PCP::PCPSourceStream.new(@core, channel, channel.source_uri)
    
    finished = false
    @server = MockPCPServer.new('localhost', 7146)
    @server.client_proc = proc {|sock|
      req = "GET /channel/#{channel_id.to_string('N')} HTTP/1.0"
      res = sock.gets("\r\n")
      assert(/#{req}/i=~res.chomp)
      headers = []
      begin
        res = sock.gets("\r\n")
        headers.push(res)
      end while res!="\r\n"
      assert(headers.any? {|h| /^x-peercast-pcp:\s*1$/=~h.chomp })
      sock.write("HTTP/1.0 200 OK\r\n")
      sock.write("\r\n")
      pcps = AtomStream.new(sock)
      packet = pcps.read
      assert_equal(PCP_HELO, packet.command)
      assert(packet.children.any? {|c| PCP_HELO_VERSION==c.command })
      ver = packet.children.find {|c| PCP_HELO_VERSION==c.command }.content.unpack('V')[0]
      assert(ver>=1218)
      pcps.write_parent(PCP_OLEH) do |s|
        s.write_bytes(PCP_HELO_SESSIONID, @session_id.to_byte_array.to_a.pack('C*'))
        s.write_short(PCP_HELO_PORT, 7146)
        a = sock.peeraddr[3].scan(/(\d+)\.(\d+).(\d+).(\d+)/)[0].collect {|d| d.to_i }.reverse.pack('C*')
        s.write_bytes(PCP_HELO_REMOTEIP, a)
        s.write_int(PCP_HELO_VERSION, 1218)
        s.write_string(PCP_HELO_AGENT, 'MockPCPServer')
      end
      pcps.write_int(PCP_OK, 0)
      pcps.write_parent(PCP_CHAN) do |s|
        s.write_bytes(PCP_CHAN_ID, channel_id.to_byte_array.to_a.pack('C*'))
        2.times do |i|
          s.write_parent(PCP_CHAN_INFO) do |ss|
            ss.write_string(PCP_CHAN_INFO_TYPE, "RAW")
            ss.write_int(PCP_CHAN_INFO_BITRATE, 7144 + i)
            ss.write_string(PCP_CHAN_INFO_GENRE, "TestTest")
            ss.write_string(PCP_CHAN_INFO_NAME, "arekuma")
            ss.write_string(PCP_CHAN_INFO_URL, "http://example.com")
            ss.write_string(PCP_CHAN_INFO_DESC, "aaaaaa")
            ss.write_string(PCP_CHAN_INFO_COMMENT, "comment")
          end
          s.write_parent(PCP_CHAN_TRACK) do |ss|
            ss.write_string(PCP_CHAN_TRACK_TITLE, 'PeerCastStation.PCP')
            ss.write_string(PCP_CHAN_TRACK_CREATOR, 'arekuma')
            ss.write_string(PCP_CHAN_TRACK_URL, 'http://example.com/peercaststation')
            ss.write_string(PCP_CHAN_TRACK_ALBUM, 'PeerCastStation')
          end
        end
      end
      pos = 0
      pcps.write_parent(PCP_CHAN) do |s|
        s.write_bytes(PCP_CHAN_ID, channel_id.to_byte_array.to_a.pack('C*'))
        s.write_parent(PCP_CHAN_PKT) do |ss|
          ss.write_bytes(PCP_CHAN_PKT_TYPE, PCP_CHAN_PKT_HEAD)
          ss.write_int(PCP_CHAN_PKT_POS, 0)
          dat = "---header---"
          ss.write_bytes(PCP_CHAN_PKT_DATA, dat)
          pos += dat.bytesize
        end
      end
      100.times do |i|
        pcps.write_parent(PCP_CHAN) do |s|
          s.write_bytes(PCP_CHAN_ID, channel_id.to_byte_array.to_a.pack('C*'))
          s.write_parent(PCP_CHAN_PKT) do |ss|
            ss.write_bytes(PCP_CHAN_PKT_TYPE, PCP_CHAN_PKT_DATA)
            ss.write_int(PCP_CHAN_PKT_POS, pos)
            dat = "data: #{i}"
            ss.write_bytes(PCP_CHAN_PKT_DATA, dat)
            pos += dat.bytesize
          end
        end
      end
      pcps.write_int(PCP_QUIT, PCP_ERROR_QUIT+PCP_ERROR_OFFAIR)
      finished = true
    }
    channel.start(source)
    sleep(0.1) until finished
    @server.close
    sleep(0.1) until channel.status==PeerCastStation::Core::ChannelStatus.closed
    assert_equal('arekuma', channel.channel_info.name)
    assert_equal(channel_id, channel.channel_info.ChannelID)
    info = channel.channel_info.extra.find_by_name(id4(PCP_CHAN_INFO))
    assert(info)
    assert_equal('RAW',      info.children.find_by_name(id4(PCP_CHAN_INFO_TYPE)).get_string)
    assert_equal(7145,       info.children.find_by_name(id4(PCP_CHAN_INFO_BITRATE)).get_int32)
    assert_equal('TestTest', info.children.find_by_name(id4(PCP_CHAN_INFO_GENRE)).get_string)
    assert_equal('aaaaaa',   info.children.find_by_name(id4(PCP_CHAN_INFO_DESC)).get_string)
    assert_equal('comment',  info.children.find_by_name(id4(PCP_CHAN_INFO_COMMENT)).get_string)
    assert_equal('http://example.com', info.children.find_by_name(id4(PCP_CHAN_INFO_URL)).get_string)
    track = channel.channel_info.extra.find_by_name(id4(PCP_CHAN_TRACK))
    assert(track)
    assert_equal('PeerCastStation.PCP', track.children.find_by_name(id4(PCP_CHAN_TRACK_TITLE)).get_string)
    assert_equal('arekuma',             track.children.find_by_name(id4(PCP_CHAN_TRACK_CREATOR)).get_string)
    assert_equal('PeerCastStation',     track.children.find_by_name(id4(PCP_CHAN_TRACK_ALBUM)).get_string)
    assert_equal('http://example.com/peercaststation', track.children.find_by_name(id4(PCP_CHAN_TRACK_URL)).get_string)
    assert(channel.content_header)
    assert_equal(0, channel.content_header.position)
    assert_equal('---header---', channel.content_header.data.to_a.pack('C*'))
    assert_equal(100, channel.contents.count)
    pos = channel.content_header.data.length
    channel.contents.to_a.each_with_index do |content, i|
      assert_equal(pos, content.position)
      assert_equal("data: #{i}", content.data.to_a.pack('C*'))
      pos += content.data.length
    end
  end

  def test_connection_tracker_error
    @core      = PeerCastStation::Core::Core.new(System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7144))
    channel_id = System::Guid.parse('531dc8dfc7fb42928ac2c0a626517a87')
    channel    = PeerCastStation::Core::Channel.new(channel_id, System::Uri.new('http://localhost:7146'))
    source     = PeerCastStation::PCP::PCPSourceStream.new(@core, channel, channel.source_uri)
    channel.start(source)
    120.times do 
      break if channel.status==PeerCastStation::Core::ChannelStatus.closed 
      sleep(1)
    end
    assert_equal(PeerCastStation::Core::ChannelStatus.closed, channel.status)
  end

  def test_connection_tracker_channel_not_found
    @core      = PeerCastStation::Core::Core.new(System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7144))
    channel_id = System::Guid.parse('531dc8dfc7fb42928ac2c0a626517a87')
    channel    = PeerCastStation::Core::Channel.new(channel_id, System::Uri.new('http://localhost:7146'))
    source     = PeerCastStation::PCP::PCPSourceStream.new(@core, channel, channel.source_uri)
    server = notfound_server(7146)
    channel.start(source)
    server.close
    120.times do 
      break if channel.status==PeerCastStation::Core::ChannelStatus.closed 
      sleep(1)
    end
    assert_equal(PeerCastStation::Core::ChannelStatus.closed, channel.status)
  end
  
  def test_connection_tracker_quit
    @core      = PeerCastStation::Core::Core.new(System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7144))
    channel_id = System::Guid.parse('531dc8dfc7fb42928ac2c0a626517a87')
    channel    = PeerCastStation::Core::Channel.new(channel_id, System::Uri.new('http://localhost:7146'))
    source     = PeerCastStation::PCP::PCPSourceStream.new(@core, channel, channel.source_uri)
    server = offair_server(7146)
    channel.start(source)
    server.close
    120.times do 
      break if channel.status==PeerCastStation::Core::ChannelStatus.closed 
      sleep(1)
    end
    assert_equal(PeerCastStation::Core::ChannelStatus.closed, channel.status)
  end
  
  class TestChannel < PeerCastStation::Core::Channel
    def self.new(*args)
      inst = super
      inst.instance_eval do 
        @log = []
      end
      inst
    end
    attr_accessor :log
    
    def select_source_host
      res = super
      @log << [:select_source_host, res]
      res
    end
  end
  
  def test_connection_tracker_unavailable
    @core      = PeerCastStation::Core::Core.new(System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7144))
    channel_id = System::Guid.parse('531dc8dfc7fb42928ac2c0a626517a87')
    channel    = TestChannel.new(channel_id, System::Uri.new('http://localhost:7146'))
    source     = PeerCastStation::PCP::PCPSourceStream.new(@core, channel, channel.source_uri)
    server = unavailable_server(7146)
    channel.start(source)
    server.close
    120.times do 
      break if channel.status==PeerCastStation::Core::ChannelStatus.closed 
      sleep(1)
    end
    assert_equal(PeerCastStation::Core::ChannelStatus.closed, channel.status)
    assert_equal(2, channel.log.size)
    assert_equal(:select_source_host, channel.log[0][0])
    assert_not_nil(channel.log[0][1])
    assert_equal(:select_source_host, channel.log[1][0])
    assert_nil(channel.log[1][1])
  end
  
  def offair_server(port)
    server = MockPCPServer.new('localhost', port)
    server.client_proc = proc {|sock|
      res = nil
      headers = []
      begin
        res = sock.gets("\r\n")
        headers.push(res)
      end while res!="\r\n"
      sock.write("HTTP/1.0 200 Unavailable\r\n")
      sock.write("\r\n")
      pcps = AtomStream.new(sock)
      packet = pcps.read
      write_oleh(pcps, @session_id, 7146, sock)
      pcps.write_int(PCP_OK, 0)
      pcps.write_int(PCP_QUIT, PCP_ERROR_QUIT+PCP_ERROR_OFFAIR)
    }
    server
  end

  def notfound_server(port)
    server = MockPCPServer.new('localhost', port)
    server.client_proc = proc {|sock|
      res = nil
      headers = []
      begin
        res = sock.gets("\r\n")
        headers.push(res)
      end while res!="\r\n"
      sock.write("HTTP/1.0 404 Not Found\r\n")
      sock.write("\r\n")
    }
    server
  end
  
  def write_oleh(stream, session_id, port, sock)
    stream.write_parent(PCP_OLEH) do |s|
      s.write_bytes(PCP_HELO_SESSIONID, session_id.to_byte_array.to_a.pack('C*'))
      s.write_short(PCP_HELO_PORT, port)
      a = sock.peeraddr[3].scan(/(\d+)\.(\d+).(\d+).(\d+)/)[0].collect {|d| d.to_i }.reverse.pack('C*')
      s.write_bytes(PCP_HELO_REMOTEIP, a)
      s.write_int(PCP_HELO_VERSION, 1218)
      s.write_string(PCP_HELO_AGENT, 'MockPCPServer')
    end
  end

  def unavailable_server(port)
    server = MockPCPServer.new('localhost', port)
    server.client_proc = proc {|sock|
      res = nil
      headers = []
      begin
        res = sock.gets("\r\n")
        headers.push(res)
      end while res!="\r\n"
      sock.write("HTTP/1.0 503 Unavailable\r\n")
      sock.write("\r\n")
      pcps = AtomStream.new(sock)
      packet = pcps.read
      write_oleh(pcps, @session_id, 7146, sock)
      pcps.write_int(PCP_OK, 0)
      pcps.write_int(PCP_QUIT, PCP_ERROR_QUIT+PCP_ERROR_UNAVAILABLE)
    }
    server
  end

  def ok_server(port, &block)
    server = MockPCPServer.new('localhost', port)
    server.client_proc = proc {|sock|
      res = nil
      res = sock.gets("\r\n") while res!="\r\n"
      sock.write("HTTP/1.0 200 OK\r\n\r\n")
      pcps = AtomStream.new(sock)
      packet = pcps.read
      pcps.write_parent(PCP_OLEH) do |s|
        s.write_bytes(PCP_HELO_SESSIONID, @session_id.to_byte_array.to_a.pack('C*'))
        s.write_short(PCP_HELO_PORT, 7146)
        a = sock.peeraddr[3].scan(/(\d+)\.(\d+).(\d+).(\d+)/)[0].collect {|d| d.to_i }.reverse.pack('C*')
        s.write_bytes(PCP_HELO_REMOTEIP, a)
        s.write_int(PCP_HELO_VERSION, 1218)
        s.write_string(PCP_HELO_AGENT, 'MockPCPServer')
      end
      pcps.write_int(PCP_OK, 0)
      block.call(pcps)
      pcps.write_int(PCP_QUIT, PCP_ERROR_QUIT+PCP_ERROR_OFFAIR)
    }
    server
  end
  
  def node_from_addr(addr, port)
    node = PeerCastStation::Core::Node.new(PeerCastStation::Core::Host.new)
    node.host.addresses.add(System::Net::IPEndPoint.new(System::Net::IPAddress.parse(addr), port))
    node
  end

  def test_connection_otherhost_error
    @core      = PeerCastStation::Core::Core.new(System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7144))
    channel_id = System::Guid.parse('531dc8dfc7fb42928ac2c0a626517a87')
    channel    = TestChannel.new(channel_id, System::Uri.new('http://localhost:7146'))
    source     = PeerCastStation::PCP::PCPSourceStream.new(@core, channel, channel.source_uri)
    other_node = node_from_addr('127.0.0.1', 7148)
    channel.nodes.add(other_node)
    tracker = unavailable_server(7146)

    closed_log = []
    source.source_closed do |sender, e|
      closed_log << [e.host, e.close_reason]
    end

    channel.start(source)
    tracker.close
    120.times do 
      break if channel.status==PeerCastStation::Core::ChannelStatus.closed 
      sleep(1)
    end
    assert_equal(PeerCastStation::Core::ChannelStatus.closed, channel.status)
    channel_log = channel.log
    assert_equal(3, channel_log.size)
    assert_equal(:select_source_host, channel_log[0][0])
    assert_not_nil(channel_log[0][1])
    assert_equal(:select_source_host, channel_log[1][0])
    assert_not_nil(channel_log[1][1])
    assert_equal(:select_source_host, channel_log[2][0])
    assert_nil(channel_log[2][1])
    assert_equal(3, closed_log.size)
    assert_equal(PeerCastStation::PCP::CloseReason.connection_error, closed_log[0][1])
    assert_equal(PeerCastStation::PCP::CloseReason.unavailable,      closed_log[1][1])
    assert_equal(PeerCastStation::PCP::CloseReason.node_not_found,   closed_log[2][1])
  end

  def test_connection_otherhost_unavailable
    @core      = PeerCastStation::Core::Core.new(System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7144))
    channel_id = System::Guid.parse('531dc8dfc7fb42928ac2c0a626517a87')
    channel    = TestChannel.new(channel_id, System::Uri.new('http://localhost:7146'))
    source     = PeerCastStation::PCP::PCPSourceStream.new(@core, channel, channel.source_uri)
    other_node = node_from_addr('127.0.0.1', 7148)
    channel.nodes.add(other_node)
    other   = unavailable_server(7148)
    tracker = unavailable_server(7146)

    closed_log = []
    source.source_closed do |sender, e|
      closed_log << [e.host, e.close_reason]
    end

    channel.start(source)
    other.close
    tracker.close
    120.times do 
      break if channel.status==PeerCastStation::Core::ChannelStatus.closed 
      sleep(1)
    end
    assert_equal(PeerCastStation::Core::ChannelStatus.closed, channel.status)
    channel_log = channel.log
    assert_equal(3, channel_log.size)
    assert_equal(:select_source_host, channel_log[0][0])
    assert_not_nil(channel_log[0][1])
    assert_equal(:select_source_host, channel_log[1][0])
    assert_not_nil(channel_log[1][1])
    assert_equal(:select_source_host, channel_log[2][0])
    assert_nil(channel_log[2][1])
    assert_equal(3, closed_log.size)
    assert_equal(PeerCastStation::PCP::CloseReason.unavailable, closed_log[0][1])
    assert_equal(PeerCastStation::PCP::CloseReason.unavailable, closed_log[1][1])
    assert_equal(PeerCastStation::PCP::CloseReason.node_not_found, closed_log[2][1])
  end

  def test_connection_otherhost_not_found
    @core      = PeerCastStation::Core::Core.new(System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7144))
    channel_id = System::Guid.parse('531dc8dfc7fb42928ac2c0a626517a87')
    channel    = TestChannel.new(channel_id, System::Uri.new('http://localhost:7146'))
    source     = PeerCastStation::PCP::PCPSourceStream.new(@core, channel, channel.source_uri)
    other_node = node_from_addr('127.0.0.1', 7148)
    channel.nodes.add(other_node)
    other   = notfound_server(7148)
    tracker = unavailable_server(7146)

    closed_log = []
    source.source_closed do |sender, e|
      closed_log << [e.host, e.close_reason]
    end

    channel.start(source)
    other.close
    tracker.close
    120.times do 
      break if channel.status==PeerCastStation::Core::ChannelStatus.closed 
      sleep(1)
    end
    assert_equal(PeerCastStation::Core::ChannelStatus.closed, channel.status)
    channel_log = channel.log
    assert_equal(3, channel_log.size)
    assert_equal(:select_source_host, channel_log[0][0])
    assert_not_nil(channel_log[0][1])
    assert_equal(:select_source_host, channel_log[1][0])
    assert_not_nil(channel_log[1][1])
    assert_equal(:select_source_host, channel_log[2][0])
    assert_nil(channel_log[2][1])
    assert_equal(3, closed_log.size)
    assert_equal(PeerCastStation::PCP::CloseReason.channel_not_found, closed_log[0][1])
    assert_equal(PeerCastStation::PCP::CloseReason.unavailable,       closed_log[1][1])
    assert_equal(PeerCastStation::PCP::CloseReason.node_not_found,    closed_log[2][1])
  end

  def test_post
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7144)
    @core = PeerCastStation::Core::Core.new(endpoint)
    channel_id = System::Guid.parse('531dc8dfc7fb42928ac2c0a626517a87')
    channel = PeerCastStation::Core::Channel.new(channel_id, System::Uri.new('http://localhost:7146'))
    source = TestPCPSourceStream.new(@core, channel, channel.source_uri)
    source.on_pcp_ok = proc {
      source.post(
        PeerCastStation::Core::Host.new,
        PeerCastStation::Core::Atom.new(id4('test'), 'hogehoge'.to_clr_string))
    }
    
    server = ok_server(7146) {|pcps|
      packet = pcps.read
      assert_equal(packet.command, 'test')
      assert_equal(packet.content, "hogehoge\0")
    }
    channel.start(source)
    server.close
    sleep(0.1) until channel.status==PeerCastStation::Core::ChannelStatus.closed
  end

  def test_bcst
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7144)
    @core = PeerCastStation::Core::Core.new(endpoint)
    channel_id = System::Guid.parse('531dc8dfc7fb42928ac2c0a626517a87')
    channel = PeerCastStation::Core::Channel.new(channel_id, System::Uri.new('http://localhost:7146'))
    source = TestPCPSourceStream.new(@core, channel, channel.source_uri)
    ok = 0
    source.on_pcp_ok = proc {
      ok += 1
    }
    output = MockOutputStream.new
    channel.output_streams.add(output)
    bcst = PeerCastStation::Core::Atom.new(
      id4(PCP_BCST),
      PeerCastStation::Core::AtomCollection.new)
    bcst.children.SetBcstTTL(11)
    bcst.children.SetBcstHops(0)
    bcst.children.SetBcstFrom(@session_id)
    bcst.children.SetBcstGroup(PeerCastStation::Core::BroadcastGroup.relays)
    bcst.children.SetBcstChannelID(channel_id)
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
    assert_equal(channel_id,            post_log[0][2].children.GetBcstChannelID)
    assert_equal(1218,                  post_log[0][2].children.GetBcstVersion)
    assert_equal(27,                    post_log[0][2].children.GetBcstVersionVP)
    assert_equal(42,                    post_log[0][2].children.GetOk)
    assert_equal(1, ok)
    post_log = source.log.select {|log| log[0]==:post }
    assert_equal(0, post_log.size)
  end

  def test_bcst_dest
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7144)
    @core = PeerCastStation::Core::Core.new(endpoint)
    channel_id = System::Guid.parse('531dc8dfc7fb42928ac2c0a626517a87')
    channel = PeerCastStation::Core::Channel.new(channel_id, System::Uri.new('http://localhost:7146'))
    source = TestPCPSourceStream.new(@core, channel, channel.source_uri)
    ok = 0
    source.on_pcp_ok = proc {
      ok += 1
    }
    output = MockOutputStream.new
    channel.output_streams.add(output)
    
    server = ok_server(7146) {|pcps|
      pcps.write_parent(PCP_BCST) do |sub|
        sub.write_byte(PCP_BCST_TTL, 11)
        sub.write_byte(PCP_BCST_HOPS, 0)
        sub.write_bytes(PCP_BCST_FROM, @session_id.to_byte_array.to_a.pack('C*'))
        sub.write_bytes(PCP_BCST_DEST, @core.host.SessionID.to_byte_array.to_a.pack('C*'))
        sub.write_byte(PCP_BCST_GROUP, PCP_BCST_GROUP_RELAYS)
        sub.write_bytes(PCP_BCST_CHANID, channel_id.to_byte_array.to_a.pack('C*'))
        sub.write_int(PCP_BCST_VERSION, 1218)
        sub.write_int(PCP_BCST_VERSION_VP, 27)
        sub.write_int(PCP_OK, 42)
      end
    }
    channel.start(source)
    server.close
    sleep(0.1) until channel.status==PeerCastStation::Core::ChannelStatus.closed
    post_log = output.log.select {|log| log[0]==:post }
    assert_equal(0, post_log.size)
    assert_equal(2, ok)
    post_log = source.log.select {|log| log[0]==:post }
    assert_equal(0, post_log.size)
  end

  def test_bcst_no_ttl
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7144)
    @core = PeerCastStation::Core::Core.new(endpoint)
    channel_id = System::Guid.parse('531dc8dfc7fb42928ac2c0a626517a87')
    channel = PeerCastStation::Core::Channel.new(channel_id, System::Uri.new('http://localhost:7146'))
    source = TestPCPSourceStream.new(@core, channel, channel.source_uri)
    ok = 0
    source.on_pcp_ok = proc {
      ok += 1
    }
    output = MockOutputStream.new
    channel.output_streams.add(output)
    
    server = ok_server(7146) {|pcps|
      pcps.write_parent(PCP_BCST) do |sub|
        sub.write_byte(PCP_BCST_TTL, 1)
        sub.write_byte(PCP_BCST_HOPS, 0)
        sub.write_bytes(PCP_BCST_FROM, @session_id.to_byte_array.to_a.pack('C*'))
        sub.write_byte(PCP_BCST_GROUP, PCP_BCST_GROUP_RELAYS)
        sub.write_bytes(PCP_BCST_CHANID, channel_id.to_byte_array.to_a.pack('C*'))
        sub.write_int(PCP_BCST_VERSION, 1218)
        sub.write_int(PCP_BCST_VERSION_VP, 27)
        sub.write_int(PCP_OK, 42)
      end
    }
    channel.start(source)
    server.close
    sleep(0.1) until channel.status==PeerCastStation::Core::ChannelStatus.closed
    post_log = output.log.select {|log| log[0]==:post }
    assert_equal(0, post_log.size)
    assert_equal(2, ok)
    post_log = source.log.select {|log| log[0]==:post }
    assert_equal(0, post_log.size)
  end
end

