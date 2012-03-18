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
$: << File.join(File.dirname(__FILE__), '..', 'PeerCastStation.HTTP', 'bin', 'Debug')
require 'PeerCastStation.Core.dll'
require 'PeerCastStation.HTTP.dll'
require 'test/unit'
using_clr_extensions PeerCastStation::Core

PCSCore = PeerCastStation::Core
PCSHTTP = PeerCastStation::HTTP

class TC_M3UPlayList < Test::Unit::TestCase
  def setup
    @peercast = PeerCastStation::Core::PeerCast.new
  end
  
  def teardown
    @peercast.stop if @peercast
  end

  def test_construct
    pls = PCSHTTP::M3UPlayList.new
    assert_equal('audio/x-mpegurl', pls.MIMEType)
    assert_equal(0, pls.channels.count)
  end

  def test_create_playlist
    pls = PCSHTTP::M3UPlayList.new
    baseuri = System::Uri.new('http://localhost/stream/')
    res = pls.create_play_list(baseuri)
    assert_equal([], res.to_a)

    channel = PCSCore::Channel.new(
      @peercast,
      System::Guid.parse('9778E62BDC59DF56F9216D0387F80BF2'),
      System::Uri.new('mock://localhost'))
    info = PCSCore::AtomCollection.new
    info.set_chan_info_name('foo')
    info.set_chan_info_type('WMV')
    channel.channel_info = PCSCore::ChannelInfo.new(info)
    pls.channels.add(channel)
    res = pls.create_play_list(baseuri).to_a.pack('C*')
    assert_equal(<<EOS, res.gsub(/\r\n/, "\n"))
http://localhost/stream/9778E62BDC59DF56F9216D0387F80BF2.wmv
EOS

    channel2 = PCSCore::Channel.new(
      @peercast,
      System::Guid.parse('61077675C74AAB30529B5294BB76F656'),
      System::Uri.new('mock://localhost'))
    info2 = PCSCore::AtomCollection.new
    info2.SetChanInfoURL('http://example.com/')
    info2.set_chan_info_name('bar')
    info2.set_chan_info_type('OGM')
    channel2.channel_info = PCSCore::ChannelInfo.new(info2)
    pls.channels.add(channel2)
    res = pls.create_play_list(baseuri).to_a.pack('C*')
    assert_equal(<<EOS, res.gsub(/\r\n/, "\n"))
http://localhost/stream/9778E62BDC59DF56F9216D0387F80BF2.wmv
http://localhost/stream/61077675C74AAB30529B5294BB76F656.ogv
EOS
  end
end

class TC_ASXPlayList < Test::Unit::TestCase
  def setup
    @peercast = PeerCastStation::Core::PeerCast.new
  end
  
  def teardown
    @peercast.stop if @peercast
  end

  def test_construct
    pls = PCSHTTP::ASXPlayList.new
    assert_equal('video/x-ms-asf', pls.MIMEType)
    assert_equal(0, pls.channels.count)
  end

  def test_create_playlist
    pls = PCSHTTP::ASXPlayList.new
    baseuri = System::Uri.new('http://localhost/stream/')
    res = pls.create_play_list(baseuri).to_a.pack('C*')
    assert_equal(<<EOS.chomp, res.gsub(/\r\n/, "\n"))
<ASX version="3.0" />
EOS

    channel = PCSCore::Channel.new(
      @peercast,
      System::Guid.parse('9778E62BDC59DF56F9216D0387F80BF2'),
      System::Uri.new('mock://localhost'))
    info = PCSCore::AtomCollection.new
    info.set_chan_info_name('foo')
    info.set_chan_info_type('WMV')
    channel.channel_info = PCSCore::ChannelInfo.new(info)
    pls.channels.add(channel)
    res = pls.create_play_list(baseuri).to_a.pack('C*')
    assert_equal(<<EOS.chomp, res.gsub(/\r\n/, "\n"))
<ASX version="3.0">
  <Title>foo</Title>
  <Entry>
    <Title>foo</Title>
    <Ref href="http://localhost/stream/9778E62BDC59DF56F9216D0387F80BF2.wmv" />
  </Entry>
</ASX>
EOS

    channel2 = PCSCore::Channel.new(
      @peercast,
      System::Guid.parse('61077675C74AAB30529B5294BB76F656'),
      System::Uri.new('mock://localhost'))
    info2 = PCSCore::AtomCollection.new
    info2.SetChanInfoURL('http://example.com/')
    info2.set_chan_info_name('bar')
    info2.set_chan_info_type('OGM')
    channel2.channel_info = PCSCore::ChannelInfo.new(info2)
    pls.channels.add(channel2)
    res = pls.create_play_list(baseuri).to_a.pack('C*')
    assert_equal(<<EOS.chomp, res.gsub(/\r\n/, "\n"))
<ASX version="3.0">
  <Title>foo</Title>
  <Entry>
    <Title>foo</Title>
    <Ref href="http://localhost/stream/9778E62BDC59DF56F9216D0387F80BF2.wmv" />
  </Entry>
  <Entry>
    <Title>bar</Title>
    <MoreInfo href="http://example.com/" />
    <Ref href="http://localhost/stream/61077675C74AAB30529B5294BB76F656.ogv" />
  </Entry>
</ASX>
EOS
  end
end

