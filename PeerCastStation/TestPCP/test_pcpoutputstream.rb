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
require 'test_pcp_common'
require 'pcp'
require 'timeout'

def read_atom(io)
  res = nil
  timeout(3) do
    res = PCP::Atom.read(io)
  end
  res
end

module TestPCP
  class TC_RelayRequest < Test::Unit::TestCase
    def test_construct
      data = System::Array[System::String].new([
        'GET /channel/9778E62BDC59DF56F9216D0387F80BF2 HTTP/1.1',
        'x-peercast-pcp:1',
        'x-peercast-pos: 200000000',
        'User-Agent: PeerCastStation/1.0',
        'foo:bar',
      ])
      res = PCSPCP::RelayRequest.new(data)
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
        res = PCSPCP::RelayRequestReader.read(data)
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
        res = PCSPCP::RelayRequestReader.read(data)
      }
    end
  end

  class TC_PCPOutputStreamFactory < Test::Unit::TestCase
    def setup
      @session_id = System::Guid.new_guid
      @endpoint   = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7147)
      @peercast   = PCSCore::PeerCast.new
      accepts =
        PeerCastStation::Core::OutputStreamType.metadata |
        PeerCastStation::Core::OutputStreamType.play |
        PeerCastStation::Core::OutputStreamType.relay |
        PeerCastStation::Core::OutputStreamType.interface
      @peercast.start_listen(@endpoint, accepts, accepts)
      @channel_id = System::Guid.parse('531dc8dfc7fb42928ac2c0a626517a87')
      @channel    = PCSCore::Channel.new(@peercast, @channel_id, System::Uri.new('http://localhost:7146'))
    end
    
    def teardown
      @peercast.stop if @peercast
    end
    
    def test_construct
      factory = PCSPCP::PCPOutputStreamFactory.new(@peercast)
      assert_equal(factory.Name, 'PCP')
      assert(factory.priority==0)
      assert(factory.respond_to?(:create_obj_ref))
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
      output_stream = factory.create(s, s, @endpoint, @channel_id, header)
      assert_kind_of(PCSPCP::PCPOutputStream, output_stream)
    end
  end

  class TC_PCPOutputStream < Test::Unit::TestCase
    class TestChannel < PCSCore::Channel
      def self.new(*args)
        instance = super
        instance.instance_eval do 
          @status = PCSCore::SourceStreamStatus.idle
          @is_relay_full = false
          @broadcasts = []
        end
        instance
      end
      attr_accessor :broadcasts

      def Status
        @status
      end
      
      def Status=(value)
        @status = value
      end

      def status=(value)
        @status = value
      end

      def IsRelayFull
        @is_relay_full
      end

      def IsRelayable(sink)
        !@is_relay_full
      end

      def IsRelayFull=(value)
        @is_relay_full = value
      end

      def is_relay_full=(value)
        @is_relay_full = value
      end

      def Broadcast(from, packet, group)
        @broadcasts << [from, packet, group]
        super
      end
    end

    def get_header(value)
      value.sub(/\r\n\r\n.*$/m, "\r\n\r\n")
    end

    def get_body(value)
      value.sub(/^.*?\r\n\r\n/m, '')
    end

    def read_http_header(io)
      header = io.read(4)
      until /\r\n\r\n$/=~header do
        header << io.read(1)
      end
      header
    end

    def assert_http_header(status, headers, value)
      header = get_header(value)
      values = header.split("\r\n")
      assert_match(%r;^HTTP/1.\d #{status} .*$;, values.shift)
      assert_match(%r;\r\n\r\n$;, header)
      header_values = {}
      values.each do |v|
        md = /^(\S+):(.*)$/.match(v)
        assert_not_nil(md)
        header_values[md[1]] = md[2].strip
      end
      headers.each do |k, v|
        assert_match(v, header_values[k])
      end
    end

    def setup
      @session_id = System::Guid.new_guid
      @endpoint   = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7147)
      @peercast   = PCSCore::PeerCast.new
      accepts =
        PeerCastStation::Core::OutputStreamType.metadata |
        PeerCastStation::Core::OutputStreamType.play |
        PeerCastStation::Core::OutputStreamType.relay |
        PeerCastStation::Core::OutputStreamType.interface
      @peercast.start_listen(@endpoint, accepts, accepts)
      @channel_id = System::Guid.parse('531dc8dfc7fb42928ac2c0a626517a87')
      @channel    = PCSCore::Channel.new(@peercast, @channel_id, System::Uri.new('http://localhost:7146'))
      chaninfo = PCSCore::AtomCollection.new
      chaninfo.set_chan_info_name('Test Channel')
      chaninfo.set_chan_info_bitrate(7144)
      chaninfo.set_chan_info_genre('Test')
      chaninfo.set_chan_info_desc('this is a test channel')
      chaninfo.SetChanInfoURL('http://www.example.com/')
      @channel.channel_info = PCSCore::ChannelInfo.new(chaninfo)
      @request = PCSPCP::RelayRequest.new(
        System::Array[System::String].new([
          'GET /channel/531dc8dfc7fb42928ac2c0a626517a87 HTTP/1.1',
          'x-peercast-pcp:1',
          'User-Agent: PeerCastStation/1.0',
          'foo:bar',
        ])
      )
      @pipe = PipeStream.new
      @input = @pipe.input
      @output = @pipe.output
      @stream = nil
    end
    
    def teardown
      if @stream then
        @stream.stop
        timeout(5) do
          sleep(0.1) until @stream.is_stopped
        end
      end
      @peercast.stop if @peercast
      @pipe.close if @pipe
    end
    
    def test_construct
      stream = PCSPCP::PCPOutputStream.new(
        @peercast,
        @input,
        @output,
        @endpoint,
        @channel,
        @request)
      assert_equal(@peercast, stream.PeerCast)
      assert_equal(@input,    stream.InputStream)
      assert_equal(@output,   stream.OutputStream)
      assert_equal(@channel,  stream.Channel)
      assert_equal(PCSCore::OutputStreamType.relay, stream.output_stream_type)
      assert(!stream.is_stopped)
      assert(stream.respond_to?(:create_obj_ref))
    end

    def test_upstream_rate
      endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
      stream = PCSPCP::PCPOutputStream.new(@peercast, @input, @output, endpoint, @channel, @request)
      assert_equal(7144, stream.upstream_rate)
    end

    def test_relay_full
      channel = TestChannel.new(@peercast, @channel_id, System::Uri.new('mock://localhost'))
      channel.is_relay_full = false
      stream = PCSPCP::PCPOutputStream.new(@peercast, @input, @output, @endpoint, channel, @request)
      assert(!stream.is_relay_full)
      channel.is_relay_full = true
      assert(!stream.is_relay_full)

      channel.is_relay_full = true
      stream = PCSPCP::PCPOutputStream.new(@peercast, @input, @output, @endpoint, channel, @request)
      assert(stream.is_relay_full)
      channel.is_relay_full = false
      assert(stream.is_relay_full)
    end

    def test_relay_not_found_channel_nil
      @stream = PCSPCP::PCPOutputStream.new(@peercast, @input, @output, @endpoint, nil, @request)
      @stream.start
      res = read_http_header(@pipe)
      assert_http_header(404, {}, res)
    end

    def test_relay_not_found_channel_not_ready
      channel = TestChannel.new(@peercast, System::Guid.empty, System::Uri.new('mock://localhost'))
      channel.status = PCSCore::SourceStreamStatus.idle
      @stream = PCSPCP::PCPOutputStream.new(@peercast, @input, @output, @endpoint, channel, @request)
      @stream.start
      res = read_http_header(@pipe)
      assert_http_header(404, {}, res)
    end

    def assert_pcp_oleh(session_id, ip, port, version, agent, atom)
      assert_equal(PCP::OLEH, atom.name)
      assert_equal(session_id.ToString('N'),  atom[PCP::HELO_SESSIONID].to_s)
      assert_equal(version,                   atom[PCP::HELO_VERSION])
      assert_equal(port,                      atom[PCP::HELO_PORT])
      assert_equal(ip.get_address_bytes.to_a, atom[PCP::HELO_REMOTEIP])
      assert_equal(agent,                     atom[PCP::HELO_AGENT])
    end

    def pcp_handshake(io)
      helo = PCP::Atom.new(PCP::HELO, [], nil)
      helo[PCP::HELO_SESSIONID] = PCP::GID.generate
      helo[PCP::HELO_VERSION]   = 1218
      helo[PCP::HELO_PORT]      = 7144
      helo[PCP::HELO_AGENT]     = File.basename(__FILE__)
      helo.write(io)
      assert_pcp_oleh(
                  @peercast.SessionID,
                  @endpoint.address,
                  7144,
                  1218,
                  @peercast.agent_name,
                  read_atom(io))
    end

    def assert_pcp_quit(code, atom)
      assert_equal(PCP::QUIT, atom.name)
      assert((atom.content.unpack('V')[0] & code)!=0) if code
    end

    def test_relay_relay_full_no_other_node
      channel = TestChannel.new(@peercast, @channel_id, System::Uri.new('mock://localhost'))
      channel.status = PCSCore::SourceStreamStatus.receiving
      channel.is_relay_full = true
      channel.content_header = PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.0), 0, 'header')
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.1),  6, 'content1'))
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.2), 14, 'content2'))
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.3), 22, 'content3'))
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.4), 30, 'content4'))
      @stream = PCSPCP::PCPOutputStream.new(@peercast, @input, @output, @endpoint, channel, @request)
      @stream.start
      header = read_http_header(@pipe)
      assert_http_header(503, {}, header)
      pcp_handshake(@pipe)
      assert_pcp_quit(PCP::ERROR_QUIT, read_atom(@pipe))
    end

    def test_relay_relay_full_with_other_nodes
      channel = TestChannel.new(@peercast, @channel_id, System::Uri.new('mock://localhost'))
      channel.status = PCSCore::SourceStreamStatus.receiving
      channel.is_relay_full = true
      16.times do |i|
        host = PCSCore::HostBuilder.new
        host.SessionID = System::Guid.new_guid
        host.LocalEndPoint = System::Net::IPEndPoint.new(
          System::Net::IPAddress.new([192,168,1,i+2].pack('C*')), 7144+i
        )
        host.GlobalEndPoint = System::Net::IPEndPoint.new(
          System::Net::IPAddress.new([123,123,123,i+1].pack('C*')), 7144+i
        )
        host.relay_count  = i*3+2
        host.direct_count = i*5+3
        host.extra.set_host_uptime(System::TimeSpan.new(143720+i*1234))
        host.extra.set_host_old_pos(i*3+2)
        host.extra.set_host_new_pos(i*5+3)
        host.extra.set_host_version(1218+i)
        host.extra.SetHostVersionVP(27+i)
        host.is_firewalled   = ((i+0) % 2)==1
        host.is_tracker      = ((i+1) % 2)==1
        host.is_relay_full   = ((i+2) % 2)==1
        host.is_direct_full  = ((i+3) % 2)==1
        host.is_receiving    = ((i+4) % 2)==1
        host.is_control_full = ((i+5) % 2)==1
        channel.add_node(host.to_host)
      end
      @stream = PCSPCP::PCPOutputStream.new(@peercast, @input, @output, @endpoint, channel, @request)
      @stream.start
      header = read_http_header(@pipe)
      assert_http_header(503, {'Content-Type' => 'application/x-peercast-pcp'}, header)
      pcp_handshake(@pipe)
      8.times do |i|
        host = read_atom(@pipe)
        assert_equal(PCP::HOST, host.name)
        node = channel.nodes.find {|n| n.SessionID.ToString('N')==host[PCP::HOST_ID].to_s }
        assert_equal(node.SessionID.ToString('N'),                         host[PCP::HOST_ID].to_s)
        assert_equal(2,                                                    host[PCP::HOST_IP].size)
        assert_equal(node.global_end_point.address.get_address_bytes.to_a, host[PCP::HOST_IP][0])
        assert_equal(node.local_end_point.address.get_address_bytes.to_a,  host[PCP::HOST_IP][1])
        assert_equal(node.global_end_point.port,                           host[PCP::HOST_PORT][0])
        assert_equal(node.local_end_point.port,                            host[PCP::HOST_PORT][1])
        assert_equal(channel.ChannelID.ToString('N'),                      host[PCP::HOST_CHANID].to_s)
        assert_equal(node.relay_count,                                     host[PCP::HOST_NUMR])
        assert_equal(node.direct_count,                                    host[PCP::HOST_NUML])
        assert_equal(node.extra.get_host_uptime.total_seconds.to_i,        host[PCP::HOST_UPTIME])
        assert_equal(node.extra.get_host_version,                          host[PCP::HOST_VERSION])
        assert_equal(node.extra.GetHostVersionVP,                          host[PCP::HOST_VERSION_VP])
        assert_nil(host[PCP::HOST_VERSION_EX_PREFIX])
        assert_nil(host[PCP::HOST_VERSION_EX_NUMBER])
        assert_nil(host[PCP::HOST_CLAP_PP])
        assert_equal(node.extra.get_host_old_pos, host[PCP::HOST_OLDPOS])
        assert_equal(node.extra.get_host_new_pos, host[PCP::HOST_NEWPOS])
        assert_equal(node.is_tracker,      (host[PCP::HOST_FLAGS1] & PCP::HOST_FLAGS1_TRACKER)!=0)
        assert_equal(node.is_relay_full,   (host[PCP::HOST_FLAGS1] & PCP::HOST_FLAGS1_RELAY)==0)
        assert_equal(node.is_direct_full,  (host[PCP::HOST_FLAGS1] & PCP::HOST_FLAGS1_DIRECT)==0)
        assert_equal(node.is_receiving,    (host[PCP::HOST_FLAGS1] & PCP::HOST_FLAGS1_RECV)!=0)
        assert_equal(node.is_control_full, (host[PCP::HOST_FLAGS1] & PCP::HOST_FLAGS1_CIN)==0)
        assert_equal(node.is_firewalled,   (host[PCP::HOST_FLAGS1] & PCP::HOST_FLAGS1_PUSH)!=0)
      end
      assert_pcp_quit(PCP::ERROR_QUIT, read_atom(@pipe))
    end

    def test_relay
      channel = TestChannel.new(@peercast, @channel_id, System::Uri.new('mock://localhost'))
      channel.status = PCSCore::SourceStreamStatus.receiving
      channel.is_relay_full = false
      @stream = PCSPCP::PCPOutputStream.new(@peercast, @input, @output, @endpoint, channel, @request)
      @stream.start
      header = read_http_header(@pipe)
      assert_http_header(200, {}, header)
      pcp_handshake(@pipe)
      channel.content_header = PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.0), 0, 'header')
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.1),  6, 'content1'))
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.2), 14, 'content2'))
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.3), 22, 'content3'))
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.4), 30, 'content4'))
      ok = read_atom(@pipe)
      assert_equal(PCP::OK, ok.name)
      assert_equal(1, ok.value)
      chan = read_atom(@pipe)
      assert_equal(PCP::CHAN, chan.name)
      assert_not_nil(chan[PCP::CHAN_INFO])
      assert_not_nil(chan[PCP::CHAN_TRACK])
      assert_not_nil(chan[PCP::CHAN_PKT])
      pkt = chan[PCP::CHAN_PKT]
      assert_equal(PCP::CHAN_PKT_HEAD, pkt[PCP::CHAN_PKT_TYPE])
      assert_equal(0,                 pkt[PCP::CHAN_PKT_POS])
      assert_equal('header',          pkt[PCP::CHAN_PKT_DATA])
      4.times do |i|
        chan = read_atom(@pipe)
        assert_equal(PCP::CHAN, chan.name)
        assert_nil(chan[PCP::CHAN_INFO])
        assert_nil(chan[PCP::CHAN_TRACK])
        assert_not_nil(chan[PCP::CHAN_PKT])
        pkt = chan[PCP::CHAN_PKT]
        assert_equal(PCP::CHAN_PKT_DATA, pkt[PCP::CHAN_PKT_TYPE])
        assert_equal(6+i*8,             pkt[PCP::CHAN_PKT_POS])
        assert_equal("content#{i+1}",   pkt[PCP::CHAN_PKT_DATA])
      end
      @stream.stop
      assert_pcp_quit(PCP::ERROR_QUIT, read_atom(@pipe))
    end

    def test_relay_with_splitting_large_packet
      channel = TestChannel.new(@peercast, @channel_id, System::Uri.new('mock://localhost'))
      channel.status = PCSCore::SourceStreamStatus.receiving
      channel.is_relay_full = false
      @stream = PCSPCP::PCPOutputStream.new(@peercast, @input, @output, @endpoint, channel, @request)
      @stream.start
      header = read_http_header(@pipe)
      assert_http_header(200, {}, header)
      pcp_handshake(@pipe)
      channel.content_header = PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.0), 0, 'header')
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.1),  6,         '1'*(15*1024)))
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.2),  6+15*1024, '2'*(16*1024)))
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.3),  6+31*1024, '3'*10))
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.4), 16+31*1024, '4'*(96*1024)))
      ok = read_atom(@pipe)
      chan = read_atom(@pipe)
      pos = 6
      [
        '1'*(15*1024),
        '2'*(15*1024),
        '2'*(1*1024),
        '3'*(10),
        '4'*(15*1024),
        '4'*(15*1024),
        '4'*(15*1024),
        '4'*(15*1024),
        '4'*(15*1024),
        '4'*(15*1024),
        '4'*(6*1024),
      ].each do |expected|
        chan = read_atom(@pipe)
        assert_equal(PCP::CHAN, chan.name)
        assert_nil(chan[PCP::CHAN_INFO])
        assert_nil(chan[PCP::CHAN_TRACK])
        assert_not_nil(chan[PCP::CHAN_PKT])
        pkt = chan[PCP::CHAN_PKT]
        assert_equal(PCP::CHAN_PKT_DATA, pkt[PCP::CHAN_PKT_TYPE])
        assert_equal(pos,                pkt[PCP::CHAN_PKT_POS])
        assert_equal(expected,           pkt[PCP::CHAN_PKT_DATA])
        pos += expected.bytesize
      end
      @stream.stop
      assert_pcp_quit(PCP::ERROR_QUIT, read_atom(@pipe))
    end

    def test_relay_with_stream_pos
      channel = TestChannel.new(@peercast, @channel_id, System::Uri.new('mock://localhost'))
      channel.status = PCSCore::SourceStreamStatus.receiving
      channel.is_relay_full = false
      channel.content_header = PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.0), 0, 'header')
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.1),  6, 'content1'))
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.2), 14, 'content2'))
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.3), 22, 'content3'))
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.4), 30, 'content4'))
      request = PCSPCP::RelayRequest.new(
        System::Array[System::String].new([
          'GET /channel/531dc8dfc7fb42928ac2c0a626517a87 HTTP/1.1',
          'x-peercast-pcp:1',
          'x-peercast-pos:22',
          'User-Agent: PeerCastStation/1.0',
          'foo:bar',
        ])
      )
      @stream = PCSPCP::PCPOutputStream.new(@peercast, @input, @output, @endpoint, channel, request)
      @stream.start
      header = read_http_header(@pipe)
      assert_http_header(200, {}, header)
      pcp_handshake(@pipe)
      ok = read_atom(@pipe)
      assert_equal(PCP::OK, ok.name)
      chan = read_atom(@pipe)
      assert_equal(PCP::CHAN, chan.name)
      assert_not_nil(chan[PCP::CHAN_INFO])
      assert_not_nil(chan[PCP::CHAN_TRACK])
      assert_not_nil(chan[PCP::CHAN_PKT])
      pkt = chan[PCP::CHAN_PKT]
      assert_equal(PCP::CHAN_PKT_HEAD, pkt[PCP::CHAN_PKT_TYPE])
      assert_equal(0,                 pkt[PCP::CHAN_PKT_POS])
      assert_equal('header',          pkt[PCP::CHAN_PKT_DATA])
      (2..3).each do |i|
        chan = read_atom(@pipe)
        assert_equal(PCP::CHAN, chan.name)
        assert_nil(chan[PCP::CHAN_INFO])
        assert_nil(chan[PCP::CHAN_TRACK])
        assert_not_nil(chan[PCP::CHAN_PKT])
        pkt = chan[PCP::CHAN_PKT]
        assert_equal(PCP::CHAN_PKT_DATA, pkt[PCP::CHAN_PKT_TYPE])
        assert_equal(6+i*8,              pkt[PCP::CHAN_PKT_POS])
        assert_equal("content#{i+1}",    pkt[PCP::CHAN_PKT_DATA])
      end
      @stream.stop
      assert_pcp_quit(PCP::ERROR_QUIT, read_atom(@pipe))
    end

    def test_bcst
      channel = TestChannel.new(@peercast, @channel_id, System::Uri.new('mock://localhost'))
      channel.status = PCSCore::SourceStreamStatus.receiving
      channel.is_relay_full = false
      @stream = PCSPCP::PCPOutputStream.new(@peercast, @input, @output, @endpoint, channel, @request)
      @stream.start
      header = read_http_header(@pipe)
      assert_http_header(200, {}, header)
      pcp_handshake(@pipe)
      channel.content_header = PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.0), 0, 'header')
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.1),  6, 'content1'))
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.2), 14, 'content2'))
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.3), 22, 'content3'))
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.4), 30, 'content4'))
      bcst = PCP::Atom.new(PCP::BCST, [])
      bcst[PCP::BCST_TTL]  = 11
      bcst[PCP::BCST_HOPS] = 0
      bcst[PCP::BCST_FROM] = PCP::GID.generate
      bcst[PCP::BCST_GROUP] = PCP::BCST_GROUP_TRACKERS
      #bcst[PCP::BCST_DEST] = PCP::GID.generate
      bcst[PCP::BCST_CHANID] = channel.ChannelID
      bcst[PCP::BCST_VERSION] = 1218
      bcst[PCP::BCST_VERSION_VP] = 27
      bcst[PCP::QUIT] = PCP::ERROR_QUIT | PCP::ERROR_OFFAIR
      bcst.write(@pipe)

      timeout(5) do
        sleep(0.1) while channel.broadcasts.empty?
      end
      broadcast = channel.broadcasts.first
      assert_not_nil(broadcast[0])
      atom = broadcast[1]
      assert_equal(PCP::BCST, atom.name.to_s)
      assert_equal(10, atom.children.GetBcstTTL)
      assert_equal(1,  atom.children.GetBcstHops)
      assert_equal(bcst[PCP::BCST_FROM].to_s,   atom.children.GetBcstFrom.ToString('N'))
      assert_equal(bcst[PCP::BCST_GROUP],       atom.children.GetBcstGroup)
      assert_equal(bcst[PCP::BCST_CHANID].to_s, atom.children.GetBcstChannelID.ToString('N'))
      assert_equal(bcst[PCP::BCST_VERSION],     atom.children.GetBcstVersion)
      assert_equal(bcst[PCP::BCST_VERSION_VP],  atom.children.GetBcstVersionVP)
      assert_equal(bcst[PCP::QUIT],             atom.children.GetQuit)
      assert_equal(PCSCore::BroadcastGroup.trackers, broadcast[2])
    end

    def test_bcst_dest_matched
      channel = TestChannel.new(@peercast, @channel_id, System::Uri.new('mock://localhost'))
      channel.status = PCSCore::SourceStreamStatus.receiving
      channel.is_relay_full = false
      @stream = PCSPCP::PCPOutputStream.new(@peercast, @input, @output, @endpoint, channel, @request)
      @stream.start
      header = read_http_header(@pipe)
      assert_http_header(200, {}, header)
      pcp_handshake(@pipe)
      channel.content_header = PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.0), 0, 'header')
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.1),  6, 'content1'))
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.2), 14, 'content2'))
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.3), 22, 'content3'))
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.4), 30, 'content4'))
      bcst = PCP::Atom.new(PCP::BCST, [])
      bcst[PCP::BCST_TTL]  = 11
      bcst[PCP::BCST_HOPS] = 0
      bcst[PCP::BCST_FROM] = PCP::GID.generate
      bcst[PCP::BCST_GROUP] = PCP::BCST_GROUP_TRACKERS
      bcst[PCP::BCST_DEST] = @peercast.SessionID
      bcst[PCP::BCST_CHANID] = channel.ChannelID
      bcst[PCP::BCST_VERSION] = 1218
      bcst[PCP::BCST_VERSION_VP] = 27
      bcst[PCP::QUIT] = PCP::ERROR_QUIT | PCP::ERROR_OFFAIR
      bcst.write(@pipe)

      sleep(0.5)
      assert(channel.broadcasts.empty?)
    end

    def test_bcst_dest_not_matched
      channel = TestChannel.new(@peercast, @channel_id, System::Uri.new('mock://localhost'))
      channel.status = PCSCore::SourceStreamStatus.receiving
      channel.is_relay_full = false
      @stream = PCSPCP::PCPOutputStream.new(@peercast, @input, @output, @endpoint, channel, @request)
      @stream.start
      header = read_http_header(@pipe)
      assert_http_header(200, {}, header)
      pcp_handshake(@pipe)
      channel.content_header = PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.0), 0, 'header')
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.1),  6, 'content1'))
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.2), 14, 'content2'))
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.3), 22, 'content3'))
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.4), 30, 'content4'))
      bcst = PCP::Atom.new(PCP::BCST, [])
      bcst[PCP::BCST_TTL]  = 11
      bcst[PCP::BCST_HOPS] = 0
      bcst[PCP::BCST_FROM] = PCP::GID.generate
      bcst[PCP::BCST_GROUP] = PCP::BCST_GROUP_TRACKERS
      bcst[PCP::BCST_DEST] = PCP::GID.generate
      bcst[PCP::BCST_CHANID] = channel.ChannelID
      bcst[PCP::BCST_VERSION] = 1218
      bcst[PCP::BCST_VERSION_VP] = 27
      bcst[PCP::QUIT] = PCP::ERROR_QUIT | PCP::ERROR_OFFAIR
      bcst.write(@pipe)

      sleep(0.5)
      assert(!channel.broadcasts.empty?)
    end

    def test_bcst_no_ttl
      channel = TestChannel.new(@peercast, @channel_id, System::Uri.new('mock://localhost'))
      channel.status = PCSCore::SourceStreamStatus.receiving
      channel.is_relay_full = false
      @stream = PCSPCP::PCPOutputStream.new(@peercast, @input, @output, @endpoint, channel, @request)
      @stream.start
      header = read_http_header(@pipe)
      assert_http_header(200, {}, header)
      pcp_handshake(@pipe)
      channel.content_header = PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.0), 0, 'header')
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.1),  6, 'content1'))
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.2), 14, 'content2'))
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.3), 22, 'content3'))
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.4), 30, 'content4'))
      bcst = PCP::Atom.new(PCP::BCST, [])
      bcst[PCP::BCST_TTL]  = 0
      bcst[PCP::BCST_HOPS] = 11
      bcst[PCP::BCST_FROM] = PCP::GID.generate
      bcst[PCP::BCST_GROUP] = PCP::BCST_GROUP_TRACKERS
      bcst[PCP::BCST_CHANID] = channel.ChannelID
      bcst[PCP::BCST_VERSION] = 1218
      bcst[PCP::BCST_VERSION_VP] = 27
      bcst[PCP::QUIT] = PCP::ERROR_QUIT | PCP::ERROR_OFFAIR
      bcst.write(@pipe)

      sleep(0.5)
      assert(channel.broadcasts.empty?)
    end

    def test_ping_host_local
      channel = TestChannel.new(@peercast, @channel_id, System::Uri.new('mock://localhost'))
      channel.status = PCSCore::SourceStreamStatus.receiving
      channel.is_relay_full = false
      @stream = PCSPCP::PCPOutputStream.new(@peercast, @input, @output, @endpoint, channel, @request)
      @stream.start
      header = read_http_header(@pipe)
      assert_http_header(200, {}, header)

      ping_conn = nil
      ping_helo = nil
      server = TCPServer.open('localhost', 7146)
      thread = Thread.new {
        begin
          socket = server.accept
          ping_conn = read_atom(socket)
          ping_helo = read_atom(socket)
          oleh = PCP::Atom.new(PCP::OLEH, [])
          oleh[PCP::HELO_SESSIONID] = session_id
          oleh.write(socket)
          socket.close
        rescue
        end
      }

      session_id = System::Guid.new_guid
      helo = PCP::Atom.new(PCP::HELO, [], nil)
      helo[PCP::HELO_SESSIONID] = session_id
      helo[PCP::HELO_VERSION]   = 1218
      helo[PCP::HELO_PING]      = 7146
      helo[PCP::HELO_AGENT]     = File.basename(__FILE__)
      helo.write(@pipe)

      assert_pcp_oleh(
                  @peercast.SessionID,
                  @endpoint.address,
                  0,
                  1218,
                  @peercast.agent_name,
                  read_atom(@pipe))
      server.close
      assert_nil(ping_conn)
      assert_nil(ping_helo)
    ensure
      server.close if server and not server.closed?
    end

    class TestPingHostPCPOutputStream < PCSPCP::PCPOutputStream
      def self.new(*args)
        super.instance_eval {
          @ping_to_any = false
          self
        }
      end
      attr_accessor :ping_to_any

      def IsPingTarget(addr)
        if @ping_to_any then
          true
        else
          super
        end
      end
    end

    def test_ping_host
      channel = TestChannel.new(@peercast, @channel_id, System::Uri.new('mock://localhost'))
      channel.status = PCSCore::SourceStreamStatus.receiving
      channel.is_relay_full = false
      @stream = TestPingHostPCPOutputStream.new(@peercast, @input, @output, @endpoint, channel, @request)
      @stream.ping_to_any = true
      @stream.start
      header = read_http_header(@pipe)
      assert_http_header(200, {}, header)

      session_id = System::Guid.new_guid

      ping_conn = nil
      ping_helo = nil
      server = TCPServer.open('localhost', 7146)
      thread = Thread.new {
        begin
          socket = server.accept
          ping_conn = read_atom(socket)
          ping_helo = read_atom(socket)
          oleh = PCP::Atom.new(PCP::OLEH, [])
          oleh[PCP::HELO_SESSIONID] = session_id
          oleh.write(socket)
          socket.close
        rescue
        end
      }

      helo = PCP::Atom.new(PCP::HELO, [], nil)
      helo[PCP::HELO_SESSIONID] = session_id
      helo[PCP::HELO_VERSION]   = 1218
      helo[PCP::HELO_PING]      = 7146
      helo[PCP::HELO_AGENT]     = File.basename(__FILE__)
      helo.write(@pipe)

      thread.join
      assert_equal("pcp\n",  ping_conn.name)
      assert_equal(1,        ping_conn.content.unpack('V')[0])
      assert_equal(PCP::HELO, ping_helo.name)
      assert_equal(1,        ping_helo.children.size)
      assert_equal(ping_helo[PCP::HELO_SESSIONID].to_s, @peercast.SessionID.ToString('N'))

      assert_pcp_oleh(
                  @peercast.SessionID,
                  @endpoint.address,
                  7146,
                  1218,
                  @peercast.agent_name,
                  read_atom(@pipe))
    ensure
      server.close if server and not server.closed?
    end

    def test_ping_host_wrong_session_id
      channel = TestChannel.new(@peercast, @channel_id, System::Uri.new('mock://localhost'))
      channel.status = PCSCore::SourceStreamStatus.receiving
      channel.is_relay_full = false
      @stream = TestPingHostPCPOutputStream.new(@peercast, @input, @output, @endpoint, channel, @request)
      @stream.ping_to_any = true
      @stream.start
      header = read_http_header(@pipe)
      assert_http_header(200, {}, header)

      ping_conn = nil
      ping_helo = nil
      server = TCPServer.open('localhost', 7146)
      thread = Thread.new {
        begin
          socket = server.accept
          ping_conn = read_atom(socket)
          ping_helo = read_atom(socket)
          oleh = PCP::Atom.new(PCP::OLEH, [])
          oleh[PCP::HELO_SESSIONID] = PCP::GID.generate
          oleh.write(socket)
          socket.close
        rescue
        end
      }

      session_id = System::Guid.new_guid
      helo = PCP::Atom.new(PCP::HELO, [], nil)
      helo[PCP::HELO_SESSIONID] = session_id
      helo[PCP::HELO_VERSION]   = 1218
      helo[PCP::HELO_PING]      = 7146
      helo[PCP::HELO_AGENT]     = File.basename(__FILE__)
      helo.write(@pipe)

      thread.join
      assert_equal("pcp\n",  ping_conn.name)
      assert_equal(1,        ping_conn.content.unpack('V')[0])
      assert_equal(PCP::HELO, ping_helo.name)
      assert_equal(1,        ping_helo.children.size)
      assert_equal(ping_helo[PCP::HELO_SESSIONID].to_s, @peercast.SessionID.ToString('N'))

      assert_pcp_oleh(
                  @peercast.SessionID,
                  @endpoint.address,
                  0,
                  1218,
                  @peercast.agent_name,
                  read_atom(@pipe))
    ensure
      server.close if server and not server.closed?
    end

    def test_pcp_host
      channel = TestChannel.new(@peercast, @channel_id, System::Uri.new('mock://localhost'))
      channel.status = PCSCore::SourceStreamStatus.receiving
      channel.is_relay_full = false
      @stream = PCSPCP::PCPOutputStream.new(@peercast, @input, @output, @endpoint, channel, @request)
      @stream.start
      header = read_http_header(@pipe)
      pcp_handshake(@pipe)

      node = PCSCore::HostBuilder.new
      node.SessionID = System::Guid.new_guid
      node.global_end_point = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7149)
      node.is_firewalled  = false
      node.is_tracker     = true
      node.is_relay_full  = false
      node.is_direct_full = false
      node.is_receiving   = true
      node.direct_count   = 10
      node.relay_count    = 38
      host = PCP::Atom.new(PCP::HOST, [], nil)
      host[PCP::HOST_ID]   = node.SessionID
      host[PCP::HOST_IP]   = node.global_end_point.address
      host[PCP::HOST_PORT] = node.global_end_point.port
      host[PCP::HOST_NUMR] = node.relay_count
      host[PCP::HOST_NUML] = node.direct_count
      host[PCP::HOST_FLAGS1] = 
          (node.is_firewalled   ? PCP::HOST_FLAGS1_PUSH : 0) |
          (node.is_tracker      ? PCP::HOST_FLAGS1_TRACKER : 0) |
          (node.is_relay_full   ? 0 : PCP::HOST_FLAGS1_RELAY) |
          (node.is_direct_full  ? 0 : PCP::HOST_FLAGS1_DIRECT) |
          (node.is_receiving    ? PCP::HOST_FLAGS1_RECV : 0) |
          (node.is_control_full ? 0 : PCP::HOST_FLAGS1_CIN)
      host.write(@pipe)
      sleep(0.5)
      assert_equal(1, channel.nodes.count)
      channel_node = channel.nodes.find {|n| n.SessionID.eql?(node.SessionID) }
      assert(channel_node)
      assert_equal(node.direct_count, channel_node.direct_count)
      assert_equal(node.relay_count,  channel_node.relay_count)
      flags1 = host[PCP::HOST_FLAGS1]
      assert_equal(node.is_firewalled,   channel_node.is_firewalled)
      assert_equal(node.is_tracker,      channel_node.is_tracker)
      assert_equal(node.is_relay_full,   channel_node.is_relay_full)
      assert_equal(node.is_direct_full,  channel_node.is_direct_full)
      assert_equal(node.is_receiving,    channel_node.is_receiving) 
      assert_equal(node.is_control_full, channel_node.is_control_full)
      assert_equal(node.global_end_point, channel_node.global_end_point)
    end

    def test_recv_rate
      channel = TestChannel.new(@peercast, @channel_id, System::Uri.new('mock://localhost'))
      channel.status = PCSCore::SourceStreamStatus.receiving
      channel.is_relay_full = false
      @stream = PCSPCP::PCPOutputStream.new(@peercast, @input, @output, @endpoint, channel, @request)
      start = Time.now
      @stream.start
      header = read_http_header(@pipe)
      assert_http_header(200, {}, header)
      pcp_handshake(@pipe)
      channel.content_header = PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.0), 0, 'header')
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.1),  6, 'content1'))
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.2), 14, 'content2'))
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.3), 22, 'content3'))
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.4), 30, 'content4'))
      ok = read_atom(@pipe)
      chan = read_atom(@pipe)
      4.times do
        chan = read_atom(@pipe)
      end
      @stream.stop
      quit = read_atom(@pipe)
      t = Time.now
      sleep([1.5-(t-start), 0].max)
      recv_rate = @stream.recv_rate
      assert_not_equal 0.to_f, recv_rate.to_f
    end

    def test_send_rate
      channel = TestChannel.new(@peercast, @channel_id, System::Uri.new('mock://localhost'))
      channel.status = PCSCore::SourceStreamStatus.receiving
      channel.is_relay_full = false
      @stream = PCSPCP::PCPOutputStream.new(@peercast, @input, @output, @endpoint, channel, @request)
      start = Time.now
      @stream.start
      header = read_http_header(@pipe)
      assert_http_header(200, {}, header)
      pcp_handshake(@pipe)
      channel.content_header = PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.0), 0, 'header')
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.1),  6, 'content1'))
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.2), 14, 'content2'))
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.3), 22, 'content3'))
      channel.contents.add(PCSCore::Content.new(0, System::TimeSpan.from_seconds(0.4), 30, 'content4'))
      ok = read_atom(@pipe)
      chan = read_atom(@pipe)
      4.times do
        chan = read_atom(@pipe)
      end
      @stream.stop
      quit = read_atom(@pipe)
      t = Time.now
      sleep([1.5-(t-start), 0].max)
      send_rate = @stream.send_rate
      assert_not_equal 0, send_rate
    end
  end
end

