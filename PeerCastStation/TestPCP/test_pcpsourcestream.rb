# PeerCastStation, a P2P streaming servent.
# Copyright (C) 2011 Ryuichi Sakamoto (kumaryu@kumaryu.net)
# 
# This program is free software: you can redistribute it and/or modify
# it under the terms of the GNU General Public License as published by
# the Free Software Foundation, either version 3 of the License, or
# (at your option) any later version.
# 
# This program is distributed in the hope that it will be useful,
# but WITHOUT ANY WARRANTY; without even the implied warranty of
# MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
# GNU General Public License for more details.
# 
# You should have received a copy of the GNU General Public License
# along with this program.  If not, see <http://www.gnu.org/licenses/>.
$: << File.join(File.dirname(__FILE__), '..', 'PeerCastStation.Core', 'bin', 'Debug')
$: << File.join(File.dirname(__FILE__), '..', 'PeerCastStation.PCP', 'bin', 'Debug')
require 'PeerCastStation.Core.dll'
require 'PeerCastStation.PCP.dll'
require 'test/unit'
require 'peca'
require 'utils'
using_clr_extensions PeerCastStation::Core
explicit_extensions PeerCastStation::Core::AtomCollectionExtensions

PCSCore = PeerCastStation::Core unless defined?(PCSCore)
PCSPCP  = PeerCastStation::PCP  unless defined?(PCSPCP)

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
  
  def stop
    @log << [:stop]
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
    @client_threads.each {|thread| thread.join }
    @server.close
  end
end

class MockStreamState
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
    @peercast   = PeerCastStation::Core::PeerCast.new
    @peercast.start_listen(@endpoint)
    @channel_id = System::Guid.parse('531dc8dfc7fb42928ac2c0a626517a87')
    @channel    = PeerCastStation::Core::Channel.new(@peercast, @channel_id, System::Uri.new('http://127.0.0.1:7146'))
  end
  
  def teardown
    @peercast.stop
  end
  
  def test_construct
    source = PeerCastStation::PCP::PCPSourceStream.new(@peercast, @channel, @channel.source_uri)
    assert_equal(@peercast, source.PeerCast)
    assert_equal(@channel, source.Channel)
    assert(!source.is_connected)
    assert_nil(source.state)
    assert_nil(source.uphost)
  end
  
  def test_create_broadcast_packet
    source = PeerCastStation::PCP::PCPSourceStream.new(@peercast, @channel, @channel.source_uri)
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
    assert_equal(@peercast.SessionID, bcst.children.GetBcstFrom)
    assert_equal(
      PeerCastStation::Core::BroadcastGroup.relays | PeerCastStation::Core::BroadcastGroup.trackers,
      bcst.children.GetBcstGroup)
    assert_equal(1218, bcst.children.GetBcstVersion)
    assert_equal(27, bcst.children.GetBcstVersionVP)
    assert_nil(bcst.children.GetBcstVersionEXPrefix)
    assert_nil(bcst.children.GetBcstVersionEXNumber)
    assert(bcst.children.to_a.any? {|atom| atom.name.to_s=='test' && atom.GetInt32==42 })
  end

  def test_create_host_packet
    source = PeerCastStation::PCP::PCPSourceStream.new(@peercast, @channel, @channel.source_uri)
    source.uphost = @channel.source_host
    @channel.add_output_stream(MockOutputStream.new(PeerCastStation::Core::OutputStreamType.play))
    @channel.contents.add(PeerCastStation::Core::Content.new(71440000000000, 'foobar'))
    host = source.create_host_packet
    assert(host)
    assert_equal(PCP_HOST, host.name.to_s)
    assert(host.has_children)
    assert_equal(@channel_id, host.children.GetHostChannelID)
    assert_equal(@peercast.SessionID, host.children.GetHostSessionID)
    assert(host.children.to_a.any? {|atom| atom.name.to_s==PCP_HOST_IP })
    assert(host.children.to_a.any? {|atom| atom.name.to_s==PCP_HOST_PORT })
    assert_equal(1, host.children.GetHostNumListeners)
    assert_equal(0, host.children.GetHostNumRelays)
    assert(host.children.GetHostUptime)
    assert_equal(71440000000000 & 0xFFFFFFFF, host.children.GetHostOldPos)
    assert_equal(71440000000000 & 0xFFFFFFFF, host.children.GetHostNewPos)
    assert_equal(1218, host.children.GetHostVersion)
    assert_equal(27, host.children.GetHostVersionVP)
    assert_nil(host.children.GetHostVersionEXPrefix)
    assert_nil(host.children.GetHostVersionEXNumber)
    addresses = [
      source.uphost.global_end_point,
      source.uphost.local_end_point
    ].compact
    assert(addresses.to_a.any? {|addr| addr.Address.Equals(host.children.GetHostUphostIP) })
    assert(addresses.to_a.any? {|addr| addr.Port==host.children.GetHostUphostPort })
    assert(host.children.GetHostFlags1)
  end

  def test_broadcast_host_info
    source = TestPCPSourceStream.new(@peercast, @channel, @channel.source_uri)
    @channel.source_stream = source
    @channel.add_output_stream(MockOutputStream.new(PeerCastStation::Core::OutputStreamType.play))
    source.broadcast_host_info
    assert_equal(:post, source.log[0][0])
    broadcast = source.log[0][2]
    assert_equal(PCP_BCST, broadcast.name.to_s)
    assert_equal(PCSCore::BroadcastGroup.trackers, broadcast.children.GetBcstGroup)
    assert_not_nil(broadcast.children.GetHost)
    assert_equal(0, @channel.output_streams[0].log.size)
  end

	def test_connect
    source = PeerCastStation::PCP::PCPSourceStream.new(@peercast, @channel, @channel.source_uri)
    assert(!source.is_connected)
    connected = 0
    server = MockPCPServer.new('localhost', 7146) {|sock| connected += 1 }
    host = PeerCastStation::Core::HostBuilder.new
    host.global_end_point = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7146)
    sleep(0.1)
    assert(source.connect(host.to_host))
    assert(source.is_connected)
    assert_not_nil(source.uphost)
    server.close
    assert_equal(1, connected)
	end

  def test_connect_failed
    source = PeerCastStation::PCP::PCPSourceStream.new(@peercast, @channel, @channel.source_uri)
    host = PeerCastStation::Core::HostBuilder.new
    host.global_end_point = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7146)
    assert(!source.connect(host.to_host))
    assert(!source.is_connected)
  end

  def test_close
    source = PeerCastStation::PCP::PCPSourceStream.new(@peercast, @channel, @channel.source_uri)
    assert(!source.is_connected)
    server = MockPCPServer.new('localhost', 7146)
    host = PeerCastStation::Core::HostBuilder.new
    host.global_end_point = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7146)
    sleep(0.1)
    assert(source.connect(host.to_host))
    assert(source.is_connected)
    source.close(PeerCastStation::PCP::CloseReason.user_shutdown)
    assert(!source.is_connected)
    server.close
  end

  def test_set_close
    source = PeerCastStation::PCP::PCPSourceStream.new(@peercast, @channel, @channel.source_uri)
    assert_nil(source.state)
    source.stop
    assert_nil(source.state)

    source.state = MockStreamState.new
    source.stop
    assert_kind_of(PeerCastStation::PCP::PCPSourceClosedState, source.state)
    assert_equal(PeerCastStation::PCP::CloseReason.user_shutdown, source.state.close_reason)
  end

  def test_reconnect
    source = PeerCastStation::PCP::PCPSourceStream.new(@peercast, @channel, @channel.source_uri)
    assert_nil(source.state)
    source.reconnect
    assert_nil(source.state)

    source.state = MockStreamState.new
    source.reconnect
    assert_kind_of(PeerCastStation::PCP::PCPSourceClosedState, source.state)
    assert_equal(PeerCastStation::PCP::CloseReason.user_reconnect, source.state.close_reason)

    source.stop
  end

  def test_ignore_host
    source = PeerCastStation::PCP::PCPSourceStream.new(@peercast, @channel, @channel.source_uri)
    host = PeerCastStation::Core::HostBuilder.new.to_host
    assert_equal(0, @channel.ignored_hosts.count)
    source.ignore_host(host)
    assert_equal(1, @channel.ignored_hosts.count)
    assert(@channel.ignored_hosts.include?(host))
  end

  def test_send_relay_request
    source = TestPCPSourceStreamNoSend.new(@peercast, @channel, @channel.source_uri)
    source.send_relay_request
    assert(source.log)
    assert_equal(1, source.log.size)
    assert_equal(:send, source.log[0][0])
    assert_equal("GET /channel/#{@channel.ChannelID.ToString('N')} HTTP/1.0",
                 source.log[0][1].to_a.pack('C*').split(/\r\n/)[0])
    assert(source.log[0][1].to_a.pack('C*').split(/\r\n/).include?("x-peercast-pcp:1"))
  end

  def test_send_pcp_helo
    source = TestPCPSourceStreamNoSend.new(@peercast, @channel, @channel.source_uri)
    @peercast.is_firewalled = nil
    source.SendPCPHelo
    assert(source.log)
    assert_equal(1, source.log.size)
    assert_equal(:send, source.log[0][0])
    helo = source.log[0][1]
    assert_equal(id4(PCP_HELO), helo.name)
    assert(helo.has_children)
    assert_not_nil(helo.children.GetHeloSessionID)
    assert_not_nil(helo.children.GetHeloAgent)
    assert_not_nil(helo.children.GetHeloVersion)
    assert_equal(@peercast.local_end_point.port, helo.children.GetHeloPing)
    assert_nil(helo.children.GetHeloPort)
    source.log.clear

    @peercast.is_firewalled = true
    source.SendPCPHelo
    helo = source.log[0][1]
    assert_nil(helo.children.GetHeloPing)
    assert_nil(helo.children.GetHeloPort)
    source.log.clear

    @peercast.is_firewalled = false
    source.SendPCPHelo
    helo = source.log[0][1]
    assert_nil(helo.children.GetHeloPing)
    assert_equal(@peercast.local_end_point.port, helo.children.GetHeloPort)
    source.log.clear
  end

  def test_process_state
    state = MockStreamState.new
    source = TestPCPSourceStreamNoSend.new(@peercast, @channel, @channel.source_uri)
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
    source = TestPCPSourceStreamNoSend.new(@peercast, @channel, @channel.source_uri)
    source.post(
      PeerCastStation::Core::HostBuilder.new.to_host,
      PeerCastStation::Core::Atom.new(id4('test'), 'hogehoge'.to_clr_string))
    source.process_events
    assert(source.log)
    assert_equal(1, source.log.size)
    assert_equal(:send,       source.log[0][0])
    assert_equal(id4('test'), source.log[0][1].name)
    assert_equal('hogehoge',  source.log[0][1].get_string.to_s)
  end

  def test_pcp_bcst
    source = TestPCPSourceStream.new(@peercast, @channel, @channel.source_uri)
    ok = 0
    source.on_pcp_ok = proc {
      ok += 1
    }
    output = MockOutputStream.new
    @channel.add_output_stream(output)
    bcst = PeerCastStation::Core::Atom.with_children(PeerCastStation::Core::Atom.PCP_BCST) {|children|
      children.SetBcstTTL(11)
      children.SetBcstHops(0)
      children.SetBcstFrom(@session_id)
      children.SetBcstGroup(PeerCastStation::Core::BroadcastGroup.relays)
      children.SetBcstChannelID(@channel_id)
      children.SetBcstVersion(1218)
      children.SetBcstVersionVP(27)
      children.SetOk(42)
    }
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
    source = TestPCPSourceStream.new(@peercast, @channel, @channel.source_uri)
    ok = 0
    source.on_pcp_ok = proc {
      ok += 1
    }
    output = MockOutputStream.new
    @channel.add_output_stream(output)
    
    bcst = PeerCastStation::Core::Atom.with_children(PeerCastStation::Core::Atom.PCP_BCST) {|children|
      children.SetBcstTTL(11)
      children.SetBcstHops(0)
      children.SetBcstFrom(@session_id)
      children.SetBcstDest(@peercast.SessionID)
      children.SetBcstGroup(PeerCastStation::Core::BroadcastGroup.relays)
      children.SetBcstChannelID(@channel_id)
      children.SetBcstVersion(1218)
      children.SetBcstVersionVP(27)
      children.SetOk(42)
    }
    source.OnPCPBcst(bcst)

    post_log = output.log.select {|log| log[0]==:post }
    assert_equal(0, post_log.size)
    assert_equal(1, ok)
    post_log = source.log.select {|log| log[0]==:post }
    assert_equal(0, post_log.size)
  end

  def test_pcp_bcst_no_ttl
    source = TestPCPSourceStream.new(@peercast, @channel, @channel.source_uri)
    ok = 0
    source.on_pcp_ok = proc {
      ok += 1
    }
    output = MockOutputStream.new
    @channel.add_output_stream(output)
    
    bcst = PeerCastStation::Core::Atom.with_children(PeerCastStation::Core::Atom.PCP_BCST) {|children|
      children.SetBcstTTL(1)
      children.SetBcstHops(0)
      children.SetBcstFrom(@session_id)
      children.SetBcstGroup(PeerCastStation::Core::BroadcastGroup.relays)
      children.SetBcstChannelID(@channel_id)
      children.SetBcstVersion(1218)
      children.SetBcstVersionVP(27)
      children.SetOk(42)
    }
    source.OnPCPBcst(bcst)

    post_log = output.log.select {|log| log[0]==:post }
    assert_equal(0, post_log.size)
    assert_equal(1, ok)
    post_log = source.log.select {|log| log[0]==:post }
    assert_equal(0, post_log.size)
  end

  def test_pcp_chan
    source = TestPCPSourceStream.new(@peercast, @channel, @channel.source_uri)
    ok = 0
    source.on_pcp_ok = proc { ok += 1 }
    chan = PeerCastStation::Core::Atom.with_children(id4(PCP_CHAN)) {|children|
      children.SetOk(42)
    }
    assert_nil(source.OnPCPChan(chan))
    assert_equal(1, ok)
  end

  def test_pcp_chan_pkt
    source = TestPCPSourceStream.new(@peercast, @channel, @channel.source_uri)
    assert_nil(@channel.content_header)
    assert_equal(0, @channel.contents.count)
    chan_pkt = PeerCastStation::Core::Atom.with_children(id4(PCP_CHAN_PKT)) {|children|
      children.SetChanPktType(PeerCastStation::Core::Atom.PCP_CHAN_PKT_TYPE_HEAD)
      children.SetChanPktData('foobar')
    }
    assert_nil(source.OnPCPChanPkt(chan_pkt))
    sleep(0.1)
    assert(@channel.content_header)
    assert_equal(0,        @channel.content_header.position)
    assert_equal('foobar', @channel.content_header.data.to_a.pack('C*'))
    assert_equal(0, @channel.contents.count)

    chan_pkt = PeerCastStation::Core::Atom.with_children(id4(PCP_CHAN_PKT)) {|children|
      children.SetChanPktType(PeerCastStation::Core::Atom.PCP_CHAN_PKT_TYPE_DATA)
      children.SetChanPktPos(6)
      children.SetChanPktData('hogefuga')
    }
    assert_nil(source.OnPCPChanPkt(chan_pkt))
    sleep(0.1)
    assert_equal(1,          @channel.contents.count)
    assert_equal('hogefuga', @channel.contents.newest.data.to_a.pack('C*'))
    assert_equal(6,          @channel.contents.newest.position)

    chan_pkt = PeerCastStation::Core::Atom.with_children(id4(PCP_CHAN_PKT)) {|children|
      children.SetChanPktType(PeerCastStation::Core::Atom.PCP_CHAN_PKT_TYPE_DATA)
      children.SetChanPktPos(0xFFFFFFFF)
      children.SetChanPktData('hogefuga')
    }
    assert_nil(source.OnPCPChanPkt(chan_pkt))
    sleep(0.1)
    assert_equal(2,          @channel.contents.count)
    assert_equal('hogefuga', @channel.contents.newest.data.to_a.pack('C*'))
    assert_equal(0xFFFFFFFF, @channel.contents.newest.position)

    chan_pkt = PeerCastStation::Core::Atom.with_children(id4(PCP_CHAN_PKT)) {|children|
      children.SetChanPktType(PeerCastStation::Core::Atom.PCP_CHAN_PKT_TYPE_DATA)
      children.SetChanPktPos(10)
      children.SetChanPktData('hogefuga')
    }
    assert_nil(source.OnPCPChanPkt(chan_pkt))
    sleep(0.1)
    assert_equal(3,              @channel.contents.count)
    assert_equal('hogefuga',     @channel.contents.newest.data.to_a.pack('C*'))
    assert_equal(0x100000000+10, @channel.contents.newest.position)

    chan_pkt = PeerCastStation::Core::Atom.with_children(id4(PCP_CHAN_PKT)) {|children|
      children.SetChanPktType(PeerCastStation::Core::Atom.PCP_CHAN_PKT_TYPE_META)
      children.SetChanPktPos(10000)
      children.SetChanPktData('meta')
    }
    assert_nil(source.OnPCPChanPkt(chan_pkt))
    sleep(0.1)
    assert_equal(0,              @channel.content_header.position)
    assert_equal(0x100000000+10, @channel.contents.newest.position)

    chan_pkt = PeerCastStation::Core::Atom.with_children(id4(PCP_CHAN_PKT)) {|children|
      children.SetChanPktType(PeerCastStation::Core::Atom.PCP_CHAN_PKT_TYPE_HEAD)
      children.SetChanPktPos(20)
      children.SetChanPktData('foobar')
    }
    assert_nil(source.OnPCPChanPkt(chan_pkt))
    sleep(0.1)
    assert(@channel.content_header)
    assert_equal(0x100000000+20, @channel.content_header.position)
    assert_equal('foobar',       @channel.content_header.data.to_a.pack('C*'))
  end

  def test_pcp_chan_info
    source = TestPCPSourceStream.new(@peercast, @channel, @channel.source_uri)
    assert_nil(@channel.channel_info.name)
    assert_equal(0, @channel.channel_info.extra.count)

    chan_info = PeerCastStation::Core::Atom.with_children(id4(PCP_CHAN_INFO)) {|children|
      children.SetChanInfoBitrate(7144)
      children.SetChanInfoURL('http://example.com')
      children.SetChanInfoType('WMV')
      children.SetChanInfoGenre('Genre')
      children.SetChanInfoDesc('Desc')
      children.SetChanInfoComment('Comment')
    }
    assert_nil(source.OnPCPChanInfo(chan_info))
    sleep(0.1)
    assert_nil(@channel.channel_info.name)
    assert_equal(6, @channel.channel_info.extra.count)
    assert_equal(7144,                 @channel.channel_info.Bitrate)
    assert_equal('http://example.com', @channel.channel_info.URL)
    assert_equal('WMV',                @channel.channel_info.ContentType)
    assert_equal('Genre',              @channel.channel_info.Genre)
    assert_equal('Desc',               @channel.channel_info.Desc)
    assert_equal('Comment',            @channel.channel_info.Comment)

    chan_info = PeerCastStation::Core::Atom.with_children(id4(PCP_CHAN_INFO)) {|children|
      children.SetChanInfoName('foobar')
      children.SetChanInfoType('OGM')
    }
    assert_nil(source.OnPCPChanInfo(chan_info))
    sleep(0.1)
    assert_equal('foobar', @channel.channel_info.name)
    assert_equal('OGM',    @channel.channel_info.content_type)
    assert_equal(2, @channel.channel_info.extra.count)
  end

  def test_pcp_chan_track
    source = TestPCPSourceStream.new(@peercast, @channel, @channel.source_uri)
    assert_nil(@channel.channel_track.name)
    assert_equal(0, @channel.channel_track.extra.count)

    chan_track = PeerCastStation::Core::Atom.with_children(id4(PCP_CHAN_TRACK)) {|children|
      children.SetChanTrackURL('http://example.com')
      children.SetChanTrackTitle('Title')
      children.SetChanTrackAlbum('Album')
      children.SetChanTrackCreator('Creator')
    }
    assert_nil(source.OnPCPChanTrack(chan_track))
    sleep(0.1)
    assert_equal(4, @channel.channel_track.extra.count)
    assert_equal('http://example.com', @channel.channel_track.URL)
    assert_equal('Title',              @channel.channel_track.Name)
    assert_equal('Album',              @channel.channel_track.Album)
    assert_equal('Creator',            @channel.channel_track.Creator)

    chan_track = PeerCastStation::Core::Atom.with_children(id4(PCP_CHAN_TRACK)) {|children|
      children.SetChanTrackURL('http://example.com')
      children.SetChanTrackTitle('Title')
    }
    assert_nil(source.OnPCPChanTrack(chan_track))
    sleep(0.1)
    assert_equal(2, @channel.channel_track.extra.count)
    assert_equal('http://example.com', @channel.channel_track.URL)
    assert_equal('Title',              @channel.channel_track.Name)
  end

  def test_pcp_helo
    source = TestPCPSourceStreamNoSend.new(@peercast, @channel, @channel.source_uri)
    helo = PeerCastStation::Core::Atom.with_children(id4(PCP_HELO)) {|children|
      children.SetHeloSessionID(@session_id)
      children.SetHeloAgent('IronRuby')
      children.SetHeloVersion(1218)
    }
    assert_nil(source.OnPCPHelo(helo))

    assert(source.log)
    assert_equal(1, source.log.size)
    assert_equal(:send, source.log[0][0])
    assert_equal(id4(PCP_OLEH), source.log[0][1].name)
    assert(source.log[0][1].children.GetHeloAgent)
    assert_equal(@peercast.SessionID,            source.log[0][1].children.GetHeloSessionID)
    assert_equal(@peercast.local_end_point.port, source.log[0][1].children.GetHeloPort)
    assert_equal(1218,                           source.log[0][1].children.GetHeloVersion)
  end

  def test_pcp_oleh
    source = TestPCPSourceStream.new(@peercast, @channel, @channel.source_uri)
    addr = System::Net::IPAddress.parse('192.168.12.34')
    oleh = PeerCastStation::Core::Atom.with_children(id4(PCP_OLEH)) {|children|
      children.SetHeloRemoteIP(addr)
      children.SetHeloSessionID(@session_id)
      children.SetHeloAgent('IronRuby')
      children.SetHeloVersion(1218)
    }
    assert_nil(@peercast.is_firewalled)
    assert_nil(@peercast.global_address)
    assert_nil(source.OnPCPOleh(oleh))
    sleep(0.1)
    assert_nil(@peercast.is_firewalled)
    assert_equal(addr, @peercast.global_address)
    assert_not_nil(@peercast.global_end_point)
    
    oleh = PeerCastStation::Core::Atom.with_children(id4(PCP_OLEH)) {|children|
      children.SetHeloRemoteIP(addr)
      children.SetHeloSessionID(@session_id)
      children.SetHeloAgent('IronRuby')
      children.SetHeloVersion(1218)
      children.SetHeloPort(0)
    }
    assert_nil(source.OnPCPOleh(oleh))
    sleep(0.1)
    assert(@peercast.is_firewalled)

    oleh = PeerCastStation::Core::Atom.with_children(id4(PCP_OLEH)) {|children|
      children.SetHeloRemoteIP(addr)
      children.SetHeloSessionID(@session_id)
      children.SetHeloAgent('IronRuby')
      children.SetHeloVersion(1218)
      children.SetHeloPort(@peercast.local_end_point.port)
    }
    assert_nil(source.OnPCPOleh(oleh))
    sleep(0.1)
    assert(!@peercast.is_firewalled)
  end

  def test_pcp_host
    source = TestPCPSourceStream.new(@peercast, @channel, @channel.source_uri)
    assert_equal(0, @channel.nodes.count)
    node = @channel.nodes.find {|n| n.SessionID.eql?(@peercast.SessionID) }
    assert_nil(node)

    host = source.create_host_packet
    assert_nil(source.OnPCPHost(host))
    sleep(0.1)

    assert_equal(1, @channel.nodes.count)
    node = @channel.nodes.find {|n| n.SessionID.eql?(@peercast.SessionID) }
    assert(node)
    assert_equal(host.children.GetHostNumListeners, node.direct_count)
    assert_equal(host.children.GetHostNumRelays,    node.relay_count)
    flags1 = host.children.GetHostFlags1
    assert_equal((flags1 & PeerCastStation::Core::PCPHostFlags1.firewalled)!=PeerCastStation::Core::PCPHostFlags1.none, node.is_firewalled)
    assert_equal((flags1 & PeerCastStation::Core::PCPHostFlags1.tracker)   !=PeerCastStation::Core::PCPHostFlags1.none, node.is_tracker)
    assert_equal((flags1 & PeerCastStation::Core::PCPHostFlags1.relay)     ==PeerCastStation::Core::PCPHostFlags1.none, node.is_relay_full)
    assert_equal((flags1 & PeerCastStation::Core::PCPHostFlags1.direct)    ==PeerCastStation::Core::PCPHostFlags1.none, node.is_direct_full)
    assert_equal((flags1 & PeerCastStation::Core::PCPHostFlags1.receiving) !=PeerCastStation::Core::PCPHostFlags1.none, node.is_receiving) 
    assert_equal((flags1 & PeerCastStation::Core::PCPHostFlags1.control_in)==PeerCastStation::Core::PCPHostFlags1.none, node.is_control_full)
    assert_not_nil(node.global_end_point)
  end

  def test_pcp_quit
    source = TestPCPSourceStream.new(@peercast, @channel, @channel.source_uri)
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
    @peercast   = PeerCastStation::Core::PeerCast.new
    @peercast.start_listen(@endpoint)
    @channel_id = System::Guid.parse('531dc8dfc7fb42928ac2c0a626517a87')
    @channel    = PeerCastStation::Core::Channel.new(@peercast, @channel_id, System::Uri.new('http://127.0.0.1:7146'))
    @source     = TestPCPSourceStreamNoIgnore.new(@peercast, @channel, @channel.source_uri)
  end
  
  def teardown
    @peercast.stop if @peercast
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
      @source.uphost = PCSCore::HostBuilder.new.to_host
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

class TC_PCPSourceConnectState < Test::Unit::TestCase
  class TestPCPSourceStreamNoConnect < PeerCastStation::PCP::PCPSourceStream
    def self.new(core, channel, tracker)
      inst = super
      inst.instance_eval do
        @log = []
        @connection_result = true
      end
      inst
    end
    attr_accessor :log, :connection_result

    def Connect(host)
      @log << [:connect, host]
      @connection_result
    end
  end

  def setup
    @session_id = System::Guid.new_guid
    @endpoint   = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7147)
    @peercast   = PeerCastStation::Core::PeerCast.new
    @peercast.start_listen(@endpoint)
    @channel_id = System::Guid.parse('531dc8dfc7fb42928ac2c0a626517a87')
    @channel    = PeerCastStation::Core::Channel.new(@peercast, @channel_id, System::Uri.new('http://127.0.0.1:7146'))
    @source     = TestPCPSourceStreamNoConnect.new(@peercast, @channel, @channel.source_uri)
  end
  
  def teardown
    @peercast.stop if @peercast
  end
  
  def test_construct
    host = PCSCore::HostBuilder.new.to_host
    state = PCSPCP::PCPSourceConnectState.new(@source, host)
    assert_equal(@source, state.owner)
    assert_equal(host,    state.host)
  end

  def test_process
    assert_equal(0, @source.log.size)

    state = PCSPCP::PCPSourceConnectState.new(@source, nil)
    next_state = state.process
    assert(next_state)
    assert_kind_of(PCSPCP::PCPSourceClosedState, next_state)
    assert_equal(PCSPCP::CloseReason.node_not_found, next_state.close_reason)
    assert_equal(0, @source.log.count)
    @source.log.clear

    host = PCSCore::HostBuilder.new.to_host
    state = PCSPCP::PCPSourceConnectState.new(@source, host)
    @source.connection_result = true
    next_state = state.process
    assert(next_state)
    assert_kind_of(PCSPCP::PCPSourceRelayRequestState, next_state)
    assert_equal(1, @source.log.count)
    assert_equal(:connect, @source.log[0][0])
    @source.log.clear

    host = PCSCore::HostBuilder.new.to_host
    state = PCSPCP::PCPSourceConnectState.new(@source, host)
    @source.connection_result = false
    next_state = state.process
    assert_kind_of(PCSPCP::PCPSourceClosedState, next_state)
    assert_equal(PCSPCP::CloseReason.connection_error, next_state.close_reason)
    assert_equal(1, @source.log.count)
    assert_equal(:connect, @source.log[0][0])
    @source.log.clear
  end
end

class TC_PCPSourceRelayRequestState < Test::Unit::TestCase
  class TestPCPSourceStreamNoSendRequest < PeerCastStation::PCP::PCPSourceStream
    def self.new(core, channel, tracker)
      inst = super
      inst.instance_eval do
        @log = []
      end
      inst
    end
    attr_accessor :log

    def SendRelayRequest
      @log << [:send_relay_request]
    end
  end

  def setup
    @session_id = System::Guid.new_guid
    @endpoint   = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7147)
    @peercast   = PeerCastStation::Core::PeerCast.new
    @peercast.start_listen(@endpoint)
    @channel_id = System::Guid.parse('531dc8dfc7fb42928ac2c0a626517a87')
    @channel    = PeerCastStation::Core::Channel.new(@peercast, @channel_id, System::Uri.new('http://127.0.0.1:7146'))
    @source     = TestPCPSourceStreamNoSendRequest.new(@peercast, @channel, @channel.source_uri)
  end
  
  def teardown
    @peercast.stop if @peercast
  end
  
  def test_construct
    state = PCSPCP::PCPSourceRelayRequestState.new(@source)
    assert_equal(@source, state.owner)
  end

  def test_process
    assert_equal(0, @source.log.size)

    state = PCSPCP::PCPSourceRelayRequestState.new(@source)
    next_state = state.process
    assert(next_state)
    assert_kind_of(PCSPCP::PCPSourceRecvRelayResponseState, next_state)
    assert_equal(1, @source.log.count)
    assert_equal(:send_relay_request, @source.log[0][0])
    @source.log.clear
  end
end

class TC_PCPSourceRecvRelayResponseState < Test::Unit::TestCase
  class TestPCPSourceStreamNoRecv < PeerCastStation::PCP::PCPSourceStream
    def self.new(core, channel, tracker)
      inst = super
      inst.instance_eval do
        @log = []
        @is_connected = false
        @relay_request_response = nil
      end
      inst
    end
    attr_accessor :log, :relay_request_response, :is_connected

    def IsConnected
      @is_connected
    end

    def RecvRelayRequestResponse
      @log << [:recv_relay_request_response]
      @relay_request_response
    end
  end

  def setup
    @session_id = System::Guid.new_guid
    @endpoint   = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7147)
    @peercast   = PeerCastStation::Core::PeerCast.new
    @peercast.start_listen(@endpoint)
    @channel_id = System::Guid.parse('531dc8dfc7fb42928ac2c0a626517a87')
    @channel    = PeerCastStation::Core::Channel.new(@peercast, @channel_id, System::Uri.new('http://127.0.0.1:7146'))
    @source     = TestPCPSourceStreamNoRecv.new(@peercast, @channel, @channel.source_uri)
  end
  
  def teardown
    @peercast.stop if @peercast
  end
  
  def test_construct
    state = PCSPCP::PCPSourceRecvRelayResponseState.new(@source)
    assert_equal(@source, state.owner)
  end

  def test_process
    assert_equal(0, @source.log.size)

    @source.relay_request_response = nil
    @source.is_connected = false
    state = PCSPCP::PCPSourceRecvRelayResponseState.new(@source)
    next_state = state.process
    assert(next_state)
    assert_kind_of(PCSPCP::PCPSourceClosedState, next_state)
    assert_equal(1, @source.log.size)
    assert_equal(:recv_relay_request_response, @source.log[0][0])
    @source.log.clear

    @source.is_connected = true
    state = PCSPCP::PCPSourceRecvRelayResponseState.new(@source)
    next_state = state.process
    assert(next_state)
    assert_equal(state, next_state)
    assert_equal(1, @source.log.size)
    assert_equal(:recv_relay_request_response, @source.log[0][0])
    @source.log.clear

    @source.relay_request_response = PCSPCP::RelayRequestResponse.new(
      System::Array[System::String].new([
        'HTTP/1.1 200 OK',
        'x-peercast-pcp:1',
        'x-peercast-pos: 200000000',
        'Content-Type: application/x-peercast-pcp',
      ])
    )
    next_state = state.process
    assert(next_state)
    assert_kind_of(PCSPCP::PCPSourcePCPHandshakeState, next_state)
    assert_equal(1, @source.log.size)
    assert_equal(:recv_relay_request_response, @source.log[0][0])
    @source.log.clear

    @source.relay_request_response = PCSPCP::RelayRequestResponse.new(
      System::Array[System::String].new([
        'HTTP/1.1 503 Unavailable',
        'x-peercast-pcp:1',
        'x-peercast-pos: 200000000',
        'Content-Type: application/x-peercast-pcp',
      ])
    )
    next_state = state.process
    assert(next_state)
    assert_kind_of(PCSPCP::PCPSourcePCPHandshakeState, next_state)
    assert_equal(1, @source.log.size)
    assert_equal(:recv_relay_request_response, @source.log[0][0])
    @source.log.clear

    @source.relay_request_response = PCSPCP::RelayRequestResponse.new(
      System::Array[System::String].new([
        'HTTP/1.1 404 Not found',
        'x-peercast-pcp:1',
        'x-peercast-pos: 200000000',
        'Content-Type: application/x-peercast-pcp',
      ])
    )
    next_state = state.process
    assert(next_state)
    assert_kind_of(PCSPCP::PCPSourceClosedState, next_state)
    assert_equal(PCSPCP::CloseReason.channel_not_found, next_state.close_reason)
    assert_equal(1, @source.log.size)
    assert_equal(:recv_relay_request_response, @source.log[0][0])
    @source.log.clear

    @source.relay_request_response = PCSPCP::RelayRequestResponse.new(
      System::Array[System::String].new([
        'HTTP/1.1 400 Bad Request',
        'x-peercast-pcp:1',
        'x-peercast-pos: 200000000',
        'Content-Type: application/x-peercast-pcp',
      ])
    )
    next_state = state.process
    assert(next_state)
    assert_kind_of(PCSPCP::PCPSourceClosedState, next_state)
    assert_equal(PCSPCP::CloseReason.access_denied, next_state.close_reason)
    assert_equal(1, @source.log.size)
    assert_equal(:recv_relay_request_response, @source.log[0][0])
    @source.log.clear
  end
end

class TC_PCPSourcePCPHandshakeState < Test::Unit::TestCase
  class TestPCPSourceStreamNoSendPCPHelo < PeerCastStation::PCP::PCPSourceStream
    def self.new(core, channel, tracker)
      inst = super
      inst.instance_eval do
        @log = []
        @recv_atom = nil
      end
      inst
    end
    attr_accessor :log, :recv_atom

    def RecvAtom
      @log << [:recv_atom]
      @recv_atom
    end

    def SendPCPHelo
      @log << [:send_pcp_helo]
    end
  end

  def setup
    @session_id = System::Guid.new_guid
    @endpoint   = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7147)
    @peercast   = PeerCastStation::Core::PeerCast.new
    @peercast.start_listen(@endpoint)
    @channel_id = System::Guid.parse('531dc8dfc7fb42928ac2c0a626517a87')
    @channel    = PeerCastStation::Core::Channel.new(@peercast, @channel_id, System::Uri.new('http://127.0.0.1:7146'))
    @source     = TestPCPSourceStreamNoSendPCPHelo.new(@peercast, @channel, @channel.source_uri)
  end
  
  def teardown
    @peercast.stop if @peercast
  end
  
  def test_construct
    state = PCSPCP::PCPSourcePCPHandshakeState.new(@source)
    assert_equal(@source, state.owner)
  end

  def test_process
    assert_equal(0, @source.log.size)

    state = PCSPCP::PCPSourcePCPHandshakeState.new(@source)
    next_state = state.process
    assert_same(state, next_state)
    assert_equal(2, @source.log.count)
    assert_equal(:send_pcp_helo, @source.log[0][0])
    assert_equal(:recv_atom,     @source.log[1][0])
    @source.log.clear

    next_state = state.process
    assert_same(state, next_state)
    assert_equal(1, @source.log.count)
    assert_equal(:recv_atom,     @source.log[0][0])
    @source.log.clear

    oleh = PCSCore::Atom.with_children(PCSCore::Atom.PCP_OLEH) {|children|
      children.SetHeloRemoteIP(System::Net::IPAddress.any)
      children.SetHeloSessionID(@session_id)
      children.SetHeloAgent('IronRuby')
      children.SetHeloVersion(1218)
    }
    @source.recv_atom = oleh
    next_state = state.process
    assert_kind_of(PCSPCP::PCPSourceReceivingState, next_state)
    assert_equal(1, @source.log.count)
    assert_equal(:recv_atom, @source.log[0][0])
    @source.log.clear

    quit = PCSCore::Atom.new(PCSCore::Atom.PCP_QUIT, PCSCore::Atom.PCP_ERROR_QUIT)
    @source.recv_atom = quit
    next_state = state.process
    assert_kind_of(PCSPCP::PCPSourceClosedState, next_state)
    assert_equal(1, @source.log.count)
    assert_equal(:recv_atom, @source.log[0][0])
    @source.log.clear
  end
end

class TC_PCPSourceReceivingState < Test::Unit::TestCase
  class TestPCPSourceStreamReceive < PeerCastStation::PCP::PCPSourceStream
    def self.new(core, channel, tracker)
      inst = super
      inst.instance_eval do
        @log = []
        @recv_atom = nil
        @next_state = nil
      end
      inst
    end
    attr_accessor :log, :recv_atom, :next_state

    def RecvAtom
      @log << [:recv_atom]
      @recv_atom
    end

    def BroadcastHostInfo
      @log << [:broadcast_host_info]
    end

    def ProcessAtom(atom)
      @log << [:process_atom, atom]
      @next_state
    end
  end

  def setup
    @session_id = System::Guid.new_guid
    @endpoint   = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7147)
    @peercast   = PeerCastStation::Core::PeerCast.new
    @peercast.start_listen(@endpoint)
    @channel_id = System::Guid.parse('531dc8dfc7fb42928ac2c0a626517a87')
    @channel    = PeerCastStation::Core::Channel.new(@peercast, @channel_id, System::Uri.new('http://127.0.0.1:7146'))
    @source     = TestPCPSourceStreamReceive.new(@peercast, @channel, @channel.source_uri)
  end
  
  def teardown
    @peercast.stop if @peercast
  end
  
  def test_construct
    state = PCSPCP::PCPSourceReceivingState.new(@source)
    assert_equal(@source, state.owner)
    assert_equal(0,       state.LastHostInfoUpdated)
  end

  def test_process
    assert_equal(0, @source.log.size)

    @source.recv_atom = nil
    state = PCSPCP::PCPSourceReceivingState.new(@source)
    state.LastHostInfoUpdated = System::Environment.TickCount
    next_state = state.process
    assert_equal(state, next_state)
    assert_equal(1, @source.log.count)
    assert_equal(:recv_atom, @source.log[0][0])
    @source.log.clear

    @source.recv_atom = PCSCore::Atom.new(PCSCore::ID4.new('test'.to_clr_string), PCSCore::AtomCollection.new)
    @source.next_state = nil
    state = PCSPCP::PCPSourceReceivingState.new(@source)
    state.LastHostInfoUpdated = System::Environment.TickCount
    next_state = state.process
    assert_equal(state, next_state)
    assert_equal(2, @source.log.count)
    assert_equal(:recv_atom,        @source.log[0][0])
    assert_equal(:process_atom,     @source.log[1][0])
    assert_equal(@source.recv_atom, @source.log[1][1])
    @source.log.clear

    @source.recv_atom  = PCSCore::Atom.new(PCSCore::ID4.new('test'.to_clr_string), PCSCore::AtomCollection.new)
    @source.next_state = MockStreamState.new
    state = PCSPCP::PCPSourceReceivingState.new(@source)
    state.LastHostInfoUpdated = System::Environment.TickCount
    next_state = state.process
    assert_equal(@source.next_state, next_state)
    assert_equal(2, @source.log.count)
    assert_equal(:recv_atom,        @source.log[0][0])
    assert_equal(:process_atom,     @source.log[1][0])
    assert_equal(@source.recv_atom, @source.log[1][1])
    @source.log.clear

    @source.recv_atom  = PCSCore::Atom.new(PCSCore::ID4.new('test'.to_clr_string), PCSCore::AtomCollection.new)
    @source.next_state = MockStreamState.new
    state = PCSPCP::PCPSourceReceivingState.new(@source)
    @source.IsHostInfoUpdated = false
    t = System::Environment.tick_count - 120000
    state.LastHostInfoUpdated = t
    next_state = state.process
    assert_equal(@source.next_state, next_state)
    assert_equal(3, @source.log.count)
    assert_equal(:broadcast_host_info, @source.log[0][0])
    assert_equal(:recv_atom,           @source.log[1][0])
    assert_equal(:process_atom,        @source.log[2][0])
    assert_equal(@source.recv_atom,    @source.log[2][1])
    assert_not_equal(t, state.LastHostInfoUpdated)
    @source.log.clear
    next_state = state.process
    assert_equal(@source.next_state, next_state)
    assert_equal(2, @source.log.count)
    assert_equal(:recv_atom,           @source.log[0][0])
    assert_equal(:process_atom,        @source.log[1][0])
    assert_equal(@source.recv_atom,    @source.log[1][1])
    @source.log.clear

    @source.recv_atom  = nil
    @source.next_state = nil
    state = PCSPCP::PCPSourceReceivingState.new(@source)
    @source.IsHostInfoUpdated = true
    state.LastHostInfoUpdated = System::Environment.tick_count - 10000
    next_state = state.process
    assert_equal(state, next_state)
    assert_equal(2, @source.log.count)
    assert_equal(:broadcast_host_info, @source.log[0][0])
    assert_equal(:recv_atom,           @source.log[1][0])
    @source.log.clear

    @source.IsHostInfoUpdated = false
    state.LastHostInfoUpdated = System::Environment.tick_count - 10000
    next_state = state.process
    assert_equal(state, next_state)
    assert_equal(1, @source.log.count)
    assert_equal(:recv_atom, @source.log[0][0])
    @source.log.clear
  end
end

