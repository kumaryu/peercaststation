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
$: << File.join(File.dirname(__FILE__), '..', 'PeerCastStation.ASF', 'bin', 'Debug')
require 'PeerCastStation.Core.dll'
require 'PeerCastStation.ASF.dll'
require 'test/unit'
PCSCore = PeerCastStation::Core unless defined?(PCSCore)
PCSASF = PeerCastStation::ASF unless defined?(PCSASF)

class TC_ASFContentReader < Test::Unit::TestCase
  def fixture(name)
    File.join(File.dirname(__FILE__), 'fixtures', name)
  end

  def setup
    @peercast = PCSCore::PeerCast.new
    @channel = PCSCore::Channel.new(
      @peercast,
      System::Guid.new('9778E62BDC59DF56F9216D0387F80BF2'.to_clr_string), 
      System::Uri.new('http://127.0.0.1:8888/'))
  end
  
  def teardown
    @peercast.stop if @peercast
  end

  def test_construct
    reader = nil
    assert_nothing_raised do
      reader = PCSASF::ASFContentReader.new
    end
    assert_equal("WMV",  reader.content_type)
    assert_equal(".wmv", reader.content_extension)
    assert_equal("application/x-mms-framed", reader.MIMEType)
  end

  def test_read_empty
    stream = System::IO::MemoryStream.new
    reader = PCSASF::ASFContentReader.new
    assert_raises(System::IO::EndOfStreamException) do
      content = reader.read(@channel, stream)
    end
  end

  def test_read
    stream = System::IO::File.open_read(fixture('test.asf'))
    reader = PCSASF::ASFContentReader.new
    content = reader.read(@channel, stream)
    assert_not_nil(content.content_header)
    assert_not_nil(content.channel_info)
    assert_equal(439, content.channel_info.bitrate)
    assert_equal(0, content.content_header.position)
    assert_equal(5271, content.content_header.data.length)
    assert_equal(7, content.contents.count)
    pos = 0+content.content_header.data.length
    content.contents.count.times do |i|
      assert_equal(pos, content.contents[i].position)
      pos += content.contents[i].data.length
    end
  ensure
    stream.close if stream
  end

  def test_read_continue
    stream = System::IO::File.open_read(fixture('test.asf'))
    reader = PCSASF::ASFContentReader.new
    cnt = 0
    content = reader.read(@channel, stream)
    assert_not_nil(content.content_header)
    assert_not_nil(content.channel_info)
    pos = 0+content.content_header.data.length
    content.contents.count.times do |i|
      pos += content.contents[i].data.length
    end
    cnt += content.contents.count

    @channel.content_header = content.content_header
    content.contents.each do |c|
      @channel.contents.add(c)
    end
    
    while cnt<361 do
      content = reader.read(@channel, stream)
      assert_nil(content.content_header)
      assert_nil(content.channel_info)
      assert_equal([8, 361-cnt].min, content.contents.count)
      content.contents.count.times do |i|
        assert_equal(pos, content.contents[i].position)
        pos += content.contents[i].data.length
        @channel.contents.add(content.contents[i])
      end
      cnt += content.contents.count
    end
  ensure
    stream.close if stream
  end
end

