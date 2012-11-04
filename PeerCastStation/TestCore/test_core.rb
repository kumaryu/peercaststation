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

module TestCore
  class TC_CoreContent < Test::Unit::TestCase
    def test_construct
      obj = PeerCastStation::Core::Content.new(3, System::TimeSpan.from_seconds(5), 10, 'content')
      assert_equal(3, obj.stream)
      assert_equal(5, obj.timestamp.total_seconds)
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
      assert(obj.respond_to?(:create_obj_ref))
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

    def test_mime_type
      info = PeerCastStation::Core::AtomCollection.new
      info.set_chan_info_type('WMV')
      obj = PeerCastStation::Core::ChannelInfo.new(info)
      assert_equal('video/x-ms-wmv', obj.MIMEType)
    end

    def test_mime_type_with_styp
      info = PeerCastStation::Core::AtomCollection.new
      info.set_chan_info_type('WMV')
      info.set_chan_info_stream_type('application/x-hoge')
      obj = PeerCastStation::Core::ChannelInfo.new(info)
      assert_equal('application/x-hoge', obj.MIMEType)
    end

    def test_content_extension
      info = PeerCastStation::Core::AtomCollection.new
      info.set_chan_info_type('WMV')
      obj = PeerCastStation::Core::ChannelInfo.new(info)
      assert_equal('.wmv', obj.content_extension)
    end

    def test_content_extension_with_sext
      info = PeerCastStation::Core::AtomCollection.new
      info.set_chan_info_type('WMV')
      info.set_chan_info_stream_ext('.hoge')
      obj = PeerCastStation::Core::ChannelInfo.new(info)
      assert_equal('.hoge', obj.content_extension)
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
      assert(obj.respond_to?(:create_obj_ref))
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
end

