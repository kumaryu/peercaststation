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
  PCSCore = PeerCastStation::Core unless defined?(PCSCore)
  class TC_CoreRawContentReaderFactory < Test::Unit::TestCase
    def test_construct
      factory = PCSCore::RawContentReaderFactory.new
      assert_kind_of(PeerCastStation::Core::IContentReaderFactory, factory)
      assert_equal("RAW", factory.name)
    end

    def test_create
      peercast = PeerCastStation::Core::PeerCast.new
      channel = PCSCore::Channel.new(
        peercast,
        System::Guid.new('9778E62BDC59DF56F9216D0387F80BF2'.to_clr_string), 
        System::Uri.new('http://127.0.0.1:8888/'))
      factory = PCSCore::RawContentReaderFactory.new
      reader = factory.create(channel)
      assert_equal("RAW", reader.name)
      assert_kind_of(PeerCastStation::Core::IContentReader, reader)
    end
  end

  class TC_CoreRawContentReader < Test::Unit::TestCase
    def setup
      @peercast = PeerCastStation::Core::PeerCast.new
      @channel = PCSCore::Channel.new(
        @peercast,
        System::Guid.new('9778E62BDC59DF56F9216D0387F80BF2'.to_clr_string), 
        System::Uri.new('http://127.0.0.1:8888/'))
      factory = PCSCore::RawContentReaderFactory.new
      @reader = factory.create(@channel)
    end
    
    def teardown
      @peercast.stop if @peercast
    end

    def test_read_empty
      stream = System::IO::MemoryStream.new
      assert_raises(System::IO::EndOfStreamException) do
        content = @reader.read(stream)
      end
    end

    def test_read
      stream = System::IO::MemoryStream.new("header\ncontent1\ncontent2\n")
      chan_info = PCSCore::AtomCollection.new
      chan_info.set_chan_info_name('foobar')
      @channel.channel_info = PCSCore::ChannelInfo.new(chan_info)
      content = @reader.read(stream)
      assert_nil(content.channel_track)
      assert_equal('RAW',    content.channel_info.content_type)
      assert_equal('application/octet-stream', content.channel_info.extra.get_chan_info_stream_type)
      assert_equal('', content.channel_info.extra.get_chan_info_stream_ext)
      assert_equal('foobar', content.channel_info.name)
      assert_equal(0, content.content_header.position)
      assert_equal(0, content.content_header.data.length)
      assert_equal(1, content.contents.count)
      assert_equal("header\ncontent1\ncontent2\n", content.contents[0].data.to_a.pack('C*'))
    end

    def test_read_many
      stream = System::IO::MemoryStream.new
      data = Array.new(10000) {|i| i%256 }.pack('C*')
      stream.write(data, 0, data.bytesize)
      stream.position = 0
      content = @reader.read(stream)
      assert_nil(content.channel_track)
      assert_equal('RAW', content.channel_info.content_type)
      assert_equal(0,          content.content_header.position)
      assert_equal(0,          content.content_header.data.length)
      assert_equal(2,          content.contents.count)
      assert_equal(0,          content.contents[0].position)
      assert_equal(8192,       content.contents[0].data.length)
      assert_equal(8192,       content.contents[1].position)
      assert_equal(10000-8192, content.contents[1].data.length)
    end

    def test_read_continue
      stream = System::IO::MemoryStream.new
      data = Array.new(10000) {|i| i%256 }.pack('C*')
      @channel.content_header = PCSCore::Content.new(3, System::TimeSpan.from_seconds(0), 30000, 'header')
      @channel.contents.add(PCSCore::Content.new(3, System::TimeSpan.from_seconds(5), 13093, 'foobar'))
      stream.write(data, 0, data.bytesize)
      stream.position = 0
      content = @reader.read(stream)

      assert_nil(content.channel_info)
      assert_nil(content.channel_track)
      assert_nil(content.content_header)
      assert_equal(2, content.contents.count)
      assert_equal(30006,      content.contents[0].position)
      assert_equal(8192,       content.contents[0].data.length)
      assert_equal(30006+8192, content.contents[1].position)
      assert_equal(10000-8192, content.contents[1].data.length)
    end
  end
end

