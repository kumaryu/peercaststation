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
require 'test_core_common'
require 'shoulda/context'

module TestCore
  class TC_CoreChannel < Test::Unit::TestCase
    def id4(s)
      PeerCastStation::Core::ID4.new(s.to_clr_string)
    end

    def setup
      @peercast = PeerCastStation::Core::PeerCast.new
      @peercast.start_listen(System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7147), 15, 15)
    end
    
    def teardown
      @peercast.stop if @peercast
    end

    def test_construct
      channel_id = System::Guid.new_guid
      channel = PeerCastStation::Core::Channel.new(@peercast, channel_id, System::Uri.new('mock://localhost'))
      assert_nil(channel.source_stream)
      assert_equal(@peercast, channel.PeerCast)
      assert_equal('mock://localhost/', channel.source_uri.to_s)
      assert_equal(channel_id, channel.ChannelID)
      assert_equal(System::Guid.empty, channel.BroadcastID)
      assert_equal(PeerCastStation::Core::SourceStreamStatus.Idle, channel.status)
      assert_equal(0, channel.output_streams.count)
      assert_equal(0, channel.nodes.count)
      assert_nil(channel.content_header)
      assert_equal(0, channel.contents.count)
      assert(channel.respond_to?(:create_obj_ref))
    end

    def test_construct_bcid
      channel_id = System::Guid.new_guid
      channel = PeerCastStation::Core::Channel.new(
        @peercast, channel_id, @peercast.BroadcastID, System::Uri.new('mock://localhost'))
      assert_nil(channel.source_stream)
      assert_equal(@peercast, channel.PeerCast)
      assert_equal('mock://localhost/', channel.source_uri.to_s)
      assert_equal(channel_id, channel.ChannelID)
      assert_equal(@peercast.BroadcastID, channel.BroadcastID)
      assert_equal(PeerCastStation::Core::SourceStreamStatus.Idle, channel.status)
      assert_equal(0, channel.output_streams.count)
      assert_equal(0, channel.nodes.count)
      assert_nil(channel.content_header)
      assert_equal(0, channel.contents.count)
    end
    
    def test_changed
      property_log = []
      content_log = []
      channel = PeerCastStation::Core::Channel.new(@peercast, System::Guid.empty, System::Uri.new('mock://localhost'))
      channel.channel_info_changed {|sender, e| property_log << 'ChannelInfo' }
      channel.channel_track_changed {|sender, e| property_log << 'ChannelTrack' }
      channel.status_changed {|sender, e| property_log << 'Status' }
      channel.nodes_changed {|sender, e| property_log << 'Nodes' }
      channel.output_streams_changed {|sender, e| property_log << 'OutputStreams' }
      channel.content_changed {|sender, e| content_log << 'content' }
      channel.source_stream = MockSourceStream.new(channel, channel.source_uri)
      chaninfo = PeerCastStation::Core::AtomCollection.new
      chaninfo.set_chan_info_name('bar')
      channel.channel_info = PeerCastStation::Core::ChannelInfo.new(chaninfo)
      chantrack = PeerCastStation::Core::AtomCollection.new
      chantrack.set_chan_track_title('foo')
      channel.channel_track = PeerCastStation::Core::ChannelTrack.new(chantrack)
      channel.add_output_stream(MockOutputStream.new)
      channel.add_node(PeerCastStation::Core::HostBuilder.new.to_host)
      channel.content_header = PeerCastStation::Core::Content.new(0, System::TimeSpan.zero, 0, 'header')
      channel.contents.add(PeerCastStation::Core::Content.new(0, System::TimeSpan.from_seconds(1), 1, 'body'))
      assert_equal(4, property_log.size)
      assert_equal('ChannelInfo',   property_log[0])
      assert_equal('ChannelTrack',  property_log[1])
      assert_equal('OutputStreams', property_log[2])
      assert_equal('Nodes',         property_log[3])
      assert_equal(2, content_log.size)
      assert_equal('content', content_log[0])
      assert_equal('content', content_log[1])
    end
    
    def test_reconnect
      log = []
      channel = PeerCastStation::Core::Channel.new(@peercast, System::Guid.empty, System::Uri.new('mock://localhost'))
      channel.closed { log << 'Closed' }
      assert(channel.is_closed)
      output_stream = MockOutputStream.new
      channel.add_output_stream(output_stream)
      channel.start(MockSourceStream.new(channel, channel.source_uri))
      sleep(0.1)
      channel.reconnect
      sleep(0.1)
      channel.close
      assert_equal(PeerCastStation::Core::SourceStreamStatus.idle, channel.status)
      assert_equal(:start,     channel.source_stream.log[0][0])
      assert_equal(:reconnect, channel.source_stream.log[1][0])
      assert_equal(:stop,      output_stream.log[0][0])
      assert_equal('Closed', log[0])
      assert(channel.is_closed)
    end

    def test_close
      log = []
      channel = PeerCastStation::Core::Channel.new(@peercast, System::Guid.empty, System::Uri.new('mock://localhost'))
      channel.closed { log << 'Closed' }
      assert(channel.is_closed)
      output_stream = MockOutputStream.new
      channel.add_output_stream(output_stream)
      channel.start(MockSourceStream.new(channel, channel.source_uri))
      assert(!channel.is_closed)
      sleep(0.1)
      channel.close
      assert_equal(PeerCastStation::Core::SourceStreamStatus.idle, channel.status)
      assert_equal(:start, channel.source_stream.log[0][0])
      assert_equal(:stop,  output_stream.log[0][0])
      assert_equal('Closed', log[0])
      assert(channel.is_closed)
    end

    def test_broadcast
      output = MockOutputStream.new
      channel = PeerCastStation::Core::Channel.new(@peercast, System::Guid.empty, System::Uri.new('mock://localhost'))
      channel.add_output_stream(output)
      source = MockSourceStream.new(channel, channel.source_uri)
      from = PeerCastStation::Core::HostBuilder.new.to_host
      packet_trackers = PeerCastStation::Core::Atom.new(id4('test'), 'trackers'.to_clr_string)
      packet_relays   = PeerCastStation::Core::Atom.new(id4('test'), 'relays'.to_clr_string)
      source.start_proc = proc {
        channel.broadcast(from, packet_trackers, PeerCastStation::Core::BroadcastGroup.trackers)
        channel.broadcast(from, packet_relays,   PeerCastStation::Core::BroadcastGroup.relays)
      }
      channel.start(source)
      sleep(0.1)
      channel.close
      source_log = source.log.select {|log| log[0]==:post }
      output_log = output.log.select {|log| log[0]==:post }
      assert_equal(2, source_log.size)
      assert_equal(from,            source_log[0][1])
      assert_equal(packet_trackers, source_log[0][2])
      assert_equal(from,            source_log[1][1])
      assert_equal(packet_relays,   source_log[1][2])
      assert_equal(1, output_log.size)
      assert_equal(from,            output_log[0][1])
      assert_equal(packet_relays,   output_log[0][2])
    end

    class TestAccessController < PeerCastStation::Core::AccessController
      def self.new(peercast, relayable, playable)
        instance = super(peercast)
        instance.instance_eval do 
          @relayable = relayable
          @playable = playable
        end
        instance
      end

      def is_channel_playable(channel, output_stream=nil)
        @playable
      end

      def is_channel_relayable(channel, output_stream=nil)
        @relayable
      end
    end

    def test_is_relay_full
      channel = PeerCastStation::Core::Channel.new(
        @peercast, System::Guid.empty, System::Uri.new('mock://localhost'))
      @peercast.access_controller = TestAccessController.new(@peercast, true, true)
      assert(!channel.is_relay_full)
      @peercast.access_controller = TestAccessController.new(@peercast, false, true)
      assert(channel.is_relay_full)
    end

    def test_is_direct_full
      channel = PeerCastStation::Core::Channel.new(
        @peercast, System::Guid.empty, System::Uri.new('mock://localhost'))
      @peercast.access_controller = TestAccessController.new(@peercast, true, true)
      assert(!channel.is_direct_full)
      @peercast.access_controller = TestAccessController.new(@peercast, true, false)
      assert(channel.is_direct_full)
    end

    def test_local_directs
      channel = PeerCastStation::Core::Channel.new(
        @peercast, System::Guid.empty, System::Uri.new('mock://localhost'))
      assert_equal(0, channel.output_streams.count)
      assert_equal(0, channel.local_directs)
      channel.add_output_stream(MockOutputStream.new(PeerCastStation::Core::OutputStreamType.play))
      channel.add_output_stream(MockOutputStream.new(PeerCastStation::Core::OutputStreamType.relay))
      channel.add_output_stream(MockOutputStream.new(PeerCastStation::Core::OutputStreamType.metadata))
      channel.add_output_stream(MockOutputStream.new(
        PeerCastStation::Core::OutputStreamType.play |
        PeerCastStation::Core::OutputStreamType.relay))
      channel.add_output_stream(MockOutputStream.new(
        PeerCastStation::Core::OutputStreamType.relay |
        PeerCastStation::Core::OutputStreamType.metadata))
      channel.add_output_stream(MockOutputStream.new(
        PeerCastStation::Core::OutputStreamType.play |
        PeerCastStation::Core::OutputStreamType.metadata))
      channel.add_output_stream(MockOutputStream.new(
        PeerCastStation::Core::OutputStreamType.play |
        PeerCastStation::Core::OutputStreamType.relay |
        PeerCastStation::Core::OutputStreamType.metadata))
      assert_equal(7, channel.output_streams.count)
      assert_equal(4, channel.local_directs)
    end

    def test_local_relays
      channel = PeerCastStation::Core::Channel.new(
        @peercast, System::Guid.empty, System::Uri.new('mock://localhost'))
      assert_equal(0, channel.output_streams.count)
      assert_equal(0, channel.local_directs)
      channel.add_output_stream(MockOutputStream.new(PeerCastStation::Core::OutputStreamType.play))
      channel.add_output_stream(MockOutputStream.new(PeerCastStation::Core::OutputStreamType.relay))
      channel.add_output_stream(MockOutputStream.new(PeerCastStation::Core::OutputStreamType.metadata))
      channel.add_output_stream(MockOutputStream.new(
        PeerCastStation::Core::OutputStreamType.play |
        PeerCastStation::Core::OutputStreamType.relay))
      channel.add_output_stream(MockOutputStream.new(
        PeerCastStation::Core::OutputStreamType.relay |
        PeerCastStation::Core::OutputStreamType.metadata))
      channel.add_output_stream(MockOutputStream.new(
        PeerCastStation::Core::OutputStreamType.play |
        PeerCastStation::Core::OutputStreamType.metadata))
      channel.add_output_stream(MockOutputStream.new(
        PeerCastStation::Core::OutputStreamType.play |
        PeerCastStation::Core::OutputStreamType.relay |
        PeerCastStation::Core::OutputStreamType.metadata))
      assert_equal(7, channel.output_streams.count)
      assert_equal(4, channel.local_relays)
    end

    def test_total_directs
      channel = PeerCastStation::Core::Channel.new(
        @peercast, System::Guid.empty, System::Uri.new('mock://localhost'))
      assert_equal(0, channel.output_streams.count)
      assert_equal(0, channel.local_directs)
      assert_equal(0, channel.total_directs)
      channel.add_output_stream(MockOutputStream.new(PeerCastStation::Core::OutputStreamType.play))
      channel.add_output_stream(MockOutputStream.new(PeerCastStation::Core::OutputStreamType.play))
      channel.add_output_stream(MockOutputStream.new(PeerCastStation::Core::OutputStreamType.play))
      assert_equal(3, channel.local_directs)
      assert_equal(3, channel.total_directs)

      relays  = 0
      directs = 0
      10.times do |i|
        node   = PeerCastStation::Core::HostBuilder.new
        node.SessionID = System::Guid.new_guid
        relay  = rand(i)
        direct = rand(i)
        node.relay_count = relay
        node.direct_count = direct
        relays  += relay
        directs += direct
        channel.add_node(node.to_host)
      end
      assert_equal(3, channel.local_directs)
      assert_equal(3+directs, channel.total_directs)
    end

    def test_total_relays
      channel = PeerCastStation::Core::Channel.new(
        @peercast, System::Guid.empty, System::Uri.new('mock://localhost'))
      assert_equal(0, channel.output_streams.count)
      assert_equal(0, channel.local_relays)
      assert_equal(0, channel.total_relays)
      channel.add_output_stream(MockOutputStream.new(PeerCastStation::Core::OutputStreamType.relay))
      channel.add_output_stream(MockOutputStream.new(PeerCastStation::Core::OutputStreamType.relay))
      channel.add_output_stream(MockOutputStream.new(PeerCastStation::Core::OutputStreamType.relay))
      assert_equal(3, channel.local_relays)
      assert_equal(3, channel.total_relays)

      relays  = 0
      directs = 0
      10.times do |i|
        node   = PeerCastStation::Core::HostBuilder.new
        node.SessionID = System::Guid.new_guid
        relay  = rand(i)
        direct = rand(i)
        node.relay_count = relay
        node.direct_count = direct
        relays  += relay
        directs += direct
        channel.add_node(node.to_host)
      end
      assert_equal(3, channel.local_relays)
      assert_equal(3+relays, channel.total_relays)
    end

    def test_self_node
      channel = PeerCastStation::Core::Channel.new(
        @peercast, System::Guid.empty, System::Uri.new('mock://localhost'))
      local_end_point = @peercast.get_local_end_point(
        System::Net::Sockets::AddressFamily.inter_network,
        PeerCastStation::Core::OutputStreamType.relay);
      global_end_point = @peercast.get_global_end_point(
        System::Net::Sockets::AddressFamily.inter_network,
        PeerCastStation::Core::OutputStreamType.relay);
      host = channel.self_node
      assert_equal(@peercast.SessionID, host.SessionID)
      assert_equal(local_end_point, host.local_end_point)
      assert_equal(global_end_point, host.global_end_point)
      assert_equal(@peercast.IsFirewalled.nil? ? true : peercast.IsFirewalled, host.IsFirewalled)
      assert_equal(channel.local_directs, host.direct_count)
      assert_equal(channel.local_relays,  host.relay_count)
      assert_equal(!@peercast.AccessController.IsChannelPlayable(channel),  host.IsDirectFull)
      assert_equal(!@peercast.AccessController.IsChannelRelayable(channel), host.IsRelayFull)
      assert_equal(true, host.IsReceiving)
    end

    class TestSourceStream
      include PeerCastStation::Core::ISourceStream
      
      def initialize(channel, tracker)
        @channel = channel
        @tracker = tracker
        @status = PeerCastStation::Core::SourceStreamStatus.idle
        @status_changed = []
        @stopped = []
        @paused = true
        @thread = nil
      end
      attr_reader :tracker, :channel, :status
      attr_accessor :paused
      
      def add_StatusChanged(handler)
        @status_changed << handler
      end
      
      def remove_StatusChanged(handler)
        @status_changed.delete(handler)
      end

      def add_Stopped(handler)
        @stopped << handler
      end
      
      def remove_Stopped(handler)
        @stopped.delete(handler)
      end

      def post(from, packet)
      end
      
      def start
        @thread = Thread.new {
          while @paused do
            sleep(0.1)
          end
          args = System::EventArgs.new
          @stopped.each do |handler|
            handler.invoke(self, args)
          end
        }
      end
      
      def reconnect
      end

      def stop
      end
    end
    
    def test_uptime
      channel = PeerCastStation::Core::Channel.new(@peercast, System::Guid.empty, System::Uri.new('mock://localhost'))
      closed = false
      channel.closed { closed = true }
      assert_equal(System::TimeSpan.zero, channel.uptime)
      channel.add_output_stream(MockOutputStream.new)
      source = TestSourceStream.new(channel, channel.source_uri)
      source.paused = true
      channel.start(source)
      sleep(0.1)
      assert(0<channel.uptime.total_milliseconds)
      source.paused = false
      channel.close
      sleep(0.1) until closed
      assert_equal(System::TimeSpan.zero, channel.uptime)
    end
  end

  class TC_CoreContentCollection < Test::Unit::TestCase
    def setup
      @collection = PeerCastStation::Core::ContentCollection.new
    end

    context 'construct' do
      should 'be contents empty' do
        assert_equal 0, @collection.count
      end

      should 'limit_packets is 100' do
        assert_equal 100, @collection.limit_packets
      end
    end

    context 'add' do
      should 'add contents ordered by stream index, timestamp and position' do
        1000.times do
          c = PeerCastStation::Core::Content.new(rand(10000), System::TimeSpan.from_seconds(rand(10000)), rand(1000000), 'content')
          @collection.add(c)
        end
        prev = nil
        @collection.each do |c|
          if prev then
            assert((prev.stream<c.stream) ||
                   (prev.stream==c.stream && prev.timestamp<c.timestamp) ||
                   (prev.stream==c.stream && prev.timestamp==c.timestamp && prev.position<c.position))
          end
          prev = c
        end
      end

      should 'ignore content have same stream index, timestamp and position' do
        100.times do
          c = PeerCastStation::Core::Content.new(10000, System::TimeSpan.from_seconds(10000), 1000000, '0'*rand(100))
          @collection.add(c)
        end
        assert_equal 1, @collection.count
      end

      should 'remove old packets if count exceed limit_packets' do
        @collection.limit_packets = 10
        5.times do |i|
          c = PeerCastStation::Core::Content.new(1, System::TimeSpan.from_seconds(10+i), rand(1000000), 'content')
          @collection.add(c)
        end
        10.times do |i|
          c = PeerCastStation::Core::Content.new(1, System::TimeSpan.from_seconds(i), rand(1000000), 'content')
          @collection.add(c)
        end
        assert_equal 10, @collection.count
        @collection.each do |c|
          assert_equal 1, c.stream
          assert((5..14).include?(c.timestamp.total_seconds))
        end
      end

      should 'remove older stream packets if count exceed limit_packets' do
        @collection.limit_packets = 10
        5.times do |i|
          c = PeerCastStation::Core::Content.new(1, System::TimeSpan.from_seconds(10+i), rand(1000000), 'content')
          @collection.add(c)
        end
        5.times do |i|
          c = PeerCastStation::Core::Content.new(0, System::TimeSpan.from_seconds(20+i), rand(1000000), 'content')
          @collection.add(c)
        end
        5.times do |i|
          c = PeerCastStation::Core::Content.new(2, System::TimeSpan.from_seconds(i), rand(1000000), 'content')
          @collection.add(c)
        end
        assert_equal 10, @collection.count
        @collection.each do |c|
          assert [1, 2].include?(c.stream)
        end
      end

      should 'fire ContentChanged event if content added' do
        fired = 0
        @collection.content_changed do
          fired += 1
        end
        10.times do
          c = PeerCastStation::Core::Content.new(rand(10000), System::TimeSpan.from_seconds(rand(10000)), rand(1000000), 'content')
          @collection.add(c)
        end
        10.times do
          c = PeerCastStation::Core::Content.new(10000, System::TimeSpan.from_seconds(10000), 1000000, 'content')
          @collection.add(c)
        end
        assert_equal 11, fired
      end
    end

    context 'remove' do
      should 'remove content have same stream index, timestamp and position' do
        10.times do |i|
          @collection.add(PeerCastStation::Core::Content.new(i, System::TimeSpan.from_seconds(5), 5, i.to_s)) if i!=5
        end
        10.times do |i|
          @collection.add(PeerCastStation::Core::Content.new(5, System::TimeSpan.from_seconds(i), 5, i.to_s)) if i!=5
        end
        10.times do |i|
          @collection.add(PeerCastStation::Core::Content.new(5, System::TimeSpan.from_seconds(5), i, i.to_s)) if i!=5
        end
        10.times do |i|
          c = PeerCastStation::Core::Content.new(i, System::TimeSpan.from_seconds(i), i, i.to_s)
          @collection.add(c)
        end
        assert @collection.remove(PeerCastStation::Core::Content.new(5, System::TimeSpan.from_seconds(5), 5, nil))
        assert_equal 36, @collection.count
        assert !@collection.any? {|c| c.stream==5 and c.timestamp.total_seconds==5 and c.position==5 }
      end

      should 'return false if no content is matched' do
        10.times do |i|
          c = PeerCastStation::Core::Content.new(i, System::TimeSpan.from_seconds(i), rand(1000000), i.to_s)
          @collection.add(c)
        end
        assert !@collection.remove(PeerCastStation::Core::Content.new(1, System::TimeSpan.from_seconds(3), 0, nil))
        assert_equal 10, @collection.count
      end

      should 'fire ContentChanged event if content removed' do
        10.times do |i|
          c = PeerCastStation::Core::Content.new(i, System::TimeSpan.from_seconds(i), i, i.to_s)
          @collection.add(c)
        end
        fired = 0
        @collection.content_changed do
          fired += 1
        end
        (7..12).each do |i|
          @collection.remove(PeerCastStation::Core::Content.new(i, System::TimeSpan.from_seconds(i), i, nil))
        end
        assert_equal 3, fired
      end
    end

    context 'clear' do
      should 'collection be empty' do
        10.times do |i|
          @collection.add(PeerCastStation::Core::Content.new(i, System::TimeSpan.from_seconds(5), rand(1000000), i.to_s)) if i!=5
        end
        @collection.clear
        assert_equal 0, @collection.count
      end

      should 'fire ContentChanged event' do
        10.times do |i|
          @collection.add(PeerCastStation::Core::Content.new(i, System::TimeSpan.from_seconds(5), rand(1000000), i.to_s)) if i!=5
        end
        fired = 0
        @collection.content_changed do
          fired += 1
        end
        @collection.clear
        @collection.clear
        @collection.clear
        assert_equal 3, fired
      end
    end

    context 'contains' do
      should 'return true if collection have same stream index, timestamp and position' do
        10.times do |i|
          @collection.add(PeerCastStation::Core::Content.new(i, System::TimeSpan.from_seconds(5), rand(1000000), i.to_s)) if i!=5
        end
        10.times do |i|
          @collection.add(PeerCastStation::Core::Content.new(5, System::TimeSpan.from_seconds(i), rand(1000000), i.to_s)) if i!=5
        end
        10.times do |i|
          c = PeerCastStation::Core::Content.new(i, System::TimeSpan.from_seconds(i), i, i.to_s)
          @collection.add(c)
        end
        assert @collection.contains(PeerCastStation::Core::Content.new(5, System::TimeSpan.from_seconds(5), 5, nil))
      end

      should 'return false if no content is matched' do
        10.times do |i|
          c = PeerCastStation::Core::Content.new(i, System::TimeSpan.from_seconds(i), rand(1000000), i.to_s)
          @collection.add(c)
        end
        assert !@collection.contains(PeerCastStation::Core::Content.new(1, System::TimeSpan.from_seconds(3), 5, nil))
      end
    end

    context 'newest' do
      should 'return content has max stream index, timestamp and position' do
        10000.times do
          c = PeerCastStation::Core::Content.new(rand(10), System::TimeSpan.from_seconds(rand(10000)), rand(1000000), 'content')
          @collection.add(c)
        end
        n = @collection.newest
        assert @collection.all? {|c|
          c.stream<n.stream or
          (c.stream==n.stream and c.timestamp<n.timestamp) or
          (c.stream==n.stream and c.timestamp==n.timestamp and c.position<=n.position)
        }
      end

      should 'return null if empty' do
        assert_nil @collection.newest
      end
    end

    context 'oldest' do
      should 'return content has min stream index, timestamp and position' do
        10000.times do
          c = PeerCastStation::Core::Content.new(rand(10), System::TimeSpan.from_seconds(rand(10000)), rand(1000000), 'content')
          @collection.add(c)
        end
        n = @collection.oldest
        assert @collection.all? {|c|
          c.stream>n.stream or
          (c.stream==n.stream and c.timestamp>n.timestamp) or
          (c.stream==n.stream and c.timestamp==n.timestamp and c.position>=n.position)
        }
      end

      should 'return null if empty' do
        assert_nil @collection.newest
      end
    end

    context 'get_newest' do
      should 'return content has max timestamp, position and newer stream index' do
        10000.times do
          c = PeerCastStation::Core::Content.new(rand(10), System::TimeSpan.from_seconds(rand(10000)), rand(1000000), 'content')
          @collection.add(c)
        end
        n = @collection.get_newest(5)
        assert @collection.select {|c| c.stream>=n.stream }.all? {|c|
          c.timestamp<n.timestamp or
          (c.timestamp==n.timestamp and c.position<=n.position)
        }
      end

      should 'return null if not found' do
        100.times do |i|
          c = PeerCastStation::Core::Content.new(i%5, System::TimeSpan.from_seconds(rand(10000)), rand(1000000), 'content')
          @collection.add(c)
        end
        assert_nil @collection.get_newest(5)
      end
    end

    context 'get_oldest' do
      should 'return content has min timestamp, position and newer stream index' do
        10000.times do
          c = PeerCastStation::Core::Content.new(rand(10), System::TimeSpan.from_seconds(rand(10000)), rand(1000000), 'content')
          @collection.add(c)
        end
        n = @collection.get_oldest(5)
        assert @collection.select {|c| c.stream>=n.stream }.all? {|c|
          c.timestamp>n.timestamp or
          (c.timestamp==n.timestamp and c.position>=n.position)
        }
      end

      should 'return null if not found' do
        100.times do |i|
          c = PeerCastStation::Core::Content.new(i%5, System::TimeSpan.from_seconds(rand(10000)), rand(1000000), 'content')
          @collection.add(c)
        end
        assert_nil @collection.get_oldest(5)
      end
    end

    context 'get_newer_contents' do
      should 'return contents has newer timestamp, stream index and position' do
        10000.times do
          c = PeerCastStation::Core::Content.new(rand(10), System::TimeSpan.from_seconds(rand(10000)), rand(1000000), 'content')
          @collection.add(c)
        end
        newer = @collection.get_newer_contents(3, System::TimeSpan.from_seconds(5000.0), 40000)
        older = @collection.select {|c| not newer.include?(c) }
        newer.each do |n|
          assert older.all? {|c|
            c.stream<n.stream or
            (c.stream==n.stream and c.timestamp<n.timestamp) or
            (c.stream==n.stream and c.timestamp==n.timestamp and c.position<n.position)
          }
        end
      end
    end

    context 'next_of' do
      should 'return content has newer timestamp, stream index and position' do
        10000.times do
          c = PeerCastStation::Core::Content.new(rand(10), System::TimeSpan.from_seconds(rand(10000)), rand(1000000), 'content')
          @collection.add(c)
        end
        n = @collection.next_of(3, System::TimeSpan.from_seconds(5000.0), 40000)
        assert_equal @collection.get_newer_contents(3, System::TimeSpan.from_seconds(5000.0), 40000).first, n
      end

      should 'return content has newer timestamp, stream index and position specified by content' do
        10000.times do
          c = PeerCastStation::Core::Content.new(rand(10), System::TimeSpan.from_seconds(rand(10000)), rand(1000000), 'content')
          @collection.add(c)
        end
        old = PeerCastStation::Core::Content.new(3, System::TimeSpan.from_seconds(5000.0), 40000, 'hoge')
        n = @collection.next_of(old)
        assert_equal @collection.get_newer_contents(3, System::TimeSpan.from_seconds(5000.0), 40000).first, n
      end

      should 'return null if nothing is matched' do
        10000.times do |i|
          c = PeerCastStation::Core::Content.new(i%10, System::TimeSpan.from_seconds(i), rand(1000000), 'content')
          @collection.add(c)
        end
        [
          [10, System::TimeSpan.from_seconds(10.0),    100],
          [5,  System::TimeSpan.from_seconds(10000.0), 0],
          [9,  System::TimeSpan.from_seconds(9999),    1000000],
        ].each do |stream, timestamp, position|
          n = @collection.next_of(stream, timestamp, position)
          assert_nil n
        end
      end
    end

    context 'find_next_by_position' do
      should 'return content has newer position and stream index' do
        10000.times do
          c = PeerCastStation::Core::Content.new(rand(10), System::TimeSpan.from_seconds(rand(10000)), rand(1000000), 'content')
          @collection.add(c)
        end
        n = @collection.find_next_by_position(3, 500000)
        assert((3==n.stream and 500000<n.position) || (3<n.stream))
      end

      should 'return null if nothing is matched' do
        10000.times do
          c = PeerCastStation::Core::Content.new(rand(10), System::TimeSpan.from_seconds(rand(10000)), rand(1000000), 'content')
          @collection.add(c)
        end
        [
          [10, 10],
          [ 5, 1000000],
        ].each do |stream, position|
          n = @collection.find_next_by_position(stream, position)
          assert_nil n
        end
      end
    end
  end
end

