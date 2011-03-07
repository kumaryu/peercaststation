$: << File.join(File.dirname(__FILE__), '..', 'PeerCastStation.Core', 'bin', 'Debug')
$: << File.join(File.dirname(__FILE__), '..', 'PeerCastStation.HTTP', 'bin', 'Debug')
require 'PeerCastStation.Core.dll'
require 'PeerCastStation.HTTP.dll'
require 'test/unit'
using_clr_extensions PeerCastStation::Core

PCSCore = PeerCastStation::Core
PCSHTTP = PeerCastStation::HTTP

class TC_PLSPlayList < Test::Unit::TestCase
  def test_construct
    pls = PCSHTTP::PLSPlayList.new
    assert_equal('audio/x-mpegurl', pls.MIMEType)
    assert_equal(0, pls.channels.count)
  end

  def test_create_playlist
    pls = PCSHTTP::PLSPlayList.new
    baseuri = System::Uri.new('http://localhost/stream/')
    res = pls.create_play_list(baseuri)
    assert_equal('', res)

    info = PCSCore::ChannelInfo.new(System::Guid.parse('9778E62BDC59DF56F9216D0387F80BF2'))
    info.name = 'foo'
    info.content_type = 'WMV'
    pls.channels.add(info)
    res = pls.create_play_list(baseuri)
    assert_equal(<<EOS, res.gsub(/\r\n/, "\n"))
http://localhost/stream/9778E62BDC59DF56F9216D0387F80BF2.wmv
EOS

    info2 = PCSCore::ChannelInfo.new(System::Guid.parse('61077675C74AAB30529B5294BB76F656'))
    info2.name = 'bar'
    info2.content_type = 'OGM'
    info2.extra.SetChanInfo(PCSCore::AtomCollection.new)
    info2.extra.GetChanInfo.SetChanInfoURL('http://example.com/')
    pls.channels.add(info2)
    res = pls.create_play_list(baseuri)
    assert_equal(<<EOS, res.gsub(/\r\n/, "\n"))
http://localhost/stream/9778E62BDC59DF56F9216D0387F80BF2.wmv
http://localhost/stream/61077675C74AAB30529B5294BB76F656.ogv
EOS
  end
end

class TC_ASXPlayList < Test::Unit::TestCase
  def test_construct
    pls = PCSHTTP::ASXPlayList.new
    assert_equal('video/x-ms-asf', pls.MIMEType)
    assert_equal(0, pls.channels.count)
  end

  def test_create_playlist
    pls = PCSHTTP::ASXPlayList.new
    baseuri = System::Uri.new('http://localhost/stream/')
    res = pls.create_play_list(baseuri)
    assert_equal(<<EOS.chomp, res.gsub(/\r\n/, "\n"))
<ASX version="3.0" />
EOS

    info = PCSCore::ChannelInfo.new(System::Guid.parse('9778E62BDC59DF56F9216D0387F80BF2'))
    info.name = 'foo'
    info.content_type = 'WMV'
    pls.channels.add(info)
    res = pls.create_play_list(baseuri)
    assert_equal(<<EOS.chomp, res.gsub(/\r\n/, "\n"))
<ASX version="3.0">
  <Title>foo</Title>
  <Entry>
    <Title>foo</Title>
    <Ref href="http://localhost/stream/9778E62BDC59DF56F9216D0387F80BF2.wmv" />
  </Entry>
</ASX>
EOS

    info2 = PCSCore::ChannelInfo.new(System::Guid.parse('61077675C74AAB30529B5294BB76F656'))
    info2.name = 'bar'
    info2.content_type = 'OGM'
    info2.extra.SetChanInfo(PCSCore::AtomCollection.new)
    info2.extra.GetChanInfo.SetChanInfoURL('http://example.com/')
    pls.channels.add(info2)
    res = pls.create_play_list(baseuri)
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

