$: << File.join(File.dirname(__FILE__), '..', 'PeerCastStation.Core', 'bin', 'Debug')
$: << File.join(File.dirname(__FILE__), '..', 'PeerCastStation.PCP', 'bin', 'Debug')
require 'PeerCastStation.Core.dll'
require 'PeerCastStation.PCP.dll'
require 'socket'
require 'peca'
require 'test/unit'

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
    @server.close
  end
end

class System::Guid
  def to_s
    self.to_byte_array.to_a.collect {|v| v.to_s(16) }.join
  end
end

class TestPCPSourceStream < Test::Unit::TestCase
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
    source = PeerCastStation::PCP::PCPSourceStream.new(@core)
  end
  
  def teardown
    @core.close if @core and not @core.is_closed
  end
  
  def test_start
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7144)
    @core = PeerCastStation::Core::Core.new(endpoint)
    source = PeerCastStation::PCP::PCPSourceStream.new(@core)
    channel_id = System::Guid.parse('531dc8dfc7fb42928ac2c0a626517a87')
    channel = PeerCastStation::Core::Channel.new(channel_id, source, System::Uri.new('http://localhost:7146'))
    
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
=begin
      pcps.write_parent(PCP_BCST) do |s|
        s.write_byte(PCP_BCST_TTL, 11)
        s.write_byte(PCP_BCST_HOPS, 0)
        s.write_bytes(PCP_BCST_FROM, @session_id.to_byte_array.to_a.pack('C*'))
        s.write_byte(PCP_BCST_GROUP, 6)
        s.write_bytes(PCP_BCST_CHANID, channel_id.to_byte_array.to_a.pack('C*'))
        s.write_int(PCP_BCST_VERSION, 1218)
        s.write_int(PCP_BCST_VERSION_VP, 27)
        s.write_parent(PCP_HOST) do |ss|
          ss.write_bytes(PCP_HOST_ID, @session_id.to_byte_array.to_a.pack('C*'))
          ss.write_bytes(PCP_HOST_IP, [1, 0, 0, 127].pack('C*'))
          ss.write_short(PCP_HOST_PORT, 7146)
          ss.write_bytes(PCP_HOST_IP, [1, 0, 0, 127].pack('C*'))
          ss.write_short(PCP_HOST_PORT, 7146)
          ss.write_int(PCP_HOST_NUML, 100)
          ss.write_int(PCP_HOST_NUMR, 200)
          ss.write_int(PCP_HOST_UPTIME, 10)
          ss.write_int(PCP_HOST_VERSION, 1218)
          ss.write_int(PCP_HOST_VERSION_VP, 28)
          ss.write_int(PCP_HOST_OLDPOS, 0)
          ss.write_int(PCP_HOST_NEWPOS, 100)
          ss.write_byte(PCP_HOST_FLAGS1,
            PCP_HOST_FLAGS1_TRACKER |
            PCP_HOST_FLAGS1_RELAY |
            PCP_HOST_FLAGS1_DIRECT |
            PCP_HOST_FLAGS1_RECV)
        end
      end
=end
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
    channel.start
    sleep(0.1) until finished
    @server.thread.join
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
    channel.contents.count.times do |i|
      assert_equal(pos, channel.contents[i].position)
      assert_equal("data: #{i}", channel.contents[i].data.to_a.pack('C*'))
      pos += channel.contents[i].data.length
    end
  end
end
