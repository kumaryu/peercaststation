# coding: utf-8
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
require 'pcp/rootserver'
require 'shoulda/context'
require 'timeout'

module TestPCP
  class TC_PCPYellowPageClientFactory < Test::Unit::TestCase
    def setup
      @endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7147)
      accepts =
        PeerCastStation::Core::OutputStreamType.metadata |
        PeerCastStation::Core::OutputStreamType.play |
        PeerCastStation::Core::OutputStreamType.relay |
        PeerCastStation::Core::OutputStreamType.interface
      @peercast = PCSCore::PeerCast.new
      @peercast.start_listen(@endpoint, accepts, accepts)
    end
    
    def teardown
      @peercast.stop if @peercast
    end
    
    def test_construct
      factory = PCSPCP::PCPYellowPageClientFactory.new(@peercast)
      assert_equal(factory.Name, 'PCP')
      assert_equal(factory.Protocol, 'pcp')
      assert(factory.respond_to?(:create_obj_ref))
    end

    def test_create
      factory = PCSPCP::PCPYellowPageClientFactory.new(@peercast)
      yp = factory.Create('Test YP', System::Uri.new('pcp://yp.example.com/'))
      assert_kind_of(PCSPCP::PCPYellowPageClient, yp)
    end
    
    def test_check_uri
      factory = PCSPCP::PCPYellowPageClientFactory.new(@peercast)
      assert(!factory.CheckURI(System::Uri.new('http://yp.example.com/')))
      assert(factory.CheckURI(System::Uri.new('pcp://yp.example.com/')))
    end
  end

  class TC_PCPYellowPageClient < Test::Unit::TestCase
    class FindTrackerTestYP
      AccessLog = Struct.new(:session_id, :channel_id)
      def initialize(port, channel_id)
        @channel_id = channel_id
        @session_id = PCP::GID.generate
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
          log = AccessLog.new(oleh[PCP::HELO_SESSIONID], channel_id)
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
        helo = PCP::Atom.new(PCP::HELO, [], nil)
        helo[PCP::HELO_SESSIONID] = @session_id
        helo[PCP::HELO_VERSION]   = 1218
        helo[PCP::HELO_PORT]      = @port
        helo[PCP::HELO_AGENT]     = self.class.name
        helo.write(io)
        oleh = PCP::Atom.read(io)
      end

      def pcp_host(io, host)
        atom = PCP::Atom.new(PCP::HOST, [], nil)
        atom[PCP::HOST_CHANID] = @channel_id
        atom[PCP::HOST_ID]   = host.SessionID
        atom[PCP::HOST_IP]   = host.global_end_point.address
        atom[PCP::HOST_PORT] = host.global_end_point.port
        atom[PCP::HOST_NUMR] = host.relay_count
        atom[PCP::HOST_NUML] = host.direct_count
        atom[PCP::HOST_FLAGS1] = 
            (host.is_firewalled   ? PCP::HOST_FLAGS1_PUSH : 0) |
            (host.is_tracker      ? PCP::HOST_FLAGS1_TRACKER : 0) |
            (host.is_relay_full   ? 0 : PCP::HOST_FLAGS1_RELAY) |
            (host.is_direct_full  ? 0 : PCP::HOST_FLAGS1_DIRECT) |
            (host.is_receiving    ? PCP::HOST_FLAGS1_RECV : 0) |
            (host.is_control_full ? 0 : PCP::HOST_FLAGS1_CIN)
        atom.write(io)
      end

      def pcp_quit(io)
        quit = PCP::Atom.new(PCP::QUIT, nil, nil)
        quit.value = PCP::ERROR_QUIT + PCP::ERROR_UNAVAILABLE
        quit.write(io)
      end
    end

    def assert_not_timeout(expires=5, &block)
      assert_nothing_raised(Timeout::Error) do
        timeout(expires, &block)
      end
    end

    def wait_equal_status(status, announcing_channel, expires=5)
      status = PCSCore::AnnouncingStatus.send(status) if status.kind_of?(Symbol)
      timeout(expires) do
        sleep 1 until announcing_channel.Status==status
      end
    end

    def wait_not_equal_status(status, announcing_channel, expires=5)
      status = PCSCore::AnnouncingStatus.send(status) if status.kind_of?(Symbol)
      timeout(expires) do
        sleep 1 until announcing_channel.Status!=status
      end
    end

    def assert_equal_status(status, announcing_channel, expires=5)
      status = PCSCore::AnnouncingStatus.send(status) if status.kind_of?(Symbol)
      begin
        timeout(expires) do
          sleep 1 until announcing_channel.Status==status
        end
      rescue Timeout::Error
      end
      assert_equal status.to_s, announcing_channel.Status.to_s
    end

    def assert_not_equal_status(status, announcing_channel, expires=5)
      status = PCSCore::AnnouncingStatus.send(status) if status.kind_of?(Symbol)
      begin
        timeout(expires) do
          sleep 1 until announcing_channel.Status!=status
        end
      rescue Timeout::Error
      end
      assert_not_equal status.to_s, announcing_channel.Status.to_s
    end

    def setup
      @endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7147)
      accepts =
        PeerCastStation::Core::OutputStreamType.metadata |
        PeerCastStation::Core::OutputStreamType.play |
        PeerCastStation::Core::OutputStreamType.relay |
        PeerCastStation::Core::OutputStreamType.interface
      @peercast = PCSCore::PeerCast.new
      @peercast.OutputStreamFactories.add(PeerCastStation::PCP::PCPPongOutputStreamFactory.new(@peercast))
      @peercast.start_listen(@endpoint, accepts, accepts)
    end
    
    def teardown
      @peercast.stop if @peercast
    end

    context 'construct' do
      setup do
        @factory = PCSPCP::PCPYellowPageClientFactory.new(@peercast)
        @yp = @factory.Create('TestYP', System::Uri.new('pcp://yp.example.com/'))
      end

      should 'Protocolがpcpである' do
        assert_equal 'pcp', @yp.Protocol
      end

      should '各種プロパティがコンストラクタに渡したのと一致する' do
        assert_equal @peercast, @yp.PeerCast
        assert_equal 'TestYP',  @yp.Name
        assert_equal 'pcp://yp.example.com/', @yp.Uri.ToString.to_s
      end
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
      yp.root = PCP::Atom.new(PCP::ROOT, [], nil)
      yp.root[PCP::ROOT_CHECKVER] = 1218
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

    def create_channel(channel_id, name)
      channel = PCSCore::Channel.new(@peercast, channel_id, @peercast.BroadcastID, System::Uri.new('http://127.0.0.1:8080/'))
      info = PCSCore::AtomCollection.new
      info.SetChanInfoName(name)
      info.SetChanInfoBitrate(7144)
      info.SetChanInfoGenre('test')
      info.SetChanInfoDesc('test channel')
      info.SetChanInfoURL('http://example.com/')
      channel.channel_info = PCSCore::ChannelInfo.new(info)
      track = PCSCore::AtomCollection.new
      track.SetChanTrackTitle('title')
      track.SetChanTrackAlbum('album')
      track.SetChanTrackCreator('creator')
      track.SetChanTrackURL('url')
      channel.channel_track = PCSCore::ChannelTrack.new(track)
      channel
    end

    context 'Announce' do
      setup do
        @client  = PCSPCP::PCPYellowPageClient.new(@peercast, 'TestYP', System::Uri.new('http://127.0.0.1:14288/'))
        @channel = create_channel(System::Guid.new_guid, 'Test Channel')
      end

      teardown do
        @client.stop_announce
      end

      should 'AnnouncingChannelsにチャンネルを追加する' do
        announcing = @client.Announce(@channel)
        assert_equal 1,          @client.AnnouncingChannels.Count
        assert_equal announcing, @client.AnnouncingChannels[0]
        assert_equal @channel,  announcing.Channel
      end

      context 'サーバが無い場合' do
        should '追加したチャンネルのStatusをErrorにする' do
          announcing = @client.Announce(@channel)
          wait_not_equal_status :idle,  announcing
          assert_equal_status   :error, announcing
        end
      end

      context 'サーバがある場合' do
        setup do
          @server = PCP::RootServer.new('127.0.0.1', 14288)
        end

        teardown do
          @server.close
        end

        should '追加したチャンネルのStatusをConnectingかConnectedにする' do
          announcing = @client.Announce(@channel)
          wait_not_equal_status :idle, announcing
          assert [
            PCSCore::AnnouncingStatus.connecting,
            PCSCore::AnnouncingStatus.connected,
          ].include?(announcing.Status)
        end

        should 'Connectedになったチャンネルはサーバに追加されてる' do
          announcing = @client.Announce(@channel)
          wait_equal_status :connected, announcing
          assert_equal 1, @server.channels.size
          c = @server.channels.values.first
          assert_equal @channel.ChannelID.ToString('N').to_s, c.channel_id.to_s
          assert_equal @channel.BroadcastID.to_s,     c.broadcast_id.to_s
          assert_equal @channel.ChannelInfo.Name,     c.info[PCP::CHAN_INFO_NAME]
          assert_equal @channel.ChannelInfo.Bitrate,  c.info[PCP::CHAN_INFO_BITRATE]
          assert_equal @channel.ChannelInfo.Genre,    c.info[PCP::CHAN_INFO_GENRE]
          assert_equal @channel.ChannelInfo.Desc,     c.info[PCP::CHAN_INFO_DESC]
          assert_equal @channel.ChannelInfo.URL,      c.info[PCP::CHAN_INFO_URL]
          assert_equal @channel.ChannelTrack.Name,    c.track[PCP::CHAN_TRACK_TITLE]
          assert_equal @channel.ChannelTrack.Album,   c.track[PCP::CHAN_TRACK_ALBUM]
          assert_equal @channel.ChannelTrack.Creator, c.track[PCP::CHAN_TRACK_CREATOR]
          assert_equal @channel.ChannelTrack.URL,     c.track[PCP::CHAN_TRACK_URL]
          assert_equal 1, c.hosts.size
          host = c.hosts.values.first
          assert_equal @peercast.SessionID.to_s,   host.session_id.to_s
          assert_equal @peercast.BroadcastID.to_s, host.broadcast_id.to_s
          assert_equal @peercast.agent_name,       host.agent
          assert_equal @endpoint.address.to_s,     host.ip.to_s
          assert_equal 7147,                       host.port
          assert_equal 1218,                       host.version
          assert_equal 27,                         host.vp_version
          assert_equal 0,                          host.info[PCP::HOST_NUML]
          assert_equal 0,                          host.info[PCP::HOST_NUMR]
          assert (host.info[PCP::HOST_FLAGS1] & PCP::HOST_FLAGS1_DIRECT)!=0
          assert (host.info[PCP::HOST_FLAGS1] & PCP::HOST_FLAGS1_RELAY)!=0
          assert (host.info[PCP::HOST_FLAGS1] & PCP::HOST_FLAGS1_PUSH)==0
          assert (host.info[PCP::HOST_FLAGS1] & PCP::HOST_FLAGS1_TRACKER)!=0
        end

        should 'サーバとの接続が切断されたらStatusがErrorになる' do
          log do
          announcing = @client.Announce(@channel)
          wait_equal_status :connected, announcing
          @server.close
          assert_equal_status :error, announcing
          end
        end

        should '複数のチャンネルも掲載できる' do
          channel1 = create_channel(System::Guid.new_guid, 'Test1')
          channel2 = create_channel(System::Guid.new_guid, 'Test2')
          announcing1 = @client.Announce(channel1)
          announcing2 = @client.Announce(channel2)
          wait_equal_status :connected, announcing1
          wait_equal_status :connected, announcing2
          assert_equal 2, @server.channels.size
          c = @server.channels[PCP::GID.from_string(channel1.ChannelID.to_s)]
          assert_equal channel1.ChannelID.to_s,   c.channel_id.to_s
          assert_equal channel1.BroadcastID.to_s, c.broadcast_id.to_s
          assert_equal 'Test1',                   c.info[PCP::CHAN_INFO_NAME]
          c = @server.channels[PCP::GID.from_string(channel2.ChannelID.to_s)]
          assert_equal channel2.ChannelID.to_s,   c.channel_id.to_s
          assert_equal channel2.BroadcastID.to_s, c.broadcast_id.to_s
          assert_equal 'Test2',                   c.info[PCP::CHAN_INFO_NAME]
        end
      end
    end

    context 'RestartAnnounce(announcing_channel)' do
      setup do
        @client  = PCSPCP::PCPYellowPageClient.new(@peercast, 'TestYP', System::Uri.new('http://127.0.0.1:14288/'))
        @channel = create_channel(System::Guid.new_guid, 'Test Channel')
        @server  = nil
      end

      teardown do
        @client.stop_announce
        @server.close if @server
      end

      should 'Errorのチャンネルでも再接続する' do
        announcing = @client.Announce(@channel)
        @client.Announce(@channel)
        wait_equal_status :error, announcing
        @client.RestartAnnounce(announcing)
        @server = PCP::RootServer.new('127.0.0.1', 14288)
        wait_not_equal_status :error, announcing
        assert [
          PCSCore::AnnouncingStatus.connecting,
          PCSCore::AnnouncingStatus.connected,
        ].include?(announcing.Status)
        @server.close
      end

      should 'Connectedのチャンネルでも再接続する' do
        @server = PCP::RootServer.new('127.0.0.1', 14288)
        announcing = @client.Announce(@channel)
        @client.Announce(@channel)
        wait_equal_status :connected, announcing
        @server.close
        @client.RestartAnnounce(announcing)
        wait_not_equal_status :connected, announcing
        assert [
          PCSCore::AnnouncingStatus.connecting,
          PCSCore::AnnouncingStatus.error,
        ].include?(announcing.Status)
      end
    end

    context 'RestartAnnounce()' do
      setup do
        @client = PCSPCP::PCPYellowPageClient.new(@peercast, 'TestYP', System::Uri.new('http://127.0.0.1:14288/'))
        @server = nil
      end

      teardown do
        @client.stop_announce
        @server.close if @server
      end

      should '全てのチャンネルが再接続する' do
        channel1 = create_channel(System::Guid.new_guid, 'Test Channel 1')
        channel2 = create_channel(System::Guid.new_guid, 'Test Channel 2')
        announcing1 = @client.Announce(channel1)
        announcing2 = @client.Announce(channel2)
        wait_equal_status :error, announcing1
        wait_equal_status :error, announcing2
        @server = PCP::RootServer.new('127.0.0.1', 14288)
        @client.RestartAnnounce
        wait_not_equal_status :error, announcing1
        wait_not_equal_status :error, announcing2
        [announcing1, announcing2].each do |announcing|
          assert [
            PCSCore::AnnouncingStatus.connecting,
            PCSCore::AnnouncingStatus.connected,
          ].include?(announcing1.Status)
        end
        @server.close
      end
    end

    context 'StopAnnounce(announcing_channel)' do
      setup do
        @client  = PCSPCP::PCPYellowPageClient.new(@peercast, 'TestYP', System::Uri.new('http://127.0.0.1:14288/'))
        @channel = create_channel(System::Guid.new_guid, 'Test Channel')
        @server  = nil
      end

      teardown do
        @client.stop_announce
        @server.close if @server
      end

      should 'AnnouncingChannelsから取り除く' do
        channel1 = create_channel(System::Guid.new_guid, 'Test1')
        channel2 = create_channel(System::Guid.new_guid, 'Test2')
        announcing1 = @client.Announce(channel1)
        announcing2 = @client.Announce(channel2)
        @client.StopAnnounce(announcing1)
        assert_equal 1, @client.AnnouncingChannels.Count
        assert_equal announcing2, @client.AnnouncingChannels[0]
        @client.StopAnnounce(announcing2)
        assert_equal 0, @client.AnnouncingChannels.Count
      end

      should 'ConnectingかConnectedだったAnnouncingChannelのStatusがIdleになる' do
        begin
          server = PCP::RootServer.new('127.0.0.1', 14288)
          announcing = @client.Announce(@channel)
          wait_not_equal_status :idle, announcing
          @client.StopAnnounce(announcing)
          wait_not_equal_status :connecting, announcing
          wait_not_equal_status :connected,  announcing
          assert_equal PCSCore::AnnouncingStatus.idle, announcing.Status
        ensure
          server.close
        end
      end
    end

    context 'StopAnnounce()' do
      setup do
        @client = PCSPCP::PCPYellowPageClient.new(@peercast, 'TestYP', System::Uri.new('http://127.0.0.1:14288/'))
      end

      teardown do
        @client.stop_announce
      end

      should 'AnnouncingChannelsから全て取り除く' do
        channel1 = create_channel(System::Guid.new_guid, 'Test1')
        channel2 = create_channel(System::Guid.new_guid, 'Test2')
        announcing1 = @client.Announce(channel1)
        announcing2 = @client.Announce(channel2)
        @client.StopAnnounce()
        assert_equal 0, @client.AnnouncingChannels.Count
      end
    end
  end
end

