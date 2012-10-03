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
require 'uri'
require 'timeout'

module TestPCP
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

  class TC_PCPSourceStreamFactory < Test::Unit::TestCase
    def setup
      @session_id = System::Guid.new_guid
      @endpoint   = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7147)
      accepts =
        PeerCastStation::Core::OutputStreamType.metadata |
        PeerCastStation::Core::OutputStreamType.play |
        PeerCastStation::Core::OutputStreamType.relay |
        PeerCastStation::Core::OutputStreamType.interface
      @peercast   = PCSCore::PeerCast.new
      @peercast.start_listen(@endpoint, accepts, accepts)
      @channel_id = System::Guid.parse('531dc8dfc7fb42928ac2c0a626517a87')
      @channel    = PCSCore::Channel.new(@peercast, @channel_id, System::Uri.new('pcp://localhost:7146'))
    end
    
    def teardown
      @peercast.stop if @peercast
    end
    
    def test_construct
      factory = PCSPCP::PCPSourceStreamFactory.new(@peercast)
      assert_equal('pcp', factory.Name)
      assert_equal('pcp', factory.scheme)
      assert_equal(@peercast, factory.PeerCast)
      assert(factory.respond_to?(:create_obj_ref))
    end

    def test_create
      factory = PCSPCP::PCPSourceStreamFactory.new(@peercast)
      stream = factory.create(@channel, @channel.source_uri)
      assert_kind_of(PCSPCP::PCPSourceStream, stream)
    end
  end

  class TestPCPServer
    AgentName = File.basename(__FILE__)
    Channel = Struct.new(:channel_id, :info, :track, :header, :contents)
    Packet  = Struct.new(:position, :data)
    def initialize(host, port)
      @host = host
      @port = port
      @channels   = []
      @hosts      = []
      @relayable  = true
      @on_atom    = {}
      @post       = []

      @session_id = PCP::GID.generate
      @server = TCPServer.open(host, port)
      @closed = false
      @client_threads = []
      @server_thread = Thread.new {
        until @closed do
          if IO.select([@server], [], [], 0.1) then
            client = @server.accept
            @client_threads << Thread.new {
              begin
                process_client(client)
              rescue System::Net::Sockets::SocketException
              rescue System::IO::IOException
              ensure
                begin
                  client.flush
                  client.close_read
                  client.close_write
                  client.close
                rescue System::ObjectDisposedException
                rescue System::IO::IOException
                end
              end
            }
          end
        end
        @server.close
      }
    end
    attr_reader :host, :port, :session_id
    attr_accessor :relayable, :on_atom
    attr_accessor :channels,  :hosts

    def close
      @closed = true
      @server_thread.join
      @client_threads.each(&:join)
    end

    def post(atom)
      @post << atom
    end

    HTTPRequest = Struct.new(:method, :uri, :version, :headers)
    def parse_http_header(header)
      header = header.split(/\r\n/)
      line = header.shift
      if /(GET|HEAD) ([\/\w]+) HTTP\/(1.\d)/=~line then
        method  = $1
        uri     = URI.parse($2)
        version = $3
        headers = {}
        while line=header.shift and line!='' do
          md = /(.*?):(.*)/.match(line)
          headers[md[1].strip.downcase] = md[2].strip
        end
        HTTPRequest.new(method, uri, version, headers)
      else
        raise RuntimeError, "Invalid request: #{line}"
      end
    end

    def read_http_header(io)
      header = io.read(4)
      until /\r\n\r\n$/=~header do
        header << io.read(1)
      end
      parse_http_header(header)
    end

    def pcp_ping(session_id, addr, port)
      begin
        ping_sock = TCPSocket.open(addr, port)
        PCP::Atom.new("pcp\n", nil, [1].pack('V')).write(ping_sock)
        helo = PCP::Atom.new(PCP::HELO, [], nil)
        helo[PCP::HELO_SESSIONID] = @session_id
        helo.write(ping_sock)
        oleh = PCP::Atom.read(ping_sock)
        return 0 unless oleh[PCP::HELO_SESSIONID]==session_id
      rescue
        return 0
      ensure
        if ping_sock and not ping_sock.closed? then
          ping_sock.flush
          ping_sock.close
        end
      end
    end

    def on_helo(sock, atom)
      session_id = atom[PCP::HELO_SESSIONID]
      if atom[PCP::HELO_PING] then
        port = pcp_ping(session_id, sock.peeraddr[3], atom[PCP::HELO_PING]) || 0
      else
        port = atom[PCP::HELO_PORT] || 0
      end
      oleh = PCP::Atom.new(PCP::OLEH, [], nil)
      oleh[PCP::HELO_AGENT]     = AgentName
      oleh[PCP::HELO_SESSIONID] = @session_id
      oleh[PCP::HELO_VERSION]   = 1218
      oleh[PCP::HELO_REMOTEIP]  = sock.peeraddr[3]
      oleh[PCP::HELO_PORT]      = port
      oleh.write(sock)
    end

    def on_bcst(sock, atom)
      atom.children.each do |c|
        case c.name
        when PCP::BCST_TTL
        when PCP::BCST_HOPS
        when PCP::BCST_FROM
        when PCP::BCST_DEST
        when PCP::BCST_GROUP
        when PCP::BCST_CHANID
        when PCP::BCST_VERSION
        when PCP::BCST_VERSION_VP
        else
          process_atom(sock, c)
        end
      end
    end

    def on_host(sock, atom)
    end

    class PCPQuitError < RuntimeError
    end

    def on_quit(sock, atom)
      raise PCPQuitError, "Quit Received"
    end

    def process_atom(sock, atom)
      return unless atom
      handler = @on_atom[atom.name]
      handler.call(atom) if handler
      case atom.name
      when PCP::BCST; on_bcst(sock, atom)
      when PCP::HOST; on_host(sock, atom)
      when PCP::QUIT; on_quit(sock, atom)
      end
    end

    class HTTPRequestError < RuntimeError
      def initialize(status, msg)
        super(msg)
        @status = status
      end
      attr_reader :status
    end

    def quit(sock, code)
      PCP::Atom.new(PCP::QUIT, nil, [code].pack('V')).write(sock)
      sock.flush
      sleep 1
      sock.close
    end

    def process_client(sock)
      begin
        request = read_http_header(sock)
        raise HTTPRequestError.new(400, 'Bad Request') unless request.headers['x-peercast-pcp']=='1'
        raise HTTPRequestError.new(404, 'Not Found')   unless %r;^/channel/([A-Fa-f0-9]{32});=~request.uri.path
        channel_id = PCP::GID.from_string($1)
        channel = @channels.find {|c| c.channel_id.to_s==channel_id.to_s }
        raise HTTPRequestError.new(404, 'Not Found')   unless channel
        if @relayable then
          sock.write([
            "HTTP/1.0 200 OK",
            "Server: #{AgentName}",
            "Content-Type:application/x-peercast-pcp",
            "x-peercast-pcp:1",
            ""
          ].join("\r\n")+"\r\n")
          helo = PCP::Atom.read(sock)
          raise RuntimeError, "Handshake failed" unless helo.name==PCP::HELO
          on_helo(sock, helo)
          PCP::Atom.new(PCP::OK, nil, [0].pack('V')).write(sock)
          chan = PCP::Atom.new(PCP::CHAN, [], nil)
          chan[PCP::CHAN_ID] = channel.channel_id
          chan.children << channel.info  if channel.info  and not channel.info.children.empty?
          chan.children << channel.track if channel.track and not channel.track.children.empty?
          chan_pkt = PCP::Atom.new(PCP::CHAN_PKT, [], nil)
          chan_pkt[PCP::CHAN_PKT_TYPE] = PCP::CHAN_PKT_HEAD
          chan_pkt[PCP::CHAN_PKT_POS]  = channel.header.position
          chan_pkt[PCP::CHAN_PKT_DATA] = channel.header.data
          last_pos = channel.header.position
          chan.children << chan_pkt
          chan.write(sock)
          until @closed do
            if IO.select([sock], [], [], 0.01) then
              process_atom(sock, PCP::Atom.read(sock))
            elsif not @post.empty? then
              atom = @post.shift
              atom.write(sock)
            else
              content = channel.contents.find {|c| c.position>last_pos }
              if content then
                chan = PCP::Atom.new(PCP::CHAN, [], nil)
                chan[PCP::CHAN_ID]    = channel.channel_id
                chan_pkt = PCP::Atom.new(PCP::CHAN_PKT, [], nil)
                chan_pkt[PCP::CHAN_PKT_TYPE] = PCP::CHAN_PKT_DATA
                chan_pkt[PCP::CHAN_PKT_POS]  = content.position
                chan_pkt[PCP::CHAN_PKT_DATA] = content.data
                last_pos = content.position
                chan.children << chan_pkt
                chan.write(sock)
              end
            end
          end
          quit(sock, PCP::ERROR_SHUTDOWN+PCP::ERROR_QUIT)
        else
          sock.write([
            "HTTP/1.0 503 Unavailable",
            "Server: #{AgentName}",
            "Content-Type:application/x-peercast-pcp",
            "x-peercast-pcp:1",
            ""
          ].join("\r\n")+"\r\n")
          helo = PCP::Atom.read(sock)
          raise RuntimeError, "Handshake failed" unless helo.name==PCP::HELO
          on_helo(sock, helo)
          hosts[0,8].each do |host|
            host.write(sock)
          end
          quit(sock,PCP::ERROR_UNAVAILABLE+PCP::ERROR_QUIT)
        end
      rescue HTTPRequestError => e
        sock.write([
          "HTTP/1.0 #{e.status} #{e.message}",
          "Server: #{AgentName}",
          ""
        ].join("\r\n")+"\r\n")
      rescue PCPQuitError
      end
    end
  end

  class TC_PCPSourceStream < Test::Unit::TestCase
    class TestChannel < PCSCore::Channel
      def self.new(*args)
        instance = super
        instance.instance_eval do 
          @is_relay_full = false
          @broadcasts = []
        end
        instance
      end
      attr_accessor :broadcasts

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
      accepts =
        PeerCastStation::Core::OutputStreamType.metadata |
        PeerCastStation::Core::OutputStreamType.play |
        PeerCastStation::Core::OutputStreamType.relay |
        PeerCastStation::Core::OutputStreamType.interface
      @peercast   = PCSCore::PeerCast.new
      @peercast.start_listen(@endpoint, accepts, accepts)
      @peercast.OutputStreamFactories.add(PeerCastStation::PCP::PCPPongOutputStreamFactory.new(@peercast))
      @channel_id = System::Guid.parse('531dc8dfc7fb42928ac2c0a626517a87')
      @source_uri = System::Uri.new('pcp://127.0.0.1:17144')
      @channel    = TestChannel.new(@peercast, @channel_id, @source_uri)
      @server     = nil
    end
    
    def teardown
      @peercast.stop if @peercast
      @server.close if @server
    end

    def setup_srv_channel(channel_id)
      srv_channel = TestPCPServer::Channel.new
      srv_channel.channel_id = channel_id
      srv_channel.info   = PCP::Atom.new(PCP::CHAN_INFO,  [], nil)
      srv_channel.info[PCP::CHAN_INFO_NAME]    = 'FooBar'
      srv_channel.info[PCP::CHAN_INFO_TYPE]    = 'RAW'
      srv_channel.info[PCP::CHAN_INFO_BITRATE] = 123
      srv_channel.track  = PCP::Atom.new(PCP::CHAN_TRACK, [], nil)
      srv_channel.header = TestPCPServer::Packet.new(0, 'header')
      srv_channel.contents = Array.new(10) {|i|
        TestPCPServer::Packet.new(6+i*10, "content%03d" % i)
      }
      srv_channel
    end
    
    def test_construct
      stream = PCSPCP::PCPSourceStream.new(
        @peercast,
        @channel,
        @source_uri)
      assert_equal(@peercast,   stream.PeerCast)
      assert_equal(@channel,    stream.Channel)
      assert_equal(@source_uri, stream.SourceUri)
      assert(!stream.is_stopped)
      assert(stream.respond_to?(:create_obj_ref))
    end

    def test_start_not_found_tracker
      srv_channel = setup_srv_channel(PCP::GID.generate)
      @server = TestPCPServer.new('127.0.0.1', 17144)
      @server.channels << srv_channel

      stream = PCSPCP::PCPSourceStream.new(
        @peercast,
        @channel,
        @source_uri)
      @channel.start(stream)
      timeout(10) do
        sleep 0.1 until stream.is_stopped
      end
      assert_equal(PCSCore::SourceStreamStatus.error.to_s, @channel.status.to_s)
      @channel.close
    end

    def test_start_unavailable_without_hosts
      srv_channel = setup_srv_channel(@channel_id)
      @server = TestPCPServer.new('127.0.0.1', 17144)
      @server.relayable = false
      @server.channels << srv_channel

      stream = PCSPCP::PCPSourceStream.new(
        @peercast,
        @channel,
        @source_uri)
      @channel.start(stream)
      timeout(10) do
        sleep 0.1 until stream.is_stopped
      end
      assert_equal(PCSCore::SourceStreamStatus.error.to_s, @channel.status.to_s)
      @channel.close
    end

    def test_start_relay
      srv_channel = setup_srv_channel(@channel_id)
      @server = TestPCPServer.new('127.0.0.1', 17144)
      @server.channels << srv_channel
      hosts = []
      @server.on_atom[PCP::HOST] = proc {|atom|
        hosts << atom
      }

      stream = PCSPCP::PCPSourceStream.new(
        @peercast,
        @channel,
        @source_uri)
      @channel.start(stream)
      timeout(5) do
        until stream.is_stopped do
          if hosts.size>0 and
             @channel.content_header and
             @channel.contents.count>=10 then
            @server.close
          else
            sleep 0.1
          end
        end
      end
      hosts.each do |host|
        assert_equal(PCP::HOST, host.name)
        assert_equal(@peercast.SessionID.to_s, host[PCP::HOST_ID].to_s)
        assert_equal([
          [127, 0, 0, 1],
          @peercast.local_address.get_address_bytes.collect {|b| b.to_i },
        ], host[PCP::HOST_IP])
        assert_equal([
          7147,
          7147,
        ], host[PCP::HOST_PORT])
        assert_equal(@channel_id.to_s, host[PCP::HOST_CHANID].to_s)
        assert_equal(0, host[PCP::HOST_NUML])
        assert_equal(0, host[PCP::HOST_NUMR])
        assert(0<=host[PCP::HOST_UPTIME])
        assert_equal(1218, host[PCP::HOST_VERSION])
        assert_equal(27,   host[PCP::HOST_VERSION_VP])
        assert_nil(host[PCP::HOST_CLAP_PP])
        assert_nil(host[PCP::HOST_OLDPOS])
        assert_nil(host[PCP::HOST_NEWPOS])
        assert_equal(
          PCP::HOST_FLAGS1_PUSH   |
          PCP::HOST_FLAGS1_RELAY  |
          PCP::HOST_FLAGS1_DIRECT |
          PCP::HOST_FLAGS1_RECV,
          host[PCP::HOST_FLAGS1])
        assert_equal([127, 0, 0, 1], host[PCP::HOST_UPHOST_IP])
        assert_equal(17144, host[PCP::HOST_UPHOST_PORT])
        assert_nil(host[PCP::HOST_UPHOST_HOPS])
      end
      assert_equal(0, @channel.content_header.position)
      assert_equal('header', @channel.content_header.data.to_a.pack('C*'))
      @channel.contents.each_with_index do |c, i|
        assert_equal(6+i*10, c.position)
        assert_equal('content%03d' % i, c.data.to_a.pack('C*'))
      end
    end

    def test_start_unavailable_with_hosts
      srv_channel = setup_srv_channel(@channel_id)
      client_hosts = []
      @server = TestPCPServer.new('127.0.0.1', 17144)
      @server.relayable = false
      @server.channels << srv_channel
      @server.on_atom[PCP::HOST] = proc {|atom| client_hosts << atom }
      server2 = TestPCPServer.new('127.0.0.1', 17145)
      server2.relayable = true
      server2.channels << srv_channel
      server2.on_atom[PCP::HOST] = proc {|atom| client_hosts << atom }

      host = PCP::Atom.new(PCP::HOST, [], nil)
      host[PCP::HOST_ID] = server2.session_id
      host.children << PCP::Atom.new(PCP::HOST_IP,   nil, [127, 0, 0, 1].reverse.pack('C*'))
      host.children << PCP::Atom.new(PCP::HOST_PORT, nil, [17145].pack('v'))
      host.children << PCP::Atom.new(PCP::HOST_IP,   nil, [127, 0, 0, 1].reverse.pack('C*'))
      host.children << PCP::Atom.new(PCP::HOST_PORT, nil, [17145].pack('v'))
      host[PCP::HOST_CHANID]  = @channel_id
      host[PCP::HOST_NUML]    = 3
      host[PCP::HOST_NUMR]    = 8
      host[PCP::HOST_UPTIME]  = rand(65536)
      host[PCP::HOST_VERSION] = 1218
      host[PCP::HOST_VERSION] = 27
      host[PCP::HOST_OLDPOS]  = 0
      host[PCP::HOST_NEWPOS]  = 96
      host[PCP::HOST_FLAGS1]  =
        PCP::HOST_FLAGS1_PUSH   |
        PCP::HOST_FLAGS1_RELAY  |
        PCP::HOST_FLAGS1_DIRECT |
        PCP::HOST_FLAGS1_RECV
      host[PCP::HOST_UPHOST_IP]   = [127, 0, 0, 1]
      host[PCP::HOST_UPHOST_PORT] = 17144
      host[PCP::HOST_UPHOST_HOPS] = 1
      @server.hosts << host

      stream = PCSPCP::PCPSourceStream.new(
        @peercast,
        @channel,
        @source_uri)
      @channel.start(stream)
      timeout(5) do
        until stream.is_stopped do
          if client_hosts.size>0 and
             @channel.content_header and
             @channel.contents.count>=10 then
            @server.close
            server2.close
          else
            sleep 0.1
          end
        end
      end
      client_hosts.each do |host|
        assert_equal(PCP::HOST, host.name)
        assert_equal(@peercast.SessionID.to_s, host[PCP::HOST_ID].to_s)
        assert_equal([
          [127, 0, 0, 1],
          @peercast.local_address.get_address_bytes.collect {|b| b.to_i },
        ], host[PCP::HOST_IP])
        assert_equal([
          7147,
          7147,
        ], host[PCP::HOST_PORT])
        assert_equal(@channel_id.to_s, host[PCP::HOST_CHANID].to_s)
        assert_equal(0, host[PCP::HOST_NUML])
        assert_equal(0, host[PCP::HOST_NUMR])
        assert(0<=host[PCP::HOST_UPTIME])
        assert_equal(1218, host[PCP::HOST_VERSION])
        assert_equal(27,   host[PCP::HOST_VERSION_VP])
        assert_nil(host[PCP::HOST_CLAP_PP])
        assert_nil(host[PCP::HOST_OLDPOS])
        assert_nil(host[PCP::HOST_NEWPOS])
        assert_equal(
          PCP::HOST_FLAGS1_PUSH   |
          PCP::HOST_FLAGS1_RELAY  |
          PCP::HOST_FLAGS1_DIRECT |
          PCP::HOST_FLAGS1_RECV,
          host[PCP::HOST_FLAGS1])
        assert_equal([127, 0, 0, 1], host[PCP::HOST_UPHOST_IP])
        assert_equal(17145, host[PCP::HOST_UPHOST_PORT])
        assert_nil(host[PCP::HOST_UPHOST_HOPS])
      end
      assert_equal(0, @channel.content_header.position)
      assert_equal('header', @channel.content_header.data.to_a.pack('C*'))
      @channel.contents.each_with_index do |c, i|
        assert_equal(6+i*10, c.position)
        assert_equal('content%03d' % i, c.data.to_a.pack('C*'))
      end
      assert_equal(1, @channel.nodes.count)
      node = @channel.nodes[0]
      assert_equal(server2.session_id.to_s, node.SessionID.to_s)
      assert_equal(System::Guid.empty, node.BroadcastID)
      assert_equal(3, node.DirectCount)
      assert_equal(8, node.RelayCount)
      assert_equal('127.0.0.1', node.GlobalEndPoint.Address.to_s)
      assert_equal(17145,       node.GlobalEndPoint.Port)
      assert_equal('127.0.0.1', node.LocalEndPoint.Address.to_s)
      assert_equal(17145,       node.LocalEndPoint.Port)
      assert( node.IsFirewalled)
      assert(!node.IsRelayFull)
      assert(!node.IsDirectFull)
      assert( node.IsReceiving)
      assert( node.IsControlFull)
      assert(!node.IsTracker)
    ensure
      server2.close if server2
    end

    def test_start_unavailable_with_invalid_hosts
      srv_channel = setup_srv_channel(@channel_id)
      client_hosts = []
      @server = TestPCPServer.new('127.0.0.1', 17144)
      @server.relayable = false
      @server.channels << srv_channel
      server2 = TestPCPServer.new('127.0.0.1', 17145)
      server2.relayable = true
      server2.channels << srv_channel
      server2.on_atom[PCP::HOST] = proc {|atom| client_hosts << atom }
      ports = [0, 17146, 17145]

      ports.each_with_index do |port, i|
        host = PCP::Atom.new(PCP::HOST, [], nil)
        host[PCP::HOST_ID] = System::Guid.new_guid
        host.children << PCP::Atom.new(PCP::HOST_IP,   nil, [127, 0, 0, 1].reverse.pack('C*'))
        host.children << PCP::Atom.new(PCP::HOST_PORT, nil, [port].pack('v'))
        host.children << PCP::Atom.new(PCP::HOST_IP,   nil, [127, 0, 0, 1].reverse.pack('C*'))
        host.children << PCP::Atom.new(PCP::HOST_PORT, nil, [port].pack('v'))
        host[PCP::HOST_CHANID]  = @channel_id
        host[PCP::HOST_NUML]    = 3
        host[PCP::HOST_NUMR]    = 8*(ports.size-i-1)
        host[PCP::HOST_UPTIME]  = rand(65536)
        host[PCP::HOST_VERSION] = 1218
        host[PCP::HOST_VERSION] = 27
        host[PCP::HOST_OLDPOS]  = 0
        host[PCP::HOST_NEWPOS]  = 96
        host[PCP::HOST_FLAGS1]  =
          PCP::HOST_FLAGS1_PUSH   |
          PCP::HOST_FLAGS1_RELAY  |
          PCP::HOST_FLAGS1_DIRECT |
          PCP::HOST_FLAGS1_RECV
        host[PCP::HOST_UPHOST_IP]   = [127, 0, 0, 1]
        host[PCP::HOST_UPHOST_PORT] = 17144
        host[PCP::HOST_UPHOST_HOPS] = 1
        @server.hosts << host
      end

      stream = PCSPCP::PCPSourceStream.new(
        @peercast,
        @channel,
        @source_uri)
      @channel.start(stream)
      timeout(5) do
        until stream.is_stopped do
          if client_hosts.size>0 and
             @channel.content_header and
             @channel.contents.count>=10 then
            @server.close
            server2.close
          else
            sleep 0.1
          end
        end
      end
      assert !client_hosts.empty?
    ensure
      server2.close if server2
    end

    def test_broadcast
      srv_channel = setup_srv_channel(@channel_id)
      @server = TestPCPServer.new('127.0.0.1', 17144)
      @server.channels << srv_channel

      bcst = PCP::Atom.new(PCP::BCST, [], nil)
      bcst[PCP::BCST_TTL]        = 11
      bcst[PCP::BCST_HOPS]       =  0
      bcst[PCP::BCST_FROM]       = @server.session_id
      bcst[PCP::BCST_GROUP]      = PCP::BCST_GROUP_ALL
      bcst[PCP::BCST_CHANID]     = @channel_id
      bcst[PCP::BCST_VERSION]    = 1218
      bcst[PCP::BCST_VERSION_VP] = 27
      bcst[PCP::OK] = 0
      @server.post(bcst)
      stream = PCSPCP::PCPSourceStream.new(
        @peercast,
        @channel,
        @source_uri)
      @channel.start(stream)
      timeout(10) do
        until stream.is_stopped do
          if @channel.broadcasts.find {|b| b[1].children.get_ok } and
             @channel.content_header and
             @channel.contents.count>=10 then
            @server.close
          else
            sleep 0.1
          end
        end
      end
      posted = @channel.broadcasts.find {|b| b[1].children.get_ok }
      assert_not_nil(posted)
      assert_equal(@server.session_id.to_s, posted[0].SessionID.ToString('N'))
      assert_equal(PCP::BCST_GROUP_ALL, posted[2])
      atom = posted[1]
      assert_equal(PCP::BCST, atom.name.to_s)
      assert_equal(@channel_id.to_s, atom.children.GetBcstChannelID.to_s)
      assert_equal(@server.session_id.to_s, atom.children.GetBcstFrom.to_s)
      assert_nil(atom.children.GetBcstDest)
      assert_equal(PCP::BCST_GROUP_ALL, atom.children.GetBcstGroup)
      assert_equal(10, atom.children.GetBcstTTL)
      assert_equal(1, atom.children.GetBcstHops)
      assert_equal(1218, atom.children.GetBcstVersion)
      assert_equal(27, atom.children.GetBcstVersionVP)
      assert_nil(atom.children.GetBcstVersionEXNumber)
      assert_nil(atom.children.GetBcstVersionEXPrefix)
      assert_equal(0, atom.children.GetOk)
    end

    def test_broadcast_to_self
      srv_channel = setup_srv_channel(@channel_id)
      @server = TestPCPServer.new('127.0.0.1', 17144)
      @server.channels << srv_channel

      bcst = PCP::Atom.new(PCP::BCST, [], nil)
      bcst[PCP::BCST_TTL]        = 11
      bcst[PCP::BCST_HOPS]       =  0
      bcst[PCP::BCST_FROM]       = @server.session_id
      bcst[PCP::BCST_DEST]       = @peercast.SessionID
      bcst[PCP::BCST_GROUP]      = PCP::BCST_GROUP_ALL
      bcst[PCP::BCST_CHANID]     = @channel_id
      bcst[PCP::BCST_VERSION]    = 1218
      bcst[PCP::BCST_VERSION_VP] = 27
      bcst[PCP::OK] = 0
      @server.post(bcst)
      stream = PCSPCP::PCPSourceStream.new(
        @peercast,
        @channel,
        @source_uri)
      @channel.start(stream)
      timeout(10) do
        until stream.is_stopped do
          if @channel.content_header and
             @channel.contents.count>=10 then
            @server.close
          else
            sleep 0.1
          end
        end
      end
      assert_nil(@channel.broadcasts.find {|b| b[1].children.get_ok })
    end

    def test_broadcast_to_other
      srv_channel = setup_srv_channel(@channel_id)
      @server = TestPCPServer.new('127.0.0.1', 17144)
      @server.channels << srv_channel

      bcst = PCP::Atom.new(PCP::BCST, [], nil)
      bcst[PCP::BCST_TTL]        = 11
      bcst[PCP::BCST_HOPS]       =  0
      bcst[PCP::BCST_FROM]       = @server.session_id
      bcst[PCP::BCST_DEST]       = PCP::GID.generate
      bcst[PCP::BCST_GROUP]      = PCP::BCST_GROUP_ALL
      bcst[PCP::BCST_CHANID]     = @channel_id
      bcst[PCP::BCST_VERSION]    = 1218
      bcst[PCP::BCST_VERSION_VP] = 27
      bcst[PCP::OK] = 0
      @server.post(bcst)
      stream = PCSPCP::PCPSourceStream.new(
        @peercast,
        @channel,
        @source_uri)
      @channel.start(stream)
      timeout(10) do
        until stream.is_stopped do
          if @channel.broadcasts.size>0 and
             @channel.content_header and
             @channel.contents.count>=10 then
            @server.close
          else
            sleep 0.1
          end
        end
      end
      posted = @channel.broadcasts.find {|b| b[1].children.get_ok }
      assert_not_nil(posted)
      assert_equal(@server.session_id.to_s, posted[0].SessionID.ToString('N'))
      assert_equal(PCP::BCST_GROUP_ALL, posted[2])
      atom = posted[1]
      assert_equal(bcst[PCP::BCST_DEST].to_s, atom.children.GetBcstDest.to_s)
    end

    def test_recv_rate
      srv_channel = setup_srv_channel(@channel_id)
      @server = TestPCPServer.new('127.0.0.1', 17144)
      @server.channels << srv_channel
      stream = PCSPCP::PCPSourceStream.new(@peercast, @channel, @source_uri)
      assert_equal 0, stream.recv_rate
      start = Time.now
      @channel.start(stream)
      timeout(5) do
        until stream.is_stopped do
          if @channel.content_header and
             @channel.contents.count>=10 then
            @server.close
          else
            sleep 0.1
          end
        end
      end
      t = Time.now
      sleep([10-(t-start), 0].max)
      recv_rate = stream.recv_rate
      assert_in_delta 120, recv_rate, 5 
    end

    def test_send_rate
      srv_channel = setup_srv_channel(@channel_id)
      @server = TestPCPServer.new('127.0.0.1', 17144)
      @server.channels << srv_channel
      stream = PCSPCP::PCPSourceStream.new(@peercast, @channel, @source_uri)
      assert_equal 0, stream.recv_rate
      start = Time.now
      @channel.start(stream)
      timeout(5) do
        until stream.is_stopped do
          if @channel.content_header and
             @channel.contents.count>=10 then
            @server.close
          else
            sleep 0.1
          end
        end
      end
      t = Time.now
      sleep([10-(t-start), 0].max)
      send_rate = stream.send_rate
      assert_in_delta 45, send_rate, 5 
    end
  end
end

