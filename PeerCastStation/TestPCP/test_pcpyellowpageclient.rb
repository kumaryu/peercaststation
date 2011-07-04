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

class TC_PCPYellowPageClientFactory < Test::Unit::TestCase
  def setup
    @endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7147)
    @peercast = PCSCore::PeerCast.new
    @peercast.start_listen(@endpoint)
  end
  
  def teardown
    @peercast.stop if @peercast
  end
  
  def test_construct
    factory = PCSPCP::PCPYellowPageClientFactory.new(@peercast)
    assert_equal(factory.Name, 'PCP')
  end

  def test_create
    factory = PCSPCP::PCPYellowPageClientFactory.new(@peercast)
    yp = factory.Create('Test YP', System::Uri.new('http://yp.example.com/'))
    assert_kind_of(PCSPCP::PCPYellowPageClient, yp)
  end
end

class TC_PCPYellowPageClient < Test::Unit::TestCase
  class FindTrackerTestYP
    AccessLog = Struct.new(:session_id, :channel_id)
    def initialize(port, channel_id)
      @channel_id = channel_id
      @session_id = GID.generate
      @port = port
      @server = TCPServer.new('127.0.0.1', @port)
      @root = nil
      @hosts = []
      @log = []
      @closed = false
      @thread = Thread.new do
        begin
          client_threads = []
          until @closed do
            client = @server.accept
            client_threads << Thread.new {
              process_client(client)
              client.close
            }
          end
        rescue
        end
      end
    end
    attr_reader :hosts
    attr_accessor :root

    def stop
      @closed = true
      @server.close
      @thread.join
    end

    class HTTPRequest
      def initialize(header)
        lines = header.split(/\r\n/)
        request = lines.shift
        @method, @path, @protocol, * = request.split(/\s+/)
        @headers = {}
        lines.each do |line|
          md = /^(.*?):(.*)$/.match(line)
          @headers[md[1].strip] = md[2].strip if md
        end
      end
      attr_reader :method, :path, :protocol, :headers

      def self.read(io)
        header = io.read(4)
        until /\r\n\r\n$/=~header do
          header << io.read(1)
        end
        self.new(header)
      end
    end

    def process_client(client)
      request = HTTPRequest.read(client)
      if %r;/channel/([A-Fa-f0-9]{32});=~request.path and
         request.headers['x-peercast-pcp']=='1' and
         $1.upcase==@channel_id.to_s.upcase then
        channel_id = $1
        client.write("#{request.protocol} 503 Unavailable\r\n\r\n")
        oleh = pcp_handshake(client)
        @root.write(client) if @root
        log = AccessLog.new(oleh[PCP_HELO_SESSIONID], channel_id)
        @log << log
        @hosts.each do |host|
          pcp_host(client, host)
        end
        pcp_quit(client)
      else
        client.write("#{request.protocol} 404 Not Found\r\n\r\n")
      end
    end

    def pcp_handshake(io)
      helo = PCPAtom.new(PCP_HELO, [], nil)
      helo[PCP_HELO_SESSIONID] = @session_id
      helo[PCP_HELO_VERSION]   = 1218
      helo[PCP_HELO_PORT]      = @port
      helo[PCP_HELO_AGENT]     = self.class.name
      helo.write(io)
      oleh = PCPAtom.read(io)
    end

    def pcp_host(io, host)
      atom = PCPAtom.new(PCP_HOST, [], nil)
      atom[PCP_HOST_CHANID] = @channel_id
      atom[PCP_HOST_ID]   = host.SessionID
      atom[PCP_HOST_IP]   = host.global_end_point.address
      atom[PCP_HOST_PORT] = host.global_end_point.port
      atom[PCP_HOST_NUMR] = host.relay_count
      atom[PCP_HOST_NUML] = host.direct_count
      atom[PCP_HOST_FLAGS1] = 
          (host.is_firewalled   ? PCP_HOST_FLAGS1_PUSH : 0) |
          (host.is_tracker      ? PCP_HOST_FLAGS1_TRACKER : 0) |
          (host.is_relay_full   ? 0 : PCP_HOST_FLAGS1_RELAY) |
          (host.is_direct_full  ? 0 : PCP_HOST_FLAGS1_DIRECT) |
          (host.is_receiving    ? PCP_HOST_FLAGS1_RECV : 0) |
          (host.is_control_full ? 0 : PCP_HOST_FLAGS1_CIN)
      atom.write(io)
    end

    def pcp_quit(io)
      quit = PCPAtom.new(PCP_QUIT, nil, nil)
      quit.value = PCP_ERROR_QUIT + PCP_ERROR_UNAVAILABLE
      quit.write(io)
    end
  end

  def setup
    @session_id = System::Guid.new_guid
    @endpoint   = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7147)
    @peercast   = PCSCore::PeerCast.new
    @peercast.start_listen(@endpoint)
    @channel_id = System::Guid.parse('531DC8DFC7FB42928AC2C0A626517A87')
  end
  
  def teardown
    @peercast.stop if @peercast
  end
  
  def test_construct
    yp = PCSPCP::PCPYellowPageClient.new(
      @peercast,
      'TestYP',
      System::Uri.new('http://yp.example.com/'))
    assert_equal(@peercast, yp.PeerCast)
    assert_equal('TestYP',  yp.Name)
    assert_equal('http://yp.example.com/', yp.Uri.ToString.to_s)
  end

  def test_announce
  end

  def test_find_tracker_connection_failed
    pcpyp = PCSPCP::PCPYellowPageClient.new(@peercast, 'TestYP', System::Uri.new('http://localhost:14288/'))
    channel_id = System::Guid.parse('4361BFA4F8E84328B9E975AAA7FA9E5E')
    uri = pcpyp.find_tracker(channel_id)
    assert_nil(uri)
  end

  def test_find_tracker_not_found
    pcpyp = PCSPCP::PCPYellowPageClient.new(@peercast, 'TestYP', System::Uri.new('http://localhost:14288/'))
    channel_id = System::Guid.parse('4361BFA4F8E84328B9E975AAA7FA9E5E')
    yp = FindTrackerTestYP.new(14288, '75C49DCA166F455A9C9DC3C64A738CD7')
    uri = pcpyp.find_tracker(channel_id)
    assert_nil(uri)
  ensure
    yp.stop
  end

  def create_host(tracker)
    host = PCSCore::HostBuilder.new
    host.SessionID = System::Guid.new_guid
    host.is_tracker = tracker
    host.global_end_point = System::Net::IPEndPoint.new(System::Net::IPAddress.parse("192.168.1.#{rand(253)+1}"), 7144)
    host.to_host
  end

  def test_find_tracker_found
    pcpyp = PCSPCP::PCPYellowPageClient.new(@peercast, 'TestYP', System::Uri.new('http://localhost:14288/'))
    channel_id = System::Guid.parse('4361BFA4F8E84328B9E975AAA7FA9E5E')
    yp = FindTrackerTestYP.new(14288, channel_id)
    yp.hosts << create_host(false)
    yp.hosts << create_host(false)
    yp.hosts << create_host(false)
    yp.hosts << create_host(true)
    yp.hosts << create_host(false)
    uri = pcpyp.find_tracker(channel_id)
    assert_equal("pcp://#{yp.hosts[3].global_end_point.ToString}/channel/#{channel_id.ToString('N')}", uri.to_s)
  ensure
    yp.stop
  end

  def test_find_tracker_found_with_root
    pcpyp = PCSPCP::PCPYellowPageClient.new(@peercast, 'TestYP', System::Uri.new('http://localhost:14288/'))
    channel_id = System::Guid.parse('4361BFA4F8E84328B9E975AAA7FA9E5E')
    yp = FindTrackerTestYP.new(14288, channel_id)
    yp.root = PCPAtom.new(PCP_ROOT, [], nil)
    yp.root[PCP_ROOT_CHECKVER] = 1218
    yp.hosts << create_host(false)
    yp.hosts << create_host(false)
    yp.hosts << create_host(false)
    yp.hosts << create_host(true)
    yp.hosts << create_host(false)
    uri = pcpyp.find_tracker(channel_id)
    assert_not_nil(uri)
  ensure
    yp.stop
  end

  def test_find_tracker_not_tracker
    pcpyp = PCSPCP::PCPYellowPageClient.new(@peercast, 'TestYP', System::Uri.new('http://localhost:14288/'))
    channel_id = System::Guid.parse('4361BFA4F8E84328B9E975AAA7FA9E5E')
    yp = FindTrackerTestYP.new(14288, channel_id)
    yp.hosts << create_host(false)
    uri = pcpyp.find_tracker(channel_id)
    assert_nil(uri)
  ensure
    yp.stop
  end
end

