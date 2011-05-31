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
require 'PeerCastStation.Core.dll'
require 'test/unit'
using_clr_extensions PeerCastStation::Core

class TC_CoreChannel < Test::Unit::TestCase
  def id4(s)
    PeerCastStation::Core::ID4.new(s.to_clr_string)
  end

  def setup
    @peercast = PeerCastStation::Core::PeerCast.new
  end
  
  def teardown
    @peercast.close if @peercast and not @peercast.is_closed
  end

  def test_construct
    channel = PeerCastStation::Core::Channel.new(@peercast, System::Guid.empty, System::Uri.new('mock://localhost'))
    assert_nil(channel.source_stream)
    assert_equal(@peercast, channel.PeerCast)
    assert_equal('mock://localhost/', channel.source_uri.to_s)
    assert_equal(System::Guid.empty, channel.ChannelID)
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
    channel.property_changed {|sender, e| property_log << e.property_name }
    channel.content_changed {|sender, e| content_log << 'content' }
    channel.source_stream = MockSourceStream.new(channel, channel.source_uri)
    chaninfo = PeerCastStation::Core::AtomCollection.new
    chaninfo.set_chan_info_name('bar')
    channel.channel_info = PeerCastStation::Core::ChannelInfo.new(chaninfo)
    chantrack = PeerCastStation::Core::AtomCollection.new
    chantrack.set_chan_track_title('foo')
    channel.channel_track = PeerCastStation::Core::ChannelTrack.new(chantrack)
    channel.add_output_stream(MockOutputStream.new)
    channel.nodes.add(PeerCastStation::Core::HostBuilder.new.to_host)
    channel.content_header = PeerCastStation::Core::Content.new(0, 'header')
    channel.contents.add(PeerCastStation::Core::Content.new(1, 'body'))
    assert_equal(6, property_log.size)
    assert_equal('SourceStream',  property_log[0])
    assert_equal('ChannelInfo',   property_log[1])
    assert_equal('ChannelTrack',  property_log[2])
    assert_equal('OutputStreams', property_log[3])
    assert_equal('Nodes',         property_log[4])
    assert_equal('ContentHeader', property_log[5])
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
    assert_equal(:close,     channel.source_stream.log[1][0])
    assert_equal(:reconnect, channel.source_stream.log[2][0])
    assert_equal(:close,     output_stream.log[0][0])
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
    assert_equal(:close, channel.source_stream.log[1][0])
    assert_equal(:close, output_stream.log[0][0])
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

  class TestSourceStream
    include PeerCastStation::Core::ISourceStream
    
    def initialize(channel, tracker)
      @channel = channel
      @tracker = tracker
      @status = PeerCastStation::Core::SourceStreamStatus.idle
      @status_changed = []
      @paused = true
    end
    attr_reader :tracker, :channel, :status
    attr_accessor :paused
    
    def add_StatusChanged(handler)
      @status_changed << handler
    end
    
    def remove_StatusChanged(handler)
      @status_changed.delete(handler)
    end

    def post(from, packet)
    end
    
    def start
      while @paused do
        sleep(0.1)
      end
    end
    
    def reconnect
    end

    def close
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

  def test_select_source_host
    channel = PeerCastStation::Core::Channel.new(@peercast, System::Guid.empty, System::Uri.new('mock://localhost'))
    selected = channel.select_source_host
    assert_equal(channel.source_host, selected)

    channel.ignore_host(channel.source_host)
    selected = channel.select_source_host
    assert_nil(selected)

    channel.clear_ignored
    node = PeerCastStation::Core::HostBuilder.new.to_host
    channel.nodes.add(node)
    selected = channel.select_source_host
    assert_equal(node, selected)
  end
end

class TC_CoreContentCollection < Test::Unit::TestCase
  def test_construct
    contents = PeerCastStation::Core::ContentCollection.new
    assert_equal(0, contents.count)
    assert_equal(160, contents.limit_packets)
  end

  def test_newest
    contents = PeerCastStation::Core::ContentCollection.new
    assert_nil(contents.newest)

    content0 = PeerCastStation::Core::Content.new(10, 'content')
    content1 = PeerCastStation::Core::Content.new(0, 'content')
    contents.add(content0)
    contents.add(content1)
    assert_equal(content0, contents.newest)
  end

  def test_oldest
    contents = PeerCastStation::Core::ContentCollection.new
    assert_nil(contents.newest)

    content0 = PeerCastStation::Core::Content.new(10, 'content')
    content1 = PeerCastStation::Core::Content.new(0, 'content')
    contents.add(content0)
    contents.add(content1)
    assert_equal(content1, contents.oldest)
  end

  def test_add
    contents = PeerCastStation::Core::ContentCollection.new
    contents.limit_packets = 10
    30.times do |i|
      content = PeerCastStation::Core::Content.new(i, "content#{i}")
      contents.add(content)
    end
    assert_equal(10,          contents.count)
    assert_equal(20,          contents.oldest.position)
    assert_equal('content20', contents.oldest.data.to_a.pack('C*'))
    assert_equal(29,          contents.newest.position)
    assert_equal('content29', contents.newest.data.to_a.pack('C*'))
    assert_nothing_raised do
      30.times do |i|
        content = PeerCastStation::Core::Content.new(i, "content#{i+30}")
        contents.add(content)
      end
    end
    assert_equal(10,          contents.count)
    assert_equal(20,          contents.oldest.position)
    assert_equal('content50', contents.oldest.data.to_a.pack('C*'))
    assert_equal(29,          contents.newest.position)
    assert_equal('content59', contents.newest.data.to_a.pack('C*'))
  end

  def test_get_newer_contents
    contents = PeerCastStation::Core::ContentCollection.new
    30.times do |i|
      content = PeerCastStation::Core::Content.new(i*10, "content#{i}")
      contents.add(content)
    end
    newer = contents.get_newer_contents(-1)
    assert_equal(30,  newer.count)
    assert_equal(0,   newer[0].position)
    assert_equal(290, newer[newer.count-1].position)

    newer = contents.get_newer_contents(0)
    assert_equal(29,  newer.count)
    assert_equal(10,  newer[0].position)
    assert_equal(290, newer[newer.count-1].position)

    newer = contents.get_newer_contents(15)
    assert_equal(28,  newer.count)
    assert_equal(20,  newer[0].position)
    assert_equal(290, newer[newer.count-1].position)

    newer = contents.get_newer_contents(285)
    assert_equal(1,   newer.count)
    assert_equal(290, newer[0].position)
    assert_equal(290, newer[newer.count-1].position)

    newer = contents.get_newer_contents(290)
    assert_equal(0, newer.count)

    newer = contents.get_newer_contents(300)
    assert_equal(0, newer.count)
  end
end

