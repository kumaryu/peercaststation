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

class MockYellowPageClientFactory
  include PeerCastStation::Core::IYellowPageClientFactory
  
  def name
    'mock_yp'
  end
  
  def create(name, uri)
    MockYellowPageClient.new(name, uri)
  end
end

class MockYellowPageClient
  include PeerCastStation::Core::IYellowPageClient
  def initialize(name, uri)
    @name = name
    @uri = uri
    @log = []
    @channels = []
  end
  attr_reader :name, :uri, :log, :channels
  
  def find_tracker(channel_id)
    @log << [:find_tracker, channel_id]
    addr = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7147)
    System::Uri.new("mock://#{addr}")
  end
  
  def announce(channel)
    @log << [:announce, channel]
    @channels << channel
  end

  def restart_announce
    @log << [:restart_announce]
  end

  def stop_announce
    @log << [:stop_announce]
    @channels.clear
  end
end

class MockSourceStreamFactory
  include PeerCastStation::Core::ISourceStreamFactory
  def initialize
    @log = []
  end
  attr_reader :log
  
  def name
    'MockSourceStream'
  end
  
  def create(channel, uri, reader=nil)
    @log << [:create, channel, uri, reader]
    MockSourceStream.new(channel, uri, reader)
  end
end

class MockSourceStream
  include PeerCastStation::Core::ISourceStream
  
  def initialize(channel, tracker, reader=nil)
    @channel = channel
    @tracker = tracker
    @reader  = reader
    @status_changed = []
    @status = PeerCastStation::Core::SourceStreamStatus.idle
    @start_proc = nil
    @stopped = []
    @log = []
  end
  attr_reader :log, :reader, :tracker, :channel, :status
  attr_accessor :start_proc

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
    @log << [:post, from, packet]
  end
  
  def start
    @log << [:start]
    @start_proc.call if @start_proc
    args = System::EventArgs.new
    @stopped.each do |handler|
      handler.invoke(self, args)
    end
  end
  
  def reconnect
    @log << [:reconnect]
  end
  
  def stop
    @log << [:stop]
  end
end

class MockOutputStream
  include PeerCastStation::Core::IOutputStream
  
  def initialize(type=0)
    @type = type
    @remote_endpoint = nil
    @upstream_rate = 0
    @is_local = false
    @log = []
    @stopped = []
  end
  attr_reader :log
  attr_accessor :remote_endpoint, :upstream_rate, :is_local

  def add_Stopped(event)
    @stopped << event
  end

  def remove_Stopped(event)
    @stopped.delete(event)
  end

  def output_stream_type
    @type
  end

  def post(from, packet)
    @log << [:post, from, packet]
  end
  
  def start
    @log << [:start]
    stop
  end
  
  def stop
    @log << [:stop]
    @stopped.each do |event|
      event.invoke(self, System::EventArgs.new)
    end
  end
end

class MockOutputStreamFactory
  include PeerCastStation::Core::IOutputStreamFactory
  
  def initialize
    @log = []
  end
  attr_reader :log
  
  def name
    'MockOutputStream'
  end
  
  def ParseChannelID(header)
    @log << [:parse_channel_id, header]
    header = header.to_a.pack('C*')
    if /^mock ([a-fA-F0-9]{32})/=~header then
      System::Guid.new($1.to_clr_string)
    else
      nil
    end
  end
  
  def create(input_stream, output_stream, remote_endpoint, channel_id, header)
    @log << [:create, input_stream, output_stream, remote_endpoint, channel_id, header]
    MockOutputStream.new
  end
end
  
class TC_CoreContent < Test::Unit::TestCase
  def test_construct
    obj = PeerCastStation::Core::Content.new(10, 'content')
    assert_equal(10, obj.position)
    assert_equal('content'.unpack('C*'), obj.data)
  end
end

class TC_CoreChannelInfo < Test::Unit::TestCase
  def test_empty
    info = PeerCastStation::Core::AtomCollection.new
    obj = PeerCastStation::Core::ChannelInfo.new(info)
    assert_nil(obj.name)
    assert_nil(obj.content_type)
    assert_nil(obj.comment)
    assert_nil(obj.desc)
    assert_nil(obj.genre)
    assert_nil(obj.URL)
    assert_equal(0, obj.bitrate)
    assert_equal(0, obj.extra.count)
  end

  def test_name
    info = PeerCastStation::Core::AtomCollection.new
    info.set_chan_info_name('name')
    obj = PeerCastStation::Core::ChannelInfo.new(info)
    assert_equal('name', obj.name)
  end

  def test_content_type
    info = PeerCastStation::Core::AtomCollection.new
    info.set_chan_info_type('WMV')
    obj = PeerCastStation::Core::ChannelInfo.new(info)
    assert_equal('WMV', obj.content_type)
  end

  def test_comment
    info = PeerCastStation::Core::AtomCollection.new
    info.set_chan_info_comment('comment')
    obj = PeerCastStation::Core::ChannelInfo.new(info)
    assert_equal('comment', obj.comment)
  end

  def test_desc
    info = PeerCastStation::Core::AtomCollection.new
    info.set_chan_info_desc('desc')
    obj = PeerCastStation::Core::ChannelInfo.new(info)
    assert_equal('desc', obj.desc)
  end

  def test_genre
    info = PeerCastStation::Core::AtomCollection.new
    info.set_chan_info_genre('genre')
    obj = PeerCastStation::Core::ChannelInfo.new(info)
    assert_equal('genre', obj.genre)
  end

  def test_url
    info = PeerCastStation::Core::AtomCollection.new
    info.SetChanInfoURL('http://example.com')
    obj = PeerCastStation::Core::ChannelInfo.new(info)
    assert_equal('http://example.com', obj.URL)
  end

  def test_bitrate
    info = PeerCastStation::Core::AtomCollection.new
    info.set_chan_info_bitrate(7144)
    obj = PeerCastStation::Core::ChannelInfo.new(info)
    assert_equal(7144, obj.bitrate)
  end

  def test_extra
    info = PeerCastStation::Core::AtomCollection.new
    info.set_chan_info_bitrate(7144)
    info.SetChanInfoURL('http://example.com')
    info.set_chan_info_genre('genre')
    info.set_chan_info_desc('desc')
    info.set_chan_info_comment('comment')
    info.set_chan_info_name('name')
    info.set_chan_info_type('WMV')
    obj = PeerCastStation::Core::ChannelInfo.new(info)
    assert_equal(7, obj.extra.count)
  end
end

class TC_CoreChannelTrack < Test::Unit::TestCase
  def test_empty
    info = PeerCastStation::Core::AtomCollection.new
    obj = PeerCastStation::Core::ChannelTrack.new(info)
    assert_nil(obj.name)
    assert_nil(obj.album)
    assert_nil(obj.creator)
    assert_nil(obj.URL)
    assert_equal(0, obj.extra.count)
  end

  def test_name
    info = PeerCastStation::Core::AtomCollection.new
    info.set_chan_track_title('name')
    obj = PeerCastStation::Core::ChannelTrack.new(info)
    assert_equal('name', obj.name)
  end

  def test_album
    info = PeerCastStation::Core::AtomCollection.new
    info.set_chan_track_album('album')
    obj = PeerCastStation::Core::ChannelTrack.new(info)
    assert_equal('album', obj.album)
  end

  def test_creator
    info = PeerCastStation::Core::AtomCollection.new
    info.set_chan_track_creator('creator')
    obj = PeerCastStation::Core::ChannelTrack.new(info)
    assert_equal('creator', obj.creator)
  end

  def test_url
    info = PeerCastStation::Core::AtomCollection.new
    info.SetChanTrackURL('http://example.com')
    obj = PeerCastStation::Core::ChannelTrack.new(info)
    assert_equal('http://example.com', obj.URL)
  end

  def test_extra
    info = PeerCastStation::Core::AtomCollection.new
    info.set_chan_track_title('name')
    info.set_chan_track_album('album')
    info.set_chan_track_creator('creator')
    info.SetChanTrackURL('http://example.com')
    obj = PeerCastStation::Core::ChannelTrack.new(info)
    assert_equal(4, obj.extra.count)
  end
end

